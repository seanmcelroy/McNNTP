namespace McNNTP.Core.Server.NNTP
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
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

        public async Task StartAccepting(CancellationToken cancellationToken = default)
        {
            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                this.Start(100);

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Start an asynchronous socket to listen for connections.
                    var client = await this.AcceptTcpClientAsync(cancellationToken);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Listener gracefully stopped due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to accept connection from listener");
            }
            finally
            {
                this.Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            EndPoint? remote;
            try
            {
                remote = client.Client.RemoteEndPoint;
            }
            catch (Exception)
            {
                // Socket COULD be closed between function call and read of RemoteEndPoint.
                remote = null;
            }

            try
            {
                // Create the state object.
                NntpConnection nntpConnection;
                var stream = client.GetStream();

                if (this.PortType == PortClass.ClearText || this.PortType == PortClass.ExplicitTLS)
                {
                    nntpConnection = new NntpConnection(this.server, client, stream, _loggerFactory.CreateLogger<NntpConnection>());
                }
                else // ImplicitTLS
                {
                    var sslStream = new SslStream(stream);
                    using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    handshakeCts.CancelAfter(TimeSpan.FromSeconds(30));

                    try
                    {
                        await sslStream.AuthenticateAsServerAsync(
                            new SslServerAuthenticationOptions { ServerCertificate = this.server.ServerAuthenticationCertificate },
                            handshakeCts.Token);
                    }
                    catch (Exception ex) when (ex is IOException or AuthenticationException or OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "TLS handshake failed or timed out for {Remote}", remote);
                        client.Dispose();
                        return;
                    }

                    nntpConnection = new NntpConnection(this.server, client, sslStream, _loggerFactory.CreateLogger<NntpConnection>(), true);
                }

                this.server.AddConnection(nntpConnection);
                await nntpConnection.Process();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection processing faulted for {Remote}", remote);
                client.Dispose();
            }
        }

    }
}
