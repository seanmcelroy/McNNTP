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

    using JetBrains.Annotations;

    using log4net;

    using McNNTP.Common;
    using McNNTP.Data;

    using NHibernate;
    using NHibernate.Linq;

    public class SqliteStoreProvider : IStoreProvider
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(SqliteStoreProvider));

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

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    newsGroups = session.Query<Newsgroup>().Where(n => n.Owner == null).AddMetagroups(session, identity).OrderBy(n => n.Name).ToList();
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
                    newsGroups = session.Query<Newsgroup>().Where(n => n.Owner.Id == identityId).OrderBy(n => n.Name).ToList();
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

        [CanBeNull]
        public IEnumerable<IMessage> GetMessages(IIdentity identity, string catalogName, int fromId, int? toId)
        {
            IList<IMessage> articleNewsgroups = null;

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
    }
}
