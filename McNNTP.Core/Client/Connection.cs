// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NntpConnection.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A connection from a server to a client
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Client
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using log4net;
    using McNNTP.Common;

    /// <summary>
    /// A connection from a server to a client.
    /// </summary>
    internal class Connection
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class.
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Connection));

        /// <summary>
        /// The <see cref="TcpClient"/> that created this connection.
        /// </summary>
        [NotNull]
        private readonly TcpClient client;

        /// <summary>
        /// The <see cref="Stream"/> instance retrieved from the <see cref="TcpClient"/> that created this connection.
        /// </summary>
        [NotNull]
        private readonly Stream stream;

        /// <summary>
        /// The NNTP reader used to retrieve responses from the server.
        /// </summary>
        [NotNull]
        private readonly NntpStreamReader reader;

        /// <summary>
        /// The remote IP address to which the connection is established.
        /// </summary>
        [NotNull]
        private readonly IPAddress remoteAddress;

        /// <summary>
        /// The local IP address to which the connection is established.
        /// </summary>
        [NotNull]
        private readonly IPAddress localAddress;

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="client">The <see cref="TcpClient"/> that created this connection.</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/>.</param>
        public Connection(
            [NotNull] TcpClient client,
            [NotNull] Stream stream)
        {
            this.client = client;
            this.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.stream = stream;
            this.reader = new NntpStreamReader(stream);

            var remoteIpEndpoint = (IPEndPoint?)this.client.Client.RemoteEndPoint;
            this.remoteAddress = remoteIpEndpoint?.Address;
            this.RemotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint?)this.client.Client.LocalEndPoint;
            this.localAddress = localIpEndpoint?.Address;
            this.LocalPort = localIpEndpoint.Port;
        }

        /// <summary>
        /// Gets a value indicating whether the user can POST messages to the server.
        /// </summary>
        public bool CanPost { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the byte transmitted counts are logged to the logging instance.
        /// </summary>
        public bool ShowBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the commands transmitted are logged to the logging instance.
        /// </summary>
        public bool ShowCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the actual bytes (data) transmitted are logged to the logging instance.
        /// </summary>
        public bool ShowData { get; set; }

        /// <summary>
        /// Gets a value indicating whether the connection is secured by Transport Layer Security.
        /// </summary>
        public bool TLS => this.stream is SslStream;

        #region Compression
        /// <summary>
        /// Gets a value indicating whether the connection should have compression enabled.
        /// </summary>
        public bool Compression { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the connection is compressed with the UNIX GZip protocol.
        /// </summary>
        public bool CompressionGZip { get; private set; }

        /// <summary>
        /// Gets a value indicating whether message terminators are also compressed.
        /// </summary>
        public bool CompressionTerminator { get; private set; }
        #endregion

        #region Derived instance properties
        /// <summary>
        /// Gets the remote IP address to which the connection is established.
        /// </summary>
        [NotNull]
        public IPAddress RemoteAddress
        {
            get { return this.remoteAddress; }
        }

        /// <summary>
        /// Gets the remote TCP port number for the remote endpoint to which the connection is established.
        /// </summary>
        public int RemotePort { get; }

        /// <summary>
        /// Gets the local IP address to which the connection is established.
        /// </summary>
        [NotNull]
        public IPAddress LocalAddress
        {
            get { return this.localAddress; }
        }

        /// <summary>
        /// Gets the local TCP port number for the local endpoint to which the connection is established.
        /// </summary>
        public int LocalPort { get; }
        #endregion

        #region IO and Connection Management

        /// <summary>
        /// Receive the next response from the server.
        /// </summary>
        /// <param name="multiLine">A value indicating whether a multi-line response is expected.</param>
        /// <returns>A <see cref="NntpResponse"/> that wraps the return code, message, and if a multi-line response, the lines included in the response.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the message comes back incomplete or malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an attempt is made to receive a response from the server, but the connection is not connected to a server.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when an attempt to read data from the connection is made when the underlying stream is already disposed or closed.</exception>
        internal async Task<NntpResponse> Receive(bool multiLine = false)
        {
            if (!this.client.Connected)
            {
                throw new InvalidOperationException("The connection is not currently connected to a server");
            }

            var line = await this.reader.ReadLineAsync();

            if (line == null)
            {
                throw new NntpException("Did not receive response from server.");
            }

            int code;
            if (line.Length < 5 || !int.TryParse(line.Substring(0, 3), out code))
            {
                throw new NntpException("Received invalid response from server.");
            }

            var message = line[4..];

            return multiLine
                ? new NntpMultilineResponse(code, message, this.reader.ReadAllLines())
                : new NntpResponse(code, message);
        }

        /// <summary>
        /// Receive the next multi-line response from the server.
        /// </summary>
        /// <returns>A <see cref="NntpMultilineResponse"/> that wraps the return code, message, and lines included in this multi-line response.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the message comes back incomplete or malformed.</exception>
        /// <exception cref="NntpException">Thrown when the message comes back incomplete or malformed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an attempt is made to receive a response from the server, but the connection is not connected to a server.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when an attempt to read data from the connection is made when the underlying stream is already disposed or closed.</exception>
        internal async Task<NntpMultilineResponse> ReceiveMultiline()
        {
            return (NntpMultilineResponse)(await this.Receive(true));
        }

        /// <summary>
        /// Sends the formatted data to the client.
        /// </summary>
        /// <param name="format">The data, or format string for data, to send to the client.</param>
        /// <param name="args">The argument applied as a format string to <paramref name="format"/> to create the data to send to the client.</param>
        /// <returns>A value indicating whether or not the transmission was successful.</returns>
        internal Task<bool> Send([NotNull][StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, [NotNull] params object[] args)
        {
            return this.SendInternal(string.Format(CultureInfo.InvariantCulture, format, args), false, CancellationToken.None);
        }

        /// <summary>
        /// Sends the formatted data to the client.
        /// </summary>
        /// <param name="format">The data, or format string for data, to send to the client.</param>
        /// <param name="args">The argument applied as a format string to <paramref name="format"/> to create the data to send to the client.</param>
        /// <returns>A value indicating whether or not the transmission was successful.</returns>
        private Task<bool> SendCompressed([NotNull][StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, [NotNull] params object[] args)
        {
            return this.SendInternal(string.Format(CultureInfo.InvariantCulture, format, args), true, CancellationToken.None);
        }

        private async Task<bool> SendInternal([NotNull] string data, bool compressedIfPossible, CancellationToken cancellationToken)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData;
            if (compressedIfPossible && this.Compression && this.CompressionGZip && this.CompressionTerminator)
            {
                byteData = await data.ZlibDeflate(cancellationToken);
            }
            else
            {
                byteData = Encoding.UTF8.GetBytes(data);
            }

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData);
                if (this.ShowBytes && this.ShowData)
                {
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                }
                else if (this.ShowBytes)
                {
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        byteData.Length);
                }
                else if (this.ShowData)
                {
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4}",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.TLS ? "!" : "<",
                        compressedIfPossible && this.CompressionGZip ? "G" : "<",
                        data.TrimEnd('\r', '\n'));
                }

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

        public async Task Shutdown()
        {
            if (this.client.Connected)
            {
                await this.Send("205 closing connection\r\n");
                this.client.Client.Shutdown(SocketShutdown.Both);
                this.client.Close();
            }

            // this.server.RemoveConnection(this);
        }
        #endregion
    }
}
