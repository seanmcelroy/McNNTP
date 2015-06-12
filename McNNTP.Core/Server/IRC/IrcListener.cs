namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;

    using JetBrains.Annotations;

    using log4net;

    using Common;
    using Database;

    internal class IrcListener : TcpListener
    {
        // Thread signal.
        private static readonly IStoreProvider _store = new SqliteStoreProvider(); // TODO: Make loaded by configuration
        private readonly IrcServer _server;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(IrcListener));

        public IrcListener([NotNull] IrcServer server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            this._server = server;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint) this.LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new IrcListener(this._server, localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();

                    // Create the state object.
                    IrcConnection ircConnection;

                    if (this.PortType == PortClass.ClearText || this.PortType == PortClass.ExplicitTLS)
                    {
                        var stream = handler.GetStream();

                        ircConnection = new IrcConnection(_store, this._server, handler, stream);
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(this._server.ServerAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        ircConnection = new IrcConnection(_store, this._server, handler, sslStream, true);
                    }

                    this._server.AddConnection(ircConnection);

                    ircConnection.Process();
                }

            }
            catch (Exception ex)
            {
                _logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}
