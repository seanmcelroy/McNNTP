namespace McNNTP.Core.Server.NNTP
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using Microsoft.Extensions.Logging;

    internal class NntpListener : TcpListener
    {
        // Thread signal.
        private readonly NntpServer server;
        private readonly ILogger<NntpListener> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public NntpListener([NotNull] NntpServer server, [NotNull] IPEndPoint localEp, [NotNull] ILogger<NntpListener> logger, [NotNull] ILoggerFactory loggerFactory)
            : base(localEp)
        {
            this.server = server;
            this._logger = logger;
            this._loggerFactory = loggerFactory;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)this.LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new NntpListener(this.server, localEndPoint, _logger, _loggerFactory);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();

                    // Create the state object.
                    NntpConnection nntpConnection;

                    if (this.PortType == PortClass.ClearText || this.PortType == PortClass.ExplicitTLS)
                    {
                        var stream = handler.GetStream();

                        nntpConnection = new NntpConnection(this.server, handler, stream, _loggerFactory.CreateLogger<NntpConnection>());
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(this.server.ServerAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _logger.LogError(ioe, "I/O Exception attempting to perform TLS handshake");
                            return;
                        }

                        nntpConnection = new NntpConnection(this.server, handler, sslStream, _loggerFactory.CreateLogger<NntpConnection>(), true);
                    }

                    this.server.AddConnection(nntpConnection);

                    nntpConnection.Process();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to accept connection from listener");
            }
        }
    }
}
