namespace McNNTP.Core.Server.IRC
{
    using System.Collections.Generic;

    using JetBrains.Annotations;

    internal interface IPrincipal
    {
        /// <summary>
        /// Gets the path to the principal.  If this is a local non-server connection, this will be null.
        /// </summary>
        [CanBeNull]
        Server LocalPath { get; }

        /// <summary>
        /// Retrieves the name for the principal
        /// </summary>
        [NotNull]
        string Name { get; }
    }
}
