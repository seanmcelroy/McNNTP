using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Server.Configuration
{
    [PublicAPI]
    public class McNNTPConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("pathHost", IsRequired = true)]
        public string PathHost { get; set; }
        
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public PortConfigurationElementCollection Ports
        {
            get { return (PortConfigurationElementCollection)base["ports"]; }
        }
    }
}
