namespace McNNTP.Core.Server.Configuration
{
    using System.Configuration;

    /// <summary>
    /// A configuration element that provides information on how users who conenct to an NNTP server authenticate.
    /// </summary>
    public class AuthenticationConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets collection of <see cref="UserDirectoryConfigurationElement"/> configuration elements that provide
        /// data on which user directories should be checked to handle authentication requests.  This can include
        /// multiple entries in a priority-based order, such as LDAP, local database, et cetera.
        /// </summary>
        [ConfigurationProperty("userDirectories", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public UserDirectoryConfigurationElementCollection UserDirectories
        {
            get { return (UserDirectoryConfigurationElementCollection)this["userDirectories"]; }
        }
    }
}
