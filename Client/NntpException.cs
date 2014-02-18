using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace McNNTP.Client
{
    [Serializable, PublicAPI]
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
