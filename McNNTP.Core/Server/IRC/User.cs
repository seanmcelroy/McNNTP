namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;

    using JetBrains.Annotations;

    /// <summary>
    /// A user that is communicating across a <see cref="IrcConnection"/>
    /// </summary>
    internal class User : IPrincipal
    {
        /// <summary>
        /// The class used to securely hash hostnames
        /// </summary>
        private static SHA256 _hasher = SHA256.Create();
        
        private string nickname;

        private string username;

        private string hostname;

        private string realname;

        private readonly Server server;

        private readonly bool local;

        public User([NotNull] IPAddress address, [NotNull] Server self)
        {
            this.hostname = address.ToString();

            try
            {
                var ipHostEntry = Dns.GetHostEntry(address);
                this.hostname = ipHostEntry.HostName;
            }
            catch (SocketException se)
            {
            }

            this.server = self;
            this.local = true;
        }

        public User([NotNull] Server remoteServer, [NotNull] string nickname, [NotNull] string username, [NotNull] string hostname, [NotNull] string mode, [NotNull] string realName)
        {
            this.server = remoteServer;
            this.local = false;
            this.nickname = nickname;
            this.username = username;
            this.hostname = hostname;
            this.realname = realName;

            // TODO: Handle remote-sent modes.
        }

        public string Nickname
        {
            get
            {
                return this.nickname;
            }
            set
            {
                #if DEBUG
                Debug.Assert(Regex.IsMatch(value, Message.RegexNickname));
                #endif
                this.nickname = value;
            }
        }
        
        public string RealName
        {
            get
            {
                return this.realname;
            }
            set
            {
                this.realname = value;
            }
        }

        public string Username
        {
            get
            {
                return this.username;
            }
            set
            {
                Debug.Assert(Regex.IsMatch(value, Message.RegexUsername));
                this.username = value;
            }
        }

        public string RawUserhost
        {
            get
            {
                return string.Format("{0}!{1}@{2}", this.nickname, this.username, this.hostname);
            }
        }

        public string SecureUserhost
        {
            get
            {
                var secureHostname = Convert.ToBase64String(_hasher.ComputeHash(System.Text.Encoding.ASCII.GetBytes(this.hostname)));
                return string.Format("{0}!{1}@{2}", this.nickname, this.username, secureHostname);
            }
        }

        [NotNull]
        public Server Server
        {
            get { return this.server; }
        }

        public bool RegistrationPassRecevied { get; set; }

        public bool RegistrationNickRecevied { get; set; }

        public bool RegistrationUserRecevied { get; set; }

        public bool Invisible { get; set; }

        public bool OperatorGlobal { get; set; }

        public bool OperatorLocal { get; set; }

        public bool ReceiveWallops { get; set; }

        public bool Restricted { get; set; }

        public string ModeString
        {
            get
            {
                return string.Format("+{0}{1}{2}{3}{4}",
                    this.Invisible ? "i" : string.Empty,
                    this.OperatorGlobal ? "o" : string.Empty,
                    this.OperatorLocal ? "O" : string.Empty,
                    this.Restricted ? "r" : string.Empty,
                    this.ReceiveWallops ? "w" : string.Empty);
            }
        }

        /// <summary>
        /// Retrieves the name for the principal
        /// </summary>
        string IPrincipal.Name { get
        {
            return this.Nickname;
        }}

        /// <summary>
        /// Gets the path to the principal.  If this is a local non-server connection, this will be null.
        /// </summary>
        Server IPrincipal.LocalPath
        {
            get
            {
                return this.local ? null : this.server;
            }
        }
    }
}
