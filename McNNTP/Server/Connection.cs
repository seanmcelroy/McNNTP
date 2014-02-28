using JetBrains.Annotations;
using McNNTP.Server.Data;
using NHibernate;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MoreLinq;
using log4net;

namespace McNNTP.Server
{
    // State object for reading client data asynchronously
    internal class Connection
    {
        private static readonly Dictionary<string, Func<Connection, string, CommandProcessingResult>> _commandDirectory;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(Connection));

        // Client socket.
        [NotNull] 
        private readonly TcpClient _client;
        [NotNull] 
        private readonly Stream _stream;
        [NotNull] 
        private readonly object _sendLock = new object();
        // Size of receive buffer.
        private const int BUFFER_SIZE = 1024;
        // Receive buffer.
        [NotNull]
        private readonly byte[] _buffer = new byte[BUFFER_SIZE];
        // Received data string.
        [NotNull]
        private readonly StringBuilder _builder = new StringBuilder();

        [CanBeNull]
        private CommandProcessingResult _inProcessCommand;

        public bool AllowStartTls { get; set; }
        public bool CanPost { get; private set; }
        public bool ShowBytes { get; set; }
        public bool ShowCommands { get; set; }
        public bool ShowData { get; set; }
        public string ServerName { get; set; }

        #region Administrator functions
        [CanBeNull]
        public string CanApproveGroups { get; set; }
        public bool CanCancel { get; set; }
        public bool CanCreateGroup { get; set; }
        public bool CanDeleteGroup { get; set; }
        public bool CanCheckGroups { get; set; }
        /// <summary>
        /// Indicates the connection can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public bool CanInject { get; set; }
        #endregion

        #region Authentication
        [CanBeNull]
        public string Username { get; set; }
        public bool Authenticated { get; set; }
        public bool TLS { get; set; }
        #endregion

        [CanBeNull]
        public string CurrentNewsgroup { get; private set; }
        public long? CurrentArticleNumber { get; private set; }

        static Connection()
        {
            _commandDirectory = new Dictionary<string, Func<Connection, string, CommandProcessingResult>>
                {
                    {"ARTICLE", (c, data) => c.Article(data) },
                    {"AUTHINFO", (c, data) => c.AuthInfo(data)},
                    {"BODY", (c, data) => c.Body(data)},
                    {"CAPABILITIES", (c, data) => c.Capabilities() },
                    {"DATE", (c, data) => c.Date()},
                    {"GROUP", (c, data) => c.Group(data)},
                    {"HDR", (c, data) => c.Hdr(data)},
                    {"HEAD", (c, data) => c.Head(data)},
                    {"HELP", (c, data) => c.Help()},
                    {"LAST", (c, data) => c.Last()},
                    {"LIST", (c, data) => c.List(data)},
                    {"LISTGROUP", (c, data) => c.ListGroup(data)},
                    {"MODE", (c, data) => c.Mode(data)},
                    {"NEWGROUPS", (c, data) => c.Newgroups(data)},
                    {"NEXT", (c, data) => c.Next()},
                    {"POST", (c, data) => c.Post()},
                    {"STAT", (c, data) => c.Stat(data)},
                    {"XOVER", (c, data) => c.XOver(data)},
                    {"QUIT", (c, data) => c.Quit()}
                };
        }

        public Connection(
            [NotNull] TcpClient client,
            [NotNull] Stream stream,
            string serverName = "freenews.local",
            bool allowStartTls = true,
            bool canPost = true,
            bool showBytes = false,
            bool showCommands = false,
            bool showData = false,
            bool tls = false)
        {
            AllowStartTls = allowStartTls;
            CanPost = canPost;
            _client = client;
            ServerName = serverName;
            ShowBytes = showBytes;
            ShowCommands = showCommands;
            ShowData = showData;
            _stream = stream;
            TLS = tls;
        }

