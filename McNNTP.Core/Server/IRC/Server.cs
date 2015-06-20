namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    using JetBrains.Annotations;

    internal class Server : IPrincipal
    {
        private static Regex ValidateNickname = new Regex(Message.RegexNickname, RegexOptions.Compiled);

        public Server([NotNull] string name)
        {
            this.Name = name;
        }

        [NotNull]
        public string Name { get; private set; }

        public string Hostname { get; private set; }

        internal int HopCount { get; set; }

        public string Token { get; private set; }

        public string Info { get; private set; }

        public DateTime LogonTime { get; private set; }

        private string Password { get; set; }

        public bool PingWait { get; private set; }

        /// <summary>
        /// This takes on a double meaning: This is essentially "this is the
        /// socket to send messages to", not the local socket only since'       
        /// we will put the entry for the local peer that connects through
        /// to a remote one here.
        /// </summary>
        internal IrcConnection IndirectConnection { get; private set; }

        /// <summary>
        /// Parent server object
        /// </summary>
        internal Server Parent { get; set; }

        public int SentMessages { get; private set; }

        public int SentBytes { get; private set; }

        public int ReceivedMessages { get; private set; }

        public int LineIndex { get; private set; }

        internal string Version { get; set; }

        internal string Flags { get; set; }

        public string Options { get; private set; }

        /// <summary>
        /// True once SERVER msg recvd
        /// </summary>
        public bool Registered { get; private set; }

        public List<Server> Servers { get; private set; }

        /// <summary>
        /// Gets the path to the principal.  If this is a local non-server connection, this will be null.
        /// </summary>
        Server IPrincipal.LocalPath
        {
            get
            {
                return this.Parent;
            }
        }
    }
}