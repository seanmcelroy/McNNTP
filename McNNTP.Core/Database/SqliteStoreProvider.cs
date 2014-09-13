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

    using log4net;

    using McNNTP.Common;
    using McNNTP.Data;

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
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(SqliteStoreProvider));

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
        public SqliteStoreProvider(string hierarchyDelimiter = "/")
        {
            this.hierarchyDelimiter = hierarchyDelimiter;
        }

        /// <summary>
        /// Gets the delimiter used to separate levels of a catalog hierarchy
        /// </summary>
        public string HierarchyDelimiter
        {
            get
            {
                return hierarchyDelimiter;
            }
        }

        /// <summary>
        /// Ensures a user has any requisite initialization in the store performed prior to their execution of other store methods
        /// </summary>
        /// <param name="identity">The identity of the user to ensure is initialized properly in the store</param>
        public void Ensure(IIdentity identity)
        {
            var personalCatalogs = GetPersonalCatalogs(identity, null);

            if (personalCatalogs != null && personalCatalogs.All(c => c.Name != "INBOX"))
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var ng = new Newsgroup
                             {
                                 CreateDate = DateTime.UtcNow,
                                 Description = "Personal inbox for " + identity.Username,
                                 Name = "INBOX",
                                 Owner = (User)identity
                             };

                    session.Save(ng);
                    session.Flush();
                    session.Close();
                }
            }
        }

        /// <summary>
        /// Retrieves a catalog by its name
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="name">The name of the catalog to retrieve</param>
        /// <returns>The catalog with the specified <paramref name="name"/>, if one exists</returns>
        public ICatalog GetCatalogByName(IIdentity identity, string name)
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
        public IEnumerable<ICatalog> GetGlobalCatalogs(IIdentity identity, string parentCatalogName)
        {
            IEnumerable<Newsgroup> newsGroups = null;
            if (this.HierarchyDelimiter == "NIL")
                return null;

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    newsGroups = session.Query<Newsgroup>()
                        .Where(n => n.Owner == null)
                        .ToList()
                        .Where(n =>
                            (parentCatalogName == null && (HierarchyDelimiter == "NIL" || n.Name.IndexOf(HierarchyDelimiter, StringComparison.OrdinalIgnoreCase) == -1)) ||
                            (parentCatalogName != null && (HierarchyDelimiter != "NIL" && n.Name.StartsWith(parentCatalogName + HierarchyDelimiter))))
                        .AddMetagroups(session, identity).OrderBy(n => n.Name).ToList();
                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle LIST", ex);
            }

            return newsGroups;
        }

        /// <summary>
        /// Retrieves an enumeration of personal catalogs available to an end-user at the root level in the store
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="parentCatalogName">The parent catalog.  When specified, this finds catalogs that are contained in this specified parent catalog</param>
        /// <returns>An enumeration of catalogs available to an end-user at the root level in the store</returns>
        public IEnumerable<ICatalog> GetPersonalCatalogs(IIdentity identity, string parentCatalogName)
        {
            IEnumerable<Newsgroup> newsGroups = null;

            int identityId;
            if (!int.TryParse(identity.Id, out identityId))
                return null;

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    newsGroups = session.Query<Newsgroup>()
                        .Where(n => n.Owner.Id == identityId)
                        .ToList()
                        .Where(n =>
                            (parentCatalogName == null && (HierarchyDelimiter == "NIL" || n.Name.IndexOf(HierarchyDelimiter, StringComparison.OrdinalIgnoreCase) == -1)) ||
                            (parentCatalogName != null && (HierarchyDelimiter != "NIL" && n.Name.StartsWith(parentCatalogName + HierarchyDelimiter))))
                        .OrderBy(n => n.Name).ToList();
                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle LIST", ex);
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
            if (string.Compare(catalogName, "INBOX", StringComparison.OrdinalIgnoreCase) == 0)
                return false;

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                    if (owner == null)
                        return false;

                    var catalog = session.Query<Newsgroup>().SingleOrDefault(ng => ng.Name == catalogName && ng.Owner.Id == owner.Id);
                    if (catalog != null)
                        return false;

                    catalog = new Newsgroup
                              {
                                  CreateDate = DateTime.UtcNow,
                                  CreatorEntity = identity.Username,
                                  DenyLocalPosting = false,
                                  DenyPeerPosting = true,
                                  Description = "Personal inbox for " + identity.Username,
                                  Moderated = false,
                                  Name = catalogName,
                                  Owner = owner
                              };
                    session.Save(catalog);
                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
                return false;
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle LIST", ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves a user by their clear-text username and password
        /// </summary>
        /// <param name="username">The username of the user</param>
        /// <param name="password">The clear-text password of the user</param>
        /// <returns>The user, if one was found with the matching username and password</returns>
        public IIdentity GetIdentityByClearAuth(string username, string password)
        {
            User admin;
            using (var session = SessionUtility.OpenSession())
            {
                admin = session.Query<User>().Fetch(a => a.Moderates).SingleOrDefault(a => a.Username == username);
                if (admin != null)
                {
                    admin.LastLogin = DateTime.UtcNow;
                    session.SaveOrUpdate(admin);
                }

                session.Close();
            }

            if (admin == null)
                return null;

            if (admin.PasswordHash != Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(admin.PasswordSalt, password)))))
            {
                _Logger.WarnFormat("User {0} failed authentication against local authentication database.", admin.Username);
                return null;
            }

            return admin;
        }

        /// <summary>
        /// Retrieves an enumeration of messages available in the specified catalog
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="catalogName">The name of the catalog in which to retrieve messages</param>
        /// <param name="fromId">The lower bound of the message identifier range to retrieve</param>
        /// <param name="toId">If specified, the upper bound of the message identifier range to retrieve</param>
        /// <returns>An enumeration of messages available in the specified catalog</returns>
        public IEnumerable<IMessage> GetMessages(IIdentity identity, string catalogName, int fromId, int? toId)
        {
            IList<IMessage> articleNewsgroups;

            using (var session = SessionUtility.OpenSession())
            {
                ICatalog ng = session.Query<Newsgroup>().AddMetagroups(session, identity).SingleOrDefault(n => n.Name == catalogName);

                if (toId == null)
                {
                    if (catalogName.EndsWith(".deleted"))
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Cancelled)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                    else if (catalogName.EndsWith(".pending"))
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Pending)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                    else
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name && !an.Cancelled && !an.Pending)
                            .Where(an => an.Number >= fromId)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                }
                else
                {
                    if (catalogName.EndsWith(".deleted"))
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Cancelled)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                    else if (catalogName.EndsWith(".pending"))
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name.Substring(0, ng.Name.Length - 8) && an.Pending)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                    else
                        articleNewsgroups = session.Query<ArticleNewsgroup>()
                            .Where(an => an.Newsgroup.Name == ng.Name && !an.Cancelled && !an.Pending)
                            .Where(an => an.Number >= fromId && an.Number <= toId.Value)
                            .OrderBy(an => an.Number)
                            .Cast<IMessage>()
                            .ToList();
                }

                session.Close();
            }

            return articleNewsgroups;
        }

        /// <summary>
        /// Creates a subscription for a user to a catalog, indicating it is 'active' or 'subscribed' for that user
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="catalogName">The name of the catalog in which to subscribe the user</param>
        /// <returns>A value indicating whether the operation was successful</returns>
        public bool CreateSubscription(IIdentity identity, string catalogName)
        {
            var success = false;
            
            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                    if (owner != null)
                    {
                        session.Save(new Subscription
                                     {
                                         Newsgroup = catalogName,
                                         Owner = owner
                                     });
                        success = true;
                    }

                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle SUBSCRIBE", ex);
            }

            return success;
        }

        /// <summary>
        /// Deletes a subscription for a user from a catalog, indicating it is 'active' or 'subscribed' for that user
        /// </summary>
        /// <param name="identity">The identity of the user making the request</param>
        /// <param name="catalogName">The name of the catalog in which to subscribe the user</param>
        /// <returns>A value indicating whether the operation was successful</returns>
        public bool DeleteSubscription(IIdentity identity, string catalogName)
        {
            var success = false;

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
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
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle UNSUBSCRIBE", ex);
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
            var subscriptions = new string[0];

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var owner = session.Query<User>().SingleOrDefault(u => u.Username == identity.Username);
                    if (owner != null)
                        subscriptions = session.Query<Subscription>().Where(ng => ng.Owner.Id == owner.Id).Select(ng => ng.Newsgroup).ToArray();

                    session.Close();
                }
            }
            catch (MappingException mex)
            {
                _Logger.Error("NHibernate Mapping Exception! (Is schema out of date or damaged?)", mex);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to handle LSUB", ex);
            }

            return subscriptions;
        }
    }
}
