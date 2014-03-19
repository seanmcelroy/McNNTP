// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Peer.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A peer is a remote server to which a local server instance can connect to exchange
//   articles
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using System.Diagnostics.CodeAnalysis;

    using JetBrains.Annotations;

    /// <summary>
    /// A peer is a remote server to which a local server instance can connect to exchange
    /// articles
    /// </summary>
    [PublicAPI]
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class Peer
    {
        /// <summary>
        /// Gets or sets the auto-incrementing primary key identify for this entity
        /// </summary>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        [PublicAPI]
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the hostname of the remote peer
        /// </summary>
        [PublicAPI]
        public virtual string Hostname { get; set; }

        /// <summary>
        /// Gets or sets the port to use for connecting to the remote peer
        /// </summary>
        [PublicAPI]
        public virtual int Port { get; set; }

        /// <summary>
        /// Gets or sets a wildmat that matches newsgroups for which this server
        /// shall actively "suck" articles into its local store
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        [PublicAPI]
        public virtual string ActiveReceiveDistribution { get; set; }

        /// <summary>
        /// Gets or sets a wildmat that matches newsgroups for which this server
        /// shall passively accept articles presented to it using
        /// the IHAVE commands.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        [PublicAPI]
        public virtual string PassiveReceiveDistribution { get; set; }

        /// <summary>
        /// Gets or sets a wildmat that matches newsgroups for which this server
        /// shall present articles into its local store
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        [PublicAPI]
        public virtual string SendDistribution { get; set; }
    }
}
