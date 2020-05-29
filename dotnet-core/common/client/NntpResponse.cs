namespace mcnntp.common.client
{
    internal class NntpResponse
    {
        public int Code { get; private set; }
        
        public string Message { get; private set; }

        internal NntpResponse(int code, string message)
        {
            this.Code = code;
            this.Message = message;
        }
    }
}
