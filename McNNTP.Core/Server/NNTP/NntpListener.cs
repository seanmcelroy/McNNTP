namespace McNNTP.Core.Server.NNTP
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using log4net;

    internal class NntpListener : TcpListener
    {
        // Thread signal.
        private readonly NntpServer server;
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(NntpListener));

        public NntpListener([NotNull] NntpServer server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            this.server = server;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)this.LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new NntpListener(this.server, localEndPoint);

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

                        nntpConnection = new NntpConnection(this.server, handler, stream);
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
                            _Logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        nntpConnection = new NntpConnection(this.server, handler, sslStream, true);
                    }

                    this.server.AddConnection(nntpConnection);

                    nntpConnection.Process();
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}
