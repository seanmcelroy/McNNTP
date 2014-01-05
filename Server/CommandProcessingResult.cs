namespace McNNTP.Server
{
    internal sealed class CommandProcessingResult
    {
        public bool IsHandled { get; set; }
        public bool IsQuitting { get; set; }

        public CommandProcessingResult(bool isHandled)
            : this(isHandled, false)
        {
        }
        public CommandProcessingResult(bool isHandled, bool isQuitting)
        {
            IsHandled = isHandled;
            IsQuitting = isQuitting;
        }
    }
}
