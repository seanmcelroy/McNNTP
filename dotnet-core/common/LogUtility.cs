// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A utility clas that provides extension methods to log4net that allow for Trace and Verbose logging levels.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace mcnntp.common
{
    using System;
    using System.Reflection;

    using log4net;

    /// <summary>
    /// A utility class that provides extension methods to log4net that allow for Trace and Verbose logging levels.
    /// </summary>
    public static class LogUtility
    {
        /// <summary>
        /// Provides a simple 'Trace' level logging of a message
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="message">The message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        public static void Trace(this ILog log, string message)
        {
            log.Logger.Log(
                MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Trace,
                message,
                null);
        }

        /// <summary>
        /// Provides a simple 'Trace' level logging of a message using a format string and arguments
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="format">The format string for the message to log</param>
        /// <param name="args">The format arguments used to formulate the message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        /// <exception cref="ArgumentNullException">Thrown when the format string is null</exception>
        /// <exception cref="FormatException">Thrown when the format string and associated arguments cannot be used to create a formatted message</exception>
        public static void TraceFormat(this ILog log, string format, params object[] args)
        {
            log.Trace(string.Format(format, args));
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="message">The message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        public static void Verbose(this ILog log, string message)
        {
            log.Logger.Log(
                MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Verbose,
                message,
                null);
        }

        /// <summary>
        /// Provides a simple 'Verbose' level logging of a message using a format string and arguments
        /// </summary>
        /// <param name="log">The logger to use to log the message</param>
        /// <param name="format">The format string for the message to log</param>
        /// <param name="args">The format arguments used to formulate the message to log</param>
        /// <exception cref="TargetException">Thrown when the current method cannot be determined through reflection</exception>
        /// <exception cref="ArgumentNullException">Thrown when the format string is null</exception>
        /// <exception cref="FormatException">Thrown when the format string and associated arguments cannot be used to create a formatted message</exception>
        public static void VerboseFormat(this ILog log, string format, params object[] args)
        {
            log.Verbose(string.Format(format, args));
        }
    }
}
