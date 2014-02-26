using JetBrains.Annotations;
using McNNTP.Server.Data;
using MoreLinq;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace McNNTP.Server
{
    public class NntpServer
    {
        private readonly Func<ISession> _sessionProvider;

        private readonly Dictionary<string, Func<Connection, string, CommandProcessingResult>> _commandDirectory;

        private readonly List<Thread> _listeners = new List<Thread>();

        public bool AllowPosting { get; set; }
        public bool AllowStartTLS { get; set; }
        public int[] ClearPorts { get; set; }
        public int[] ExplicitTLSPorts { get; set; }
        public int[] ImplicitTLSPorts { get; set; }
        public string ServerPath { get; set; }

        public NntpServer([NotNull] Func<ISession> sessionProvider)
        {
            _commandDirectory = new Dictionary<string, Func<Connection, string, CommandProcessingResult>>
                {
                    {"ARTICLE", Article},
                    {"AUTHINFO", AuthInfo},
                    {"BODY", Body},
                    {"CAPABILITIES", (s, c) => Capabilities(s)},
                    {"DATE", (s, c) => Date(s)},
                    {"GROUP", Group},
                    {"HDR", Hdr},
                    {"HEAD", Head},
                    {"HELP", (s, c) => Help(s)},
                    {"LAST", Last},
                    {"LIST", List},
                    {"LISTGROUP", ListGroup},
                    {"MODE", Mode},
                    {"NEWGROUPS", Newgroups},
                    {"NEXT", Next},
                    {"POST", Post},
                    {"STAT", Stat},
                    {"XOVER", XOver},
                    {"QUIT", (s, c) => Quit(s)}
                };

            _sessionProvider = sessionProvider;

            // TODO: Put this in a custom config section
            ServerPath = "freenews.localhost";

            AllowStartTLS = true;
            ShowData = true;
        }

        #region Connection and IO
        private static readonly List<Connection> _connections = new List<Connection>();

        public void Start()
        {
            _listeners.Clear();

            foreach (var clearPort in ClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(localEndPoint)
                {
                    AcceptCallback = AcceptCallback,
                    PortType = PortClass.ClearText,
                };

                _listeners.Add(new Thread(listener.StartAccepting));
            }

            foreach (var thread in _listeners)
                thread.Start();
        }


        private void AcceptCallback(IAsyncResult ar)
        {
            var acceptState = (AcceptAsyncState) ar.AsyncState;

            // Signal the main thread to continue.
            acceptState.AcceptComplete.Set();

            // Get the socket that handles the client request.
            var listener = acceptState.Listener;
            var handler = listener.EndAcceptTcpClient(ar);
            //Thread.CurrentThread.Name = string.Format("{0}:{1}", ((IPEndPoint)handler.RemoteEndPoint).Address, ((IPEndPoint)handler.RemoteEndPoint).Port);
            
            // Create the state object.
            var state = new Connection
            {
                CanPost = AllowPosting,
                Client = handler,
                Stream = handler.GetStream()
            };

            //var sslStream = new SslStream(state.Stream);
            //sslStream.AuthenticateAsServer();
            //state.Stream = sslStream;

            _connections.Add(state);

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
            if (state.CanPost)
// ReSharper restore ConvertIfStatementToConditionalTernaryExpression
                Send(state, "200 Service available, posting allowed\r\n");
            else
                Send(state, "201 Service available, posting prohibited\r\n");

            Debug.Assert(state.Stream != null);
            try
            {
                state.Stream.BeginRead(state.Buffer, 0, Connection.BUFFER_SIZE, ReadCallback, state);
            }
            catch (IOException se)
            {
                Send(state, "403 Archive server temporarily offline\r\n");
                Console.WriteLine(se.ToString());
            }
            catch (SocketException se)
            {
                Send(state, "403 Archive server temporarily offline\r\n");
                Console.WriteLine(se.ToString());
            }
        }
        private void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var connection = (Connection)ar.AsyncState;
            if (connection.Client == null || connection.Stream == null)
                return;

            // Read data from the client socket.
            int bytesRead;
            try
            {
                bytesRead = connection.Stream.EndRead(ar);
            }
            catch (IOException)
            {
                Send(connection, "403 Archive server temporarily offline\r\n");
                return;
            }
            catch (SocketException)
            {
                Send(connection, "403 Archive server temporarily offline\r\n");
                return;
            }

            // There  might be more data, so store the data received so far.
            connection.Builder.Append(Encoding.ASCII.GetString(connection.Buffer, 0, bytesRead));

            // Not all data received OR no more but not yet ending with the delimiter. Get more.
            var content = connection.Builder.ToString();
            if (bytesRead == Connection.BUFFER_SIZE || (bytesRead == 0 && !content.EndsWith("\r\n", StringComparison.Ordinal)))
            {
                if (!connection.Client.Connected)
                    return;

                try
                {
                    connection.Stream.BeginRead(connection.Buffer, 0, Connection.BUFFER_SIZE, ReadCallback, connection);
                }
                catch (IOException sex)
                {
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    Console.Write(sex.ToString());
                }
                catch (SocketException sex)
                {
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    Console.Write(sex.ToString());
                }
                return;
            }

            // All the data has been read from the 
            // client. Display it on the console.
            if (ShowBytes && ShowData)
                Console.WriteLine("{0}:{1} <<< {2} bytes: {3}", ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Address, ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Port, content.Length, content.TrimEnd('\r', '\n'));
            else if (ShowBytes)
                Console.WriteLine("{0}:{1} <<< {2} bytes", ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Address, ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Port, content.Length);
            else if (ShowData)
                Console.WriteLine("{0}:{1} <<< {2}", ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Address, ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Port, content.TrimEnd('\r', '\n'));

            if (connection.InProcessCommand != null && connection.InProcessCommand.MessageHandler != null)
            {
                // Ongoing read - don't parse it for commands
                var result = connection.InProcessCommand.MessageHandler.Invoke(connection, content, connection.InProcessCommand);
                if (result.IsQuitting)
                    connection.InProcessCommand = null;
            }
            else
            {
                var command = content.Split(' ').First().TrimEnd('\r', '\n');
                if (_commandDirectory.ContainsKey(command))
                {
                    try
                    {
                        if (ShowCommands)
                            Console.WriteLine("{0}:{1} <<< {2}", ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Address, ((IPEndPoint)connection.Client.Client.RemoteEndPoint).Port, content.TrimEnd('\r', '\n'));
                                
                        var result = _commandDirectory[command].Invoke(connection, content);

                        if (!result.IsHandled)
                            Send(connection, "500 Unknown command\r\n");
                        else if (result.MessageHandler != null)
                            connection.InProcessCommand = result;
                        else if (result.IsQuitting)
                            return;
                    }
                    catch (Exception ex)
                    {
                        Send(connection, "403 Archive server temporarily offline\r\n");
                        Console.WriteLine(ex.ToString());
                    }
                }
                else
                    Send(connection, "500 Unknown command\r\n");
            }

            connection.Builder.Clear();

            if (!connection.Client.Connected)
                return;

            // Not all data received. Get more.
            try
            {
                connection.Stream.BeginRead(connection.Buffer, 0, Connection.BUFFER_SIZE, ReadCallback, connection);
            }
            catch (IOException sex)
            {
                Send(connection, "403 Archive server temporarily offline\r\n");
                Console.WriteLine(sex.ToString());
            }
            catch (SocketException sex)
            {
                Send(connection, "403 Archive server temporarily offline\r\n");
                Console.WriteLine(sex.ToString());
            }
        }
        private void Send(Connection connection, string data)
        {
            Send(connection, data, true, Encoding.UTF8);
        }
        private void Send([NotNull] Connection connection, [NotNull] string data, bool async, [NotNull] Encoding encoding)
        {
            if (connection.Client == null || connection.Stream == null)
                return;

            // Convert the string data to byte data using ASCII encoding.
            var byteData = encoding.GetBytes(data);
            var remoteEndPoint = (IPEndPoint)connection.Client.Client.RemoteEndPoint;

            try
            {
                if (async)
                {
                    // Begin sending the data to the remote device.
                    connection.Stream.BeginWrite(byteData, 0, byteData.Length, SendCallback, new SendAsyncState {Payload = data, Connection = connection});
                }
                else // Block
                {
                    connection.Stream.Write(byteData, 0, byteData.Length);
                    if (ShowBytes && ShowData)
                        Console.WriteLine("{0}:{1} >>> {2} bytes: {3}", remoteEndPoint.Address, remoteEndPoint.Port, byteData.Length, data.TrimEnd('\r', '\n'));
                    else if (ShowBytes)
                        Console.WriteLine("{0}:{1} >>> {2} bytes", remoteEndPoint.Address, remoteEndPoint.Port, byteData.Length);
                    else if (ShowData)
                        Console.WriteLine("{0}:{1} >>> {2}", remoteEndPoint.Address, remoteEndPoint.Port, data.TrimEnd('\r', '\n'));
                }
            }
            catch (IOException)
            {
                // Don't send 403 - the sending socket isn't working.
                Console.WriteLine("{0}:{1} XXX CONNECTION TERMINATED", remoteEndPoint.Address, remoteEndPoint.Port);
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                Console.WriteLine("{0}:{1} XXX CONNECTION TERMINATED", remoteEndPoint.Address, remoteEndPoint.Port);
            }
        }
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var handler = (SendAsyncState)ar.AsyncState;

                // Complete sending the data to the remote device.
                if (handler.Connection.Stream == null)
                    return;

                handler.Connection.Stream.EndWrite(ar);

                if (handler.Connection.Client == null)
                    return;

                var remoteEndPoint = (IPEndPoint)handler.Connection.Client.Client.RemoteEndPoint;
                //if (ShowBytes && ShowData)
                //    Console.WriteLine("{0}:{1} >>> {2} bytes: {3}", remoteEndPoint.Address, remoteEndPoint.Port, bytesSent, handler.Payload.TrimEnd('\r', '\n'));
                //else if (ShowBytes)
                //    Console.WriteLine("{0}:{1} >>> {2} bytes", remoteEndPoint.Address, remoteEndPoint.Port, bytesSent);
                //else 
                if (ShowData)
                    Console.WriteLine("{0}:{1} >>> {2}", remoteEndPoint.Address, remoteEndPoint.Port, handler.Payload.TrimEnd('\r', '\n'));
            }
            catch (ObjectDisposedException)
            {
                // Don't send 403 - the sending socket isn't working
            }
            catch (Exception e)
            {
                // Don't send 403 - the sending socket isn't working
                Console.WriteLine(e.ToString());
                throw;
            }
        }
        #endregion
        
        #region NNTP Commands
        private CommandProcessingResult Article(Connection connection, string content)
        {
            var param = (string.Compare(content, "ARTICLE\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!connection.CurrentArticleNumber.HasValue)
                {
                    Send(connection, "430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else if (string.IsNullOrEmpty(connection.CurrentNewsgroup) && !param.StartsWith("<", StringComparison.Ordinal))
            {
                Send(connection, "412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }
            
            using (var session = _sessionProvider.Invoke())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == connection.CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleId;
                    if (!int.TryParse(param, out articleId))
                    {
                        Send(connection, "423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send(connection, "430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send(connection, "423 No article with that number\r\n");
                            break;
                        case 3:
                            Send(connection, "420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (connection.SendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n",
                                    (!string.IsNullOrEmpty(connection.CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0) ? article.Id : 0,
                                    article.MessageId), false, Encoding.UTF8);
                                break;
                            case 2:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n", article.Id, article.MessageId), false, Encoding.UTF8);
                                break;
                            case 3:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n", article.Id, article.MessageId), false, Encoding.UTF8);
                                break;
                        }

                        Send(connection, article.Headers + "\r\n", false, Encoding.UTF8);
                        Send(connection, article.Body + "\r\n.\r\n", false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult AuthInfo(Connection connection, string content)
        {
            // RFC 4643 - NNTP AUTHENTICATION
            var param = (string.Compare(content, "AUTHINFO\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrWhiteSpace(param))
            {
                Send(connection, "481 Authentication failed/rejected\r\n");
                return new CommandProcessingResult(true);
            }

            var args = param.Split(' ');
            if (args.Length != 2)
            {
                Send(connection, "481 Authentication failed/rejected\r\n");
                return new CommandProcessingResult(true);
            }

            if (string.Compare(args[0], "USER", StringComparison.OrdinalIgnoreCase) == 0)
            {
                connection.Username = args.Skip(1).Aggregate((c,n) => c + " " + n);
                Send(connection, "381 Password required\r\n");
                return new CommandProcessingResult(true);
            }

            if (string.Compare(args[0], "PASS", StringComparison.OrdinalIgnoreCase) == 0)
            {
                if (connection.Username == null)
                {
                    Send(connection, "482 Authentication commands issued out of sequence\r\n");
                    return new CommandProcessingResult(true);
                }

                if (connection.Authenticated)
                {
                    Send(connection, "502 Command unavailable\r\n");
                    return new CommandProcessingResult(true);
                }

                var password = args.Skip(1).Aggregate((c, n) => c + " " + n);
                var saltBytes = new byte[64];
                var rng = RandomNumberGenerator.Create();
                rng.GetNonZeroBytes(saltBytes);

                Administrator[] allAdmins;
                using (var session = _sessionProvider.Invoke())
                {
                    allAdmins = session.Query<Administrator>().ToArray();
                    session.Close();
                }

                var admin = allAdmins
                        .SingleOrDefault(a =>
                                a.Username == connection.Username &&
                                a.PasswordHash ==
                                Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(a.PasswordSalt, password)))));
                
                if (admin == null)
                {
                    Send(connection, "481 Authentication failed/rejected\r\n");
                    return new CommandProcessingResult(true);
                }

                connection.CanApproveGroups = admin.CanApproveGroups;
                connection.CanCancel = admin.CanCancel;
                connection.CanCheckGroups = admin.CanCheckGroups;
                connection.CanCreateGroup = admin.CanCreateGroup;
                connection.CanDeleteGroup = admin.CanDeleteGroup;
                connection.CanInject = admin.CanInject;
                
                connection.Authenticated = true;
                
                Send(connection, "281 Authentication accepted\r\n");
                return new CommandProcessingResult(true);
            }

            return new CommandProcessingResult(false);
        }
        private CommandProcessingResult Body(Connection connection, string content)
        {
            var param = (string.Compare(content, "BODY\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!connection.CurrentArticleNumber.HasValue)
                {
                    Send(connection, "430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(connection.CurrentNewsgroup))
                {
                    Send(connection, "412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = _sessionProvider.Invoke())
            {
                int type;
                Article article;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == connection.CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().Single(a => a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleId;
                    if (!int.TryParse(param, out articleId))
                    {
                        Send(connection, "423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send(connection, "430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send(connection, "423 No article with that number\r\n");
                            break;
                        case 3:
                            Send(connection, "420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (connection.SendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n",
                                    (!string.IsNullOrEmpty(connection.CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0) ? article.Id : 0,
                                    article.MessageId));
                                break;
                            case 2:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                            case 3:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                        }

                        Send(connection, article.Body, false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Capabilities(Connection connection)
        {
            var sb = new StringBuilder();
            sb.Append("101 Capability list:\r\n");
            sb.Append("VERSION 2\r\n");
            //sb.Append("IHAVE\r\n");
            sb.Append("HDR\r\n");
            sb.Append("LIST ACTIVE NEWSGROUPS\r\n");
            //sb.Append("NEWNEWS\r\n");
            //sb.Append("OVER\r\n");
            sb.Append("POST\r\n");
            sb.Append("READER\r\n");
            if (AllowStartTLS)
                sb.Append("STARTTLS\r\n");
            sb.Append("IMPLEMENTATION McNNTP 1.0.0\r\n");
            sb.Append(".\r\n");
            Send(connection, sb.ToString());
            return new CommandProcessingResult(true);
        }

        private CommandProcessingResult Date(Connection connection)
        {
            Send(connection, string.Format(CultureInfo.InvariantCulture, "111 {0:yyyyMMddHHmmss}\r\n", DateTime.UtcNow));
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Group(Connection connection, string content)
        {
            content = content.TrimEnd('\r', '\n').Substring(content.IndexOf(' ') + 1).Split(' ')[0];
            Newsgroup ng;
            using (var session = _sessionProvider.Invoke())
            {
                ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == content);
            }

            if (ng == null)
                Send(connection, string.Format("411 {0} is unknown\r\n", content));
            else
            {
                connection.CurrentNewsgroup = ng.Name;
                connection.CurrentArticleNumber = ng.LowWatermark;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
                if (ng.PostCount == 0)
// ReSharper restore ConvertIfStatementToConditionalTernaryExpression
                    Send(connection, string.Format("211 0 0 0 {0}\r\n", ng.Name));
                else
                    Send(connection, string.Format("211 {0} {1} {2} {3}\r\n", ng.PostCount, ng.LowWatermark, ng.HighWatermark, ng.Name));
            }
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Hdr(Connection connection, string content)
        {
            var parts = content.TrimEnd('\r', '\n').Split(' ');
            if (parts.Length < 2 || parts.Length > 3)
            {
                Send(connection, "501 Syntax Error\r\n");
                return new CommandProcessingResult(true);
            }
            
            int type;

            if (parts.Length == 3 && parts[2].StartsWith("<", StringComparison.Ordinal))
                type = 1;
            else if (parts.Length == 3 && !parts[2].StartsWith("<", StringComparison.Ordinal))
            {
                type = 2;
                int articleId;
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out articleId))
                {
                    Send(connection, "501 Syntax Error\r\n");
                    return new CommandProcessingResult(true);
                }

                if (string.IsNullOrEmpty(connection.CurrentNewsgroup))
                {
                    Send(connection, "412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else //if (parts.Length == 2)
            {
                type = 3;
                if (string.IsNullOrEmpty(connection.CurrentNewsgroup))
                {
                    Send(connection, "412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
                if (!connection.CurrentArticleNumber.HasValue)
                {
                    Send(connection, "420 Current article number is invalid\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = _sessionProvider.Invoke())
            {
                IEnumerable<Article> articles;
                switch (type)
                {
                    case 1:
                        articles = new[] { session.Query<Article>().SingleOrDefault(a => a.MessageId == parts[2]) };
                        break;
                    case 2:
                        var range = ParseRange(parts[2]);
                        if (range.Equals(default(System.Tuple<int, int?>)))
                        {
                            Send(connection, "501 Syntax Error\r\n");
                            return new CommandProcessingResult(true);
                        }

                        articles = (range.Item2.HasValue)
                            ? session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id >= range.Item1 && a.Id <= range.Item2)
                            : session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id >= range.Item1);
                        break;
                    case 3:
                        Debug.Assert(connection.CurrentArticleNumber.HasValue);
                        articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == connection.CurrentArticleNumber.Value);
                        break;
                    default:
                        // Unrecognized...
                        Send(connection, "501 Syntax Error\r\n");
                        return new CommandProcessingResult(true);
                }

                if (!articles.Any())
                    switch (type)
                    {
                        case 1:
                            Send(connection, "430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send(connection, "423 No articles in that range\r\n");
                            break;
                        case 3:
                            Send(connection, "420 Current article number is invalid\r\n");
                            break;
                    }
                else
                {
                    lock (connection.SendLock)
                    {
                        Send(connection, "225 Headers follow (multi-line)\r\n");

                        Func<Article, string> headerFunction;
                        switch (parts[1].ToUpperInvariant())
                        {
                            case "DATE":
                                headerFunction = a => a.Date;
                                break;
                            case "FROM":
                                headerFunction = a => a.From;
                                break;
                            case "MESSAGE-ID":
                                headerFunction = a => a.MessageId;
                                break;
                            case "REFERENCES":
                                headerFunction = a => a.References;
                                break;
                            case "SUBJECT":
                                headerFunction = a => a.Subject;
                                break;
                            default:
                            {
                                Dictionary<string, string> headers, headersAndFullLines;
                                headerFunction = a => Data.Article.TryParseHeaders(a.Headers, out headers, out headersAndFullLines) 
                                    ? headers.Any(h => string.Compare(h.Key, parts[1], StringComparison.OrdinalIgnoreCase) == 0)
                                        ? headers.Single(h => string.Compare(h.Key, parts[1], StringComparison.OrdinalIgnoreCase) == 0).Value
                                        : null
                                    : null;
                                break;
                            }
                        }

                        foreach (var article in articles)
                            if (type == 1)
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "{0} {1}\r\n",
                                    (!string.IsNullOrEmpty(connection.CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0) ? article.MessageId : "0",
                                    headerFunction.Invoke(article)), false, Encoding.UTF8);
                            else
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "{0} {1}\r\n",
                                    article.Id,
                                    headerFunction.Invoke(article)), false, Encoding.UTF8);

                        Send(connection, ".\r\n");
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Head(Connection connection, string content)
        {
            var param = (string.Compare(content, "HEAD\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!connection.CurrentArticleNumber.HasValue)
                {
                    Send(connection, "430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(connection.CurrentNewsgroup))
                {
                    Send(connection, "412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            
            using (var session = _sessionProvider.Invoke())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == connection.CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().FirstOrDefault(a => a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleId;
                    if (!int.TryParse(param, out articleId))
                    {
                        Send(connection, "423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send(connection, "430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send(connection, "423 No article with that number\r\n");
                            break;
                        case 3:
                            Send(connection, "420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (connection.SendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n",
                                    (string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0) ? article.Id : 0, article.MessageId));
                                break;
                            case 2:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                            case 3:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                        }

                        Send(connection, article.Headers + "\r\n.\r\n", false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Help(Connection connection)
        {
            var sb = new StringBuilder();
            sb.Append("100 Help text follows\r\n");

            var dirName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dirName != null && File.Exists(Path.Combine(dirName, "HelpFile.txt")))
            {
                using (var sr = new StreamReader(Path.Combine(dirName, "HelpFile.txt"), Encoding.UTF8))
                {
                    sb.Append(sr.ReadToEnd());
                    sr.Close();
                }
            }
            else
            {
                sb.Append("The list of commands understood by this server are:\r\n");
                foreach (var cmd in _commandDirectory)
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}\r\n", cmd.Key);
            }

            if (!sb.ToString().EndsWith("\r\n.\r\n"))
                sb.Append("\r\n.\r\n");
            
            Send(connection, sb.ToString());
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Last(Connection connection, string content)
        {
            // If the currently selected newsgroup is invalid, a 412 response MUST be returned.
            if (string.IsNullOrWhiteSpace(connection.CurrentNewsgroup))
            {
                Send(connection, "412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }

            var currentArticleNumber = connection.CurrentArticleNumber;

            Article previousArticle;

            if (!currentArticleNumber.HasValue)
            {
                Send(connection, "420 Current article number is invalid\r\n");
                return new CommandProcessingResult(true);
            }

            using (var session = _sessionProvider.Invoke())
            {
                previousArticle = session.Query<Article>().Fetch(a => a.Newsgroup)
                    .Where(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id < currentArticleNumber.Value)
                    .MaxBy(a => a.Id);
            }

            // If the current article number is already the first article of the newsgroup, a 422 response MUST be returned.
            if (previousArticle == null)
            {
                Send(connection, "422 No previous article in this group\r\n");
                return new CommandProcessingResult(true);
            }

            connection.CurrentArticleNumber = previousArticle.Id;

            Send(connection, string.Format("223 {0} {1} retrieved\r\n", previousArticle.Id, previousArticle.MessageId));
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult List(Connection connection, string content)
        {
            if (string.Compare(content, "LIST\r\n", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(content, "LIST ACTIVE\r\n", StringComparison.OrdinalIgnoreCase) == 0)
            {
                IList<Newsgroup> newsGroups;

                try
                {
                    using (var session = _sessionProvider.Invoke())
                    {
                        newsGroups = session.Query<Newsgroup>().OrderBy(n => n.Name).ToList();
                    }
                }
                catch (MappingException mex)
                {
                    Console.WriteLine(mex.ToString());
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }
                catch (Exception)
                {
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }

                lock (connection.SendLock)
                {
                    Send(connection, "215 list of newsgroups follows\r\n");
                    foreach (var ng in newsGroups)
                        Send(connection, string.Format("{0} {1} {2} {3}\r\n", ng.Name, ng.HighWatermark, ng.LowWatermark, connection.CanPost ? "y" : "n"), false, Encoding.UTF8);
                    Send(connection, ".\r\n");
                }
                return new CommandProcessingResult(true);
            }

            if (string.Compare(content, "LIST NEWSGROUPS\r\n", StringComparison.OrdinalIgnoreCase) == 0)
            {
                IList<Newsgroup> newsGroups;

                try
                {
                    using (var session = _sessionProvider.Invoke())
                    {
                        newsGroups = session.Query<Newsgroup>().OrderBy(n => n.Name).ToList();
                    }
                }
                catch (MappingException mex)
                {
                    Console.WriteLine(mex.ToString());
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }
                catch (Exception)
                {
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }

                lock (connection.SendLock)
                {
                    Send(connection, "215 information follows\r\n");
                    foreach (var ng in newsGroups)
                        Send(connection, string.Format("{0}\t{1}\r\n", ng.Name, ng.Description), false, Encoding.UTF8);
                    Send(connection, ".\r\n");
                }
                return new CommandProcessingResult(true);
            }

            Send(connection, "501 Syntax Error\r\n");
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult ListGroup(Connection connection, string content)
        {
            var parts = content.TrimEnd('\r', '\n').Split(' ');

            if (parts.Length == 1 && connection.CurrentNewsgroup == null)
                Send(connection, "412 No newsgroup selected\r\n");

            using (var session = _sessionProvider.Invoke())
            {
                var name = (parts.Length == 2) ? parts[1] : connection.CurrentNewsgroup;
                var ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);

                if (ng == null)
                    Send(connection, "411 No such newsgroup\r\n");
                else
                {
                    connection.CurrentNewsgroup = ng.Name;
                    if (ng.PostCount == 0)
                    {
                        lock (connection.SendLock)
                        {
                            Send(connection, string.Format("211 0 0 0 {0}\r\n", ng.Name));
                        }
                    }
                    else
                    {
                        IList<Article> articles;
                        if (parts.Length < 3)
                            articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name).OrderBy(a => a.Id).ToList();
                        else
                        {
                            var range = ParseRange(parts[2]);
                            if (range.Equals(default(System.Tuple<int, int?>)))
                            {
                                Send(connection, "501 Syntax Error\r\n");
                                return new CommandProcessingResult(true);
                            }

                            if (!range.Item2.HasValue) // LOW-
                                articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name && a.Id >= range.Item1).OrderBy(a => a.Id).ToList();
                            else // LOW-HIGH
                                articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name && a.Id >= range.Item1 && a.Id <= range.Item2.Value).ToList();
                        }

                        connection.CurrentArticleNumber = !articles.Any() ? default(long?) : articles.First().Id;

                        lock (connection.SendLock)
                        {
                            Send(connection, string.Format("211 {0} {1} {2} {3}\r\n", ng.PostCount, ng.LowWatermark, ng.HighWatermark, ng.Name), false, Encoding.UTF8);
                            foreach (var article in articles)
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "{0}\r\n", article.Id.ToString(CultureInfo.InvariantCulture)), false, Encoding.UTF8);
                            Send(connection, ".\r\n", false, Encoding.UTF8);
                        }
                    }
                }
            }


            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Mode(Connection connection, string content)
        {
            if (content.StartsWith("MODE READER", StringComparison.OrdinalIgnoreCase))
            {
                Send(connection, "200 This server is not a mode-switching server, but whatever!\r\n");
                return new CommandProcessingResult(true);
            }

            Send(connection, "501 Syntax Error\r\n");
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Newgroups(Connection connection, string content)
        {
            var parts = content.TrimEnd('\r', '\n').Split(' ');

            var dateTime = string.Join(" ", parts.ElementAt(1), parts.ElementAt(2));
            DateTime afterDate;
            if (!(parts.ElementAt(1).Length == 8 && DateTime.TryParseExact(dateTime, "yyyyMMdd HHmmss", CultureInfo.InvariantCulture, parts.Length == 4 ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out afterDate)))
                if (!(parts.ElementAt(1).Length == 6 && DateTime.TryParseExact(dateTime, "yyMMdd HHmmss", CultureInfo.InvariantCulture, parts.Length == 4 ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out afterDate)))
                    afterDate = DateTime.MinValue;

            if (afterDate != DateTime.MinValue)
            {
                IList<Newsgroup> newsGroups;
                using (var session = _sessionProvider.Invoke())
                {
                    newsGroups = session.Query<Newsgroup>().Where(n => n.CreateDate >= afterDate).OrderBy(n => n.Name).ToList();
                }

                lock (connection.SendLock)
                {
                    Send(connection, "231 List of new newsgroups follows (multi-line)\r\n", false, Encoding.UTF8);
                    foreach (var ng in newsGroups)
                        Send(connection, string.Format("{0} {1} {2} {3}\r\n", ng.Name, ng.HighWatermark, ng.LowWatermark,
                            connection.CanPost ? "y" : "n"), false, Encoding.UTF8);
                    Send(connection, ".\r\n", false, Encoding.UTF8);
                }
            }
            else
            {
                lock (connection.SendLock)
                {
                    Send(connection, "231 List of new newsgroups follows (multi-line)\r\n");
                    Send(connection, ".\r\n");
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Next(Connection connection, string content)
        {
            // If the currently selected newsgroup is invalid, a 412 response MUST be returned.
            if (string.IsNullOrWhiteSpace(connection.CurrentNewsgroup))
            {
                Send(connection, "412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }

            var currentArticleNumber = connection.CurrentArticleNumber;

            Article previousArticle;

            if (!currentArticleNumber.HasValue)
            {
                Send(connection, "420 Current article number is invalid\r\n");
                return new CommandProcessingResult(true);
            }

            using (var session = _sessionProvider.Invoke())
            {
                previousArticle = session.Query<Article>().Fetch(a => a.Newsgroup)
                    .Where(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id > currentArticleNumber.Value)
                    .MinBy(a => a.Id);
            }

            // If the current article number is already the last article of the newsgroup, a 421 response MUST be returned.
            if (previousArticle == null)
            {
                Send(connection, "421 No next article in this group\r\n");
                return new CommandProcessingResult(true);
            }

            connection.CurrentArticleNumber = previousArticle.Id;

            Send(connection, string.Format("223 {0} {1} retrieved\r\n", previousArticle.Id, previousArticle.MessageId));
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Post(Connection connection, string content)
        {
            if (!connection.CanPost)
            {
                Send(connection, "440 Posting not permitted\r\n");
                return new CommandProcessingResult(true);
            }

            Send(connection, "340 Send article to be posted\r\n");

            Func<Connection, string, CommandProcessingResult, CommandProcessingResult> messageAccumulator = null;
            messageAccumulator = (conn, msg, prev) =>
            {
                if (
                    // Message ends naturally
                    msg != null && (msg.EndsWith("\r\n.\r\n")) ||
                    // Message delimiter comes in second batch
                    (prev != null && prev.Message != null && prev.Message.EndsWith("\r\n") && msg != null && msg.EndsWith(".\r\n")))
                {
                    try
                    {
                        Article article;
                        if (!Data.Article.TryParse(prev.Message == null ? msg.Substring(0, msg.Length - 5) : prev.Message + msg, out article))
                        {
                            Send(connection, "441 Posting failed\r\n");
                            return new CommandProcessingResult(true, true);
                        }

                        bool canApprove;
                        if (connection.CanInject)
                            canApprove = true;
                        else if (string.IsNullOrWhiteSpace(connection.CanApproveGroups))
                            canApprove = false;
                        else if (connection.CanApproveGroups == "*")
                            canApprove = true;
                        else
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            canApprove = article.Newsgroups.Split(' ').All(n => connection.CanApproveGroups.Split(' ').Contains(n));
                        }

                        if (!canApprove)
                        {
                            article.Approved = null;
                            article.RemoveHeader("Approved");
                        }
                        if (!connection.CanCancel)
                        {
                            article.Supersedes = null;
                            article.RemoveHeader("Supersedes");
                        }
                        if (!connection.CanInject)
                        {
                            article.InjectionDate = DateTime.UtcNow.ToString("r");
                            article.ChangeHeader("Injection-Date", DateTime.UtcNow.ToString("r"));
                            article.InjectionInfo = null;
                            article.RemoveHeader("Injection-Info");
                            article.Xref = null;
                            article.RemoveHeader("Xref");

                            // RFC 5536 3.2.6. The Followup-To header field SHOULD NOT appear in a message, unless its content is different from the content of the Newsgroups header field.
                            if (!string.IsNullOrWhiteSpace(article.FollowupTo) &&
                                string.Compare(article.FollowupTo, article.Newsgroups, StringComparison.OrdinalIgnoreCase) == 0)
                                article.FollowupTo = null;
                        }

                        if ((article.Control != null && article.Control.StartsWith("cancel ", StringComparison.OrdinalIgnoreCase) && !connection.CanCancel) ||
                            (article.Control != null && article.Control.StartsWith("newgroup ", StringComparison.OrdinalIgnoreCase) && !connection.CanCreateGroup) ||
                            (article.Control != null && article.Control.StartsWith("rmgroup ", StringComparison.OrdinalIgnoreCase) && !connection.CanDeleteGroup) ||
                            (article.Control != null && article.Control.StartsWith("checkgroups ", StringComparison.OrdinalIgnoreCase) && !connection.CanCheckGroups))
                        {
                            Send(connection, "480 Permission to issue control message denied\r\n");
                            return new CommandProcessingResult(true, true);
                        }
                             
                        article.Control = null;

                        using (var session = _sessionProvider.Invoke())
                        {
                            foreach (var newsgroupName in article.Newsgroups.Split(' '))
                            {
                                var newsgroupNameClosure = newsgroupName;
                                var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == newsgroupNameClosure);
                                if (newsgroup == null)
                                    continue;

                                article.Id = 0;
                                article.Newsgroup = newsgroup;
                                article.Path = ServerPath;
                                session.Save(article);
                            }

                            session.Close();
                        }

                        Send(connection, "240 Article received OK\r\n");

                        return new CommandProcessingResult(true, true)
                        {
                            Message = prev.Message + msg
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        Send(connection, "441 Posting failed\r\n");
                        return new CommandProcessingResult(true);
                    }
                }

                return new CommandProcessingResult(true, false)
                {
                    MessageHandler = messageAccumulator,
                    Message = prev == null ? msg : prev.Message == null ? msg : prev.Message + "\r\n" + msg
                };
            };

            return messageAccumulator.Invoke(connection, null, null);
        }
        private CommandProcessingResult Quit(Connection connection)
        {
            lock (connection.SendLock)
            {
                if (connection.Client != null)
                {
                    Send(connection, "205 closing connection\r\n", false, Encoding.UTF8); // Block.
                    connection.Client.Client.Shutdown(SocketShutdown.Both);
                    connection.Client.Close();
                }
            }
            return new CommandProcessingResult(true, true);
        }
        private CommandProcessingResult StartTLS(Connection connection, string content)
        {
            if (connection.TLS)
            {
                Send(connection, "502 Command unavailable\r\n");
                return new CommandProcessingResult(true);
            }

            Send(connection, "580 Can not initiate TLS negotiation\r\n");
            return new CommandProcessingResult(true);
        }

        private CommandProcessingResult Stat(Connection connection, string content)
        {
            var param = (string.Compare(content, "STAT\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!connection.CurrentArticleNumber.HasValue)
                {
                    Send(connection, "430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(connection.CurrentNewsgroup))
                {
                    Send(connection, "412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            
            using (var session = _sessionProvider.Invoke())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == connection.CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleId;
                    if (!int.TryParse(param, out articleId))
                    {
                        Send(connection, "423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == connection.CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send(connection, "430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send(connection, "423 No article with that number\r\n");
                            break;
                        case 3:
                            Send(connection, "420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (connection.SendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n",
                                    (!string.IsNullOrEmpty(connection.CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0) ? article.Id : 0,
                                    article.MessageId));
                                break;
                            case 2:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n", article.Id, article.MessageId));
                                break;
                            case 3:
                                Send(connection, string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n", article.Id, article.MessageId));
                                break;
                        }
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult XOver(Connection connection, string content)
        {
            var rangeExpression = content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (connection.CurrentNewsgroup == null)
                Send(connection, "412 No news group current selected\r\n");
            else
            {
                Newsgroup ng;
                IList<Article> articles;

                try
                {
                    using (var session = _sessionProvider.Invoke())
                    {
                        ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == connection.CurrentNewsgroup);

                        if (string.IsNullOrEmpty(rangeExpression))
                            articles =
                                session.Query<Article>()
                                    .Fetch(a => a.Newsgroup)
                                    .Where(a => a.Newsgroup.Name == ng.Name)
                                    .OrderBy(a => a.Id)
                                    .ToList();
                        else
                        {
                            var range = ParseRange(rangeExpression);
                            if (range.Equals(default(System.Tuple<int, int?>)))
                            {
                                Send(connection, "501 Syntax Error\r\n");
                                return new CommandProcessingResult(true);
                            }

                            if (!range.Item2.HasValue) // LOW-
                            {
                                articles =
                                    session.Query<Article>()
                                        .Fetch(a => a.Newsgroup)
                                        .Where(a => a.Newsgroup.Name == ng.Name && a.Id >= range.Item1)
                                        .OrderBy(a => a.Id)
                                        .ToList();
                            }
                            else // LOW-HIGH
                            {
                                articles =
                                    session.Query<Article>()
                                        .Fetch(a => a.Newsgroup)
                                        .Where(a => a.Newsgroup.Name == ng.Name && a.Id >= range.Item1 && a.Id <= range.Item2.Value)
                                        .OrderBy(a => a.Id)
                                        .ToList();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Send(connection, "403 Archive server temporarily offline\r\n");
                    Console.WriteLine(ex.ToString());
                    return new CommandProcessingResult(true);
                }

                if (ng == null)
                {
                    Send(connection, "411 No such newsgroup\r\n");
                    return new CommandProcessingResult(true);
                }

                if (!articles.Any())
                {
                    Send(connection, "420 No article(s) selected\r\n");
                    return new CommandProcessingResult(true);
                }

                connection.CurrentArticleNumber = articles.First().Id;
                Func<string, string> unfold = i => string.IsNullOrWhiteSpace(i) ? i : i.Replace("\r\n", "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

                lock (connection.SendLock)
                {
                    Send(connection, "224 Overview information follows\r\n", false, Encoding.UTF8);
                    foreach (var article in articles)
                        Send(connection,
                            string.Format(CultureInfo.InvariantCulture,
                                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\r\n",
                                string.CompareOrdinal(article.Newsgroup.Name, connection.CurrentNewsgroup) == 0 ? article.Id : 0,
                                unfold(article.Subject),
                                unfold(article.From),
                                unfold(article.Date),
                                unfold(article.MessageId),
                                unfold(article.References),
                                unfold((article.Body.Length*2).ToString(CultureInfo.InvariantCulture)),
                                unfold(article.Body.Split(new[] {"\r\n"}, StringSplitOptions.None).Length.ToString(CultureInfo.InvariantCulture))), false,
                            Encoding.UTF8);
                    Send(connection, ".\r\n", false, Encoding.UTF8);
                }
            }

            return new CommandProcessingResult(true);
        }
        #endregion

        #region Interactivity
        public bool ShowBytes { get; set; }
        public bool ShowCommands { get; set; }
        public bool ShowData { get; set; }

        internal static Dictionary<IPEndPoint, string> GetAllBuffs()
        {
        again:
            foreach (var conn in _connections)
                lock (conn.SendLock)
                {
                    try
                    {
                        if (conn.Client == null || conn.Client.Client == null || conn.Client.Client.Connected) 
                            continue;

                        _connections.Remove(conn);
                        conn.Client.Client.Shutdown(SocketShutdown.Both);
                        conn.Client.Close();
                        conn.Client = null;
                        goto again;
                    }
                    catch (ObjectDisposedException)
                    {
                        conn.Client = null;
                        _connections.Remove(conn);
                        goto again;
                    }
                    catch (IOException)
                    {
                        conn.Client = null;
                        _connections.Remove(conn);
                        goto again;
                    }
                    catch (SocketException)
                    {
                        conn.Client = null;
                        _connections.Remove(conn);
                        goto again;
                    }
                }

            // ReSharper disable once PossibleNullReferenceException
            return _connections.Where(c => c.Client != null).ToDictionary(conn => (IPEndPoint)conn.Client.Client.RemoteEndPoint, conn => conn.Builder.ToString());
        }
        #endregion
        private static System.Tuple<int, int?> ParseRange(string input)
        {
            int low, high;
            if (input.IndexOf('-') == -1)
            {
                if (!int.TryParse(input, out low))
                    return default(System.Tuple<int, int?>);
                return new System.Tuple<int, int?>(low, low);
            }
            if (input.EndsWith("-", StringComparison.Ordinal))
            {
                if (!int.TryParse(input, out low))
                    return default(System.Tuple<int, int?>);
                return new System.Tuple<int, int?>(low, null);
            }

            if (!int.TryParse(input.Substring(0, input.IndexOf('-')), NumberStyles.Integer, CultureInfo.InvariantCulture, out low))
                return default(System.Tuple<int, int?>);
            if (!int.TryParse(input.Substring(input.IndexOf('-') + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out high))
                return default(System.Tuple<int, int?>);

            return new System.Tuple<int, int?>(low, high);
        }

        public void InitializeDatabase()
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

            using (var connection = new SQLiteConnection(configuration.GetProperty("connection.connection_string")))
            {
                connection.Open();
                try
                {
                    var update = new SchemaUpdate(configuration);
                    update.Execute(false, true);

                    // Update failed..  recreate it.
                    if (!VerifyDatabase())
                    {
                        var export = new SchemaExport(configuration);
                        export.Execute(false, true, false, connection, null);

                        using (var session = _sessionProvider.Invoke())
                        {
                            session.Save(new Newsgroup
                            {
                                CreateDate = DateTime.UtcNow,
                                Description = "Control group for the repository",
                                Name = "freenews.config"
                            });
                            session.Close();
                        }
                    }
                    else
                    {
                        // Ensure placeholder data is there.
                        using (var session = _sessionProvider.Invoke())
                        {
                            var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                            if (newsgroupCount > 0)
                                session.Save(new Newsgroup
                                {
                                    CreateDate = DateTime.UtcNow,
                                    Description = "Control group for the repository",
                                    Name = "freenews.config"
                                });
                        }
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public bool VerifyDatabase()
        {
            try
            {
                using (var session = _sessionProvider.Invoke())
                {
                    var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                    Console.WriteLine("Verified database has {0} newsgroups", newsgroupCount);

                    var articleCount = session.Query<Article>().Count(a => a.Headers != null);
                    Console.WriteLine("Verified database has {0} articles", articleCount);

                    var adminCount = session.Query<Administrator>().Count(a => a.CanInject);
                    Console.WriteLine("Verified database has {0} local admins", adminCount);

                    session.Close();

                    return newsgroupCount > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}