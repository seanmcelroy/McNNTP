using System.Collections.Generic;

namespace McNNTP.Core.Client
{
    internal class NntpMultilineResponse : NntpResponse
    {
        internal IEnumerable<string> Lines { get; private set; }

        internal NntpMultilineResponse(int code, string message, IEnumerable<string> lines)
            : base(code, message)
        {
            this.Lines = lines;
        }
    }
}
