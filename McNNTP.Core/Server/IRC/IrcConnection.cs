// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IrcConnection.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2015.  All rights reserved.
// </copyright>
// <summary>
//   A connection from a client to the server
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using McNNTP.Common;
    using McNNTP.Core.Server.Configuration;

    /// <summary>
    /// A connection from a client to the server
    /// </summary>
    internal class IrcConnection : IConnection
    {
        /// <summary>
        /// The class used to securely hash hostnames
        /// </summary>
        private static readonly SHA256 _Hasher = SHA256.Create();

        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1024;

        /// <summary>
        /// A command-indexed dictionary with function pointers to support client command
        /// </summary>
        private static readonly Dictionary<string, Func<IrcConnection, Message, Task<CommandProcessingResult>>> _CommandDirectory;

        /// <summary>
        /// A command-indexed dictionary with a tuple of invocation count and byte count
        /// </summary>
        private static readonly ConcurrentDictionary<string, Tuple<ulong, ulong>> _CommandStats = new ConcurrentDictionary<string, Tuple<ulong, ulong>>();

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(IrcConnection));

        /// <summary>
        /// The store to use to satisfy requests of this connection
        /// </summary>
        private readonly IStoreProvider store;

        /// <summary>
        /// The server instance to which this connection belongs
        /// </summary>
        [NotNull]
        private readonly IrcServer server;

        /// <summary>
        /// The <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull] 
        private readonly TcpClient client;

        /// <summary>
        /// The <see cref="Stream"/> instance retrieved from the <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull] 
        private readonly Stream stream;

        /// <summary>
        /// The stream receive buffer
        /// </summary>
        [NotNull]
        private readonly byte[] buffer = new byte[BufferSize];

        /// <summary>
        /// The received data buffer appended to from the stream buffer
        /// </summary>
        [NotNull]
        private readonly StringBuilder builder = new StringBuilder();

        /// <summary>
        /// The remote IP address to which the connection is established
        /// </summary>
        [NotNull]
        private readonly IPAddress remoteAddress;

        /// <summary>
        /// The remote TCP port number for the remote endpoint to which the connection is established
        /// </summary>
        private readonly int remotePort;

        /// <summary>
        /// The local IP address to which the connection is established
        /// </summary>
        [NotNull]
        private readonly IPAddress localAddress;

        /// <summary>
        /// The local TCP port number for the local endpoint to which the connection is established
        /// </summary>
        private readonly int localPort;

        /// <summary>
        /// The number of messages sent over this connection
        /// </summary>
        public ulong SentMessageCount { get; private set; }

        /// <summary>
        /// The amount of data sent over this connection in bytes
        /// </summary>
        public ulong SentMessageBytes { get; private set; }

        /// <summary>
        /// The number of messages received over this connection
        /// </summary>
        public ulong RecvMessageCount { get; private set; }

        /// <summary>
        /// The amount of data received over this connection in bytes
        /// </summary>
        public ulong RecvMessageBytes { get; private set; }

        /// <summary>
        /// The date the connection was opened, stored as a UTC value
        /// </summary>
        public DateTime Established { get; private set; }

        /// <summary>
        /// For commands that handle conversational request-replies, this is a reference to the
        /// command that should handle new input received by the main process loop.
        /// </summary>
        [CanBeNull]
        private CommandProcessingResult inProcessCommand;

        /// <summary>
        /// Gets the principal identified on this connection
        /// </summary>
        [CanBeNull]
        public IPrincipal Principal { get; private set; }

        /// <summary>
        /// Gets or sets the address that was listening for this connection when it was received, if this connection was an inbound address
        /// </summary>
        [CanBeNull]
        public IPAddress ListenAddress { get; set; }

        /// <summary>
        /// Gets or sets the port that was listening for this connection when it was received, if this connection was an inbound address
        /// </summary>
        [CanBeNull]
        public int? ListenPort { get; set; }

        /// <summary>
        /// Initializes static members of the <see cref="IrcConnection"/> class.
        /// </summary>
        static IrcConnection()
        {
            _CommandDirectory = new Dictionary<string, Func<IrcConnection, Message, Task<CommandProcessingResult>>>
                               {
                                   { "CAP", async (c, m) => await c.Capability(m) },
                                   { "LIST", async (c, m) => await c.List(m) },
                                   { "LUSERS", async (c, m) => await c.Lusers(m) },
                                   { "MODE", async (c, m) => await c.Mode(m) },
                                   { "MOTD", async (c, m) => await c.Motd(m) },
                                   { "NICK", async (c, m) => await c.Nick(m) },
                                   { "OPER", async (c, m) => await c.Oper(m) },
                                   { "PRIVMSG", async (c, m) => await c.PrivMsg(m) },
                                   { "QUIT", async (c, m) => await c.Quit(m) },
                                   { "STATS", async (c, m) => await c.Stats(m) },
                                   { "USER", async (c, m) => await c.User(m) },
                                   { "VERSION", async (c, m) => await c.Version(m) },
                               };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcConnection"/> class.
        /// </summary>
        /// <param name="store">The catalog and message store to use to back operations for this connection</param>
        /// <param name="server">The server instance that owns this connection</param>
        /// <param name="client">The <see cref="TcpClient"/> that accepted this connection</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/></param>
        /// <param name="listener">The <see cref="TcpListener"/> instance that sourced this connection, if it was an inbound request</param>
        /// <param name="tls">Whether or not the connection has implicit Transport Layer Security</param>
        public IrcConnection(
            [NotNull] IStoreProvider store,
            [NotNull] IrcServer server,
            [NotNull] TcpClient client,
            [NotNull] Stream stream,
            [CanBeNull] TcpListener listener,
            bool tls = false)
        {
            this.store = store;

            this.client = client;
            this.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ShowBytes = server.ShowBytes;
            this.ShowCommands = server.ShowCommands;
            this.ShowData = server.ShowData;
            this.server = server;
            this.stream = stream;
            this.ListenAddress = listener != null ? ((IPEndPoint)listener.LocalEndpoint).Address : null;
            this.ListenPort = listener != null ? ((IPEndPoint)listener.LocalEndpoint).Port : default(int?);
            this.TLS = tls;

            var remoteIpEndpoint = (IPEndPoint)this.client.Client.RemoteEndPoint;
            this.remoteAddress = remoteIpEndpoint.Address;
            this.remotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this.client.Client.LocalEndPoint;
            this.localAddress = localIpEndpoint.Address;
            this.localPort = localIpEndpoint.Port;
            this.Established = DateTime.UtcNow;
        }

        public bool ShowBytes { get; set; }

        public bool ShowCommands { get; set; }

        public bool ShowData { get; set; }

        #region Authentication
        [CanBeNull]
        public string Username { get; set; }

        [CanBeNull]
        public IIdentity Identity { get; set; }

        public bool TLS { get; set; }
        #endregion
        
        /// <summary>
        /// Gets the catalog currently selected by this connection
        /// </summary>
        [PublicAPI, CanBeNull]
        public string CurrentCatalog { get; private set; }

        /// <summary>
        /// Gets a value indicating the catalog currently selected by this connection
        /// </summary>
        [PublicAPI]
        public bool CurrentCatalogReadOnly { get; private set; }

        /// <summary>
        /// Gets the message number currently selected by this connection for the selected catalog
        /// </summary>
        [PublicAPI]
        public long? CurrentMessageNumber { get; private set; }

        #region Derived instance properties
        /// <summary>
        /// Gets the remote IP address to which the connection is established
        /// </summary>
        [NotNull]
        public IPAddress RemoteAddress
        {
            get { return this.remoteAddress; }
        }

        /// <summary>
        /// Gets the remote TCP port number for the remote endpoint to which the connection is established
        /// </summary>
        public int RemotePort
        {
            get { return this.remotePort; }
        }

        /// <summary>
        /// Gets the local IP address to which the connection is established
        /// </summary>
        [NotNull]
        public IPAddress LocalAddress
        {
            get { return this.localAddress; }
        }

        /// <summary>
        /// Gets the local TCP port number for the local endpoint to which the connection is established
        /// </summary>
        public int LocalPort
        {
            get { return this.localPort; }
        }
        #endregion

        #region IO and Connection Management
        /// <summary>
        /// The primary processing loop for an active connection
        /// </summary>
        public async void Process()
        {
            Debug.Assert(this.stream != null, "The stream was 'null', but it should not have been because the connection was accepted and processing is beginning.");

            try
            {
                while (true)
                {
                    if (!this.client.Connected || !this.client.Client.Connected) return;

                    if (!this.stream.CanRead)
                    {
                        await this.SendError("Unable to read from stream");
                        this.Shutdown();
                        return;
                    }

                    var bytesRead = await this.stream.ReadAsync(this.buffer, 0, BufferSize);
                    this.RecvMessageBytes += (ulong)bytesRead;

                    // There  might be more data, so store the data received so far.
                    this.builder.Append(Encoding.ASCII.GetString(this.buffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var builderString = this.builder.ToString();
                    if (bytesRead == BufferSize 
                        || (
                            !builderString.EndsWith("\r\n", StringComparison.Ordinal)
                            && !builderString.EndsWith("\n", StringComparison.Ordinal))
                        )
                    {
                        // Read some more.
                        continue;
                    }

                    // There could be MORE THAN ONE command in one read.
                    var builderMoreThanOne = 
                        builderString.Length > 2 && (
                            builderString.Substring(0, builderString.Length - 2).IndexOf("\r\n", StringComparison.Ordinal) > -1
                            || builderString.Substring(0, builderString.Length - 2).IndexOf("\n", StringComparison.Ordinal) > -1
                        );
                    string[] inputs;
                    if (builderMoreThanOne)
                    {
                        var split = builderString.Split(new[] { "\r\n" }, StringSplitOptions.None);
                        if (split.Length == 1)
                            split = builderString.Split(new[] { "\n" }, StringSplitOptions.None);

                        inputs = split.Take(split.Length - 1).ToArray();
                        this.builder.Clear();
                        if (!string.IsNullOrEmpty(split.Last()))
                            this.builder.Append(split.Last());
                    }
                    else
                    {
                        inputs = new[] { builderString };
                        this.builder.Clear();
                    }

                    // All the data has been read from the 
                    // client. Display it on the console.
                    if (this.ShowBytes && this.ShowData)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes: {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                            builderString.Length,
                            builderString.TrimEnd('\r', '\n'));
                    else if (this.ShowBytes)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                            builderString.Length);
                    else if (this.ShowData)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                            builderString.TrimEnd('\r', '\n'));
                    
                    foreach (var input in inputs)
                    { 
                        if (this.inProcessCommand != null && this.inProcessCommand.MessageHandler != null)
                        {
                            // Ongoing read - don't parse it for commands
                            this.inProcessCommand = await this.inProcessCommand.MessageHandler(input, this.inProcessCommand);
                            if (this.inProcessCommand != null && this.inProcessCommand.IsQuitting)
                                this.inProcessCommand = null;
                        }
                        else
                        {
                            var mesasge = new Message(input);
                            this.RecvMessageCount++;

                            var cmd = mesasge.Command.ToUpperInvariant();

                            if (_CommandDirectory.ContainsKey(cmd))
                            {
                                // Update command stats
                                Tuple<ulong, ulong> currentStat;
                                if (_CommandStats.TryGetValue(cmd, out currentStat))
                                    _CommandStats.TryUpdate(cmd, new Tuple<ulong, ulong>(currentStat.Item1 + 1, currentStat.Item2 + (ulong)bytesRead), currentStat);
                                else
                                    _CommandStats.TryAdd(cmd, new Tuple<ulong, ulong>(1, (ulong)bytesRead));

                                try
                                {
                                    if (this.ShowCommands)
                                        _Logger.TraceFormat(
                                            "{0}:{1} >{2}> {3} {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                                            cmd,
                                            mesasge.Parameters.Aggregate((c,n) => c + " " + n));

                                    var result = await _CommandDirectory[cmd].Invoke(this, mesasge);

                                    if (result.MessageHandler != null) this.inProcessCommand = result;
                                    else if (result.IsQuitting) return;
                                }
                                catch (Exception ex)
                                {
                                    _Logger.Error("Exception processing a command", ex);
                                    break;
                                }
                            }
                            else
                                await this.SendNumeric(CommandCode.ERR_UNKNOWNCOMMAND, string.Format("{0}: Unknown command", cmd));
                        }
                    }
                }
            }
            catch (DecoderFallbackException dfe)
            {
                _Logger.Error("Decoder Fallback Exception socket " + this.RemoteAddress, dfe);
            }
            catch (IOException se)
            {
                _Logger.Error("I/O Exception on socket " + this.RemoteAddress, se);
            }
            catch (SocketException se)
            {
                _Logger.Error("Socket Exception on socket " + this.RemoteAddress, se);
            }
            catch (NotSupportedException nse)
            {
                _Logger.Error("Not Supported Exception", nse);
            }
            catch (ObjectDisposedException ode)
            {
                _Logger.Error("Object Disposed Exception", ode);
            }
        }

        /// <summary>
        /// Sends the formatted data to the client
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>A value indicating whether or not the transmission was successful</returns>
        [StringFormatMethod("format"), NotNull]
        protected internal async Task<bool> Send(Message message)
        {
            var result = await this.SendInternal(message.OutgoingString());
            this.SentMessageCount++;
            return result;
        }

        private async Task<bool> SendInternal([NotNull] string data)
        {
            // Convert the string data to byte data using ASCII encoding.
            var byteData = Encoding.UTF8.GetBytes(data);

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData, 0, byteData.Length);
                this.SentMessageBytes += (ulong)byteData.Length;

                if (this.ShowBytes && this.ShowData)
                    _Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                else if (this.ShowBytes)
                    _Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
                        byteData.Length);
                else if (this.ShowData)
                    _Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
                        data.TrimEnd('\r', '\n'));

                return true;
            }
            catch (IOException)
            {
                // Don't send 403 - the sending socket isn't working.
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (ObjectDisposedException)
            {
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
        }

        protected internal async Task<bool> SendError([NotNull] string text)
        {
            return await this.SendInternal(string.Format("ERROR :{0} {1}\r\n", this.server.Self.Name, text));
        }

        protected internal async Task<bool> SendReply([NotNull] string numeric, string text)
        {
            Debug.Assert(this.Principal != null);
            Debug.Assert(this.Principal.Name != null);
            return await this.Send(new Message(this.server.Self.Name, numeric, string.Format("{0} {1}", this.Principal.Name, text)));
        }

        protected internal async Task<bool> SendNumeric([NotNull] string numeric, string errorText)
        {
            return await this.Send(new Message(this.server.Self.Name, numeric, errorText));
        }

        //private async Task<bool> SendNumeric(string numeric, string numericText)
        //{
        //    var message = new Message(Name, numeric, numericText);
        //    if (IsLocal)
        //        await this.Send(message.OutgoingString(string.Empty));
        //    else
        //        await this.Send()
        //}

        /// <summary>
        /// Shuts down the IRC connection
        /// </summary>
        public void Shutdown()
        {
            if (this.client.Connected)
            {
                this.client.Client.Shutdown(SocketShutdown.Both);
                this.client.Close();
            }

            this.server.RemoveConnection(this);

            var p = this.Principal;
            if (p != null)
                this.server.RemovePrincipal(p);
        }
        #endregion

        #region Commands

        /// <summary>
        /// CAP command is used to negotiate capabilities of the IRC server between the client and server
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/draft-mitchell-irc-capabilities-01#section-3.2">DRAFT FOR IRC CAPABILITIES</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Capability(Message m)
        {
            //var subcommand = m[0];
            // Do nothing.
            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// The list command is used to list channels and their topics.  If the
        /// &lt;channel&gt; parameter is used, only the status of that channel is
        /// displayed.
        /// 
        ///    Command: LIST
        ///    Parameters: [ &lt;channel&gt; *( "," &lt;channel&gt; ) [ &lt;target&gt; ] ]
        /// 
        /// Wildcards are allowed in the &lt;target&gt; parameter.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.2.6">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> List(Message m)
        {
            if (!this.VerifyRegisteredUser())
                return new CommandProcessingResult(false);

            var chanList = m[0];
            var target = m[1];

            var user = (User)this.Principal;
            Debug.Assert(user != null);

            if (string.IsNullOrWhiteSpace(target) || this.server.Self.Name.MatchesWildchar(target))
            {
                /* https://tools.ietf.org/html/rfc2811#section-4.2.6
                 * This means that there is no way of getting this channel's name from
                 * the server without being a member.  In other words, these channels
                 * MUST be omitted from replies to queries like the WHOIS command.
                 */
                foreach (var c in this.server.Channels
                    .Where(c => (!c.Private && !c.Secret) || c.UsersModes.Select(u => u.Key).Contains(user))
                    .Where(c => string.IsNullOrWhiteSpace(chanList) || chanList.Split(',').Contains(c.Name, new ScandanavianStringComparison())))
                    await this.SendReply(CommandCode.RPL_LIST, string.Format("{0} {1} :{2}", c.Name, c.UsersModes.Select(u => u.Key).Count(u => !u.Invisible), c.Topic));
                await this.SendReply(CommandCode.RPL_LISTEND, ":End of LIST");
            }
            else if (!await this.server.SendPeersByTarget(m, target))
                await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));

            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// The LUSERS command is used to get statistics about the size of the
        /// IRC network.  If no parameter is given, the reply will be about the
        /// whole net.  If a &lt;mask&gt; is specified, then the reply will only
        /// concern the part of the network formed by the servers matching the
        /// mask.  Finally, if the &lt;target&gt; parameter is specified, the request
        /// is forwarded to that server which will generate the reply.
        /// 
        /// Command: LUSERS
        /// Parameters: [ &lt;mask&gt; [ &lt;target&gt; ] ]
        /// 
        /// Wildcards are allowed in the &lt;target&gt; parameter.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.4.1">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Lusers(Message m)
        {
            var mask = m[0];
            var target = m[1];

            if (string.IsNullOrWhiteSpace(target))
            {
                var predicate = string.IsNullOrWhiteSpace(mask) 
                    ? (s => true) 
                    : new Func<Server, bool>(s => Regex.IsMatch(s.Name, "^" + Regex.Escape(mask).Replace(@"\*", ".*").Replace(@"\?", ".") + "$"));

                await this.SendReply(CommandCode.RPL_LUSERCLIENT, string.Format(":There are {0} users and {1} services on {2} servers", this.server.Users.Count(u => predicate(u.Server)), this.server.Services.Count(s => predicate(s.Server)), this.server.Servers.Count(s => predicate(s)) + (predicate(this.server.Self) ? 1 : 0)));
                await this.SendReply(CommandCode.RPL_LUSEROP, string.Format("{0} :operator(s) online", this.server.Users.Count(u => (u.OperatorGlobal || u.OperatorLocal) && predicate(u.Server))));
                // This is not defined, so for this project, if this is just a user who has not positively authenticated, we call them 'unknown'
                await this.SendReply(CommandCode.RPL_LUSERUNKNOWN, string.Format("{0} :unknown connection(s)", this.server.Connections.Count(c => string.IsNullOrWhiteSpace(c.AuthenticatedUsername))));
                await this.SendReply(CommandCode.RPL_LUSERCHANNELS, string.Format("{0} :channels formed", this.server.Channels.Count(c => !c.Secret)));
                await this.SendReply(CommandCode.RPL_LUSERME, string.Format(":I have {0} clients and {1} servers", this.server.Users.Count(u => u.Server == this.server.Self) + this.server.Services.Count(u => u.Server == this.server.Self), this.server.Servers.Count(u => u.Parent == this.server.Self)));
            }
            else if (!await this.server.SendPeersByTarget(m, target))
                await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));

            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// The user MODE's are typically changes which affect either how the
        /// client is seen by others or what 'extra' messages the client is sent.
        /// 
        ///    Command: MODE
        /// Parameters: &lt;nickname&gt; *( ( "+" / "-" ) *( "i" / "w" / "o" / "O" / "r" ) )
        /// A user MODE command MUST only be accepted if both the sender of the
        /// message and the nickname given as a parameter are both the same.  If
        /// no other parameter is given, then the server will return the current
        /// settings for the nick.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.1.5">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Mode(Message m)
        {
            var target = m[0];
            var modes = m[1];

            if (m.Parameters.Count() < 2 || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(modes) || !this.VerifyRegisteredUser())
            {
                await this.SendNumeric(CommandCode.ERR_NEEDMOREPARAMS, "MODE :Not enough parameters");
                return new CommandProcessingResult(true);
            }

            if (Regex.IsMatch(Message.RegexChannel, target))
            {
                // TODO: Channel modes
                await this.SendNumeric(CommandCode.ERR_NEEDMOREPARAMS, "MODE :Not enough parameters");
                return new CommandProcessingResult(true);
            }
            // ReSharper disable once PossibleNullReferenceException
            if (new ScandanavianStringComparison().Compare(target, this.Principal.Name) != 0)
            {
                await this.SendNumeric(CommandCode.ERR_USERSDONTMATCH, ":Cannot change mode for other users");
                return new CommandProcessingResult(true);
            }

            // User modes
            var user = (User)this.Principal;
            Debug.Assert(user != null);
            var unknownModeFlagSeen = false;

            foreach (Match match in Regex.Matches(modes, @"(?<mode>[\+\-][iwoOr]*)"))
            {
                var s = match.Groups["mode"].Value;
                if (s.Length < 2)
                    continue;

                if (s[0] == '+')
                {
                    // Adding mode
                    foreach (var c in s.ToCharArray().Skip(1))
                        switch (c)
                        {
                            // The flag 'a' SHALL NOT be toggled by the user using the MODE command, instead use of the AWAY command is REQUIRED.
                            case 'i':
                                user.Invisible = true;
                                break;
                            case 'O':
                            case 'o':
                                /* If a user attempts to make themselves an operator using the "+o" or
                                     * "+O" flag, the attempt SHOULD be ignored as users could bypass the
                                     * authentication mechanisms of the OPER command.  There is no
                                     * restriction, however, on anyone `deopping' themselves (using "-o" or
                                     * "-O").
                                     */
                                break;
                            case 'r':
                                user.Restricted = true;
                                break;
                            case 'w':
                                user.ReceiveWallops = true;
                                break;
                            default:
                                unknownModeFlagSeen = true;
                                break;
                        }
                }
                else if (s[0] == '-')
                {
                    // Removing mode
                    foreach (var c in s.ToCharArray().Skip(1))
                        switch (c)
                        {
                            // The flag 'a' SHALL NOT be toggled by the user using the MODE command, instead use of the AWAY command is REQUIRED.
                            case 'i':
                                user.Invisible = false;
                                break;
                            case 'O':
                                user.OperatorLocal = false;
                                break;
                            case 'o':
                                user.OperatorGlobal = false;
                                break;
                            case 'r':
                                /* On the other hand, if a user attempts to make themselves unrestricted
                                     * using the "-r" flag, the attempt SHOULD be ignored.
                                     */
                                break;
                            case 'w':
                                user.ReceiveWallops = false;
                                break;
                            default:
                                unknownModeFlagSeen = true;
                                break;
                        }
                }
            }

            if (unknownModeFlagSeen)
                await this.SendNumeric(CommandCode.ERR_UMODEUNKNOWNFLAG, ":Unknown MODE flag");

            await this.SendReply(CommandCode.RPL_UMODEIS, user.ModeString);
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// The MOTD command is used to get the "Message Of The Day" of the given
        /// server, or current server if &lt;target&gt; is omitted.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.4.1">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Motd(Message m)
        {
            var target = m[0];

            if (string.IsNullOrWhiteSpace(target))
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
                var proto = mcnntpConfigurationSection.Protocols.OfType<Core.Server.Configuration.IRC.IrcProtocolConfigurationElement>().SingleOrDefault();

                if (proto != null && !string.IsNullOrWhiteSpace(proto.MotdPath))
                {
                    var loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (loc != null)
                    {
                        var path = Path.Combine(loc, proto.MotdPath);
                        if (File.Exists(path))
                        {
                            await this.SendReply(CommandCode.RPL_MOTDSTART, string.Format(":- {0} Message of the day - ", this.server.Self.Name));
                            using (var sr = new StreamReader(path))
                            {
                                var all = await sr.ReadToEndAsync();
                                foreach (var line in all.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                                    await this.SendReply(CommandCode.RPL_MOTD, string.Format(":- {0}", line.Length <= 80 ? line : line.Substring(0, 80)));
                            }

                            await this.SendReply(CommandCode.RPL_ENDOFMOTD, ":End of MOTD command");
                            return await Task.FromResult(new CommandProcessingResult(true));
                        }

                    }

                    await this.SendNumeric(CommandCode.ERR_NOMOTD, ":MOTD File is missing");
                }
                else
                    await this.SendNumeric(CommandCode.ERR_NOMOTD, ":MOTD File is missing");
            }
            else if (!await this.server.SendPeersByTarget(m, target))
                await this.SendNumeric(CommandCode.ERR_NOMOTD, ":MOTD File is missing");

            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// NICK command is used to give user a nickname or change the existing one.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.1.2">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Nick(Message m)
        {
            var newNick = m[0];

            if (this.Principal != null && newNick == this.Principal.Name)
                // Silently Ignore.
                return new CommandProcessingResult(true);

            var principalAsUser = this.Principal as User;
            var principalAsServer = this.Principal as Server;

            if (principalAsUser != null && principalAsUser.Restricted)
                await this.SendNumeric(CommandCode.ERR_RESTRICTED, ":Your connection is restricted!");
            else if (string.IsNullOrWhiteSpace(newNick))
                await this.SendNumeric(CommandCode.ERR_NONICKNAMEGIVEN, ":No nickname given");
            else if (!this.server.VerifyNickname(newNick))
                await this.SendNumeric(CommandCode.ERR_ERRONEUSNICKNAME, newNick + " :Erroneous nickname");
            else if (this.server.NickReserved(newNick))
                await this.SendNumeric(CommandCode.ERR_UNAVAILRESOURCE, newNick + " :Nick/channel is temporarily unavailable");
            else if (this.server.NickInUse(newNick, false))
                await this.SendNumeric(CommandCode.ERR_NICKNAMEINUSE, newNick + " :Nickname is already in use");
            else
            {
                var oldNick = this.Principal != null ? this.Principal.Name : null;

                // Is this a BRAND NEW LOCAL USER?
                if (this.Principal == null)
                {
                    var user = new User(this.localAddress, this.server.Self)
                                     {
                                         Nickname = newNick
                                     };
                    this.Principal = user;
                    principalAsUser = user;
                    this.server.Users.Add(user);

                    await this.Send(new Message(principalAsUser.RawUserhost, "NICK", newNick));
                }
                else if (this.VerifyRegisteredUser())
                {
                    // An existing user sent this and is just changing their nickname.
                    await this.Send(new Message(principalAsUser.RawUserhost, "NICK", newNick));
                    principalAsUser.Nickname = newNick;
                    await this.server.SendPeers(new Message(principalAsUser.Nickname, "NICK", newNick));
                }
                else if (principalAsUser != null && principalAsUser.RegistrationUserRecevied)
                {
                    // An existing user sent this, and they sent USER before NICK.  Deal with it.
                    principalAsUser.Nickname = newNick;
                    await this.Send(new Message(principalAsUser.RawUserhost, "NICK", newNick));
                    await this.server.SendPeers(new Message(principalAsUser.Nickname, "NICK", newNick));
                }
                else if (principalAsServer != null)
                {
                    var hopcount = m[1];
                    var username = m[2];
                    var hostname = m[3];
                    var mode = m[5];
                    var realname = m[6];

                    int hc;

                    if (string.IsNullOrWhiteSpace(hopcount) || !int.TryParse(hopcount, out hc))
                        return new CommandProcessingResult(true);
                    if (string.IsNullOrWhiteSpace(username) || !Regex.IsMatch(username, Message.RegexUsername))
                        return new CommandProcessingResult(true);
                    if (string.IsNullOrWhiteSpace(hostname) || !Regex.IsMatch(hostname, Message.RegexChanString))
                        return new CommandProcessingResult(true);
                    if (string.IsNullOrWhiteSpace(realname))
                        return new CommandProcessingResult(true);

                    // This is a user announced from a server connection
                    var user = new User(principalAsServer, newNick, username, hostname, mode, realname);
                    this.server.Users.Add(user);

                    var parameters = m.Parameters.ToArray();

                    /* The <hopcount> parameter is used by servers to indicate how far away
                     * a user is from its home server.  A local connection has a hopcount of
                     * 0.  The hopcount value is incremented by each passed server.
                     * https://tools.ietf.org/html/rfc2813#section-4.1.3
                     */
                    hc++;
                    parameters[1] = hc.ToString();

                    await this.server.SendPeers(new Message(this.server.Self.Name, "NICK", parameters), principalAsServer);
                }
                else
                    throw new InvalidOperationException("Unhandled condition");

                // Send to local users in the same channel as this user.
                foreach (var chan in this.server.Channels.Where(c => c.UsersModes.Any(u => u.Key.Nickname == newNick)))
                    await this.server.SendChannelMembers(chan, new Message(this.server.Self.Name, "NICK", newNick));
                
                // TODO: Copy history (CopyHistory) for WHOWAS
                if (this.VerifyRegisteredUser())
                {
                    // TODO: COPYHISTORY
                }

                if (principalAsUser != null)
                    principalAsUser.RegistrationNickRecevied = true;

                await this.SendLoginBanner();
            }

            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// A normal user uses the OPER command to obtain operator privileges.
        /// The combination of &lt;name&gt; and &lt;password&gt; are REQUIRED to gain
        /// Operator privileges.  Upon success, the user will receive a MODE
        /// message indicating the new user modes.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.1.4">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Oper(Message m)
        {
            if (!this.VerifyRegisteredUser())
                return new CommandProcessingResult(false);

            if (m.Parameters.Count() < 2)
            {
                await this.SendNumeric(CommandCode.ERR_NEEDMOREPARAMS, "OPER :Not enough parameters");
                return new CommandProcessingResult(true);
            }

            string dns;
            try
            {
                var entry = await Dns.GetHostEntryAsync(this.remoteAddress);
                dns = entry.HostName;
            }
            catch (SocketException)
            {
                dns = null;
            }

            var name = m[0];
            var pass = m[1];

            if (string.IsNullOrWhiteSpace(pass))
            {
                await this.SendNumeric(CommandCode.ERR_PASSWDMISMATCH, ":Password incorrect");
                return new CommandProcessingResult(true);
            }
            
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
            var opers = mcnntpConfigurationSection.Protocols
                .OfType<Core.Server.Configuration.IRC.IrcProtocolConfigurationElement>()
                .SelectMany(p => p.Operators)
                .Where(o => new ScandanavianStringComparison().Compare(o.Name, name) == 0)
                .ToArray();

            if (opers.Length == 0)
            {
                await this.SendNumeric(CommandCode.ERR_NOOPERHOST, ":No O-lines for your host");
                return new CommandProcessingResult(true);
            }

            opers = opers.Where(o => (!string.IsNullOrWhiteSpace(dns) && dns.MatchesWildchar(o.HostMask)) || (o.HostMask.Contains('/') && this.remoteAddress.MatchesCIDRRange(o.HostMask))).ToArray();

            if (opers.Length == 0)
            {
                await this.SendNumeric(CommandCode.ERR_NOOPERHOST, ":No O-lines for your host");
                return new CommandProcessingResult(true);
            }

            var compare = string.Concat(_Hasher.ComputeHash(Encoding.UTF8.GetBytes(pass)).Select(b => b.ToString("X2")));
            opers = opers.Where(o => string.Compare(compare, o.Sha256HashedPassword, StringComparison.OrdinalIgnoreCase) == 0).ToArray();

            if (opers.Length == 0)
            {
                await this.SendNumeric(CommandCode.ERR_PASSWDMISMATCH, ":Password incorrect");
                return new CommandProcessingResult(true);
            }

            var user = (User)this.Principal;
            Debug.Assert(user != null);

            if (opers.Any(o => o.Global))
                user.OperatorGlobal = true;
            else
                user.OperatorLocal = true;

            await this.SendReply(CommandCode.RPL_YOUREOPER, ":You are now an IRC operator");
            await this.SendReply(CommandCode.RPL_UMODEIS, user.ModeString);
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// PRIVMSG is used to send private messages between users, as well as to
        /// send messages to channels.  &lt;msgtarget&gt; is usually the nickname of
        /// the recipient of the message, or a channel name.
        /// 
        ///    Command: PRIVMSG
        ///    Parameters: &lt;msgtarget&gt; &lt;text to be sent&gt;
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.3.1">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> PrivMsg(Message m)
        {
            var target = m[0];
            var payload = m[1];

            if (string.IsNullOrWhiteSpace(target))
            {
                await this.SendNumeric(CommandCode.ERR_NORECIPIENT, string.Format(":No recipient given ({0})", m.Command));
                return new CommandProcessingResult(true);
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                await this.SendNumeric(CommandCode.ERR_NOTEXTTOSEND, ":No text to send");
                return new CommandProcessingResult(true);
            }

            var comparer = new ScandanavianStringComparison();

            var targetUser = this.server.Users.SingleOrDefault(u => comparer.Compare(u.Nickname, target) == 0);
            if (targetUser != null)
            {
                m.Prefix = targetUser.SecureUserhost;

                if (targetUser.Server == this.server.Self)
                {
                    // Locally connected target user.
                    var targetUserConnection = this.server.Connections.SingleOrDefault(c => comparer.Compare(c.PrincipalName, target) == 0);
                    if (targetUserConnection != null)
                    {
                        // Pass the message on
                        await ((IrcConnection)targetUserConnection.Connection).Send(m);

                        // RPL_AWAY is only sent by the server to which the client is connected.
                        if (!string.IsNullOrWhiteSpace(targetUser.AwayMessage))
                            await this.SendReply(CommandCode.RPL_AWAY, string.Format("{0} :{1}", targetUser.Nickname, targetUser.AwayMessage));
                    }
                }
                else
                    await this.server.SendPeerByChain(targetUser.Server, m);

                return new CommandProcessingResult(true);
            }

            var targetChannel = this.server.Channels.SingleOrDefault(c => comparer.Compare(c.Name, target) == 0);
            var principalAsUser = this.Principal as User;
            if (targetChannel != null)
            {
                if (principalAsUser == null)
                {
                    await this.SendNumeric(CommandCode.ERR_CANNOTSENDTOCHAN, string.Format("{0} :Cannot send to channel", targetChannel.Name));
                    return new CommandProcessingResult(true);
                }

                string userChannelMode;
                if (!targetChannel.UsersModes.TryGetValue(principalAsUser, out userChannelMode))
                    userChannelMode = "!";
                
                if (targetChannel.Moderated && userChannelMode.IndexOfAny(new[] { 'O', 'o', 'v' }, 0) == -1)
                {
                    await this.SendNumeric(CommandCode.ERR_CANNOTSENDTOCHAN, string.Format("{0} :Cannot send to channel", targetChannel.Name));
                    return new CommandProcessingResult(true);
                }

                if (targetChannel.NoExternalMessages && userChannelMode == "!")
                {
                    await this.SendNumeric(CommandCode.ERR_CANNOTSENDTOCHAN, string.Format("{0} :Cannot send to channel", targetChannel.Name));
                    return new CommandProcessingResult(true);
                }

                /* Servers MUST NOT allow a channel member who is banned from the
                 * channel to speak on the channel, unless this member is a channel
                 * operator or has voice privilege. (See Section 4.1.3 (Voice
                 * Privilege)).
                 * https://tools.ietf.org/html/rfc2811#section-4.3.1
                 */
                if ((targetChannel.BanMasks.Any(bm => bm.Key.MatchesWildchar(principalAsUser.RawUserhost)) || targetChannel.BanMasks.Any(bm => bm.Key.MatchesWildchar(principalAsUser.SecureUserhost))) && userChannelMode.IndexOfAny(new[] { 'O', 'o', 'v' }, 0) == -1)
                {
                    await this.SendNumeric(CommandCode.ERR_CANNOTSENDTOCHAN, string.Format("{0} :Cannot send to channel", targetChannel.Name));
                    return new CommandProcessingResult(true);
                }

                m.Prefix = targetChannel.Anonymous ? "anonymous!anonymous@anonymous." : principalAsUser.SecureUserhost;

                await this.server.SendChannelMembers(targetChannel, m);
                return new CommandProcessingResult(true);
            }

            await this.SendNumeric(CommandCode.ERR_NOSUCHNICK, string.Format("{0} :No such nick/channel", target));
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// Handles the QUIT command from a client, which shuts down the socket and destroys this object.
        /// </summary>
        /// <returns>A command processing result specifying the connection is quitting.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.1.7">RFC 2812</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> Quit(Message m)
        {
            if (this.client.Connected)
            {
                Debug.Assert(m != null);
                Debug.Assert(m[0] != null);
                await this.SendError(m[0]);
                this.client.Client.Shutdown(SocketShutdown.Both);
                this.client.Close();
            }

            this.Shutdown();

            return new CommandProcessingResult(true, true);
        }

        /// <summary>
        /// The stats command is used to query statistics of certain server.  If
        /// &lt;query&gt; parameter is omitted, only the end of stats reply is sent
        /// back.
        /// 
        /// A query may be given for any single letter which is only checked by
        /// the destination server and is otherwise passed on by intermediate
        /// servers, ignored and unaltered.
        /// 
        /// Command: STATS
        /// Parameters: [ &lt;query&gt; [ &lt;target&gt; ] ]
        /// 
        /// Wildcards are allowed in the &lt;target&gt; parameter.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.4.4">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Stats(Message m)
        {
            var query = m[0];
            var target = m[1];

            if (string.IsNullOrWhiteSpace(target))
            {
                // If <query> parameter is omitted, only the end of stats reply is sent back.
                if (string.IsNullOrWhiteSpace(query))
                    await this.SendReply(CommandCode.RPL_ENDOFSTATS, ":End of STATS report");
                else
                    switch (query.ToLowerInvariant())
                    {
                        case "l":
                            /* Returns a list of the server's connections, showing how
                             * long each connection has been established and the
                             * traffic over that connection in Kbytes and messages for
                             * each direction;
                             */
                            var grouped = this.server.Connections.Where(c => c.ListenAddress != null && c.ListenPort != null).GroupBy(c => new Tuple<IPAddress, int>(c.ListenAddress, c.ListenPort.Value)).ToArray();

                            foreach (var g in grouped)
                            {
                                var linkname = string.Format("{0}[{1}.{2}][{3}]", this.server.Self.Name, g.Key.Item1, g.Key.Item2, "*");
                                const string Sendq = "0";
                                var recvKBytes = g.Sum(c => (long)c.RecvMessageBytes) / 1024;
                                var recvMsgs = g.Sum(c => (long)c.RecvMessageCount);
                                var sentKBytes = g.Sum(c => (long)c.SentMessageBytes) / 1024;
                                var sentMsgs = g.Sum(c => (long)c.SentMessageCount);
                                var timeopen = (int)(DateTime.UtcNow - g.Min(c => c.Established)).TotalSeconds;

                                await this.SendReply(CommandCode.RPL_STATSLINKINFO, string.Format("{0} {1} {2} {3} {4} {5} {6}", linkname, Sendq, sentMsgs, sentKBytes, recvMsgs, recvKBytes, timeopen));
                            }

                            break;
                        case "m":
                            /* Returns the usage count for each of commands supported
                             * by the server; commands for which the usage count is
                             * zero MAY be omitted;
                             */
                            foreach (var stat in _CommandStats.ToArray().OrderBy(c => c.Key))
                                await this.SendReply(CommandCode.RPL_STATSCOMMANDS, string.Format("{0} {1} {2}", stat.Key, stat.Value.Item1, stat.Value.Item2));
                            
                            break;
                        case "o":
                            // Returns a list of configured privileged users, operators;
                            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                            var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
                            foreach (var o in mcnntpConfigurationSection.Protocols.OfType<Core.Server.Configuration.IRC.IrcProtocolConfigurationElement>().SelectMany(p => p.Operators))
                                await this.SendReply(CommandCode.RPL_STATSOLINE, string.Format("O {0} * {1}", o.HostMask, o.Name));

                            break;
                        case "u":
                            // Returns a string showing how long the server has been up.
                            Debug.Assert(this.server.StartDate != null);
                            var uptime = DateTime.UtcNow - this.server.StartDate.Value;
                            await this.SendReply(CommandCode.RPL_STATSUPTIME, string.Format(":Server Up {0} days {1}:{2:00}:{3:00}", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds));
                            break;
                    }

                await this.SendReply(CommandCode.RPL_ENDOFSTATS, string.Format("{0} :End of STATS report", query));
            }
            else if (!await this.server.SendPeersByTarget(m, target))
                await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));

            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// The USER command is used at the beginning of connection to specify
        /// the username, hostname and realname of a new user.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.1.3">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> User(Message m)
        {
            if (m.Parameters.Count() < 4)
                await this.SendNumeric(CommandCode.ERR_NEEDMOREPARAMS, "USER :Not enough parameters");

            var username = m[0];
            var mode = m[1];
            var realname = m[3];

            byte iMode;

            if (!byte.TryParse(mode, out iMode))
                iMode = 0;
                 
            var principalAsUser = this.Principal as User;

            if (principalAsUser != null && !string.IsNullOrWhiteSpace(principalAsUser.Username))
                await this.SendNumeric(CommandCode.ERR_ALREADYREGISTERED, ":Unauthorized command (already registered)");
            else if (principalAsUser != null)
            {
                principalAsUser.Username = username;
                principalAsUser.RealName = realname;
                principalAsUser.RegistrationUserRecevied = true;

                principalAsUser.Invisible = (iMode & (1 << 3)) != 0;
                principalAsUser.ReceiveWallops = (iMode & (1 << 2)) != 0;
            }
            else
            {
                // Is this a BRAND NEW LOCAL USER?
                if (this.Principal == null)
                {
                    var user = new User(this.localAddress, this.server.Self)
                    {
                        Username = username,
                        RealName = realname,
                        Invisible = (iMode & (1 << 3)) != 0,
                        ReceiveWallops = (iMode & (1 << 2)) != 0
                    };
                    this.Principal = user;
                    principalAsUser = user;
                    this.server.Users.Add(user);
                }
                else
                    throw new InvalidOperationException("Unhandled condition");
                
                principalAsUser.RegistrationUserRecevied = true;
            }

            await this.SendLoginBanner();

            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// The VERSION command is used to query the version of the server
        /// program.  An optional parameter &lt;target&gt; is used to query the version
        /// of the server program which a client is not directly connected to.
        /// 
        /// Command: VERSION
        /// Parameters: [ &lt;target&gt; ]
        /// 
        /// Wildcards are allowed in the &lt;target&gt; parameter.
        /// </summary>
        /// <param name="m">The message provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="https://tools.ietf.org/html/rfc2812#section-3.4.3">RFC 2812</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Version(Message m)
        {
            var target = m[0];

            if (string.IsNullOrWhiteSpace(target))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                var version = fvi.FileVersion;

#if DEBUG
                const string Debug = "DEBUG";
#else
                const string Debug = "RELEASE";
#endif

                await this.SendReply(CommandCode.RPL_VERSION, string.Format("{0}.{1} {2} :{3}", version, Debug, this.server.Self.Name, "Pre-release software"));
            }
            else if (!await this.server.SendPeersByTarget(m, target))
                await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));

            return await Task.FromResult(new CommandProcessingResult(true));
        }
        #endregion

        private async Task SendLoginBanner()
        {
            if (this.VerifyRegisteredUser())
            {
                var user = (User)this.Principal;
                Debug.Assert(user != null);

                var assembly = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                var version = fvi.FileVersion;

                await this.SendReply(CommandCode.RPL_WELCOME, string.Format(":Welcome to the Internet Relay Network {0}", user.RawUserhost));
                await this.SendReply(CommandCode.RPL_YOURHOST, string.Format(":Your host is {0}, running version {1}", this.server.Self.Name, version));
                await this.SendReply(CommandCode.RPL_CREATED, string.Format(":This server was created {0}", this.RetrieveLinkerTimestamp()));
                await this.SendReply(CommandCode.RPL_MYINFO, string.Format("{0} {1} {2} {3}", this.server.Self.Name, version, "iowghraAsORTVSxNCWqBzvdHtGpI", "lvhopsmntikrRcaqOALQbSeIKVfMCuzNTGjZ"));
            }
        }

        private DateTime RetrieveLinkerTimestamp()
        {
            var filePath = Assembly.GetCallingAssembly().Location;
            const int PeHeaderOffset = 60;
            const int LinkerTimestampOffset = 8;
            var b = new byte[2048];
            Stream s = null;

            try
            {
                s = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                if (s != null)
                {
                    s.Close();
                }
            }

            var i = BitConverter.ToInt32(b, PeHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(b, i + LinkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }

        private bool VerifyRegisteredUser()
        {
            var principalAsUser = this.Principal as User;
            return principalAsUser != null && principalAsUser.RegistrationUserRecevied && principalAsUser.RegistrationNickRecevied;
        }
    }
}
