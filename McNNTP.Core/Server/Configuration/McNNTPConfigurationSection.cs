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
    using JetBrains.Annotations;

    /// <summary>
    /// The primary configuration section for the server instance
    /// </summary>
    [PublicAPI]
// ReSharper disable once InconsistentNaming
    public class McNNTPConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets or sets the configuration element relating to how users can authenticate to the server instance
        /// </summary>
        [ConfigurationProperty("authentication", IsRequired = false)]
        [UsedImplicitly]
        public AuthenticationConfigurationElement Authentication
        {
            get { return (AuthenticationConfigurationElement)base["authentication"]; }
            set { this["authentication"] = value; }
        }

        /// <summary>
        /// Gets or sets the configuration element relating to how messages POSTed to this server receive their Path header
        /// </summary>
        [ConfigurationProperty("pathHost", IsRequired = true)]
        [UsedImplicitly]
        public string PathHost
        {
            get { return (string)this["pathHost"]; }
            set { this["pathHost"] = value; }
        }

        /// <summary>
        /// Gets the configuration element relating to how networking ports are made available to connect to this server instance
        /// </summary>
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public PortConfigurationElementCollection Ports
        {
            get { return (PortConfigurationElementCollection)base["ports"]; }
        }

        /// <summary>
        /// Gets or sets the configuration element relating to how users can connect using transport layer security (TLS) / secure sockets layer (SSL) security
        /// </summary>
        [ConfigurationProperty("ssl", IsRequired = false)]
        [UsedImplicitly]
        public SslConfigurationElement Ssl
        {
            get { return (SslConfigurationElement)base["ssl"]; }
            set { this["ssl"] = value; }
        }
    }
}
