using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Core.Server.Configuration
{
    public class AuthenticationConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("userDirectories", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public UserDirectoryConfigurationElementCollection UserDirectories
        {
            get { return (UserDirectoryConfigurationElementCollection)base["userDirectories"]; }
        }
    }
}
