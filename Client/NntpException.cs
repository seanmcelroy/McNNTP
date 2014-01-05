using System;
using System.Runtime.Serialization;

namespace McNNTP.Client
{
    [Serializable]
    public class NntpException : Exception
    {
        public NntpException()
        {
        }
        public NntpException(string message)
            : base(message)
        {
        }
        public NntpException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected NntpException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    };
}
