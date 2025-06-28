// --------------------------------------------------------------------------------------------------------------------
// <copyright file="McNNTPConfigurationSection.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   The primary configuration section for the server instance
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration
{
    using System.Configuration;

    /// <summary>
    /// The primary configuration section for the server instance
    /// </summary>
    public class McNNTPConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets or sets the configuration element relating to how users can authenticate to the server instance
        /// </summary>
        [ConfigurationProperty("authentication", IsRequired = false)]
        public AuthenticationConfigurationElement Authentication
        {
            get { return (AuthenticationConfigurationElement)base["authentication"]; }
            set { this["authentication"] = value; }
        }

        /// <summary>
        /// Gets or sets the configuration element relating to how messages POSTed to this server receive their Path header
        /// </summary>
        [ConfigurationProperty("pathHost", IsRequired = true)]
        public string PathHost
        {
            get { return (string)this["pathHost"]; }
            set { this["pathHost"] = value; }
        }

        /// <summary>
        /// A collection of <see cref="ProtocolsConfigurationElement"/> configuration elements that provide
        /// data on which user directories should be checked to handle authentication requests.  This can include
        /// multiple entries in a priority-based order, such as LDAP, local database, et cetera.
        /// </summary>
        [ConfigurationProperty("protocols", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(ProtocolsConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public ProtocolsConfigurationElementCollection Protocols
        {
            get { return (ProtocolsConfigurationElementCollection)base["protocols"]; }
        }

        /// <summary>
        /// Gets the configuration element relating to how networking ports are made available to connect to this server instance
        /// </summary>
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public PortConfigurationElementCollection Ports
        {
            get { return (PortConfigurationElementCollection)base["ports"]; }
        }

        /// <summary>
        /// Gets or sets the configuration element relating to how users can connect using transport layer security (TLS) / secure sockets layer (SSL) security
        /// </summary>
        [ConfigurationProperty("ssl", IsRequired = false)]
        public SslConfigurationElement Ssl
        {
            get { return (SslConfigurationElement)base["ssl"]; }
            set { this["ssl"] = value; }
        }
    }
}
