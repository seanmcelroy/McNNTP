using System.ServiceProcess;
using McNNTP.Server;

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

            _server = new NntpServer(Database.SessionUtility.OpenSession)
            {
                AllowPosting = true,
                ClearPorts = new[] { 119 }
            };

            if (!_server.VerifyDatabase())
            {
                System.Console.WriteLine("Unable to verify a database.  Would you like to create and initialize a database?");
                _server.InitializeDatabase();
            }
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