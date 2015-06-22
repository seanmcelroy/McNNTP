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
    internal class IrcConnection
    {
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
                                   { "LUSERS", async (c, m) => await c.Lusers(m) },
                                   { "MOTD", async (c, m) => await c.Motd(m) },
                                   { "NICK", async (c, m) => await c.Nick(m) },
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
                await this.SendReply(CommandCode.RPL_LUSEROP, string.Format("{0} :operator(s) online", this.server.Users.Count(u => u.Operator && predicate(u.Server))));
                // This is not defined, so for this project, if this is just a user who has not positively authenticated, we call them 'unknown'
                await this.SendReply(CommandCode.RPL_LUSERUNKNOWN, string.Format("{0} :unknown connection(s)", this.server.Connections.Count(c => string.IsNullOrWhiteSpace(c.AuthenticatedUsername))));
                await this.SendReply(CommandCode.RPL_LUSERCHANNELS, string.Format("{0} :channels formed", this.server.Channels.Count));
                await this.SendReply(CommandCode.RPL_LUSERME, string.Format(":I have {0} clients and {1} servers", this.server.Users.Count(u => u.Server == this.server.Self) + this.server.Services.Count(u => u.Server == this.server.Self), this.server.Servers.Count(u => u.Parent == this.server.Self)));
                return await Task.FromResult(new CommandProcessingResult(true));
            }

            // TODO: Resolve target
            await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));
            return await Task.FromResult(new CommandProcessingResult(true));
        }

        /// <summary>
        /// The MOTD command is used to get the "Message Of The Day" of the given
        /// server, or current server if <target> is omitted.
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

            // TODO: Resolve target
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
                else if (principalAsUser != null && principalAsUser.RegistrationNickRecevied && principalAsUser.RegistrationUserRecevied)
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
                foreach (var chan in this.server.Channels.Where(c => c.Users.Any(u => u.Nickname == newNick)))
                    await this.server.SendChannelMembers(chan, new Message(this.server.Self.Name, "NICK", newNick));
                
                // TODO: Copy history (CopyHistory) for WHOWAS
                if (principalAsUser != null && principalAsUser.RegistrationNickRecevied && principalAsUser.RegistrationUserRecevied)
                {
                    // TODO: COPYHISTORY
                }

                if (principalAsUser != null)
                    principalAsUser.RegistrationNickRecevied = true;

                await this.SendLoginBanner(principalAsUser);
            }

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
                            break;
                        case "u":
                            // Returns a string showing how long the server has been up.
                            break;
                    }

                await this.SendReply(CommandCode.RPL_ENDOFSTATS, string.Format("{0} :End of STATS report", query));
                return await Task.FromResult(new CommandProcessingResult(true));
            }

            // TODO: Resolve target
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

            await this.SendLoginBanner(principalAsUser);

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
                return await Task.FromResult(new CommandProcessingResult(true));
            }

            // TODO: Resolve target
            await this.SendNumeric(CommandCode.ERR_NOSUCHSERVER, string.Format("{0} :No such server", target));
            return await Task.FromResult(new CommandProcessingResult(true));
        }
        #endregion

        private async Task SendLoginBanner([CanBeNull] User user)
        {
            if (user != null && user.RegistrationNickRecevied && user.RegistrationUserRecevied)
            {
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
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;
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

            var i = BitConverter.ToInt32(b, c_PeHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }
    }
}
