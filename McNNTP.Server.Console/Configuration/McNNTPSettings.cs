// --------------------------------------------------------------------------------------------------------------------
// <copyright file="McNNTPSettings.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   Modern configuration settings model for McNNTP server
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Server.Console.Configuration
{
    /// <summary>
    /// Modern configuration settings model for McNNTP server
    /// </summary>
    public class McNNTPSettings
    {
        /// <summary>
        /// The configuration section name
        /// </summary>
        public const string SectionName = "McNNTP";

        /// <summary>
        /// Gets or sets the path host for message headers
        /// </summary>
        public string PathHost { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authentication configuration
        /// </summary>
        public AuthenticationSettings Authentication { get; set; } = new();

        /// <summary>
        /// Gets or sets the port configurations
        /// </summary>
        public List<PortSettings> Ports { get; set; } = new();

        /// <summary>
        /// Gets or sets the SSL configuration
        /// </summary>
        public SslSettings Ssl { get; set; } = new();
    }

    /// <summary>
    /// Authentication configuration settings
    /// </summary>
    public class AuthenticationSettings
    {
        /// <summary>
        /// Gets or sets the user directory configurations
        /// </summary>
        public List<UserDirectorySettings> UserDirectories { get; set; } = new();
    }

    /// <summary>
    /// User directory configuration settings
    /// </summary>
    public class UserDirectorySettings
    {
        /// <summary>
        /// Gets or sets the type of user directory (Local, LDAP, etc.)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the priority for this directory
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Port configuration settings
    /// </summary>
    public class PortSettings
    {
        /// <summary>
        /// Gets or sets the port number
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the SSL mode (ClearText, ImplicitTLS, ExplicitTLS)
        /// </summary>
        public string Ssl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the protocol (nntp)
        /// </summary>
        public string Protocol { get; set; } = string.Empty;
    }

    /// <summary>
    /// SSL configuration settings
    /// </summary>
    public class SslSettings
    {
        /// <summary>
        /// Gets or sets whether to generate a self-signed server certificate
        /// </summary>
        public bool GenerateSelfSignedServerCertificate { get; set; } = true;

        /// <summary>
        /// Gets or sets the server certificate thumbprint
        /// </summary>
        public string? ServerCertificateThumbprint { get; set; }
    }
}