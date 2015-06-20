using System.Configuration;
using JetBrains.Annotations;

namespace McNNTP.Core.Server.Configuration
{
    /// <summary>
    /// A configuration element that provides information on how users who conenct to an NNTP server authenticate
    /// </summary>
    public class ProtocolsConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// A collection of <see cref="ProtocolConfigurationElementBase"/> configuration elements that provide
        /// data on which user directories should be checked to handle authentication requests.  This can include
        /// multiple entries in a priority-based order, such as LDAP, local database, et cetera.
        /// </summary>
        [ConfigurationProperty("protocols", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public ProtocolsConfigurationElementCollection Protocols
        {
            get { return (ProtocolsConfigurationElementCollection)base["protocols"]; }
        }
    }
}
