using System.Collections.ObjectModel;

namespace mcnntp.common.client
{
    internal class NntpMultilineResponse : NntpResponse
    {
        internal ReadOnlyCollection<string> Lines { get; private set; }

        internal NntpMultilineResponse(int code, string message, ReadOnlyCollection<string> lines)
            : base(code, message)
        {
            this.Lines = lines;
        }
    }
}
