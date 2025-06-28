// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DatabaseUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A set of utility classes that assist with the setup, upgrading, and use of databases
//   as brokered through the NHibernate library.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Database
{
    using System;
    using System.Data.SQLite;
    using System.Linq;
    using System.Security;

    using Microsoft.Extensions.Logging;

    using McNNTP.Data;

    using NHibernate.Cfg;
    using NHibernate.Tool.hbm2ddl;

    /// <summary>
    /// A set of utility classes that assist with the setup, upgrading, and use of databases
    /// as brokered through the NHibernate library.
    /// </summary>
    public static class DatabaseUtility
    {
        private static ILogger? _logger;

        public static void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        public static bool RebuildSchema(string? dud = null)
        {
            var configuration = CreateConfiguration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);

            using (var connection = new SQLiteConnection(configuration.GetProperty("connection.connection_string")))
            {
                connection.Open();
                try
                {
                    var export = new SchemaExport(configuration);
                    export.Execute(false, true, false, connection, null);
                    WriteBaselineData();
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Unable to rebuild the database schema", ex);
                    throw;
                }
                finally
                {
                    connection.Close();
                }
            }

            return false;
        }

        /// <summary>
        /// Updates a news database schema to the latest as dictated by the object relational model in code.
        /// </summary>
        /// <returns>A value indicating whether the update schema method was successful.</returns>
        public static bool UpdateSchema()
        {
            var configuration = CreateConfiguration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);

            try
            {
                var update = new SchemaUpdate(configuration);
                update.Execute(false, true);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Unable to update the database schema", ex);
                return false;
            }

            _logger?.LogInformation("Updated the database schema");

            WriteBaselineData();
            return true;
        }

        public static bool VerifyDatabase(bool quiet = false)
        {
            var configuration = CreateConfiguration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);

            //try
            //{
            //    var validator = new SchemaValidator(configuration);
            //    validator.Validate();
            //}
            //catch (Exception ex)
            //{
            //    _logger.Error("The database schema is out of date: " + ex.Message, ex);
            //    return false;
            //}

            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                    if (newsgroupCount == 0 && !quiet)
                    {
                        _logger?.LogWarning("Verified database has 0 newsgroups");
                    }
                    else if (!quiet)
                    {
                        _logger?.LogInformation("Verified database has {0} newsgroup{1}", newsgroupCount, newsgroupCount == 1 ? null : "s");
                    }

                    var articleCount = session.Query<Article>().Count(a => a.Headers != null);
                    var article = session.Query<Article>().FirstOrDefault(a => a.ArticleNewsgroups.Any(an => !an.Cancelled));
                    if (article != null)
                    {
                        var an = article.InjectionDate;
                        article.InjectionDate = "test";
                        session.Save(article);
                        article.InjectionDate = an;
                        session.Save(article);
                    }
                    if (!quiet)
                    {
                        _logger?.LogInformation("Verified database has {0} article{1}", articleCount, articleCount == 1 ? null : "s");
                    }

                    var adminCount = session.Query<User>().Count(a => a.CanInject);
                    if (adminCount == 0 && !quiet)
                    {
                        _logger?.LogWarning("Verified database has 0 local admins");
                    }
                    else if (!quiet)
                    {
                        _logger?.LogInformation("Verified database has {0} local admin{1}", adminCount, adminCount == 1 ? null : "s");
                    }

                    var peerCount = session.Query<Peer>().Count();
                    if (peerCount == 0 && !quiet)
                    {
                        _logger?.LogWarning("Verified database has 0 distribution patterns");
                    }
                    else if (!quiet)
                    {
                        _logger?.LogInformation("Verified database has {0} distribution pattern{1}", peerCount, peerCount == 1 ? null : "s");
                    }

                    var distPatternCount = session.Query<DistributionPattern>().Count();
                    if (distPatternCount == 0 && !quiet)
                    {
                        _logger?.LogWarning("Verified database has 0 distribution patterns");
                    }
                    else if (!quiet)
                    {
                        _logger?.LogInformation("Verified database has {0} distribution pattern{1}", distPatternCount, distPatternCount == 1 ? null : "s");
                    }

                    session.Close();

                    return newsgroupCount > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteBaselineData()
        {
            // Ensure placeholder data is there.
            using (var session = SessionUtility.OpenSession())
            {
                if (!session.Query<Newsgroup>().Any(n => n.Name == "freenews.config"))
                {
                    session.Save(new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        Description = "Control group for the repository",
                        Moderated = true,
                        Name = "freenews.config",
                    });
                    _logger?.LogInformation("Created 'freenews.config' group");
                }

                if (!session.Query<Newsgroup>().Any(n => n.Name == "freenews.misc"))
                {
                    session.Save(new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        Description = "Test group for the repository",
                        Moderated = false,
                        Name = "freenews.misc",
                    });
                    _logger?.LogInformation("Created 'freenews.misc' group");
                }

                if (!session.Query<Newsgroup>().Any(n => n.Name == "junk"))
                {
                    session.Save(new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        Description = "Junk group for the repository",
                        Moderated = true,
                        Name = "junk",
                    });
                    _logger?.LogInformation("Created 'junk' group");
                }

                if (!session.Query<User>().Any())
                {
                    var admin = new User
                    {
                        CanApproveAny = true,
                        CanCancel = true,
                        CanCheckCatalogs = true,
                        CanCreateCatalogs = true,
                        CanDeleteCatalogs = true,
                        CanInject = true,
                        LocalAuthenticationOnly = true,
                        Username = "LOCALADMIN",
                    };

                    var ss = new SecureString();
                    foreach (var c in "CHANGEME")
                    {
                        ss.AppendChar(c);
                    }

                    admin.SetPassword(ss);

                    session.Save(admin);

                    _logger?.LogInformation("Created 'LOCALADMIN' administrator with password 'CHANGEME'.  Please authenticate locally and change your password with a 'changepass' control message");
                }

                session.Close();
            }
        }

        /// <summary>
        /// Creates a programmatic NHibernate configuration to avoid XML parsing issues
        /// </summary>
        /// <returns>A configured NHibernate Configuration object</returns>
        public static Configuration CreateConfiguration()
        {
            var configuration = new Configuration();
            
            // Configure properties programmatically
            configuration.SetProperty("connection.driver_class", "NHibernate.Driver.SQLite20Driver");
            configuration.SetProperty("connection.connection_string", "Data Source=news.db;Pooling=true;FailIfMissing=false;Version=3");
            configuration.SetProperty("dialect", "NHibernate.Dialect.SQLiteDialect");
            configuration.SetProperty("query.substitutions", "true=1;false=0");
            configuration.SetProperty("show_sql", "false");
            
            return configuration;
        }
    }
}
