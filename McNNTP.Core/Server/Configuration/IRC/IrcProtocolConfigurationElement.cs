// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LocalDirectoryConfigurationElement.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A configuration reference to the local user directory located within the news database
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration.IRC
{
    using System.Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// A configuration reference to the local user directory located within the news database
    /// </summary>
    public class IrcProtocolConfigurationElement : ProtocolConfigurationElementBase
    {
        /// <summary>
        /// Gets or sets the LDAP server hostname or IP
        /// </summary>
        [ConfigurationProperty("motd", IsRequired = false), PublicAPI]
        public string MotdPath
        {
            get { return (string)this["motd"]; }
            set { this["motd"] = value; }
        }
    }
}
