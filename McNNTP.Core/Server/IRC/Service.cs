﻿namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;

    using JetBrains.Annotations;

    /// <summary>
    /// A service that is communicating across a <see cref="IrcConnection"/>
    /// </summary>
    internal class Service : IPrincipal
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

        public Service([NotNull] IPAddress address, [NotNull] Server self)
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

        public Service([NotNull] Server remoteServer, [NotNull] string nickname, [NotNull] string username, [NotNull] string hostname, [NotNull] string mode, [NotNull] string realName)
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

        public bool ReceiveWallops { get; set; }

        public bool Restricted { get; set; }

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
