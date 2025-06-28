// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SqliteStoreProvider.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   Defines the SqliteStoreProvider type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.Extensions.Logging;
    using System.Diagnostics.CodeAnalysis;

    using Common;
    using Data;

    using NHibernate;
    using NHibernate.Linq;

    /// <summary>
    /// A provider of a store that is backed by a SQLite database
    /// </summary>
    public class SqliteStoreProvider : IStoreProvider
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private readonly ILogger<SqliteStoreProvider> _logger;

        /// <summary>
        /// The delimiter used to separate levels of a catalog hierarchy
        /// </summary>
        private readonly string hierarchyDelimiter;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteStoreProvider"/> class.
        /// </summary>
        /// <param name="hierarchyDelimiter">
        /// The delimiter used to separate levels of a catalog hierarchy
        /// </param>
        public SqliteStoreProvider([NotNull] ILogger<SqliteStoreProvider> logger, string hierarchyDelimiter = "/")
        {
            this._logger = logger;
            this.hierarchyDelimiter = hierarchyDelimiter;
        }

        /// <summary>
        /// Gets the delimiter used to separate levels of a catalog hierarchy
        /// </summary>
        public string HierarchyDelimiter
        {
            get
            {
                return this.hierarchyDelimiter;
            }
        }

        /// <summary>
        /// Ensures a user has any requisite initialization in the store performed prior to their execution of other store methods.
        /// </summary>
        /// <param name="identity">The identity of the user to ensure is initialized properly in the store.</param>
        public void Ensure(IIdentity identity)
        {
            var personalCatalogs = this.GetPersonalCatalogs(identity, null);

            if (personalCatalogs?.All(c => c.Name != "INBOX") == true)
            {
                using var session = SessionUtility.OpenSession();
                var ng = new Newsgroup
                {
                    CreateDate = DateTime.UtcNow,
                    Description = "Personal inbox for " + identity.Username,
                    Name = "INBOX",
                    Owner = (User)identity,
                };

                session.Save(ng);
                session.Flush();
            }
        }

        /// <summary>
        /// Retrieves a catalog by its name
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="name">The name of the catalog to retrieve</param>
        /// <returns>The catalog with the specified <paramref name="name"/>, if one exists</returns>
        public ICatalog? GetCatalogByName(IIdentity identity, string name)
        {
            Newsgroup ng;
            using (var session = SessionUtility.OpenSession())
            {
                ng = session.Query<Newsgroup>().AddMetagroups(session, identity).SingleOrDefault(n => n.Name == name);
                session.Close();
            }

            return ng;
        }

        /// <summary>
        /// Retrieves an enumeration of global catalogs available to an end-user at the root level in the store
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="parentCatalogName">The parent catalog.  When specified, this finds catalogs that are contained in this specified parent catalog</param>
        /// <returns>An enumeration of catalogs available to an end-user at the root level in the store</returns>
        public IEnumerable<ICatalog>? GetGlobalCatalogs(IIdentity identity, string parentCatalogName)
        {
            IEnumerable<Newsgroup>? newsGroups = null;
            if (this.HierarchyDelimiter == "NIL")
            {
                return null;
            }

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    newsGroups = session.Query<Newsgroup>()
                        .Where(n => n.Owner == null)
                        .ToList()
                        .Where(n =>
                            (parentCatalogName == null && (this.HierarchyDelimiter == "NIL" || n.Name.IndexOf(this.HierarchyDelimiter, StringComparison.OrdinalIgnoreCase) == -1)) ||
                            (parentCatalogName != null && (this.HierarchyDelimiter != "NIL" && n.Name.StartsWith(parentCatalogName + this.HierarchyDelimiter))))
                        .AddMetagroups(session, identity).OrderBy(n => n.Name).ToList();
                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle LIST");
            }

            return newsGroups;
        }

        /// <summary>
        /// Retrieves an enumeration of personal catalogs available to an end-user at the root level in the store
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="parentCatalogName">The parent catalog.  When specified, this finds catalogs that are contained in this specified parent catalog</param>
        /// <returns>An enumeration of catalogs available to an end-user at the root level in the store</returns>
        public IEnumerable<ICatalog>? GetPersonalCatalogs(IIdentity identity, string? parentCatalogName)
        {
            IEnumerable<Newsgroup>? newsGroups = null;

            if (!int.TryParse(identity.Id, out int identityId))
            {
                return null;
            }

            try
            {
                using var session = SessionUtility.OpenSession();
                newsGroups = [.. session.Query<Newsgroup>()
                        .Where(n => n.Owner.Id == identityId)
                        .ToList()
                        .Where(n =>
                            (parentCatalogName == null && (this.HierarchyDelimiter == "NIL" || n.Name.IndexOf(this.HierarchyDelimiter, StringComparison.OrdinalIgnoreCase) == -1)) ||
                            (parentCatalogName != null && (this.HierarchyDelimiter != "NIL" && n.Name.StartsWith(parentCatalogName + this.HierarchyDelimiter))))
                        .OrderBy(n => n.Name)];
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle LIST");
            }

            return newsGroups;
        }

        /// <summary>
        /// Creates a personal catalog
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="catalogName">The name of the personal catalog</param>
        /// <returns>A value indicating whether the operation was successful</returns>
        public bool CreatePersonalCatalog(IIdentity identity, string catalogName)
        {
            if (string.Equals(catalogName, "INBOX", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                    if (owner == null)
                    {
                        return false;
                    }

                    var catalog = session.Query<Newsgroup>().SingleOrDefault(ng => ng.Name == catalogName && ng.Owner.Id == owner.Id);
                    if (catalog != null)
                    {
                        return false;
                    }

                    catalog = new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        CreatorEntity = identity.Username,
                        DenyLocalPosting = false,
                        DenyPeerPosting = true,
                        Description = "Personal inbox for " + identity.Username,
                        Moderated = false,
                        Name = catalogName,
                        Owner = owner,
                    };
                    session.Save(catalog);
                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle LIST");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves a user by their clear-text username and password.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <param name="password">The clear-text password of the user.</param>
        /// <returns>The user, if one was found with the matching username and password.</returns>
        public IIdentity? GetIdentityByClearAuth(string username, string password)
        {
            User? admin;
            using (var session = SessionUtility.OpenSession())
            {
                admin = session.Query<User>().Fetch(a => a.Moderates).SingleOrDefault(a => a.Username == username);
                if (admin != null)
                {
                    admin.LastLogin = DateTime.UtcNow;
                    session.SaveOrUpdate(admin);
                }
            }

            if (admin == null)
            {
                return null;
            }

            if (admin.PasswordHash != Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(string.Concat(admin.PasswordSalt, password)))))
            {
                _logger.LogWarning("User {0} failed authentication against local authentication database.", admin.Username);
                return null;
            }

            return admin;
        }

        /// <summary>
        /// Retrieves an enumeration of messages available in the specified catalog.
        /// </summary>
        /// <param name="identity">The identity of the user making the request.</param>
        /// <param name="catalogName">The name of the catalog in which to retrieve messages.</param>
        /// <param name="fromId">The lower bound of the message identifier range to retrieve.</param>
        /// <param name="toId">If specified, the upper bound of the message identifier range to retrieve.</param>
        /// <returns>An enumeration of messages available in the specified catalog.</returns>
        public IEnumerable<IMessage> GetMessages(IIdentity identity, string catalogName, int fromId, int? toId)
        {
            IList<IMessage> articleNewsgroups;

            using (var session = SessionUtility.OpenSession())
            {
                var ng = session.Query<Newsgroup>().AddMetagroups(session, identity).SingleOrDefault(n => n.Name == catalogName);
                if (ng == null)
                {
                    return [];
                }

                if (toId == null)
                    {
                        if (catalogName.EndsWith(".deleted"))
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Cancelled)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                        else if (catalogName.EndsWith(".pending"))
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Pending)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                        else
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name && !an.Cancelled && !an.Pending)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                    }
                    else
                    {
                        if (catalogName.EndsWith(".deleted"))
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Cancelled)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                        else if (catalogName.EndsWith(".pending"))
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Pending)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                        else
                        {
                            articleNewsgroups = [.. session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name && !an.Cancelled && !an.Pending)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()];
                        }
                    }

                session.Close();
            }

            return articleNewsgroups;
        }

        /// <summary>
        /// Creates a subscription for a user to a catalog, indicating it is 'active' or 'subscribed' for that user.
        /// </summary>
        /// <param name="identity">The identity of the user making the request.</param>
        /// <param name="catalogName">The name of the catalog in which to subscribe the user.</param>
        /// <returns>A value indicating whether the operation was successful.</returns>
        public bool CreateSubscription(IIdentity identity, string catalogName)
        {
            var success = false;

            try
            {
                using var session = SessionUtility.OpenSession();
                var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                if (owner != null)
                {
                    session.Save(new Subscription
                    {
                        Newsgroup = catalogName,
                        Owner = owner,
                    });
                    success = true;
                }
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle SUBSCRIBE");
            }

            return success;
        }

        /// <summary>
        /// Deletes a subscription for a user from a catalog, indicating it is 'active' or 'subscribed' for that user
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="catalogName">The name of the catalog in which to subscribe the user</param>
        /// <returns>A value indicating whether the operation was successful</returns>
        public bool DeleteSubscription(IIdentity? identity, string catalogName)
        {
            var success = false;

            try
            {
                using var session = SessionUtility.OpenSession();
                var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                if (owner != null)
                {
                    var sub = session.Query<Subscription>().SingleOrDefault(s => s.Owner.Id == owner.Id && s.Newsgroup == catalogName);
                    if (sub != null)
                    {
                        session.Delete(sub);
                        session.Flush();
                    }

                    success = true;
                }

                session.Close();
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle UNSUBSCRIBE");
            }

            return success;
        }

        /// <summary>
        /// Retrieves the list of catalogs a user has identified as 'active' or 'subscribed' for themselves
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <returns>A list of catalog names that are subscribed to by the specified <paramref name="identity"/></returns>
        public IEnumerable<string> GetSubscriptions(IIdentity identity)
        {
            var subscriptions = Array.Empty<string>();

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                    if (owner != null)
                    {
                        subscriptions = [.. session.Query<Subscription>().Where(ng => ng.Owner.Id == owner.Id).Select(ng => ng.Newsgroup)];
                    }

                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _logger.LogError(mex, "NHibernate Mapping Exception! (Is schema out of date or damaged?)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when trying to handle LSUB");
            }

            return subscriptions;
        }

        /// <summary>
        /// Retrieves an enumeration of message details available in the specified catalog.
        /// </summary>
        /// <param name="identity">The identity of the user making the request.</param>
        /// <param name="catalogName">The name of the catalog in which to retrieve message details.</param>
        /// <param name="fromId">The lower bound of the message identifier range to retrieve.</param>
        /// <param name="toId">If specified, the upper bound of the message identifier range to retrieve.</param>
        /// <returns>An enumeration of message details available in the specified catalog.</returns>
        public IEnumerable<IMessageDetail> GetMessageDetails(IIdentity? identity, string catalogName, int fromId, int? toId)
        {
            // TODO: Add flags for virtual metagroups.
            IList<IMessageDetail> articleFlags;

            using (var session = SessionUtility.OpenSession())
            {
                var ng = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == catalogName);

                if (toId == null)
                {
                    articleFlags = [.. session.Query<ArticleFlag>()
                        .Where(af => af.Id == ng.Id)
                        .Where(af => af.Id >= fromId)
                        .OrderBy(af => af.Id)
                        .Cast<IMessageDetail>()];
                }
                else
                {
                    articleFlags = [.. session.Query<ArticleFlag>()
                        .Where(af => af.Id == ng.Id)
                        .Where(af => af.Id >= fromId && af.Id <= toId.Value)
                        .OrderBy(af => af.Id)
                        .Cast<IMessageDetail>()];
                }

                session.Close();
            }

            return articleFlags;
        }
    }
}
