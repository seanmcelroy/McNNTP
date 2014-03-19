// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectionMetadata.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   Metadata about a connection from a client to the server instance
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Server
{
    using System.Net;

    using JetBrains.Annotations;

    /// <summary>
    /// Metadata about a connection from a client to the server instance
    /// </summary>
    public class ConnectionMetadata
    {
        /// <summary>
        /// Gets or sets the remote address of the client that is connected to the server
        /// </summary>
        public IPAddress RemoteAddress { get; set; }

        /// <summary>
        /// Gets or sets the remote port of the client that is connected to the server
        /// </summary>
        public int RemotePort { get; set; }

        /// <summary>
        /// Gets or sets the username as authenticated successfully by the client to the server, if authenticated
        /// </summary>
        [CanBeNull]
        public string AuthenticatedUsername { get; set; }
    }
}
