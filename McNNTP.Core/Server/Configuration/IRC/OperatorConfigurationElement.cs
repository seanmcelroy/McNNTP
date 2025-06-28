// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LocalDirectoryConfigurationElement.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   This element allows the server to accept a user as an 'operator'
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server.Configuration.IRC
{
    using System.Configuration;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// A configuration reference to the local user directory located within the news database.
    /// </summary>
    public class OperatorConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the allowed hostmask or CIDR-notation IP block for sources supplying the OPER command.
        /// </summary>
        [ConfigurationProperty("hostmask", IsRequired = true)]
        [NotNull]
        public string HostMask
        {
            get { return (string)this["hostmask"]; }
            set { this["hostmask"] = value; }
        }

        /// <summary>
        /// Gets or sets the nickname for the user permitted to authenticate with this configuration line.
        /// </summary>
        [ConfigurationProperty("name", IsRequired = true)]
        [NotNull]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the password of the user.
        /// </summary>
        [ConfigurationProperty("sha256PasswordHash", IsRequired = true)]
        [NotNull]
        public string Sha256HashedPassword
        {
            get { return (string)this["sha256PasswordHash"]; }
            set { this["sha256PasswordHash"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the nickname for the user permitted to authenticate with this configuration line.
        /// </summary>
        [ConfigurationProperty("global", IsRequired = false)]
        public bool Global
        {
            get { return (bool)this["global"]; }
            set { this["global"] = value; }
        }
    }
}
