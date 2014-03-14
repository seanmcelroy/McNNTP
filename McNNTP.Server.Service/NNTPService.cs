namespace McNNTP.Server.Service
{
    using System.Configuration;
    using System.Linq;
    using System.ServiceProcess;

    using log4net;
    using log4net.Config;

    using McNNTP.Core.Server;
    using McNNTP.Core.Server.Configuration;

    public class NntpService : ServiceBase
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(NntpService));

        private static NntpServer server;

        public NntpService()
        {
            AutoLog = true;
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = false;
            CanStop = true;
            ServiceName = "McNNTP";

            // Setup LOG4NET
            XmlConfigurator.Configure();

            // Load configuration
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
            Logger.InfoFormat("Loaded configuration from {0}", config.FilePath);

            server = new NntpServer
            {
                AllowPosting = true,
                NntpClearPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ClearText").Select(p => p.Port).ToArray(),
                NntpExplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ExplicitTLS").Select(p => p.Port).ToArray(),
                NntpImplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ImplicitTLS").Select(p => p.Port).ToArray(),
                LdapDirectoryConfiguration = mcnntpConfigurationSection.Authentication.UserDirectories.OfType<LdapDirectoryConfigurationElement>().OrderBy(l => l.Priority).FirstOrDefault(),
                PathHost = mcnntpConfigurationSection.PathHost,
                SslGenerateSelfSignedServerCertificate = mcnntpConfigurationSection.Ssl == null || mcnntpConfigurationSection.Ssl.GenerateSelfSignedServerCertificate,
                SslServerCertificateThumbprint = mcnntpConfigurationSection.Ssl == null ? null : mcnntpConfigurationSection.Ssl.ServerCertificateThumbprint
            };
        }

        protected override void OnStart(string[] args)
        {
            server.Start();
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            server.Stop();
            base.OnStop();
        }
    }
}