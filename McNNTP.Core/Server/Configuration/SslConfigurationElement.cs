using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Core.Server.Configuration
{
    [UsedImplicitly]
    public class SslConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// The port number
        /// </summary>
        [ConfigurationProperty("generateSelfSignedServerCertificate", IsRequired = false, DefaultValue = true)]
        public bool GenerateSelfSignedServerCertificate
        {
            get { return (bool)this["generateSelfSignedServerCertificate"]; }
            set { this["generateSelfSignedServerCertificate"] = value; }
        }

        /// <summary>
        /// Configuration information about NewRelic
        /// </summary>
        [ConfigurationProperty("serverCertificateThumbprint", IsRequired = false)]
        public string ServerCertificateThumbprint
        {
            get { return (string)this["serverCertificateThumbprint"]; }
            set { this["serverCertificateThumbprint"] = value; }
        }
    }
}
