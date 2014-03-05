using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Core.Server.Configuration
{
    [PublicAPI]
// ReSharper disable once InconsistentNaming
    public class McNNTPConfigurationSection : ConfigurationSection
    {
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
    }
}
