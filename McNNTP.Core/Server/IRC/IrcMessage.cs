namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    internal class IrcMessage
    {
        /// <summary>
        /// This tell us whether we've had a call to get a param. On the first
        /// such call, we go ahead and GetParam the entire string into storage
        /// so that successive calls will bypass GetParam.
        /// </summary>
        /// <remarks>
        /// Command is in Params(0)
        /// </remarks>
        private List<string> Params;

        private string prefix;

        public string Prefix
        {
            get
            {
                return prefix;
            }
            private set
            {
                // RFC 2812 2.3.1
                Debug.Assert(value.IndexOf('\0') == -1);

                // RFC 2812 2.3.1
                Debug.Assert(value.IndexOf(':') == -1);

                // No leading whitespaces allowed between colon and prefix. RFC 2812 2.3
                prefix = value.Trim();
            }
        }

        public bool IsNumeric
        {
            get
            {
                int i;
                return this.Params[0].Length == 3 && int.TryParse(this.Params[0], out i) && i >= 1 && i <= 599;
            }
        }

        public string this[int parameterIndex]
        {
            get
            {
                // 15 max parameters, RFC 2813 3.3
                Debug.Assert(parameterIndex >= 0);
                Debug.Assert(parameterIndex <= 15);

                if (parameterIndex > this.Params.Count)
                    return string.Empty;
                return this.Params[parameterIndex];
            }
            set
            {
                // 15 max parameters, RFC 2813 3.3
                Debug.Assert(parameterIndex >= 0);
                Debug.Assert(parameterIndex <= 15);

                // Packets are relatively short lived. (Only exist through a Process())
                // Don't fuss about doing a cascading removal of empty elements at
                // array's end.. just grow, don't shrink.
                this.Params[parameterIndex] = value;
            }
        }

        public IrcMessage(string prefix, string command, string parameters)
        {
            // HERE'S OUR MESSAGE SYNTAX
            // :[servername | nick [ !user@host.domain ] ] command parameters...
            this.Prefix = prefix;
            this.Params[0] = command;

            // CHEAT: ISON violates it's own RFC. ISON definition allows for
            // greater than 15 parameters, which is against RFC 2812.
            // Pretend it has a colon before it to process correctly.
            // Notice the colon add doesn't chop off a char at the end.
            if (command == "ISON" && parameters[0] != ':')
                parameters = ":" + parameters;

            string sDud;

            for (var repeat = 1; repeat <= 15; repeat++)
            {
                sDud = IrcUtility.ParamGet(parameters, repeat, " ", false, 1);

                if (sDud.Length == 0)
                    break;

                if (sDud[0] == ':')
                {
                    // Handle space-weilding parameters
                    this.Params[repeat] = IrcUtility.ParamGet(parameters, repeat, " ", true, 1);
                    break;
                }


                // If quotation marks surround the parameter, tear them off. (mIRC is dumb.)
                // Note this will not affect msg text since it will ignore :"blah",
                // only getting things like ":blah". But watch out and don't kill
                // things like ""
                if (sDud.Length > 2 && sDud[0] == '\"' && sDud[sDud.Length - 1] == '\"')
                    sDud = sDud.Substring(1, sDud.Length - 2);

                this.Params[repeat] = sDud;
            }
        }


        internal string OutgoingString(string preParam)
        {
            Debug.Assert(preParam != "\0");

            var ret = new StringBuilder();

            // Add prefix.
            if (this.Prefix.Length > 0)
                ret.AppendFormat(":{0}", this.Prefix);

            // Command
            ret.AppendFormat(" {0}", this.Params[0].Trim());

            // Pre-Param
            if (!string.IsNullOrEmpty(preParam))
                ret.AppendFormat(" {0}", preParam.Trim());

            // Other params.
            ret.AppendFormat(" {0}", this.Params.Skip(1).Aggregate((c, n) => c + " " + n));

            // RFC2812 2.3.1 - No NULL in packet.
            Debug.Assert(ret.ToString().IndexOf("\0", StringComparison.Ordinal) == -1);

            return ret.ToString().Substring(0, 510) + "\r\n";
        }
    }
}
