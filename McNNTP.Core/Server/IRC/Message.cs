namespace McNNTP.Core.Server.IRC
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using JetBrains.Annotations;

    /// <summary>
    /// An IRC message
    /// </summary>
    internal class Message
    {
        public const string RegexShortNamePattern = @"^[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9]*$";

        public const string RegexUsername = @"^[\x01-\x09\x0B-\x0C\x0E-\x1F\x21-\x3F\x41-\xFF]+$";

        public const string RegexHostName = @"^([A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9]*)(\.[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9]*)*$";

        public const string RegexNickname = @"^[A-Za-z\x5B-\x60\x7B-\x7D][A-Za-z\x5B-\x60\x7B-\x7D0-9\-]{0,8}$";

        public const string RegexChanString = @"^[\x01-\x07\x08-\x09\x0B-\x0C\x0E-\x1F\x21-\x2B\x2D-\x39\x3B-\xFF]+$";

        public const string RegexChannel = @"^(\#|\+|(\![A-Z0-9]{5})|\&)[\x01-\x07\x08-\x09\x0B-\x0C\x0E-\x1F\x21-\x2B\x2D-\x39\x3B-\xFF]+(\:[\x01-\x07\x08-\x09\x0B-\x0C\x0E-\x1F\x21-\x2B\x2D-\x39\x3B-\xFF]+)?$";

        public const string RegexParams = @"^(:(?<prefix>\S+))?(?<command>\S+)((?!:)(?<params>.+?))?(:(?<trail>.+))?$";

        private static Regex ParameterRegex = new Regex(RegexParams, RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private readonly string message;

        private Match _match = null;

        private Match MessageMatch
        {
            get
            {
                this._match = this._match ?? Regex.Match(this.message, RegexParams);
                return this._match;
            }
        }

        [CanBeNull]
        public string Prefix
        {
            get
            {
                return this.MessageMatch.Groups["prefix"].Value;
            }
        }

        [NotNull]
        public string Command
        {
            get
            {
                return this.MessageMatch.Groups["command"].Value;
            }
        }

        [NotNull]
        public IEnumerable<string> Parameters
        {
            get
            {
                var parameters = this.MessageMatch.Groups["params"].Value.Trim().Split(' ');
                var trail = this.MessageMatch.Groups["trail"].Value;

                if (!string.IsNullOrWhiteSpace(trail))
                    return parameters.Union(new[] { trail });

                return parameters;
            }
        }

        public bool IsNumeric
        {
            get
            {
                int i;
                return this.Command.Length == 3 && int.TryParse(this.Command, out i) && i >= 1 && i <= 599;
            }
        }

        [CanBeNull]
        public string this[int parameterIndex]
        {
            get
            {
                return this.Parameters.Skip(parameterIndex).FirstOrDefault();
            }
        }

        public Message(string message)
        {
            this.message = message;
        }

        public Message(string prefix, string command, params string[] parameters)
        {
            this.message = string.IsNullOrWhiteSpace(prefix) 
                ? string.Format("{0} {1}", command, parameters.Aggregate((c, n) => c + " " + n)) 
                : string.Format(":{0} {1} {2}", prefix, command, parameters.Aggregate((c, n) => c + " " + n));
        }

        internal string OutgoingString()
        {
            return (this.message.Length <= 510 ? this.message : this.message.Substring(0, 510)) + "\r\n";
        }
    }
}
