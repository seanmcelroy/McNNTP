using System.ComponentModel;
using System.ServiceProcess;
using JetBrains.Annotations;

namespace McNNTP.Service
{
    [RunInstaller(true), UsedImplicitly]
    public partial class NntpServiceInstaller : System.Configuration.Install.Installer
    {
        public NntpServiceInstaller()
        {
            InitializeComponent();

            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalService
            };

            var serviceInstaller = new ServiceInstaller
            {
                DelayedAutoStart = true,
                Description = "An open-source NNTP server in C#",
                DisplayName = "McNNTP Communications Server",
                ServiceName = "McNNTP",
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }
    }
}
