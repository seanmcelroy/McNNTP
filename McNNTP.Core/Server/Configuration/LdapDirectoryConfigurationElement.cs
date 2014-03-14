// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LdapDirectoryConfigurationElement.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   The configuration element that specifies how a user may authenticate to the server instance using an
//   LDAP protocol lookup to a directory server
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration
{
    using System.Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// The configuration element that specifies how a user may authenticate to the server instance using an
    /// LDAP protocol lookup to a directory server
    /// </summary>
    public class LdapDirectoryConfigurationElement : UserDirectoryConfigurationElement
    {
        /// <summary>
        /// Gets or sets the LDAP server hostname or IP
        /// </summary>
        [ConfigurationProperty("ldapServer", IsRequired = true), PublicAPI]
        public string LdapServer
        {
            get { return (string)this["ldapServer"]; }
            set { this["ldapServer"] = value; }
        }

        /// <summary>
        /// Gets or sets the LDAP search path to use to find users
        /// </summary>
        [ConfigurationProperty("searchPath", IsRequired = true), PublicAPI]
        public string SearchPath
        {
            get { return (string)this["searchPath"]; }
            set { this["searchPath"] = value; }
        }

        /// <summary>
        /// Gets or sets the username of a credential used to search the directory
        /// </summary>
        [ConfigurationProperty("lookupAccountUsername", IsRequired = true), PublicAPI]
        public string LookupAccountUsername
        {
            get { return (string)this["lookupAccountUsername"]; }
            set { this["lookupAccountUsername"] = value; }
        }

        /// <summary>
        /// Gets or sets the password of a credential used to search the directory
        /// </summary>
        [ConfigurationProperty("lookupAccountPassword", IsRequired = true), PublicAPI]
        public string LookupAccountPassword
        {
            get { return (string)this["lookupAccountPassword"]; }
            set { this["lookupAccountPassword"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether users who do not exist in the local database
        /// but successfully authenticate to LDAP should be automatically
        /// added to the local database.
        /// </summary>
        [ConfigurationProperty("autoEnroll", IsRequired = false), PublicAPI]
        public bool AutoEnroll
        {
            get { return (bool)this["autoEnroll"]; }
            set { this["autoEnroll"] = value; }
        }

        /// <summary>
        /// Gets or sets the group that if an auto-enrolled LDAP user has a membership in
        /// at the time of enrollment, the user will be treated as a local
        /// news server admin
        /// </summary>
        [ConfigurationProperty("autoEnrollAdminGroup", IsRequired = false), PublicAPI]
        public string AutoEnrollAdminGroup
        {
            get { return (string)this["autoEnrollAdminGroup"]; }
            set { this["autoEnrollAdminGroup"] = value; }
        }
    }
}
