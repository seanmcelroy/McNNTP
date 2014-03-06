using log4net;
using McNNTP.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace McNNTP.Core.Server
{
    public class NntpServer
    {
        private readonly List<Tuple<Thread, NntpListener>> _listeners = new List<Tuple<Thread, NntpListener>>();
        private static readonly ILog _logger = LogManager.GetLogger(typeof(Connection));
        private readonly List<Connection> _connections = new List<Connection>();

        internal X509Certificate2 _serverAuthenticationCertificate;

        public bool AllowPosting { get; set; }
        public bool AllowStartTLS { get; set; }
        public int[] ClearPorts { get; set; }
        public int[] ExplicitTLSPorts { get; set; }
        public int[] ImplicitTLSPorts { get; set; }
        public string PathHost { get; set; }
        public bool SslGenerateSelfSignedServerCertificate { get; set; }
        public string SslServerCertificateThumbprint { get; set; }

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

        public NntpServer()
        {
            AllowStartTLS = true;
            ShowData = true;
        }

        #region Connection and IO
        public void Start()
        {
            _listeners.Clear();
            
            // Setup SSL
            if (!string.IsNullOrWhiteSpace(SslServerCertificateThumbprint))
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
                        ExplicitTLSPorts = new int[0];
                        ImplicitTLSPorts = new int[0];
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
            else if (SslGenerateSelfSignedServerCertificate || (ExplicitTLSPorts != null && ExplicitTLSPorts.Any()) || (ImplicitTLSPorts != null && ImplicitTLSPorts.Any()))
            {
                byte[] pfx = CertificateUtility.CreateSelfSignCertificatePfx("CN=freenews", DateTime.Now, DateTime.Now.AddYears(100), "password");
                _serverAuthenticationCertificate = new X509Certificate2(pfx, "password");
            }

            foreach (var clearPort in ClearPorts)
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

            foreach (var implicitTlsPort in ImplicitTLSPorts)
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
                listener.Item1.Start();
                _logger.InfoFormat("Listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
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
                thread.Item1.Abort();
        }

        internal void AddConnection(Connection connection)
        {
            _connections.Add(connection);
            _logger.VerboseFormat("Connection from {0}:{1} to {2}:{3}", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort);
        }

        internal void RemoveConnection(Connection connection)
        {
            _connections.Remove(connection);
            if (connection.Identity == null)
                _logger.VerboseFormat("Disconnection from {0}:{1}", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort);
            else
                _logger.VerboseFormat("Disconnection from {0}:{1} ({2})", connection.RemoteAddress, connection.RemotePort, connection.LocalAddress, connection.LocalPort, connection.Identity.Username);
        }

        #endregion
        
        #region Interactivity
        public bool ShowBytes { get; set; }
        public bool ShowCommands { get; set; }
        public bool ShowData { get; set; }

        #endregion
    }
}