using System.ServiceProcess;

namespace McNNTP.Service
{
    public class NntpService : ServiceBase
    {
        public NntpService()
        {
            AutoLog = true;
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = true;
            CanShutdown = true;
            CanStop = true;
            ServiceName = "McNNTP";
        }

        protected override void OnStart(string[] args)
        {

            base.OnStart(args);
        }
    }
}