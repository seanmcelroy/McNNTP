namespace McNNTP.Core.Client
{
    internal class NntpResponse
    {
        internal NntpResponse(int code, string message)
        {
            this.Code = code;
            this.Message = message;
        }

        public int Code { get; private set; }

        public string Message { get; private set; }
    }
}
