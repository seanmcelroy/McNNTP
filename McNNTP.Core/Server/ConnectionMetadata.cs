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
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;

    /// <summary>
    /// Metadata about a connection from a client to the server instance.
    /// </summary>
    public class ConnectionMetadata
    {
        /// <summary>
        /// Gets or sets the remote address of the client that is connected to the server.
        /// </summary>
        [NotNull]
        public IPAddress RemoteAddress { get; set; }

        /// <summary>
        /// Gets or sets the remote port of the client that is connected to the server.
        /// </summary>
        public int RemotePort { get; set; }

        /// <summary>
        /// Gets or sets the number of messages sent over this connection.
        /// </summary>
        public ulong SentMessageCount { get; set; }

        /// <summary>
        /// Gets or sets the amount of data sent over this connection in bytes.
        /// </summary>
        public ulong SentMessageBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of messages received over this connection.
        /// </summary>
        public ulong RecvMessageCount { get; set; }

        /// <summary>
        /// Gets or sets the amount of data received over this connection in bytes.
        /// </summary>
        public ulong RecvMessageBytes { get; set; }

        /// <summary>
        /// Gets or sets the date the connection was opened, stored as a UTC value.
        /// </summary>
        public DateTime Established { get; set; }

        /// <summary>
        /// Gets or sets the address that was listening for this connection when it was received, if this connection was an inbound address.
        /// </summary>
        public IPAddress? ListenAddress { get; set; }

        /// <summary>
        /// Gets or sets the port that was listening for this connection when it was received, if this connection was an inbound address.
        /// </summary>
        public int? ListenPort { get; set; }

        /// <summary>
        /// Gets or sets the username as authenticated successfully by the client to the server, if authenticated.
        /// </summary>
        public string? AuthenticatedUsername { get; set; }

        /// <summary>
        /// Gets or sets the name of the principal associated with this connection.
        /// </summary>
        /// <remarks>
        /// The user may have a principal but not be authenticated, such as with an anonymous user.
        /// </remarks>
        public string? PrincipalName { get; set; }

        /// <summary>
        /// Gets or sets the connection this metadata is associated with.
        /// </summary>
        [NotNull]
        public IConnection Connection { get; internal set; }
    }
}
