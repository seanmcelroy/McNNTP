using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McNNTP
{
    public static class LogUtility
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void Trace(this ILog log, string message, Exception exception)
        {
            log.Logger.Log(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Trace, message, exception);
        }

        public static void Trace(this ILog log, string message)
        {
            log.Trace(message, null);
        }

        public static void TraceFormat(this ILog log, string format, params object[] args)
        {
            log.Trace(string.Format(format, args), null);
        }

        public static void Verbose(this ILog log, string message, Exception exception)
        {
            log.Logger.Log(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType,
                log4net.Core.Level.Verbose, message, exception);
        }

        public static void Verbose(this ILog log, string message)
        {
            log.Verbose(message, null);
        }

        public static void VerboseFormat(this ILog log, string format, params object[] args)
        {
            log.Verbose(string.Format(format, args), null);
        }
    }
}
