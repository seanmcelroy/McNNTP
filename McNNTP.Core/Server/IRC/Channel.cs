namespace McNNTP.Core.Server.IRC
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Text.RegularExpressions;

    using JetBrains.Annotations;

    /// <summary>
    /// A 'room' or group of users that broadcast messages to one another
    /// </summary>
    internal class Channel
    {
        [NotNull]
        private string name;

        public Channel([NotNull] string name)
        {
            this.name = name;
        }

        [NotNull]
        public string Name
        {
            get
            {
                return this.name;
            }
            set
            {
                #if DEBUG
                Debug.Assert(Regex.IsMatch(value, Message.RegexChannel));
                #endif
                this.name = value;
            }
        }

        /// <summary>
        /// Gets or sets the channel's 'private' flag
        /// </summary>
        public bool Private { get; set; }

        /// <summary>
        /// Gets or sets the channel's 'secret' flag
        /// </summary>
        public bool Secret { get; set; }

        /// <summary>
        /// Gets or sets the channel's topic
        /// </summary>
        public string Topic { get; set; }

        public bool Anonymous { get; set; }

        public bool InviteOnly { get; set; }

        public bool Moderated { get; set; }

        public bool NoExternalMessages { get; set; }

        public bool Quiet { get; set; }

        public bool ServerReop { get; set; }

        public bool TopicLocked { get; set; }

        public int? UserLimit { get; set; }

        [CanBeNull]
        public string Key { get; set; }

        public string ModeString
        {
            get
            {
                return string.Format("+{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{11} {10} {12}",
                    this.Anonymous ? "a" : string.Empty,
                    this.InviteOnly ? "i" : string.Empty,
                    this.Moderated ? "m" : string.Empty,
                    this.NoExternalMessages ? "n" : string.Empty,
                    this.Private ? "p" : string.Empty,
                    this.Quiet ? "q" : string.Empty,
                    this.ServerReop ? "r" : string.Empty,
                    this.Secret ? "s" : string.Empty,
                    this.TopicLocked ? "t" : string.Empty,
                    this.UserLimit.HasValue ? "l" : string.Empty,
                    this.UserLimit.HasValue ? this.UserLimit.Value.ToString() : string.Empty,
                    !string.IsNullOrWhiteSpace(this.Key) ? "k" : string.Empty,
                    !string.IsNullOrWhiteSpace(this.Key) ? this.Key : string.Empty).Trim();
            }
        }

        /// <summary>
        /// All users known across all local and remote servers
        /// </summary>
        internal readonly ConcurrentDictionary<User, string> UsersModes = new ConcurrentDictionary<User, string>();

        internal readonly ConcurrentDictionary<string, string> BanMasks = new ConcurrentDictionary<string, string>();
    }
}
