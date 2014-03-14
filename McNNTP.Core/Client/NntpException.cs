// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NntpException.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   An exception that is raised when the response from a remote NNTP server is invalid or unexpected
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Client
{
    using System;
    using System.Runtime.Serialization;

    using JetBrains.Annotations;

    /// <summary>
    /// An exception that is raised when the response from a remote NNTP server is invalid or unexpected
    /// </summary>
    [Serializable, PublicAPI]
    public class NntpException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NntpException"/> class.
        /// </summary>
        public NntpException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NntpException"/> class.
        /// </summary>
        /// <param name="message">The exception message</param>
        public NntpException([NotNull] string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NntpException"/> class.
        /// </summary>
        /// <param name="format">The format string for the exception message</param>
        /// <param name="args">The format string arguments for the exception message</param>
        [StringFormatMethod("format")]
        public NntpException([NotNull] string format, object[] args)
            : base(string.Format(format, args))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NntpException"/> class.
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The underlying exception that this exception wraps, if any</param>
        public NntpException([NotNull] string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NntpException"/> class.
        /// </summary>
        /// <param name="info">The serialized exception information</param>
        /// <param name="context">The streaming context for the serialized exception</param>
        protected NntpException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
