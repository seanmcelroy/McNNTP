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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using log4net;
    using Common;

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
        private static readonly Dictionary<string, Func<IrcConnection, string, string, Task<CommandProcessingResult>>> CommandDirectory;

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(IrcConnection));

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
        /// For commands that handle conversational request-replies, this is a reference to the
        /// command that should handle new input received by the main process loop.
        /// </summary>
        [CanBeNull]
        private CommandProcessingResult inProcessCommand;

        /// <summary>
        /// Initializes static members of the <see cref="IrcConnection"/> class.
        /// </summary>
        static IrcConnection()
        {
            //CommandDirectory = new Dictionary<string, Func<IrcConnection, string, string, Task<CommandProcessingResult>>>
            //                   {
            //                       { "NICK", async (c, tag, command) => await c.Nick(command) },
            //                   };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcConnection"/> class.
        /// </summary>
        /// <param name="store">The catalog and message store to use to back operations for this connection</param>
        /// <param name="server">The server instance that owns this connection</param>
        /// <param name="client">The <see cref="TcpClient"/> that accepted this connection</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/></param>
        /// <param name="tls">Whether or not the connection has implicit Transport Layer Security</param>
        public IrcConnection(
            [NotNull] IStoreProvider store,
            [NotNull] IrcServer server,
            [NotNull] TcpClient client,
            [NotNull] Stream stream,
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
            this.TLS = tls;

            var remoteIpEndpoint = (IPEndPoint)this.client.Client.RemoteEndPoint;
            this.remoteAddress = remoteIpEndpoint.Address;
            this.remotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this.client.Client.LocalEndPoint;
            this.localAddress = localIpEndpoint.Address;
            this.localPort = localIpEndpoint.Port;
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
                return await this.SendInternal(format + "\r\n");

            return await this.SendInternal(string.Format(CultureInfo.InvariantCulture, format, args) + "\r\n");
        }

        private async Task<bool> SendInternal([NotNull] string data)
        {
            // Convert the string data to byte data using ASCII encoding.
            var byteData = Encoding.UTF8.GetBytes(data);

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData, 0, byteData.Length);
                if (this.ShowBytes && this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                else if (this.ShowBytes)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
                        byteData.Length);
                else if (this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4}", this.RemoteAddress, this.RemotePort, this.TLS ? "!" : "<",
                        "<",
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
        }
        #endregion

    }
}
