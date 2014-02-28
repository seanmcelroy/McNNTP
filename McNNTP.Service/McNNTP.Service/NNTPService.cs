using System.ServiceProcess;
using McNNTP.Server;
using log4net;

namespace McNNTP.Service
{
    public class NntpService : ServiceBase
    {
        private static NntpServer _server;

        public NntpService()
        {
            AutoLog = true;
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = false;
            CanStop = true;
            ServiceName = "McNNTP";

            _server = new NntpServer()
            {
                AllowPosting = true,
                // TODO: Move to configuration
                ClearPorts = new[] { 119 }
            };
        }

        protected override void OnStart(string[] args)
        {
            _server.Start();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            _server.Stop();
            base.OnStop();
        }
    }
}