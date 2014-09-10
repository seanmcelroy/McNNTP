namespace McNNTP.Core.Server
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;

    using JetBrains.Annotations;

    using log4net;

    using McNNTP.Common;
    using McNNTP.Core.Database;

    internal class ImapListener : TcpListener
    {
        // Thread signal.
        private static readonly IStoreProvider _store = new SqliteStoreProvider(); // TODO: Make loaded by configuration
        private readonly ImapServer _server;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(ImapListener));

        public ImapListener([NotNull] ImapServer server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            _server = server;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new ImapListener(_server, localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();

                    // Create the state object.
                    ImapConnection ImapConnection;

                    if (PortType == PortClass.ClearText ||
                        PortType == PortClass.ExplicitTLS)
                    {
                        var stream = handler.GetStream();

                        ImapConnection = new ImapConnection(_store, _server, handler, stream);
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(_server.ServerAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        ImapConnection = new ImapConnection(_store, _server, handler, sslStream, true);
                    }

                    _server.AddConnection(ImapConnection);

                    ImapConnection.Process();
                }

            }
            catch (Exception ex)
            {
                _logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}
