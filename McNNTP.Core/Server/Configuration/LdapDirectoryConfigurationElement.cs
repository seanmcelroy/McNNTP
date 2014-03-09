using System.Configuration;

namespace McNNTP.Core.Server.Configuration
{
    public class LdapDirectoryConfigurationElement : UserDirectoryConfigurationElement
    {
        /// <summary>
        /// The LDAP server hostname or IP
        /// </summary>
        [ConfigurationProperty("ldapServer", IsRequired = true)]
        public string LdapServer
        {
            get { return (string)this["ldapServer"]; }
            set { this["ldapServer"] = value; }
        }

        /// <summary>
        /// The LDAP search path to use to find users
        /// </summary>
        [ConfigurationProperty("searchPath", IsRequired = true)]
        public string SearchPath
        {
            get { return (string)this["searchPath"]; }
            set { this["searchPath"] = value; }
        }

        /// <summary>
        /// The username of a credential used to search the directory
        /// </summary>
        [ConfigurationProperty("lookupAccountUsername", IsRequired = true)]
        public string LookupAccountUsername
        {
            get { return (string)this["lookupAccountUsername"]; }
            set { this["lookupAccountUsername"] = value; }
        }

        /// <summary>
        /// The password of a credential used to search the directory
        /// </summary>
        [ConfigurationProperty("lookupAccountPassword", IsRequired = true)]
        public string LookupAccountPassword
        {
            get { return (string)this["lookupAccountPassword"]; }
            set { this["lookupAccountPassword"] = value; }
        }

        /// <summary>
        /// Whether or not users who do not exist in the local database
        /// but successfully authenticate to LDAP should be automatically
        /// added to the local database.
        /// </summary>
        [ConfigurationProperty("autoEnroll", IsRequired = false)]
        public bool AutoEnroll
        {
            get { return (bool)this["autoEnroll"]; }
            set { this["autoEnroll"] = value; }
        }

        /// <summary>
        /// The group that if an auto-enrolled LDAP user has a membership in
        /// at the time of enrollment, the user will be treated as a local
        /// news server admin
        /// </summary>
        [ConfigurationProperty("autoEnrollAdminGroup", IsRequired = false)]
        public string AutoEnrollAdminGroup
        {
            get { return (string)this["autoEnrollAdminGroup"]; }
            set { this["autoEnrollAdminGroup"] = value; }
        }
    }
}
