// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LocalDirectoryConfigurationElement.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   IRC protocol-specific configuration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration.IRC
{
    using System.Configuration;

    /// <summary>
    /// IRC protocol-specific configuration.
    /// </summary>
    public class IrcProtocolConfigurationElement : ProtocolConfigurationElementBase
    {
        /// <summary>
        /// Gets or sets the LDAP server hostname or IP.
        /// </summary>
        [ConfigurationProperty("motd", IsRequired = false)]
        public string MotdPath
        {
            get { return (string)this["motd"]; }
            set { this["motd"] = value; }
        }

        /// <summary>
        /// Gets a collection of <see cref="OperatorConfigurationElement"/> configuration elements that provide
        /// O: lines, or the oper block, enumerating allowed administrators.
        /// </summary>
        [ConfigurationProperty("operators", IsRequired = false)]
        public OperatorsConfigurationElementCollection Operators
        {
            get { return (OperatorsConfigurationElementCollection)this["operators"]; }
        }
    }
}
