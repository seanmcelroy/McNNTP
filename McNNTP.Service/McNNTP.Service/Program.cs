using System.ServiceProcess;

namespace McNNTP.Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var servicesToRun = new ServiceBase[] 
            { 
                new NntpService()
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
