namespace McNNTP.Core.Client
{
    internal class NntpMultilineResponse : NntpResponse
    {
        internal NntpMultilineResponse(int code, string message, IEnumerable<string> lines)
            : base(code, message)
        {
            this.Lines = lines;
        }

        internal IEnumerable<string> Lines { get; init; }
    }
}
