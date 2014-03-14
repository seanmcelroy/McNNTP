using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using JetBrains.Annotations;
using log4net;

namespace McNNTP.Core.Server
{
    internal class NntpListener : TcpListener
    {
        // Thread signal.
        private readonly NntpServer _server;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(NntpListener));

        public NntpListener([NotNull] NntpServer server, [NotNull] IPEndPoint localEp)
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
            var listener = new NntpListener(_server, localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();

                    // Create the state object.
                    Connection connection;

                    if (PortType == PortClass.ClearText ||
                        PortType == PortClass.ExplicitTLS)
                    {
                        var stream = handler.GetStream();

                        connection = new Connection(_server, handler, stream);
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(_server._serverAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        connection = new Connection(_server, handler, sslStream, true);
                    }

                    _server.AddConnection(connection);

                    connection.Process();
                }

            }
            catch (Exception ex)
            {
                _logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}