        #region IO and Connection Management
        public void Process()
        {
            // ReSharper disable ConvertIfStatementToConditionalTernaryExpression
            if (CanPost)
                // ReSharper restore ConvertIfStatementToConditionalTernaryExpression
                Send("200 Service available, posting allowed\r\n");
            else
                Send("201 Service available, posting prohibited\r\n");

            System.Diagnostics.Debug.Assert(_stream != null);
            try
            {
                _stream.BeginRead(_buffer, 0, BUFFER_SIZE, ReadCallback, null);
            }
            catch (IOException se)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.Error("I/O Exception on BeginRead", se);
            }
            catch (SocketException se)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.Error("Socket Exception on BeginRead", se);
            }
        }
        private void ReadCallback(IAsyncResult ar)
        {
            // Read data from the client socket.
            int bytesRead;
            try
            {
                bytesRead = _stream.EndRead(ar);
            }
            catch (IOException ioe)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.ErrorFormat("I/O Exception on EndRead", ioe);
                return;
            }
            catch (SocketException sex)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.ErrorFormat("Socket Exception on EndRead", sex);
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            // There  might be more data, so store the data received so far.
            _builder.Append(Encoding.ASCII.GetString(_buffer, 0, bytesRead));

            // Not all data received OR no more but not yet ending with the delimiter. Get more.
            var content = _builder.ToString();
            if (bytesRead == BUFFER_SIZE || (bytesRead == 0 && !content.EndsWith("\r\n", StringComparison.Ordinal)))
            {
                if (!_client.Connected)
                    return;

                try
                {
                    _stream.BeginRead(_buffer, 0, BUFFER_SIZE, ReadCallback, null);
                }
                catch (IOException ioe)
                {
                    Send("403 Archive server temporarily offline\r\n");
                    _logger.ErrorFormat("I/O Exception on BeginRead", ioe);
                }
                catch (SocketException sex)
                {
                    Send("403 Archive server temporarily offline\r\n");
                    _logger.ErrorFormat("Socket Exception on BeginRead", sex);
                }
                return;
            }

            // All the data has been read from the 
            // client. Display it on the console.
            if (ShowBytes && ShowData)
                _logger.TraceFormat("{0}:{1} >{2}> {3} bytes: {4}", ((IPEndPoint)_client.Client.RemoteEndPoint).Address, ((IPEndPoint)_client.Client.RemoteEndPoint).Port, TLS ? "!" : ">", content.Length, content.TrimEnd('\r', '\n'));
            else if (ShowBytes)
                _logger.TraceFormat("{0}:{1} >{2}> {3} bytes", ((IPEndPoint)_client.Client.RemoteEndPoint).Address, ((IPEndPoint)_client.Client.RemoteEndPoint).Port, TLS ? "!" : ">", content.Length);
            else if (ShowData)
                _logger.TraceFormat("{0}:{1} >{2}> {3}", ((IPEndPoint)_client.Client.RemoteEndPoint).Address, ((IPEndPoint)_client.Client.RemoteEndPoint).Port, TLS ? "!" : ">", content.TrimEnd('\r', '\n'));

            if (_inProcessCommand != null && _inProcessCommand.MessageHandler != null)
            {
                // Ongoing read - don't parse it for commands
                var result = _inProcessCommand.MessageHandler.Invoke(content, _inProcessCommand);
                if (result.IsQuitting)
                    _inProcessCommand = null;
            }
            else
            {
                var command = content.Split(' ').First().TrimEnd('\r', '\n');
                if (_commandDirectory.ContainsKey(command))
                {
                    try
                    {
                        if (ShowCommands)
                            _logger.TraceFormat("{0}:{1} >{2}> {3}", ((IPEndPoint)_client.Client.RemoteEndPoint).Address, ((IPEndPoint)_client.Client.RemoteEndPoint).Port, TLS ? "!" : ">", content.TrimEnd('\r', '\n'));

                        var result = _commandDirectory[command].Invoke(this, content);

                        if (!result.IsHandled)
                            Send("500 Unknown command\r\n");
                        else if (result.MessageHandler != null)
                            _inProcessCommand = result;
                        else if (result.IsQuitting)
                            return;
                    }
                    catch (Exception ex)
                    {
                        Send("403 Archive server temporarily offline\r\n");
                        _logger.Error("Exception processing a command", ex);
                    }
                }
                else
                    Send("500 Unknown command\r\n");
            }

            _builder.Clear();

            if (!_client.Connected)
                return;

            // Not all data received. Get more.
            try
            {
                _stream.BeginRead(_buffer, 0, BUFFER_SIZE, ReadCallback, null);
            }
            catch (IOException sex)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.Error("I/O Exception on BeginRead", sex);
            }
            catch (SocketException sex)
            {
                Send("403 Archive server temporarily offline\r\n");
                _logger.Error("Socket Exception on BeginRead", sex);
            }
        }
        public void Send(string data)
        {
            Send(data, true, Encoding.UTF8);
        }
        public void Send([NotNull] string data, bool async, [NotNull] Encoding encoding)
        {
            // Convert the string data to byte data using ASCII encoding.
            var byteData = encoding.GetBytes(data);
            var remoteEndPoint = (IPEndPoint)_client.Client.RemoteEndPoint;

            try
            {
                if (async)
                {
                    // Begin sending the data to the remote device.
                    _stream.BeginWrite(byteData, 0, byteData.Length, SendCallback, data);
                }
                else // Block
                {
                    _stream.Write(byteData, 0, byteData.Length);
                    if (ShowBytes && ShowData)
                        _logger.TraceFormat("{0}:{1} <{2}< {3} bytes: {4}", remoteEndPoint.Address, remoteEndPoint.Port, TLS ? "!" : "<", byteData.Length, data.TrimEnd('\r', '\n'));
                    else if (ShowBytes)
                        _logger.TraceFormat("{0}:{1} <{2}< {3} bytes", remoteEndPoint.Address, remoteEndPoint.Port, TLS ? "!" : "<", byteData.Length);
                    else if (ShowData)
                        _logger.TraceFormat("{0}:{1} <{2}< {3}", remoteEndPoint.Address, remoteEndPoint.Port, TLS ? "!" : "<", data.TrimEnd('\r', '\n'));
                }
            }
            catch (IOException)
            {
                // Don't send 403 - the sending socket isn't working.
                _logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", remoteEndPoint.Address, remoteEndPoint.Port);
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                _logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", remoteEndPoint.Address, remoteEndPoint.Port);
            }
        }
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var data = (string)ar.AsyncState;

                // Complete sending the data to the remote device.
                _stream.EndWrite(ar);

                var remoteEndPoint = (IPEndPoint)_client.Client.RemoteEndPoint;
                //if (ShowBytes && ShowData)
                //    _logger.TraceFormat("{0}:{1} <<< {2} bytes: {3}", remoteEndPoint.Address, remoteEndPoint.Port, bytesSent, handler.Payload.TrimEnd('\r', '\n'));
                //else if (ShowBytes)
                //    _logger.TraceFormat("{0}:{1} <<< {2} bytes", remoteEndPoint.Address, remoteEndPoint.Port, bytesSent);
                //else 
                if (ShowData)
                    _logger.TraceFormat("{0}:{1} <{2}< {3}", remoteEndPoint.Address, remoteEndPoint.Port, TLS ? "!" : "<", data.TrimEnd('\r', '\n'));
            }
            catch (ObjectDisposedException)
            {
                // Don't send 403 - the sending socket isn't working
            }
            catch (Exception e)
            {
                // Don't send 403 - the sending socket isn't working
                _logger.ErrorFormat("Exception on EndWrite", e);
                throw;
            }
        }
        public void Shutdown()
        {
            lock (_sendLock)
            {
                if (_client.Connected)
                {
                    Send("205 closing connection\r\n", false, Encoding.UTF8); // Block.
                    _client.Client.Shutdown(SocketShutdown.Both);
                    _client.Close();
                }
            }
        }
        #endregion

        #region Commands
        private CommandProcessingResult Article(string content)
        {
            var param = (string.Compare(content, "ARTICLE\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!CurrentArticleNumber.HasValue)
                {
                    Send("430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else if (string.IsNullOrEmpty(CurrentNewsgroup) && !param.StartsWith("<", StringComparison.Ordinal))
            {
                Send("412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number == CurrentArticleNumber);
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
                        Send("423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send("430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send("423 No article with that number\r\n");
                            break;
                        case 3:
                            Send("420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (_sendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n",
                                    (!string.IsNullOrEmpty(CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0) ? article.Id : 0,
                                    article.MessageId), false, Encoding.UTF8);
                                break;
                            case 2:
                                Send(string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n", article.Id, article.MessageId), false, Encoding.UTF8);
                                break;
                            case 3:
                                Send(string.Format(CultureInfo.InvariantCulture, "220 {0} {1} Article follows (multi-line)\r\n", article.Id, article.MessageId), false, Encoding.UTF8);
                                break;
                        }

                        Send(article.Headers + "\r\n", false, Encoding.UTF8);
                        Send(article.Body + "\r\n.\r\n", false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult AuthInfo(string content)
        {
            // RFC 4643 - NNTP AUTHENTICATION
            var param = (string.Compare(content, "AUTHINFO\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrWhiteSpace(param))
            {
                Send("481 Authentication failed/rejected\r\n");
                return new CommandProcessingResult(true);
            }

            var args = param.Split(' ');
            if (args.Length != 2)
            {
                Send("481 Authentication failed/rejected\r\n");
                return new CommandProcessingResult(true);
            }

            if (string.Compare(args[0], "USER", StringComparison.OrdinalIgnoreCase) == 0)
            {
                Username = args.Skip(1).Aggregate((c, n) => c + " " + n);
                Send("381 Password required\r\n");
                return new CommandProcessingResult(true);
            }

            if (string.Compare(args[0], "PASS", StringComparison.OrdinalIgnoreCase) == 0)
            {
                if (Username == null)
                {
                    Send("482 Authentication commands issued out of sequence\r\n");
                    return new CommandProcessingResult(true);
                }

                if (Authenticated)
                {
                    Send("502 Command unavailable\r\n");
                    return new CommandProcessingResult(true);
                }

                var password = args.Skip(1).Aggregate((c, n) => c + " " + n);
                var saltBytes = new byte[64];
                var rng = RandomNumberGenerator.Create();
                rng.GetNonZeroBytes(saltBytes);

                Administrator[] allAdmins;
                using (var session = Database.SessionUtility.OpenSession())
                {
                    allAdmins = session.Query<Administrator>().ToArray();
                    session.Close();
                }

                var admin = allAdmins
                        .SingleOrDefault(a =>
                                a.Username == Username &&
                                a.PasswordHash ==
                                Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(a.PasswordSalt, password)))));

                if (admin == null)
                {
                    Send("481 Authentication failed/rejected\r\n");
                    return new CommandProcessingResult(true);
                }

                CanApproveGroups = admin.CanApproveGroups;
                CanCancel = admin.CanCancel;
                CanCheckGroups = admin.CanCheckGroups;
                CanCreateGroup = admin.CanCreateGroup;
                CanDeleteGroup = admin.CanDeleteGroup;
                CanInject = admin.CanInject;

                Authenticated = true;

                Send("281 Authentication accepted\r\n");
                return new CommandProcessingResult(true);
            }

            return new CommandProcessingResult(false);
        }
        private CommandProcessingResult Body(string content)
        {
            var param = (string.Compare(content, "BODY\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!CurrentArticleNumber.HasValue)
                {
                    Send("430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(CurrentNewsgroup))
                {
                    Send("412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                int type;
                Article article;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number == CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().Single(a => a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleNumber;
                    if (!int.TryParse(param, out articleNumber))
                    {
                        Send("423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number == articleNumber);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send("430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send("423 No article with that number\r\n");
                            break;
                        case 3:
                            Send("420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (_sendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n",
                                    (!string.IsNullOrEmpty(CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0) ? article.Id : 0,
                                    article.MessageId));
                                break;
                            case 2:
                                Send(string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                            case 3:
                                Send(string.Format(CultureInfo.InvariantCulture, "222 {0} {1} Body follows (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                        }

                        Send(article.Body, false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }

        private CommandProcessingResult Capabilities()
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
            if (AllowStartTls)
                sb.Append("STARTTLS\r\n");
            sb.Append("IMPLEMENTATION McNNTP 1.0.0\r\n");
            sb.Append(".\r\n");
            Send(sb.ToString());
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Date()
        {
            Send(string.Format(CultureInfo.InvariantCulture, "111 {0:yyyyMMddHHmmss}\r\n", DateTime.UtcNow));
            return new CommandProcessingResult(true);
        }        
        private CommandProcessingResult Group(string content)
        {
            content = content.TrimEnd('\r', '\n').Substring(content.IndexOf(' ') + 1).Split(' ')[0];
            Newsgroup ng;
            using (var session = Database.SessionUtility.OpenSession())
            {
                ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == content);
            }

            if (ng == null)
                Send(string.Format("411 {0} is unknown\r\n", content));
            else
            {
                CurrentNewsgroup = ng.Name;
                CurrentArticleNumber = ng.LowWatermark;

                // ReSharper disable ConvertIfStatementToConditionalTernaryExpression
                if (ng.PostCount == 0)
                    // ReSharper restore ConvertIfStatementToConditionalTernaryExpression
                    Send(string.Format("211 0 0 0 {0}\r\n", ng.Name));
                else
                    Send(string.Format("211 {0} {1} {2} {3}\r\n", ng.PostCount, ng.LowWatermark, ng.HighWatermark, ng.Name));
            }
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Hdr(string content)
        {
            var parts = content.TrimEnd('\r', '\n').Split(' ');
            if (parts.Length < 2 || parts.Length > 3)
            {
                Send("501 Syntax Error\r\n");
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
                    Send("501 Syntax Error\r\n");
                    return new CommandProcessingResult(true);
                }

                if (string.IsNullOrEmpty(CurrentNewsgroup))
                {
                    Send("412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else //if (parts.Length == 2)
            {
                type = 3;
                if (string.IsNullOrEmpty(CurrentNewsgroup))
                {
                    Send("412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
                if (!CurrentArticleNumber.HasValue)
                {
                    Send("420 Current article number is invalid\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = Database.SessionUtility.OpenSession())
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
                            Send("501 Syntax Error\r\n");
                            return new CommandProcessingResult(true);
                        }

                        articles = (range.Item2.HasValue)
                            ? session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id >= range.Item1 && a.Id <= range.Item2)
                            : session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id >= range.Item1);
                        break;
                    case 3:
                        System.Diagnostics.Debug.Assert(CurrentArticleNumber.HasValue);
                        articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id == CurrentArticleNumber.Value);
                        break;
                    default:
                        // Unrecognized...
                        Send("501 Syntax Error\r\n");
                        return new CommandProcessingResult(true);
                }

                if (!articles.Any())
                    switch (type)
                    {
                        case 1:
                            Send("430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send("423 No articles in that range\r\n");
                            break;
                        case 3:
                            Send("420 Current article number is invalid\r\n");
                            break;
                    }
                else
                {
                    lock (_sendLock)
                    {
                        Send("225 Headers follow (multi-line)\r\n");

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
                                Send(string.Format(CultureInfo.InvariantCulture, "{0} {1}\r\n",
                                    (!string.IsNullOrEmpty(CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0) ? article.MessageId : "0",
                                    headerFunction.Invoke(article)), false, Encoding.UTF8);
                            else
                                Send(string.Format(CultureInfo.InvariantCulture, "{0} {1}\r\n",
                                    article.Id,
                                    headerFunction.Invoke(article)), false, Encoding.UTF8);

                        Send(".\r\n");
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Head(string content)
        {
            var param = (string.Compare(content, "HEAD\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!CurrentArticleNumber.HasValue)
                {
                    Send("430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(CurrentNewsgroup))
                {
                    Send("412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id == CurrentArticleNumber);
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
                        Send("423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id == articleId);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send("430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send("423 No article with that number\r\n");
                            break;
                        case 3:
                            Send("420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (_sendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n",
                                    (string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0) ? article.Id : 0, article.MessageId));
                                break;
                            case 2:
                                Send(string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                            case 3:
                                Send(string.Format(CultureInfo.InvariantCulture, "221 {0} {1} Headers follow (multi-line)\r\n", article.Id, article.MessageId));
                                break;
                        }

                        Send(article.Headers + "\r\n.\r\n", false, Encoding.UTF8);
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Help()
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

            Send(sb.ToString());
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Last()
        {
            // If the currently selected newsgroup is invalid, a 412 response MUST be returned.
            if (string.IsNullOrWhiteSpace(CurrentNewsgroup))
            {
                Send("412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }

            var currentArticleNumber = CurrentArticleNumber;

            Article previousArticle;

            if (!currentArticleNumber.HasValue)
            {
                Send("420 Current article number is invalid\r\n");
                return new CommandProcessingResult(true);
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                previousArticle = session.Query<Article>().Fetch(a => a.Newsgroup)
                    .Where(a => a.Newsgroup.Name == CurrentNewsgroup && a.Id < currentArticleNumber.Value)
                    .MaxBy(a => a.Id);
            }

            // If the current article number is already the first article of the newsgroup, a 422 response MUST be returned.
            if (previousArticle == null)
            {
                Send("422 No previous article in this group\r\n");
                return new CommandProcessingResult(true);
            }

            CurrentArticleNumber = previousArticle.Id;

            Send(string.Format("223 {0} {1} retrieved\r\n", previousArticle.Id, previousArticle.MessageId));
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult List(string content)
        {
            if (string.Compare(content, "LIST\r\n", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(content, "LIST ACTIVE\r\n", StringComparison.OrdinalIgnoreCase) == 0)
            {
                IList<Newsgroup> newsGroups;

                try
                {
                    using (var session = Database.SessionUtility.OpenSession())
                    {
                        newsGroups = session.Query<Newsgroup>().OrderBy(n => n.Name).ToList();
                    }
                }
                catch (MappingException mex)
                {
                    _logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
                    Send("403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception when trying to handle LIST", ex);
                    Send("403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }

                lock (_sendLock)
                {
                    Send("215 list of newsgroups follows\r\n");
                    foreach (var ng in newsGroups)
                        Send(string.Format("{0} {1} {2} {3}\r\n", ng.Name, ng.HighWatermark, ng.LowWatermark, CanPost ? "y" : "n"), false, Encoding.UTF8);
                    Send(".\r\n");
                }
                return new CommandProcessingResult(true);
            }

            if (string.Compare(content, "LIST NEWSGROUPS\r\n", StringComparison.OrdinalIgnoreCase) == 0)
            {
                IList<Newsgroup> newsGroups;

                try
                {
                    using (var session = Database.SessionUtility.OpenSession())
                    {
                        newsGroups = session.Query<Newsgroup>().OrderBy(n => n.Name).ToList();
                    }
                }
                catch (MappingException mex)
                {
                    _logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
                    Send("403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception when trying to handle LIST", ex);
                    Send("403 Archive server temporarily offline\r\n");
                    return new CommandProcessingResult(true);
                }

                lock (_sendLock)
                {
                    Send("215 information follows\r\n");
                    foreach (var ng in newsGroups)
                        Send(string.Format("{0}\t{1}\r\n", ng.Name, ng.Description), false, Encoding.UTF8);
                    Send(".\r\n");
                }
                return new CommandProcessingResult(true);
            }

            Send("501 Syntax Error\r\n");
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult ListGroup(string content)
        {
            var parts = content.TrimEnd('\r', '\n').Split(' ');

            if (parts.Length == 1 && CurrentNewsgroup == null)
                Send("412 No newsgroup selected\r\n");

            using (var session = Database.SessionUtility.OpenSession())
            {
                var name = (parts.Length == 2) ? parts[1] : CurrentNewsgroup;
                var ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);

                if (ng == null)
                    Send("411 No such newsgroup\r\n");
                else
                {
                    CurrentNewsgroup = ng.Name;
                    if (ng.PostCount == 0)
                    {
                        lock (_sendLock)
                        {
                            Send(string.Format("211 0 0 0 {0}\r\n", ng.Name));
                        }
                    }
                    else
                    {
                        IList<Article> articles;
                        if (parts.Length < 3)
                            articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name).OrderBy(a => a.Number).ToList();
                        else
                        {
                            var range = ParseRange(parts[2]);
                            if (range.Equals(default(System.Tuple<int, int?>)))
                            {
                                Send("501 Syntax Error\r\n");
                                return new CommandProcessingResult(true);
                            }

                            if (!range.Item2.HasValue) // LOW-
                                articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name && a.Number >= range.Item1).OrderBy(a => a.Number).ToList();
                            else // LOW-HIGH
                                articles = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == ng.Name && a.Number >= range.Item1 && a.Number <= range.Item2.Value).ToList();
                        }

                        CurrentArticleNumber = !articles.Any() ? default(long?) : articles.First().Number;

                        lock (_sendLock)
                        {
                            Send(string.Format("211 {0} {1} {2} {3}\r\n", ng.PostCount, ng.LowWatermark, ng.HighWatermark, ng.Name), false, Encoding.UTF8);
                            foreach (var article in articles)
                                Send(string.Format(CultureInfo.InvariantCulture, "{0}\r\n", article.Number.ToString(CultureInfo.InvariantCulture)), false, Encoding.UTF8);
                            Send(".\r\n", false, Encoding.UTF8);
                        }
                    }
                }
            }


            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Mode(string content)
        {
            if (content.StartsWith("MODE READER", StringComparison.OrdinalIgnoreCase))
            {
                Send("200 This server is not a mode-switching server, but whatever!\r\n");
                return new CommandProcessingResult(true);
            }

            Send("501 Syntax Error\r\n");
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Newgroups(string content)
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
                using (var session = Database.SessionUtility.OpenSession())
                {
                    newsGroups = session.Query<Newsgroup>().Where(n => n.CreateDate >= afterDate).OrderBy(n => n.Name).ToList();
                }

                lock (_sendLock)
                {
                    Send("231 List of new newsgroups follows (multi-line)\r\n", false, Encoding.UTF8);
                    foreach (var ng in newsGroups)
                        Send(string.Format("{0} {1} {2} {3}\r\n", ng.Name, ng.HighWatermark, ng.LowWatermark,
                            CanPost ? "y" : "n"), false, Encoding.UTF8);
                    Send(".\r\n", false, Encoding.UTF8);
                }
            }
            else
            {
                lock (_sendLock)
                {
                    Send("231 List of new newsgroups follows (multi-line)\r\n.\r\n");
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Next()
        {
            // If the currently selected newsgroup is invalid, a 412 response MUST be returned.
            if (string.IsNullOrWhiteSpace(CurrentNewsgroup))
            {
                Send("412 No newsgroup selected\r\n");
                return new CommandProcessingResult(true);
            }

            var currentArticleNumber = CurrentArticleNumber;

            Article previousArticle;

            if (!currentArticleNumber.HasValue)
            {
                Send("420 Current article number is invalid\r\n");
                return new CommandProcessingResult(true);
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                previousArticle = session.Query<Article>().Fetch(a => a.Newsgroup)
                    .Where(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number > currentArticleNumber.Value)
                    .MinBy(a => a.Number);
            }

            // If the current article number is already the last article of the newsgroup, a 421 response MUST be returned.
            if (previousArticle == null)
            {
                Send("421 No next article in this group\r\n");
                return new CommandProcessingResult(true);
            }

            CurrentArticleNumber = previousArticle.Number;

            Send(string.Format("223 {0} {1} retrieved\r\n", previousArticle.Number, previousArticle.MessageId));
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Post()
        {
            if (!CanPost)
            {
                Send("440 Posting not permitted\r\n");
                return new CommandProcessingResult(true);
            }

            Send("340 Send article to be posted\r\n");

            Func<string, CommandProcessingResult, CommandProcessingResult> messageAccumulator = null;
            messageAccumulator = (msg, prev) =>
            {
                if (
                    // Message ends naturally
                    msg != null && (msg.EndsWith("\r\n.\r\n", StringComparison.OrdinalIgnoreCase)) ||
                    // Message delimiter comes in second batch
                    (prev != null && prev.Message != null && prev.Message.EndsWith("\r\n", StringComparison.OrdinalIgnoreCase) && msg != null && msg.EndsWith(".\r\n", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        Article article;
                        if (!Data.Article.TryParse(prev.Message == null ? msg.Substring(0, msg.Length - 5) : prev.Message + msg, out article))
                        {
                            Send("441 Posting failed\r\n");
                            return new CommandProcessingResult(true, true);
                        }

                        bool canApprove;
                        if (CanInject)
                            canApprove = true;
                        else if (string.IsNullOrWhiteSpace(CanApproveGroups))
                            canApprove = false;
                        else if (CanApproveGroups == "*")
                            canApprove = true;
                        else
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            canApprove = article.Newsgroups.Split(' ').All(n => CanApproveGroups.Split(' ').Contains(n));
                        }

                        if (!canApprove)
                        {
                            article.Approved = null;
                            article.RemoveHeader("Approved");
                        }
                        if (!CanCancel)
                        {
                            article.Supersedes = null;
                            article.RemoveHeader("Supersedes");
                        }
                        if (!CanInject)
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

                        if ((article.Control != null && article.Control.StartsWith("cancel ", StringComparison.OrdinalIgnoreCase) && !CanCancel) ||
                            (article.Control != null && article.Control.StartsWith("newgroup ", StringComparison.OrdinalIgnoreCase) && !CanCreateGroup) ||
                            (article.Control != null && article.Control.StartsWith("rmgroup ", StringComparison.OrdinalIgnoreCase) && !CanDeleteGroup) ||
                            (article.Control != null && article.Control.StartsWith("checkgroups ", StringComparison.OrdinalIgnoreCase) && !CanCheckGroups))
                        {
                            Send("480 Permission to issue control message denied\r\n");
                            return new CommandProcessingResult(true, true);
                        }

                        article.Control = null;

                        using (var session = Database.SessionUtility.OpenSession())
                        {
                            foreach (var newsgroupName in article.Newsgroups.Split(' '))
                            {
                                var newsgroupNameClosure = newsgroupName;
                                var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == newsgroupNameClosure);
                                if (newsgroup == null)
                                    continue;

                                article.Id = 0;
                                article.Newsgroup = newsgroup;
                                article.Number = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == newsgroupName).Max(a => a.Number) + 1;
                                article.Path = ServerName;
                                session.Save(article);
                            }

                            session.Close();
                        }

                        Send("240 Article received OK\r\n");
                        return new CommandProcessingResult(true, true)
                        {
                            Message = prev.Message + msg
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Exception when trying to handle POST", ex);
                        Send("441 Posting failed\r\n");
                        return new CommandProcessingResult(true);
                    }
                }

                return new CommandProcessingResult(true, false)
                {
                    MessageHandler = messageAccumulator,
                    Message = prev == null ? msg : prev.Message == null ? msg : prev.Message + "\r\n" + msg
                };
            };

            return messageAccumulator.Invoke(null, null);
        }
        private CommandProcessingResult Quit()
        {
            Shutdown();
            return new CommandProcessingResult(true, true);
        }
        private CommandProcessingResult StartTLS(string content)
        {
            if (TLS)
            {
                Send("502 Command unavailable\r\n");
                return new CommandProcessingResult(true);
            }

            Send("580 Can not initiate TLS negotiation\r\n");
            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult Stat(string content)
        {
            var param = (string.Compare(content, "STAT\r\n", StringComparison.OrdinalIgnoreCase) == 0)
                ? null
                : content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (string.IsNullOrEmpty(param))
            {
                if (!CurrentArticleNumber.HasValue)
                {
                    Send("430 No article with that message-id\r\n");
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(CurrentNewsgroup))
                {
                    Send("412 No newsgroup selected\r\n");
                    return new CommandProcessingResult(true);
                }
            }

            using (var session = Database.SessionUtility.OpenSession())
            {
                Article article;
                int type;
                if (string.IsNullOrEmpty(param))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number == CurrentArticleNumber);
                    type = 3;
                }
                else if (param.StartsWith("<", StringComparison.Ordinal))
                {
                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.MessageId == param);
                    type = 1;
                }
                else
                {
                    int articleNumber;
                    if (!int.TryParse(param, out articleNumber))
                    {
                        Send("423 No article with that number\r\n");
                        return new CommandProcessingResult(true);
                    }

                    article = session.Query<Article>().Fetch(a => a.Newsgroup).SingleOrDefault(a => a.Newsgroup.Name == CurrentNewsgroup && a.Number == articleNumber);
                    type = 2;
                }

                if (article == null)
                    switch (type)
                    {
                        case 1:
                            Send("430 No article with that message-id\r\n");
                            break;
                        case 2:
                            Send("423 No article with that number\r\n");
                            break;
                        case 3:
                            Send("420 Current article number is invalid\r\n");
                            break;

                    }
                else
                {
                    lock (_sendLock)
                    {
                        switch (type)
                        {
                            case 1:
                                Send(string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n",
                                    (!string.IsNullOrEmpty(CurrentNewsgroup) && string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0) ? article.Number : 0,
                                    article.MessageId));
                                break;
                            case 2:
                                Send(string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n", article.Number, article.MessageId));
                                break;
                            case 3:
                                Send(string.Format(CultureInfo.InvariantCulture, "223 {0} {1}\r\n", article.Number, article.MessageId));
                                break;
                        }
                    }
                }
            }

            return new CommandProcessingResult(true);
        }
        private CommandProcessingResult XOver(string content)
        {
            var rangeExpression = content.Substring(content.IndexOf(' ') + 1).TrimEnd('\r', '\n');

            if (CurrentNewsgroup == null)
                Send("412 No news group current selected\r\n");
            else
            {
                Newsgroup ng;
                IList<Article> articles;

                try
                {
                    using (var session = Database.SessionUtility.OpenSession())
                    {
                        ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == CurrentNewsgroup);

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
                                Send("501 Syntax Error\r\n");
                                return new CommandProcessingResult(true);
                            }

                            if (!range.Item2.HasValue) // LOW-
                            {
                                articles =
                                    session.Query<Article>()
                                        .Fetch(a => a.Newsgroup)
                                        .Where(a => a.Newsgroup.Name == ng.Name && a.Number >= range.Item1)
                                        .OrderBy(a => a.Number)
                                        .ToList();
                            }
                            else // LOW-HIGH
                            {
                                articles =
                                    session.Query<Article>()
                                        .Fetch(a => a.Newsgroup)
                                        .Where(a => a.Newsgroup.Name == ng.Name && a.Number >= range.Item1 && a.Number <= range.Item2.Value)
                                        .OrderBy(a => a.Number)
                                        .ToList();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Send("403 Archive server temporarily offline\r\n");
                    _logger.Error("Exception when trying to handle XOVER", ex);
                    return new CommandProcessingResult(true);
                }

                if (ng == null)
                {
                    Send("411 No such newsgroup\r\n");
                    return new CommandProcessingResult(true);
                }

                if (!articles.Any())
                {
                    Send("420 No article(s) selected\r\n");
                    return new CommandProcessingResult(true);
                }

                CurrentArticleNumber = articles.First().Id;
                Func<string, string> unfold = i => string.IsNullOrWhiteSpace(i) ? i : i.Replace("\r\n", "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

                lock (_sendLock)
                {
                    Send("224 Overview information follows\r\n", false, Encoding.UTF8);
                    foreach (var article in articles)
                        Send(
                            string.Format(CultureInfo.InvariantCulture,
                                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\r\n",
                                string.CompareOrdinal(article.Newsgroup.Name, CurrentNewsgroup) == 0 ? article.Id : 0,
                                unfold(article.Subject),
                                unfold(article.From),
                                unfold(article.Date),
                                unfold(article.MessageId),
                                unfold(article.References),
                                unfold((article.Body.Length * 2).ToString(CultureInfo.InvariantCulture)),
                                unfold(article.Body.Split(new[] { "\r\n" }, StringSplitOptions.None).Length.ToString(CultureInfo.InvariantCulture))), false,
                            Encoding.UTF8);
                    Send(".\r\n", false, Encoding.UTF8);
                }
            }

            return new CommandProcessingResult(true);
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
    }
}
