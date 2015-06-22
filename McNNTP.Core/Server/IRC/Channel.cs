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

        /// <summary>
        /// All users known across all local and remote servers
        /// </summary>
        internal readonly ConcurrentBag<User> Users = new ConcurrentBag<User>();
    }
}
