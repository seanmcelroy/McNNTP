// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IStoreProvider.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   An IStoreProvider is an implementation that provides access to catalogs and messages within those catalogs
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Common
{
    using System.Collections.Generic;

    using JetBrains.Annotations;

    /// <summary>
    /// An IStoreProvider is an implementation that provides access to catalogs and messages within those catalogs
    /// </summary>
    /// <remarks>
    /// This abstraction can allow multiple sources of data (databases, newsRC files, mailboxes) to be represented generically
    /// such that multiple mediums can operate over them
    /// </remarks>
    public interface IStoreProvider
    {
        /// <summary>
        /// Ensures a user has any requisite initialization in the store performed prior to their execution of other store methods
        /// </summary>
        /// <param name="identity">The identity of the user to ensure is initialized properly in the store</param>
        void Ensure(IIdentity identity);

        /// <summary>
        /// Retrieves a catalog by its name
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="name">The name of the catalog to retrieve</param>
        /// <returns>The catalog with the specified <paramref name="name"/>, if one exists</returns>
        [CanBeNull, Pure]
        ICatalog GetCatalogByName(IIdentity identity, string name);

        /// <summary>
        /// Retrieves an enumeration of global catalogs available to an end-user at the root level in the store
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="parentCatalogName">The parent catalog.  When specified, this finds catalogs that are contained in this specified parent catalog</param>
        /// <returns>An enumeration of catalogs available to an end-user at the root level in the store</returns>
        [CanBeNull, Pure]
        IEnumerable<ICatalog> GetGlobalCatalogs(IIdentity identity, [CanBeNull] string parentCatalogName = null);

        /// <summary>
        /// Retrieves an enumeration of personal catalogs available to an end-user at the root level in the store
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="parentCatalogName">The parent catalog.  When specified, this finds catalogs that are contained in this specified parent catalog</param>
        /// <returns>An enumeration of catalogs available to an end-user at the root level in the store</returns>
        [CanBeNull, Pure]
        IEnumerable<ICatalog> GetPersonalCatalogs(IIdentity identity, string parentCatalogName = null);

        /// <summary>
        /// Retrieves a user by their clear-text username and password
        /// </summary>
        /// <param name="username">The username of the user</param>
        /// <param name="password">The clear-text password of the user</param>
        /// <returns>The user, if one was found with the matching username and password</returns>
        [CanBeNull]
        IIdentity GetIdentityByClearAuth(string username, string password);

        /// <summary>
        /// Retrieves an enumeration of messages available in the specified catalog
        /// </summary>
        /// <param name="catalogName">The name of the catalog in which to retrieve messages</param>
        /// <returns>An enumeration of messages available in the specified catalog</returns>
        [CanBeNull]
        [Pure]
        IEnumerable<IMessage> GetMessages(IIdentity identity, string catalogName, int fromId, int? toId);
    }
}
