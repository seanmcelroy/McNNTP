namespace McNNTP.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using McNNTP.Common;
    using McNNTP.Core.Server.Configuration;

    public class NntpServer
    {
        private readonly List<Tuple<Thread, NntpListener>> _listeners = new List<Tuple<Thread, NntpListener>>();

        private static readonly ILog _logger = LogManager.GetLogger(typeof(NntpServer));

        private readonly List<Connection> _connections = new List<Connection>();

        internal X509Certificate2 _serverAuthenticationCertificate;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="NntpServer"/> class.
        /// </summary>
        public NntpServer()
        {
            AllowStartTLS = true;
            ShowData = true;
        }

        public bool AllowPosting { get; set; }

        public bool AllowStartTLS { get; set; }

        [NotNull]
        public int[] NntpClearPorts { get; set; }

        [NotNull]
        public int[] NntpExplicitTLSPorts { get; set; }

        [NotNull]
        public int[] NntpImplicitTLSPorts { get; set; }

        [CanBeNull]
        public LdapDirectoryConfigurationElement LdapDirectoryConfiguration { get; set; }

        [NotNull]
        public string PathHost { get; set; }

        public bool SslGenerateSelfSignedServerCertificate { get; set; }

        [CanBeNull]
        public string SslServerCertificateThumbprint { get; set; }

        [NotNull]
        public IReadOnlyList<ConnectionMetadata> Connections
        {
            get
            {
                return _connections.Select(c => new ConnectionMetadata
                {
                    AuthenticatedUsername = c.Identity == null ? null : c.Identity.Username,
                    RemoteAddress = c.RemoteAddress,
                    RemotePort = c.RemotePort
                })
                .ToList()
                .AsReadOnly();
            }
        }

        public bool ShowBytes { get; set; }

        public bool ShowCommands { get; set; }

        public bool ShowData { get; set; }

        #region Connection and IO
        /// <summary>
        /// Starts listener threads to begin processing requests
        /// </summary>
        /// <exception cref="CryptographicException">Thrown when an error occurs while a SSL certificate is loaded to support TLS-enabled ports</exception>
        public void Start()
        {
            _listeners.Clear();

            // Test LDAP connection, if configured
            if (LdapDirectoryConfiguration != null)
            {
                _logger.InfoFormat("Testing LDAP connection to {0} with lookup account {1}", LdapDirectoryConfiguration.LdapServer, LdapDirectoryConfiguration.LookupAccountUsername);

                if (LdapUtility.UserExists(
                    LdapDirectoryConfiguration.LdapServer,
                    LdapDirectoryConfiguration.SearchPath,
                    LdapDirectoryConfiguration.LookupAccountUsername,
                    LdapDirectoryConfiguration.LookupAccountPassword,
                    LdapDirectoryConfiguration.LookupAccountUsername))
                    _logger.Info("LDAP lookup account successfully found.");
                else
                {
                    _logger.Warn("Unable to find LDAP lookup account.  LDAP authentication is being disabled.");
                    LdapDirectoryConfiguration = null;
                }
            }

            // Setup SSL
            if (!string.IsNullOrWhiteSpace(SslServerCertificateThumbprint) && SslServerCertificateThumbprint != null)
            {
                var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.OpenExistingOnly);
                try
                {
                    var collection = store.Certificates.Find(X509FindType.FindByThumbprint, SslServerCertificateThumbprint, true);
                    if (collection.Cast<X509Certificate2>().Count(c => c.HasPrivateKey) == 0)
                    {
                        _logger.WarnFormat(@"No valid certificate with a public and private key could be found in the LocalMachine\Personal store with thumbprint: {0}.  Disabling SSL.", SslServerCertificateThumbprint);
                        AllowStartTLS = false;
                        this.NntpExplicitTLSPorts = new int[0];
                        this.NntpImplicitTLSPorts = new int[0];
                    }
                    else
                    {
                        _logger.InfoFormat("Located valid certificate with subject '{0}' and serial {1}", collection[0].Subject, collection[0].SerialNumber);
                        _serverAuthenticationCertificate = collection[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }
            else if (SslGenerateSelfSignedServerCertificate || this.NntpExplicitTLSPorts.Any() || this.NntpImplicitTLSPorts.Any())
            {
                var pfx = CertificateUtility.CreateSelfSignCertificatePfx("CN=freenews", DateTime.Now, DateTime.Now.AddYears(100), "password");
                _serverAuthenticationCertificate = new X509Certificate2(pfx, "password");
            }

            foreach (var clearPort in this.NntpClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(this, localEndPoint)
                {
                    PortType = PortClass.ClearText
                };

                _listeners.Add(new Tuple<Thread, NntpListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var implicitTlsPort in this.NntpImplicitTLSPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, implicitTlsPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(this, localEndPoint)
                {
                    PortType = PortClass.ImplicitTLS
                };

                _listeners.Add(new Tuple<Thread, NntpListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var listener in _listeners)
            {
                try
                {
                    listener.Item1.Start();
                    _logger.InfoFormat("Listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (OutOfMemoryException oom)
                {
                    _logger.Error("Unable to start listener thread.  Not enough memory.", oom);
                }
            }
        }

        public void Stop()
        {
            foreach (var listener in _listeners)
            {
                listener.Item2.Stop();
                _logger.InfoFormat("Stopped listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
            }

            Task.WaitAll(_connections.Select(connection => connection.Shutdown()).ToArray());

            foreach (var thread in _listeners)
            {
                try
                {
                    thread.Item1.Abort();
                }
                catch (SecurityException se)
                {
                    _logger.Error(
                        "Unable to abort the thread due to a security exception.  Application will now exit.",
                        se);
                    Environment.Exit(se.HResult);
                }
                catch (ThreadStateException tse)
                {
                    _logger.Error(
                        "Unable to abort the thread due to a thread state exception.  Application will now exit.",
                        tse);
                    Environment.Exit(tse.HResult);
                }
            }
        }

        internal void AddConnection([NotNull] Connection connection)
        {
            _connections.Add(connection);
            _logger.VerboseFormat("Connection from {0}:{1} to {2}:{3}", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort);
        }

        internal void RemoveConnection([NotNull] Connection connection)
        {
            _connections.Remove(connection);
            if (connection.Identity == null)
                _logger.VerboseFormat("Disconnection from {0}:{1}", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort);
            else
                _logger.VerboseFormat("Disconnection from {0}:{1} ({2})", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort, connection.Identity.Username);
        }

        #endregion
    }
}