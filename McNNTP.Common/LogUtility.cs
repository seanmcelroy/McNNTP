// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A utility class that provides extension methods to Microsoft.Extensions.Logging that allow for Trace and Verbose logging levels.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A utility class that provides extension methods to Microsoft.Extensions.Logging that allow for Trace and Verbose logging levels.
    /// </summary>
    public static class LogUtility
    {
        /// <summary>
        /// Provides a simple 'Trace' level logging of a message.
        /// </summary>
        /// <param name="logger">The logger to use to log the message.</param>
        /// <param name="message">The message to log.</param>
        public static void Trace([NotNull] this ILogger logger, [NotNull] string message)
        {
            logger.LogTrace(message);
        }

        /// <summary>
        /// Provides a simple 'Trace' level logging of a message using a format string and arguments.
        /// </summary>
        /// <param name="logger">The logger to use to log the message.</param>
        /// <param name="format">The format string for the message to log.</param>
        /// <param name="args">The format arguments used to formulate the message to log.</param>
        public static void TraceFormat([NotNull] this ILogger logger, [NotNull][StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object[] args)
        {
            logger.LogTrace(format, args);
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message (mapped to Debug level).
        /// </summary>
        /// <param name="logger">The logger to use to log the message.</param>
        /// <param name="message">The message to log.</param>
        public static void Verbose([NotNull] this ILogger logger, [NotNull] string message)
        {
            logger.LogDebug(message);
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message using a format string and arguments (mapped to Debug level).
        /// </summary>
        /// <param name="logger">The logger to use to log the message.</param>
        /// <param name="format">The format string for the message to log.</param>
        /// <param name="args">The format arguments used to formulate the message to log.</param>
        public static void VerboseFormat([NotNull] this ILogger logger, [NotNull][StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object[] args)
        {
            logger.LogDebug(format, args);
        }
    }
}