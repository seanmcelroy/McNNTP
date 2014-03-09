using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Core.Server.Configuration
{
    [PublicAPI]
// ReSharper disable once InconsistentNaming
    public class McNNTPConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("authentication", IsRequired = false)]
        [UsedImplicitly]
        public AuthenticationConfigurationElement Authentication
        {
            get { return (AuthenticationConfigurationElement)base["authentication"]; }
            set { this["authentication"] = value; }
        }

        [ConfigurationProperty("pathHost", IsRequired = true)]
        [UsedImplicitly]
        public string PathHost
        {
            get { return (string)this["pathHost"]; }
            set { this["pathHost"] = value; }
        }
        
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public PortConfigurationElementCollection Ports
        {
            get { return (PortConfigurationElementCollection)base["ports"]; }
        }

        [ConfigurationProperty("ssl", IsRequired = false)]
        [UsedImplicitly]
        public SslConfigurationElement SSL
        {
            get { return (SslConfigurationElement)base["ssl"]; }
            set { this["ssl"] = value; }
        }
    }
}
