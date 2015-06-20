// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IrcServer.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   Defines the an NNTP server utility that provides connection management and command handling to expose
//   a fully functioning USENET news server.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Permissions;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Common;
    using Configuration;

    /// <summary>
    /// Defines the an IRC server utility that provides connection management and command handling to expose
    /// a fully functioning Internet Relay Chat server over the IRC protocol.
    /// </summary>
    public class IrcServer
    {
        private static readonly Regex _ValidateNickname = new Regex(Message.RegexNickname, RegexOptions.Compiled);

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(IrcServer));

        /// <summary>
        /// A list of threads and the associated TCP new-connection listeners that are serviced by each by the client
        /// </summary>
        private readonly List<Tuple<Thread, IrcListener>> listeners = new List<Tuple<Thread, IrcListener>>();

        /// <summary>
        /// A list of connections currently established to this server instance
        /// </summary>
        private readonly List<IrcConnection> connections = new List<IrcConnection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServer"/> class.
        /// </summary>
        public IrcServer()
        {
            this.ShowData = true;
        }

        [NotNull]
        public int[] IrcClearPorts { get; set; }

        [NotNull]
        public int[] IrcImplicitTLSPorts { get; set; }

        [CanBeNull]
        public LdapDirectoryConfigurationElement LdapDirectoryConfiguration { get; set; }
        
        public bool SslGenerateSelfSignedServerCertificate { get; set; }

        /// <summary>
        /// Gets or sets the thumbprint of the X.509 certificate to lookup for presentation to clients requesting
        /// secure access over transport layer security (TLS)
        /// </summary>
        [CanBeNull]
        public string SslServerCertificateThumbprint { get; set; }

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
                    Established = c.Established,
                    ListenAddress = c.ListenAddress,
                    ListenPort = c.ListenPort,
                    RecvMessageBytes = c.RecvMessageBytes,
                    RecvMessageCount = c.RecvMessageCount,
                    SentMessageBytes = c.SentMessageBytes,
                    SentMessageCount = c.SentMessageCount
                })
                .ToList()
                .AsReadOnly();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the byte transmitted counts are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the commands transmitted are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the actual bytes (data) transmitted are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowData { get; set; }

        /// <summary>
        /// Gets the X.509 server certificate this instance presents to clients
        /// attempting to connect via TLS.
        /// </summary>
        [CanBeNull]
        internal X509Certificate2 ServerAuthenticationCertificate { get; private set; }

        /// <summary>
        /// All channels known
        /// </summary>
        internal readonly ConcurrentBag<Channel> Channels = new ConcurrentBag<Channel>();

        /// <summary>
        /// All servers (peers) known across all local and remote servers
        /// </summary>
        /// <remarks>This does not include the 'Self', or running instance</remarks>
        internal readonly List<Server> Servers = new List<Server>();

        /// <summary>
        /// All services known across all local and remote servers
        /// </summary>
        internal readonly List<Service> Services = new List<Service>();

        /// <summary>
        /// All users known across all local and remote servers
        /// </summary>
        internal readonly List<User> Users = new List<User>();

        /// <summary>
        /// Nicknames that are reserved for use, with their reservation expiration date.
        /// </summary>
        internal readonly ConcurrentDictionary<string, DateTime> ReservedNicknames = new ConcurrentDictionary<string, DateTime>(new ScandanavianStringComparison());

        // TODO: Move to configuration
        internal readonly Server Self = new Server("freenews")
        {
            HopCount = 0,
            Version = "0210",
            Flags = "UNSET"
        };

        #region Connection and IO
        /// <summary>
        /// Starts listener threads to begin processing requests
        /// </summary>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when an error occurs while a SSL certificate is loaded to support TLS-enabled ports</exception>
        /// <exception cref="SecurityException">Thrown when the certificate store cannot be successfully opened to look up a SSL certificate by its thumbprint</exception>
        [StorePermission(SecurityAction.Demand, EnumerateCertificates = true, OpenStore = true)]
        public void Start()
        {
            this.listeners.Clear();

            // Test LDAP connection, if configured
            if (this.LdapDirectoryConfiguration != null)
            {
                Logger.InfoFormat("Testing LDAP connection to {0} with lookup account {1}", this.LdapDirectoryConfiguration.LdapServer, this.LdapDirectoryConfiguration.LookupAccountUsername);

                if (LdapUtility.UserExists(this.LdapDirectoryConfiguration.LdapServer, this.LdapDirectoryConfiguration.SearchPath, this.LdapDirectoryConfiguration.LookupAccountUsername, this.LdapDirectoryConfiguration.LookupAccountPassword, this.LdapDirectoryConfiguration.LookupAccountUsername))
                    Logger.Info("LDAP lookup account successfully found.");
                else
                {
                    Logger.Warn("Unable to find LDAP lookup account.  LDAP authentication is being disabled.");
                    this.LdapDirectoryConfiguration = null;
                }
            }

            // Setup SSL
            if (!String.IsNullOrWhiteSpace(this.SslServerCertificateThumbprint) && this.SslServerCertificateThumbprint != null)
            {
                var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.OpenExistingOnly);
                try
                {
                    var collection = store.Certificates.Find(X509FindType.FindByThumbprint, this.SslServerCertificateThumbprint, true);
                    if (collection.Cast<X509Certificate2>().Count(c => c.HasPrivateKey) == 0)
                    {
                        Logger.WarnFormat(@"No valid certificate with a public and private key could be found in the LocalMachine\Personal store with thumbprint: {0}.  Disabling SSL.", this.SslServerCertificateThumbprint);
                        this.IrcImplicitTLSPorts = new int[0];
                    }
                    else
                    {
                        Logger.InfoFormat("Located valid certificate with subject '{0}' and serial {1}", collection[0].Subject, collection[0].SerialNumber);
                        this.ServerAuthenticationCertificate = collection[0];
                    }
                }
                finally
                {
                    store.Close();
                }
            }
            else if (this.SslGenerateSelfSignedServerCertificate || this.IrcImplicitTLSPorts.Any())
            {
                var pfx = CertificateUtility.CreateSelfSignCertificatePfx("CN=freenews", DateTime.Now, DateTime.Now.AddYears(100), "password");
                this.ServerAuthenticationCertificate = new X509Certificate2(pfx, "password");
            }

            foreach (var clearPort in this.IrcClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new IrcListener(this, localEndPoint)
                {
                    PortType = PortClass.ClearText
                };

                this.listeners.Add(new Tuple<Thread, IrcListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var implicitTlsPort in this.IrcImplicitTLSPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, implicitTlsPort);

                // Create a TCP/IP socket.
                var listener = new IrcListener(this, localEndPoint)
                {
                    PortType = PortClass.ImplicitTLS
                };

                this.listeners.Add(new Tuple<Thread, IrcListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var listener in this.listeners)
            {
                try
                {
                    listener.Item1.Start();
                    Logger.InfoFormat("Listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (OutOfMemoryException oom)
                {
                    Logger.Error("Unable to start listener thread.  Not enough memory.", oom);
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
                    Logger.InfoFormat("Stopped listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (SocketException)
                {
                    Logger.ErrorFormat("Exception attempting to stop listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
            }

            Parallel.ForEach(this.connections, async c =>
            {
                await c.SendError("Server is shutting down");
                c.Shutdown();
            });

            foreach (var thread in this.listeners)
            {
                try
                {
                    thread.Item1.Abort();
                }
                catch (SecurityException se)
                {
                    Logger.Error(
                        "Unable to abort the thread due to a security exception.  Application will now exit.",
                        se);
                    Environment.Exit(se.HResult);
                }
                catch (ThreadStateException tse)
                {
                    Logger.Error(
                        "Unable to abort the thread due to a thread state exception.  Application will now exit.",
                        tse);
                    Environment.Exit(tse.HResult);
                }
            }
        }

        internal void AddConnection([NotNull] IrcConnection ircConnection)
        {
            this.connections.Add(ircConnection);
            Logger.VerboseFormat("Connection from {0}:{1} to {2}:{3}", ircConnection.RemoteAddress, ircConnection.RemotePort, ircConnection.LocalAddress, ircConnection.LocalPort);
        }

        internal void RemoveConnection([NotNull] IrcConnection ircConnection)
        {
            this.connections.Remove(ircConnection);
            if (ircConnection.Identity == null)
                Logger.VerboseFormat("Disconnection from {0}:{1}", ircConnection.RemoteAddress, ircConnection.RemotePort, ircConnection.LocalAddress, ircConnection.LocalPort);
            else
                Logger.VerboseFormat("Disconnection from {0}:{1} ({2})", ircConnection.RemoteAddress, ircConnection.RemotePort, ircConnection.LocalAddress, ircConnection.LocalPort, ircConnection.Identity.Username);
        }
        #endregion

        internal void RemovePrincipal([NotNull] IPrincipal principal)
        {
            var user = principal as User;
            if (user != null && this.Users.Contains(user))
                this.Users.Remove(user);
        }

        public bool NickInUse(string nickname, bool searchLocalOnly)
        {
            if (searchLocalOnly)
                return this.Users.Any(u => u.Server.Name == this.Self.Name && (new ScandanavianStringComparison()).Compare(u.Nickname, nickname) == 0);

            return this.Users.Any(u => (new ScandanavianStringComparison()).Compare(u.Nickname, nickname) == 0);
        }

        public bool NickReserved(string nickname)
        {
            DateTime expiration;
            if (!this.ReservedNicknames.TryGetValue(nickname, out expiration))
                return false;

            if (expiration >= DateTime.UtcNow)
                return true;

            this.ReservedNicknames.TryRemove(nickname, out expiration);
            return false;
        }

        public bool VerifyNickname(string nickname)
        {
            if (!_ValidateNickname.IsMatch(nickname))
                return false;

            // Because of this, servers MUST forbid users from using the nickname "anonymous".
            // https://tools.ietf.org/html/rfc2811#section-4.2.1
            if (string.Compare(nickname, "anonymous", StringComparison.OrdinalIgnoreCase) == 0)
                return false;

            return true;
        }

        internal async Task<bool> SendChannelMembers(Channel channel, Message message)
        {
            // TODO: Complete.
            return await Task.FromResult(true);
        }

        internal async Task<bool> SendPeers(Message message, Server except = null)
        {
            Debug.Assert(message.Prefix == null || message.Prefix.IndexOf('@') == -1);

            // TODO: Complete.
            return await Task.FromResult(true);
        }
    }
}