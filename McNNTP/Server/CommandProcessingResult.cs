using System;
using JetBrains.Annotations;

namespace McNNTP.Server
{
    internal sealed class CommandProcessingResult
    {
        public bool IsHandled { get; set; }
        public bool IsQuitting { get; set; }
        
        /// <summary>
        /// If not null, this indicates the request was the 
        /// start of a message that should be read until its end,
        /// at which time this function should be invoked on the result.
        /// </summary>
        [CanBeNull]
        public Func<string, CommandProcessingResult, CommandProcessingResult> MessageHandler { get; set; }

        [CanBeNull]
        public string Message { get; set; }

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
