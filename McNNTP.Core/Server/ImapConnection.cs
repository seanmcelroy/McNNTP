// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImapConnection.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A connection from a client to the server
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using log4net;
    using Common;

    /// <summary>
    /// A connection from a client to the server
    /// </summary>
    internal class ImapConnection
    {
        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1024;

        /// <summary>
        /// A command-indexed dictionary with function pointers to support client command
        /// </summary>
        private static readonly Dictionary<string, Func<ImapConnection, string, string, Task<CommandProcessingResult>>> CommandDirectory;

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ImapConnection));

        /// <summary>
        /// The store to use to satisfy requests of this connection
        /// </summary>
        private readonly IStoreProvider store;

        /// <summary>
        /// The server instance to which this connection belongs
        /// </summary>
        [NotNull]
        private readonly ImapServer server;

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
        /// For commands that handle conversational request-replies, this is a reference to the
        /// command that should handle new input received by the main process loop.
        /// </summary>
        [CanBeNull]
        private CommandProcessingResult inProcessCommand;

        /// <summary>
        /// Initializes static members of the <see cref="ImapConnection"/> class.
        /// </summary>
        static ImapConnection()
        {
            CommandDirectory = new Dictionary<string, Func<ImapConnection, string, string, Task<CommandProcessingResult>>>
                               {
                                   { "CHECK", async (c, tag, command) => await c.Noop("CHECK", tag) },
                                   { "CLOSE", async (c, tag, command) => await c.Close(tag) },
                                   { "CREATE", async (c, tag, command) => await c.Create(tag, command) },
                                   { "EXAMINE", async (c, tag, command) => await c.Select("EXAMINE", tag, command) },
                                   { "LOGIN", async (c, tag, command) => await c.Login(tag, command) },
                                   { "LSUB", async (c, tag, command) => await c.LSub(tag, command) },
                                   { "CAPABILITY", async (c, tag, command) => await c.Capability(tag) },
                                   { "LIST", async (c, tag, command) => await c.List(tag, command) },
                                   { "LOGOUT", async (c, tag, command) => await c.Logout(tag) },
                                   { "NOOP", async (c, tag, command) => await c.Noop("NOOP", tag) },
                                   { "SELECT", async (c, tag, command) => await c.Select("SELECT", tag, command) },
                                   { "SUBSCRIBE", async (c, tag, command) => await c.Subscribe(tag, command) },
                                   { "STATUS", async (c, tag, command) => await c.Status(tag, command) },
                                   { "UID", async (c, tag, command) => await c.Uid(tag, command) },
                                   { "UNSUBSCRIBE", async (c, tag, command) => await c.Unsubscribe(tag, command) }
                               };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImapConnection"/> class.
        /// </summary>
        /// <param name="store">The catalog and message store to use to back operations for this connection</param>
        /// <param name="server">The server instance that owns this connection</param>
        /// <param name="client">The <see cref="TcpClient"/> that accepted this connection</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/></param>
        /// <param name="tls">Whether or not the connection has implicit Transport Layer Security</param>
        public ImapConnection(
            [NotNull] IStoreProvider store,
            [NotNull] ImapServer server,
            [NotNull] TcpClient client,
            [NotNull] Stream stream,
            bool tls = false)
        {
            this.store = store;

            this.AllowStartTls = server.AllowStartTLS;
            this.client = client;
            this.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ShowBytes = server.ShowBytes;
            this.ShowCommands = server.ShowCommands;
            this.ShowData = server.ShowData;
            this.server = server;
            this.stream = stream;
            this.TLS = tls;

            var remoteIpEndpoint = (IPEndPoint)this.client.Client.RemoteEndPoint;
            this.remoteAddress = remoteIpEndpoint.Address;
            this.remotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this.client.Client.LocalEndPoint;
            this.localAddress = localIpEndpoint.Address;
            this.localPort = localIpEndpoint.Port;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the connection can be upgraded to use
        /// Transport Later Security (TLS) through the STARTTLS command.
        /// </summary>
        [PublicAPI]
        public bool AllowStartTls { get; set; }

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

        #region Compression
        /// <summary>
        /// Gets a value indicating whether the connection should have compression enabled
        /// </summary>
        [PublicAPI]
        public bool Compression { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the connection is compressed with the UNIX GZip protocol
        /// </summary>
        [PublicAPI]
        public bool CompressionGZip { get; private set; }

        /// <summary>
        /// Gets a value indicating whether message terminators are also compressed
        /// </summary>
        [PublicAPI]
        public bool CompressionTerminator { get; private set; }
        #endregion

        /// <summary>
        /// Gets the catalog currently selected by this connection
        /// </summary>
        [PublicAPI, CanBeNull]
        public string CurrentCatalog { get; private set; }

        /// <summary>
        /// Gets a value indicating the catalog currently selected by this connection
        /// </summary>
        [PublicAPI, CanBeNull]
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
            await this.Send("* OK IMAP4rev1 Service Ready");

            Debug.Assert(this.stream != null, "The stream was 'null', but it should not have been because the connection was accepted and processing is beginning.");

            bool send403;

            try
            {
                while (true)
                {
                    if (!this.client.Connected || !this.client.Client.Connected) return;

                    if (!this.stream.CanRead)
                    {
                        await this.Send("* BYE Unable to read from stream");
                        this.Shutdown();
                        return;
                    }

                    var bytesRead = await this.stream.ReadAsync(this.buffer, 0, BufferSize);

                    // There  might be more data, so store the data received so far.
                    this.builder.Append(Encoding.ASCII.GetString(this.buffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var builderString = this.builder.ToString();
                    if (bytesRead == BufferSize || !builderString.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    // There could be MORE THAN ONE command in one read.
                    var builderMoreThanOne = builderString.Length > 2 && builderString.Substring(0, builderString.Length - 2).IndexOf("\r\n", StringComparison.Ordinal) > -1;
                    string[] inputs;
                    if (builderMoreThanOne)
                    {
                        var split = builderString.Split(new[] { "\r\n" }, StringSplitOptions.None);
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
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes: {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                            builderString.Length,
                            builderString.TrimEnd('\r', '\n'));
                    else if (this.ShowBytes)
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                            builderString.Length);
                    else if (this.ShowData)
                        Logger.TraceFormat(
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
                            var parts = input.Split(' ');
                            var tag = parts.First();
                            if (parts.Length < 2) 
                                await this.Send("{0} BAD unexpected end of data", "*");
                            else
                            {
                                var command = parts.ElementAt(1).TrimEnd('\r', '\n').ToUpperInvariant();
                                var phrase = parts.Skip(1).Aggregate((c, n) => c + " " + n).TrimEnd('\r', '\n');
                                if (CommandDirectory.ContainsKey(command))
                                {
                                    try
                                    {
                                        if (this.ShowCommands)
                                            Logger.TraceFormat(
                                                "{0}:{1} >{2}> {3}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : ">",
                                                phrase);

                                        var result = await CommandDirectory[command].Invoke(this, tag, phrase);

                                        if (!result.IsHandled) 
                                            await this.Send("{0} BAD unexpected end of data", "*");
                                        else if (result.MessageHandler != null) this.inProcessCommand = result;
                                        else if (result.IsQuitting) return;
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error("Exception processing a command", ex);
                                        break;
                                    }
                                }
                                else
                                    await this.Send("{0} BAD unexpected end of data", tag);
                            }
                        }
                    }
                }
            }
            catch (DecoderFallbackException dfe)
            {
                send403 = true;
                Logger.Error("Decoder Fallback Exception socket " + this.RemoteAddress, dfe);
            }
            catch (IOException se)
            {
                send403 = true;
                Logger.Error("I/O Exception on socket " + this.RemoteAddress, se);
            }
            catch (SocketException se)
            {
                send403 = true;
                Logger.Error("Socket Exception on socket " + this.RemoteAddress, se);
            }
            catch (NotSupportedException nse)
            {
                Logger.Error("Not Supported Exception", nse);
                return;
            }
            catch (ObjectDisposedException ode)
            {
                Logger.Error("Object Disposed Exception", ode);
                return;
            }

            if (send403)
                await this.Send("403 Archive server temporarily offline");
        }

        /// <summary>
        /// Sends the formatted data to the client
        /// </summary>
        /// <param name="format">The data, or format string for data, to send to the client</param>
        /// <param name="args">The argument applied as a format string to <paramref name="format"/> to create the data to send to the client</param>
        /// <returns>A value indicating whether or not the transmission was successful</returns>
        [StringFormatMethod("format"), NotNull]
        protected internal async Task<bool> Send([NotNull] string format, [NotNull] params object[] args)
        {
            if (args.Length == 0)
                return await this.SendInternal(format + "\r\n", false);

            return await this.SendInternal(string.Format(CultureInfo.InvariantCulture, format, args) + "\r\n", false);
        }

        private async Task<bool> SendInternal([NotNull] string data, bool compressedIfPossible)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData;
            if (compressedIfPossible && this.Compression && this.CompressionGZip && this.CompressionTerminator)
                byteData = await data.GZipCompress();
            else
                byteData = Encoding.UTF8.GetBytes(data);

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData, 0, byteData.Length);
                if (this.ShowBytes && this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                else if (this.ShowBytes)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        byteData.Length);
                else if (this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        data.TrimEnd('\r', '\n'));

                return true;
            }
            catch (IOException)
            {
                // Don't send 403 - the sending socket isn't working.
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (ObjectDisposedException)
            {
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
        }

        /// <summary>
        /// Shuts down the IMAP connection
        /// </summary>
        public void Shutdown()
        {
            if (this.client.Connected)
            {
                this.client.Client.Shutdown(SocketShutdown.Both);
                this.client.Close();
            }

            this.server.RemoveConnection(this);
        }
        #endregion

        #region Commands

        private async Task<CommandProcessingResult> Close(string tag)
        {
            // TODO: Remove all deleted messages per RFC 3.5.0 6.4.2

            this.CurrentCatalog = null;
            this.CurrentCatalogReadOnly = false;
            this.CurrentMessageNumber = null;
            
            await this.Send("{0} OK CLOSE completed", tag);
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// Creates a mailbox in the store
        /// </summary>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <param name="command">The command for the create mailbox request</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.3.3">RFC 3501</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> Create([NotNull] string tag, [NotNull] string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"CREATE\s(""(?<mbox>[^""]+)""|(?<mbox>[^\s]+))", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.Send("{0} BAD Unable to parse CREATE parameters", tag);
                return new CommandProcessingResult(true);
            }

            // RFC 3501 6.3.3: If the mailbox name is suffixed with the server's hierarchy
            // separator character (as returned from the server by a LIST
            // command), this is a declaration that the client intends to create
            // mailbox names under this name in the hierarchy.  Server
            // implementations that do not require this declaration MUST ignore
            // the declaration.  In any case, the name created is without the
            // trailing hierarchy delimiter.
            var mbox = match.Groups["mbox"].Value;
            if (this.store.HierarchyDelimiter != "NIL" && mbox.EndsWith(this.store.HierarchyDelimiter, StringComparison.OrdinalIgnoreCase))
                mbox = mbox.Substring(0, mbox.Length - this.store.HierarchyDelimiter.Length);

            var result = this.store.CreatePersonalCatalog(this.Identity, mbox);
            if (result)
                await this.Send("{0} OK CREATE completed", tag);
            else
                await this.Send("{0} NO CREATE failed", tag);

            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// Allows a user to authenticate
        /// </summary>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <param name="command">The command</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.2.3">RFC 3501</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Login([NotNull] string tag, [NotNull] string command)
        {
            if (this.Identity != null)
            {
                await this.Send("{0} BAD User already authenticated as {1}", tag, this.Identity.Username);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"LOGIN\s(""(?<username>[^""]+)""|(?<username>[^\s]+))\s(""(?<password>[^""]*)""|(?<password>.*))", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.Send("{0} BAD Unable to parse username and password", tag);
                return new CommandProcessingResult(true);
            }

            this.Username = match.Groups["username"].Value;
            var password = match.Groups["password"].Value;

            //if (admin == null)
            //{
            //    if (this.server.LdapDirectoryConfiguration != null && this.server.LdapDirectoryConfiguration.AutoEnroll)
            //    {
            //        var memberships = LdapUtility.GetUserGroupMemberships(
            //            server.LdapDirectoryConfiguration.LdapServer,
            //            server.LdapDirectoryConfiguration.LookupAccountUsername,
            //            server.LdapDirectoryConfiguration.LookupAccountPassword,
            //            Username);

            //        // Auto enroll the user as an administrator.
            //        throw new NotImplementedException("Auto enrollment is not yet implemented.");

            //        // if (memberships.Any(m => string.Compare(m, this.server.LdapDirectoryConfiguration.AutoEnrollAdminGroup, StringComparison.OrdinalIgnoreCase) == 0))
            //        // {
            //        //     // Auto enroll the user as an administrator.
            //        //     throw new NotImplementedException("Auto enrollment is not yet implemented.");
            //        // }
            //        // else 
            //        // {
            //        //     // Auto enroll the user as a non-administrator.
            //        //     throw new NotImplementedException("Auto enrollment is not yet implemented.");
            //        // }
            //    }
            //    else
            //    {
            //        // No user with this username in the local database
            //        await Send("{0} NO login failure: username or password rejected", tag);
            //        return new CommandProcessingResult(true);
            //    }
            //}

            if (this.server.LdapDirectoryConfiguration != null)
            {
                // LDAP authentication
                if (!LdapUtility.AuthenticateUser(
                        this.server.LdapDirectoryConfiguration.LdapServer,
                        this.server.LdapDirectoryConfiguration.SearchPath, this.Username,
                        password))
                {
                    Logger.WarnFormat("User {0} failed authentication against LDAP server.", this.Username);
                    await this.Send("{0} NO login failure: username or password rejected", tag);
                    return new CommandProcessingResult(true);
                }
            }
            else
            {
                // Local authentication
                this.Identity = this.store.GetIdentityByClearAuth(this.Username, password);
                if (this.Identity == null)
                {
                    Logger.WarnFormat("User {0} failed authentication against local authentication database.", this.Username);
                    await this.Send("{0} NO login failure: username or password rejected", tag);
                    return new CommandProcessingResult(true);
                }
            }

            if (this.Identity.LocalAuthenticationOnly && !IPAddress.IsLoopback(this.RemoteAddress))
            {
                await this.Send("{0} NO Authentication not allowed except locally", tag);
                return new CommandProcessingResult(true);
            }

            Logger.InfoFormat("User {0} authenticated from {1}", this.Identity.Username, this.RemoteAddress);

            // Ensure user has personal INBOX defined.
            this.store.Ensure(this.Identity);

            await this.Send("{0} OK LOGIN completed", tag);
            return new CommandProcessingResult(true);
        }
        
        /// <summary>
        /// Handles the CAPABILITY command from a client, which allows a client to retrieve a list
        /// of the functionality available in this server. 
        /// </summary>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.1.1">RFC 3501</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Capability([NotNull] string tag)
        {
            await this.Send("* CAPABILITY IMAP4rev1");
            await this.Send("{0} OK CAPABILITY completed", tag);
            return new CommandProcessingResult(true);
        }
        
        /// <summary>
        /// Handles the LOGOUT command from a client, which shuts down the socket and destroys this object.
        /// </summary>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <returns>A command processing result specifying the connection is quitting.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.1.3">RFC 3501</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> Logout([NotNull] string tag)
        {
            await this.Send("* BYE IMAP4rev1 Server logging out");
            await this.Send("{0} OK LOGOUT completed", tag);

            this.Shutdown();
            return new CommandProcessingResult(true, true);
        }

        /// <summary>
        /// Handles the NOOP command from a client, which does nothing
        /// </summary>
        /// <param name="match">The specific command supplied.  This one method can handle other commands that are NOOP operations, such as CHECK.</param>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.1.2">RFC 3501</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> Noop([NotNull] string match, [NotNull] string tag)
        {
            // TODO: See note in RFC 3501 6.1.2 - This could be improved to return unread message count for periodic polling
            if (this.Identity != null && this.CurrentCatalog != null)
            {
                var catalog = this.store.GetCatalogByName(this.Identity, this.CurrentCatalog);
                if (catalog != null)
                    await this.Send("* {0} EXISTS", catalog.MessageCount);
            }

            await this.Send("{0} OK {1} completed", tag, match);
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// Handles the SELECT command from a client, which focuses on a particular catalog for other operations
        /// </summary>
        /// <param name="match">Match parameters for the select</param>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <param name="command">The full command received by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3501#section-6.3.1">RFC 3501</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> Select([NotNull] string match, [NotNull] string tag, [NotNull] string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var regex = Regex.Match(command, @"SELECT\s(""(?<mbox>[^""]+)""|(?<mbox>[^\s]+))", RegexOptions.IgnoreCase);
            if (!regex.Success)
            {
                await this.Send("{0} BAD Unable to parse {1} parameters", tag, match);
                
                // RFC 3501 3.1 - A failed SELECT command will move from Selected to Authenticated state
                this.CurrentCatalog = null;
                this.CurrentCatalogReadOnly = false;

                return new CommandProcessingResult(true);
            }

            var mbox = regex.Groups["mbox"].Value;

            var ng = this.store.GetCatalogByName(this.Identity, mbox);
            if (ng == null)
            {
                await this.Send("{0} BAD Unable to locate mailbox", tag);
                return new CommandProcessingResult(true);
            }

            this.CurrentCatalog = ng.Name;
            this.CurrentCatalogReadOnly = match == "EXAMINE";

            await this.Send("* FLAGS ()"); // TODO: Implement message flags
            await this.Send("* {0} EXISTS", ng.MessageCount);
            await this.Send("* {0} RECENT", ng.MessageCount); // TODO: Implement \Recent flag
            // TODO: Note section 6.3.1 of RFC 3501 - I'm not implementing some optional elements I probably should like UNSEEN, PERMANENTFLAGS
            await this.Send("* OK [UIDNEXT {0}]", ng.HighWatermark == null ? 1 : ng.HighWatermark + 1);
            await this.Send("* OK [UIDVALIDITY {0:yyyyMMddhhmm}]", ng.CreateDateUtc);

            if (this.CurrentCatalogReadOnly)
                await this.Send("{0} OK [READ-ONLY] {1} completed", tag, match);
            else if (ng.Owner != null && ng.Owner.Equals(this.Identity))
                await this.Send("{0} OK [READ-WRITE] {1} completed", tag, match);
            else
                await this.Send("{0} OK [READ-ONLY] {1} completed", tag, match);

            return new CommandProcessingResult(true);
        }

        private async Task<CommandProcessingResult> LSub([NotNull] string tag, [NotNull] string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"LSUB\s(""(?<ref>[^""]+)""|(?<ref>[^\s]+))\s(""(?<mbox>[^""]*)""|(?<mbox>.*))", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.Send("{0} BAD Unable to parse LSUB parameters", tag);
                return new CommandProcessingResult(true);
            }

            var mbox = match.Groups["mbox"].Value;

            var subs = this.store.GetSubscriptions(this.Identity);

            if (string.IsNullOrEmpty(mbox))
                foreach (var sub in subs.AsParallel())
                    await this.Send(@"* LSUB () {0} {1}", this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", sub);
            else
            {
                var regex = new Regex(Regex.Escape(mbox).Replace(@"\*", ".*").Replace(@"%", this.store.HierarchyDelimiter == "NIL" ? ".*" : "[^" + Regex.Escape(this.store.HierarchyDelimiter) + "]*").Replace(@"\?", "."), RegexOptions.IgnoreCase);
                foreach (var sub in subs.AsParallel().Where(c => regex.IsMatch(c)))
                    await this.Send(@"* LSUB () {0} {1}", this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", sub);
            }

            await this.Send("{0} OK LSUB completed", tag);
            return new CommandProcessingResult(true);
        }

        [NotNull] 
        private async Task<CommandProcessingResult> Subscribe([NotNull] string tag, [NotNull] string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"SUBSCRIBE\s(""(?<mbox>[^""]*)""|(?<mbox>.*))", RegexOptions.IgnoreCase);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["mbox"].Value))
            {
                await this.Send("{0} BAD Unable to parse SUBSCRIBE parameters", tag);
                return new CommandProcessingResult(true);
            }

            var mbox = match.Groups["mbox"].Value;

            var subs = this.store.GetSubscriptions(this.Identity).ToArray();

            if (subs.Any(s => string.Compare(s, mbox, StringComparison.OrdinalIgnoreCase) == 0))
            {
                // Already subscribed
                await this.Send("{0} OK SUBSCRIBE completed", tag);
                return new CommandProcessingResult(true);
            }

            if (this.store.CreateSubscription(this.Identity, mbox))
            {
                await this.Send("{0} OK SUBSCRIBE completed", tag);
                return new CommandProcessingResult(true);
            }

            await this.Send("{0} NO SUBSCRIBE failed", tag);
            return new CommandProcessingResult(true);
        }

        private async Task<CommandProcessingResult> Unsubscribe(string tag, string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"UNSUBSCRIBE\s(""(?<mbox>[^""]*)""|(?<mbox>.*))", RegexOptions.IgnoreCase);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["mbox"].Value))
            {
                await this.Send("{0} BAD Unable to parse UNSUBSCRIBE parameters", tag);
                return new CommandProcessingResult(true);
            }

            var mbox = match.Groups["mbox"].Value;

            var subs = this.store.GetSubscriptions(this.Identity).ToArray();

            if (subs.Any(s => string.Compare(s, mbox, StringComparison.OrdinalIgnoreCase) == 0) && this.store.DeleteSubscription(this.Identity, mbox))
            {
                await this.Send("{0} OK UNSUBSCRIBE completed", tag);
                return new CommandProcessingResult(true);
            }

            await this.Send("{0} NO UNSUBSCRIBE failed", tag);
            return new CommandProcessingResult(true);
        }

        /// <summary>
        /// Handles the LIST command from a client, which allows a client to retrieve blocks
        /// of information depending on the parameters and arguments supplied with the command.
        /// </summary>
        /// <param name="tag">The tag for the command sequence as sent by the client</param>
        /// <param name="command">The full command request provided by the client</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3977#section-7.6.1">RFC 3977</a> for more information.</remarks>
        [NotNull]
        private async Task<CommandProcessingResult> List([NotNull] string tag, [NotNull] string command)
        {
            var identity = this.Identity;

            if (identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"LIST\s(""(?<ref>[^""]+)""|(?<ref>[^\s]+))\s(""(?<mbox>[^""]*)""|(?<mbox>.*))", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.Send("{0} BAD Unable to parse LIST parameters", tag);
                return new CommandProcessingResult(true);
            }

            var globalCatalogs = this.store.GetGlobalCatalogs(this.Identity);
            if (globalCatalogs == null)
            {
                await this.Send("{0} BAD Global catalogs temporarily offline", tag);
                return new CommandProcessingResult(true);
            }

            var mbox = match.Groups["mbox"].Value;

            if (string.IsNullOrEmpty(mbox))
                foreach (var ng in globalCatalogs.AsParallel())
                {
                    var flags = this.GetFlags(identity, ng.Name, this.store);
                    await this.Send(@"* LIST ({0}) {1} ""{2}""", flags, this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", ng.Name);
                }
            else
            {
                var regex = new Regex(Regex.Escape(mbox).Replace(@"\*", ".*").Replace(@"%", this.store.HierarchyDelimiter == "NIL" ? ".*" : "[^" + Regex.Escape(this.store.HierarchyDelimiter) + "]*").Replace(@"\?", "."), RegexOptions.IgnoreCase);
                foreach (var ng in globalCatalogs.AsParallel().Where(c => regex.IsMatch(c.Name)))
                {
                    var flags = this.GetFlags(identity, ng.Name, this.store);
                    await this.Send(@"* LIST ({0}) {1} ""{2}""", flags, this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", ng.Name);
                }
            }

            var personalCatalogs = this.store.GetPersonalCatalogs(this.Identity);
            if (personalCatalogs == null)
            {
                await this.Send("{0} BAD Personal catalogs temporarily offline", tag);
                return new CommandProcessingResult(true);
            }

            if (string.IsNullOrEmpty(mbox))
                foreach (var ng in personalCatalogs.AsParallel())
                {
                    var flags = this.GetFlags(identity, ng.Name, this.store);
                    await this.Send(@"* LIST ({0}) {1} ""{2}""", flags, this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", ng.Name);
                }
            else
            {
                var regex = new Regex(Regex.Escape(mbox).Replace(@"\*", ".*").Replace(@"%", this.store.HierarchyDelimiter == "NIL" ? ".*" : "[^" + Regex.Escape(this.store.HierarchyDelimiter) + "]*").Replace(@"\?", "."), RegexOptions.IgnoreCase);
                foreach (var ng in personalCatalogs.AsParallel().Where(c => regex.IsMatch(c.Name)))
                {
                    var flags = this.GetFlags(identity, ng.Name, this.store);
                    await this.Send(@"* LIST ({0}) {1} ""{2}""", flags, this.store.HierarchyDelimiter == "NIL" ? "NIL" : "\"" + this.store.HierarchyDelimiter + "\"", ng.Name);
                }
            }
            
            await this.Send("{0} OK LIST completed.", tag);
            return new CommandProcessingResult(true);
        }

        private async Task<CommandProcessingResult> Status(string tag, string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var match = Regex.Match(command, @"STATUS\s(""(?<mbox>[^""]+)""|(?<mbox>[^\s]+))\s(""(?<items>[^""]*)""|(?<items>.*))", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.Send("{0} BAD Unable to parse STATUS parameters", tag);
                return new CommandProcessingResult(true);
            }

            var mbox = match.Groups["mbox"].Value;
            var items = match.Groups["items"].Value;

            var ng = this.store.GetCatalogByName(this.Identity, mbox);
            if (ng == null)
            {
                await this.Send("{0} NO No such mailbox.", tag);
                return new CommandProcessingResult(true);
            }

            var sb = new StringBuilder();
            sb.AppendFormat("* STATUS {0} (", mbox);
            var open = 0;
            if (items.IndexOf("MESSAGES", StringComparison.OrdinalIgnoreCase) > -1)
                sb.AppendFormat("{0}MESSAGES {1}", ++open == 1 ? string.Empty : " ", ng.MessageCount);
            if (items.IndexOf("RECENT", StringComparison.OrdinalIgnoreCase) > -1)
                sb.AppendFormat("{0}RECENT {1}", ++open == 1 ? string.Empty : " ", ng.MessageCount); // TODO: Implement the RECENT and \Recent flags
            if (items.IndexOf("UIDNEXT", StringComparison.OrdinalIgnoreCase) > -1)
                sb.AppendFormat("{0}UIDNEXT {1}", ++open == 1 ? string.Empty : " ", ng.HighWatermark == null ? 1 : ng.HighWatermark + 1);
            if (items.IndexOf("UIDVALIDITY", StringComparison.OrdinalIgnoreCase) > -1)
                sb.AppendFormat("{0}UIDVALIDITY {1:yyyyMMddhhmm}", ++open == 1 ? string.Empty : " ", ng.CreateDateUtc);
            if (items.IndexOf("UNSEEN", StringComparison.OrdinalIgnoreCase) > -1)
                sb.AppendFormat("{0}UNSEEN {1}", ++open == 1 ? string.Empty : " ", ng.MessageCount); // TODO: Implement the \Seen flag
            sb.Append(")");
            await this.Send(sb.ToString());
            await this.Send("{0} OK STATUS completed", tag);
            return new CommandProcessingResult(true);
        }

        [NotNull, Pure]
        private async Task<CommandProcessingResult> Uid([NotNull] string tag, string command)
        {
            if (this.Identity == null)
            {
                await this.Send("{0} NO Connection not yet authenticated", tag);
                return new CommandProcessingResult(true);
            }

            var matchFetch = Regex.Match(command, @"UID\sFETCH\s(""(?<lbound>[^""]+)""|(?<lbound>[^\s]+))(:(""(?<ubound>[^""]*)""|(?<ubound>[^\s]+)))?\s(\((?<elems>[^\)]+)\)|(?<elems>.*))", RegexOptions.IgnoreCase);
            if (!matchFetch.Success)
            {
                await this.Send("{0} BAD Unable to parse UID parameters", tag);
                return new CommandProcessingResult(true);
            }

            if (this.CurrentCatalog == null)
            {
                await this.Send("{0} NO No mailbox selected", tag);
                return new CommandProcessingResult(true);
            }

            var lbound = matchFetch.Groups["lbound"].Value;
            var ubound = (matchFetch.Groups["ubound"] != null) ? matchFetch.Groups["ubound"].Value : null;
            var elems = matchFetch.Groups["elems"].Value;

            int lboundNumber, uboundNumberTemp;
            lboundNumber = int.TryParse(lbound, out lboundNumber) ? lboundNumber : 1;
            var uboundNumber = ubound == null ? default(int?) : int.TryParse(ubound, out uboundNumberTemp) ? uboundNumberTemp : default(int?);
            
            var messages = this.store.GetMessages(this.Identity, this.CurrentCatalog, lboundNumber, uboundNumber);

            if (messages == null)
            {
                await this.Send("{0} BAD Archive server temporarily offline", tag);
                return new CommandProcessingResult(true);
            }

            var i = 0;
            var showFlags = elems.IndexOf("FLAGS", StringComparison.OrdinalIgnoreCase) > -1;
            var flags = showFlags ? this.store.GetMessageDetails(this.Identity, this.CurrentCatalog, lboundNumber, uboundNumber).ToArray() : new IMessageDetail[0];

            Parallel.ForEach(
                messages, 
                async message =>
                {
                i++;
                var sb = new StringBuilder();
                sb.AppendFormat("* {0} FETCH (", i);
                if (showFlags)
                {
                    var flag = flags.SingleOrDefault(f => f.MessageId == message.Id);
                    if (flag == null)
                        sb.Append("FLAGS () "); // TODO: Implement message flags
                    else
                    {
                        var flagBuilder = new StringBuilder();
                        if (flag.Answered.HasValue)
                            flagBuilder.Append(@"\Answered ");
                        if (flag.Deleted.HasValue)
                            flagBuilder.Append(@"\Deleted ");
                        if (flag.Important.HasValue)
                            flagBuilder.Append(@"\Flagged ");
                        if (flag.Seen.HasValue)
                            flagBuilder.Append(@"\Seen ");

                        sb.AppendFormat("FLAGS ({0}) ", flagBuilder.ToString().TrimEnd());
                    }
                }

                if (elems.IndexOf("RFC822.SIZE", StringComparison.OrdinalIgnoreCase) > -1)
                    sb.AppendFormat("RFC822.SIZE {0} ", message.HeaderRaw.Length + 1 + message.Body.Length);

                sb.AppendFormat("UID {0}", message.Id);

                if (elems.IndexOf("BODY[]", StringComparison.OrdinalIgnoreCase) > -1 ||
                    elems.IndexOf("BODY.PEEK[]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    var sb2 = new StringBuilder();
                    foreach (var headerLine in message.HeaderRaw.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        sb2.AppendLine(headerLine);
                    sb2.AppendLine();

                    foreach (var bodyLine in message.Body.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        sb2.AppendLine(bodyLine);

                    sb.AppendFormat(" BODY[] {{{0}}}\r\n{1}", sb2.Length, sb2);
                }

                if (elems.IndexOf("BODY[HEADER]", StringComparison.OrdinalIgnoreCase) > -1 ||
                    elems.IndexOf("BODY.PEEK[HEADER]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    var sb2 = new StringBuilder();
                    foreach (var headerLine in message.HeaderRaw.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        sb2.AppendLine(headerLine);
                    sb2.AppendLine();

                    sb.AppendFormat(" BODY[HEADER] {{{0}}}\r\n{1}", sb2.Length, sb2);
                }

                if (elems.IndexOf("BODY[TEXT]", StringComparison.OrdinalIgnoreCase) > -1 ||
                    elems.IndexOf("BODY.PEEK[TEXT]", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    var sb2 = new StringBuilder();
                    foreach (var bodyLine in message.Body.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                        sb2.AppendLine(bodyLine);
                    sb2.AppendLine();

                    sb.AppendFormat(" BODY[TEXT] {{{0}}}\r\n{1}", sb2.Length, sb2);
                }

                if (elems.IndexOf("BODY[HEADER.FIELDS", StringComparison.OrdinalIgnoreCase) > -1 ||
                    elems.IndexOf("BODY.PEEK[HEADER.FIELDS", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    var sb2 = new StringBuilder();
                    var fieldMatch = Regex.Match(elems, @"HEADER.FIELDS\s*\(((?<header>.*\b)(\s+|\)))*", RegexOptions.IgnoreCase);
                    if (fieldMatch.Success)
                    {
                        foreach (var field in fieldMatch.Groups["header"].Value.Split(' ').Select(x => x.ToUpperInvariant()))
                        {
                            var headers = message.Headers;
                            if (headers != null && headers.ContainsKey(field))
                            {
                                var headerLine = headers[field].Item2;
                                if (!string.IsNullOrWhiteSpace(headerLine))
                                    sb2.AppendLine(headerLine);
                            }
                        }

                        sb2.AppendLine();
                        sb.AppendFormat(" BODY[HEADER.FIELDS ({0})] {{{1}}}\r\n{2}", fieldMatch.Groups["header"].Value.ToUpperInvariant(), sb2.Length, sb2);
                    }
                }

                sb.Append(")");

                await this.Send(sb.ToString());
            });

            await this.Send("{0} OK UID FETCH completed", tag);
            return new CommandProcessingResult(true);
        }
        #endregion

        private string GetFlags([NotNull] IIdentity identity, [NotNull] string catalogName, [NotNull] IStoreProvider store)
        {
            var flags = new StringBuilder();

            if (string.Compare(catalogName, "All Mail", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\All ");

            if (string.Compare(catalogName, "Drafts", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\Drafts ");

            if (string.Compare(catalogName, "Important", StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(catalogName, "Starred", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\Flagged ");

            if (string.Compare(catalogName, "Junk", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(catalogName, "Junk Mail", StringComparison.OrdinalIgnoreCase) == 0 || 
                string.Compare(catalogName, "Spam", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\Junk ");

            if (string.Compare(catalogName, "Sent", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(catalogName, "Sent Items", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(catalogName, "Sent Mail", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(catalogName, "Outbox", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\Sent ");

            if (string.Compare(catalogName, "Trash", StringComparison.OrdinalIgnoreCase) == 0)
                flags.Append(@"\Trash ");

            var subs = store.GetGlobalCatalogs(identity, catalogName);
            if (subs != null && subs.Any())
                flags.Append(@"\HasChildren ");
            else
            {
                subs = store.GetPersonalCatalogs(identity, catalogName);
                if (subs != null && subs.Any())
                    flags.Append(@"\HasChildren ");
                else
                    flags.Append(@"\HasNoChildren ");
            }

            return flags.ToString().TrimEnd();
        }
    }
}
