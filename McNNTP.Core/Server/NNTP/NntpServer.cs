// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NntpServer.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   Defines the an NNTP server utility that provides connection management and command handling to expose
//   a fully functioning USENET news server.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.NNTP
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using McNNTP.Common;

    /// <summary>
    /// Defines the an NNTP server utility that provides connection management and command handling to expose
    /// a fully functioning USENET news server.
    /// </summary>
    public class NntpServer
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class.
        /// </summary>
        private readonly ILogger<NntpServer> _logger;

        /// <summary>
        /// The logger factory to create loggers for components.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// A list of threads and the associated TCP new-connection listeners that are serviced by each by the client.
        /// </summary>
        private readonly List<Tuple<Thread, NntpListener>> listeners = [];

        /// <summary>
        /// A list of connections currently established to this server instance.
        /// </summary>
        private readonly List<NntpConnection> connections = new List<NntpConnection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NntpServer"/> class.
        /// </summary>
        public NntpServer([NotNull] ILogger<NntpServer> logger, [NotNull] ILoggerFactory loggerFactory)
        {
            this._logger = logger;
            this._loggerFactory = loggerFactory;
            this.AllowStartTLS = true;
            this.ShowData = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to allow users to post to the server.
        /// When this value is set to false, the server is read-only for regular client connections.
        /// </summary>
        public bool AllowPosting { get; set; }

        public bool AllowStartTLS { get; set; }

        [NotNull]
        public int[] NntpClearPorts { get; set; }

        [NotNull]
        public int[] NntpExplicitTLSPorts { get; set; }

        [NotNull]
        public int[] NntpImplicitTLSPorts { get; set; }

        [NotNull]
        public string PathHost { get; set; }

        public bool SslGenerateSelfSignedServerCertificate { get; set; }

        /// <summary>
        /// Gets or sets the thumbprint of the X.509 certificate to lookup for presentation to clients requesting
        /// secure access over transport layer security (TLS)
        /// </summary>
        public string? SslServerCertificateThumbprint { get; set; }

        [NotNull]
        public IReadOnlyList<ConnectionMetadata> Connections
        {
            get
            {
                return this.connections.Select(c => new ConnectionMetadata
                {
                    AuthenticatedUsername = c.Identity == null ? null : c.Identity.Username,
                    RemoteAddress = c.RemoteAddress,
                    RemotePort = c.RemotePort,
                })
                .ToList()
                .AsReadOnly();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the byte transmitted counts are logged to the logging instance
        /// </summary>
        public bool ShowBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the commands transmitted are logged to the logging instance
        /// </summary>
        public bool ShowCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the actual bytes (data) transmitted are logged to the logging instance.
        /// </summary>
        public bool ShowData { get; set; }

        /// <summary>
        /// Gets the X.509 server certificate this instance presents to clients
        /// attempting to connect via TLS.
        /// </summary>
        internal X509Certificate2? ServerAuthenticationCertificate { get; private set; }

        #region Connection and IO

        /// <summary>
        /// Starts listener threads to begin processing requests.
        /// </summary>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when an error occurs while a SSL certificate is loaded to support TLS-enabled ports.</exception>
        /// <exception cref="SecurityException">Thrown when the certificate store cannot be successfully opened to look up a SSL certificate by its thumbprint.</exception>
        public void Start()
        {
            this.listeners.Clear();

            // Setup SSL
            if (!string.IsNullOrWhiteSpace(this.SslServerCertificateThumbprint) && this.SslServerCertificateThumbprint != null)
            {
                var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.OpenExistingOnly);
                try
                {
                    var collection = store.Certificates.Find(X509FindType.FindByThumbprint, this.SslServerCertificateThumbprint, true);
                    if (collection.Cast<X509Certificate2>().Count(c => c.HasPrivateKey) == 0)
                    {
                        _logger.LogWarning(@"No valid certificate with a public and private key could be found in the LocalMachine\Personal store with thumbprint: {0}.  Disabling SSL.", this.SslServerCertificateThumbprint);
                        this.AllowStartTLS = false;
                        this.NntpExplicitTLSPorts = [];
                        this.NntpImplicitTLSPorts = [];
                    }
                    else
                    {
                        _logger.LogInformation("Located valid certificate with subject '{0}' and serial {1}", collection[0].Subject, collection[0].SerialNumber);
                        this.ServerAuthenticationCertificate = collection[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }
            else if (this.SslGenerateSelfSignedServerCertificate || this.NntpExplicitTLSPorts.Any() || this.NntpImplicitTLSPorts.Any())
            {
                // Create cross-platform self-signed certificate using modern .NET APIs
                this.ServerAuthenticationCertificate = CreateSelfSignedCertificate("freenews");
            }

            foreach (var clearPort in this.NntpClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(this, localEndPoint, _loggerFactory.CreateLogger<NntpListener>(), _loggerFactory)
                {
                    PortType = PortClass.ClearText,
                };

                this.listeners.Add(new Tuple<Thread, NntpListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var implicitTlsPort in this.NntpImplicitTLSPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, implicitTlsPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(this, localEndPoint, _loggerFactory.CreateLogger<NntpListener>(), _loggerFactory)
                {
                    PortType = PortClass.ImplicitTLS,
                };

                this.listeners.Add(new Tuple<Thread, NntpListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var listener in this.listeners)
            {
                try
                {
                    listener.Item1.Start();
                    _logger.LogInformation("Listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (OutOfMemoryException oom)
                {
                    _logger.LogError(oom, "Unable to start listener thread.  Not enough memory.");
                }
            }
        }

        public void Stop()
        {
            foreach (var listener in this.listeners)
            {
                try
                {
                    listener.Item2.Stop();
                    _logger.LogInformation("Stopped listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (SocketException ex)
                {
                    _logger.LogError(ex, "Exception attempting to stop listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
            }

            Task.WaitAll(this.connections.Select(connection => connection.Shutdown()).ToArray());

            foreach (var thread in this.listeners)
            {
                try
                {
                    thread.Item1.Abort();
                }
                catch (SecurityException se)
                {
                    _logger.LogError(
                        se,
                        "Unable to abort the thread due to a security exception.  Application will now exit.");
                    Environment.Exit(se.HResult);
                }
                catch (ThreadStateException tse)
                {
                    _logger.LogError(
                        tse,
                        "Unable to abort the thread due to a thread state exception.  Application will now exit.");
                    Environment.Exit(tse.HResult);
                }
            }
        }

        internal void AddConnection([NotNull] NntpConnection nntpConnection)
        {
            this.connections.Add(nntpConnection);
            _logger.VerboseFormat("Connection from {0}:{1} to {2}:{3}", nntpConnection.RemoteAddress, nntpConnection.RemotePort, nntpConnection.LocalAddress, nntpConnection.LocalPort);
        }

        internal void RemoveConnection([NotNull] NntpConnection nntpConnection)
        {
            this.connections.Remove(nntpConnection);
            if (nntpConnection.Identity == null)
            {
                _logger.VerboseFormat("Disconnection from {0}:{1}", nntpConnection.RemoteAddress, nntpConnection.RemotePort, nntpConnection.LocalAddress, nntpConnection.LocalPort);
            }
            else
            {
                _logger.VerboseFormat("Disconnection from {0}:{1} ({2})", nntpConnection.RemoteAddress, nntpConnection.RemotePort, nntpConnection.LocalAddress, nntpConnection.LocalPort, nntpConnection.Identity.Username);
            }
        }

        /// <summary>
        /// Creates a self-signed certificate using modern cross-platform .NET APIs
        /// </summary>
        /// <param name="commonName">The common name for the certificate</param>
        /// <returns>A new self-signed X509Certificate2</returns>
        private static X509Certificate2 CreateSelfSignedCertificate(string commonName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            // Add basic constraints
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            
            // Add key usage
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            
            // Add enhanced key usage for server authentication
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], false)); // Server Authentication OID

            // Create the certificate with a 100-year validity period
            var certificate = request.CreateSelfSigned(
                DateTimeOffset.Now.AddDays(-1), 
                DateTimeOffset.Now.AddYears(100));

            // Export and reimport to ensure the certificate has a private key
            var pfxData = certificate.Export(X509ContentType.Pfx, "");
            return new X509Certificate2(pfxData, "", X509KeyStorageFlags.Exportable);
        }
        #endregion
    }
}