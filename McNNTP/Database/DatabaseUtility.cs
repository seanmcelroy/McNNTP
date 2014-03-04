using System.Security;
using McNNTP.Server.Data;
using NHibernate.Cfg;
using NHibernate.Linq;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Data.SQLite;
using System.Linq;
using log4net;

namespace McNNTP.Database
{
    public static class DatabaseUtility
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(DatabaseUtility));
        
        public static bool RebuildSchema(string dud = null)
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

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
                    _logger.Error("Unable to rebuild the database schema", ex);
                    throw;
                }
                finally
                {
                    connection.Close();
                }
            }

            return false;
        }
        public static bool UpdateSchema()
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

            try
            {
                var update = new SchemaUpdate(configuration);
                update.Execute(false, true);
            }
            catch (Exception ex)
            {
                _logger.Error("Unable to update the database schema", ex);
                return false;
            }

            _logger.InfoFormat("Updated the database schema");

            WriteBaselineData();
            return true;
        }
        public static bool VerifyDatabase(bool quiet = false)
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

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
                    var all = !session.Query<Newsgroup>().Any(n => n.Name == "freenews.config");
                    if (newsgroupCount == 0 && !quiet)
                        _logger.Warn("Verified database has 0 newsgroups");
                    else if (!quiet)
                        _logger.InfoFormat("Verified database has {0} newsgroup{1}", newsgroupCount, newsgroupCount == 1 ? null : "s");

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
                        _logger.InfoFormat("Verified database has {0} article{1}", articleCount, articleCount == 1 ? null : "s");

                    var adminCount = session.Query<Administrator>().Count(a => a.CanInject);
                    if (adminCount == 0 && !quiet)
                        _logger.Warn("Verified database has 0 local admins");
                    else if (!quiet)
                        _logger.InfoFormat("Verified database has {0} local admin{1}", adminCount, adminCount == 1 ? null : "s");

                    var distPatternCount = session.Query<DistributionPattern>().Count();
                    if (distPatternCount == 0 && !quiet)
                        _logger.Warn("Verified database has 0 distribution patterns");
                    else if (!quiet)
                        _logger.InfoFormat("Verified database has {0} distribution pattern{1}", distPatternCount, distPatternCount == 1 ? null : "s");

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
                        Name = "freenews.config"
                    });
                    _logger.InfoFormat("Created 'freenews.config' group");
                }

                if (!session.Query<Newsgroup>().Any(n => n.Name == "junk"))
                { 
                    session.Save(new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        Description = "Junk group for the repository",
                        Moderated = true,
                        Name = "junk"
                    });
                    _logger.InfoFormat("Created 'junk' group");
                }

                if (!session.Query<Administrator>().Any())
                {
                    var admin = new Administrator
                    {
                        CanApproveAny = true,
                        CanCancel = true,
                        CanCheckGroups = true,
                        CanCreateGroup = true,
                        CanDeleteGroup = true,
                        CanInject = true,
                        LocalAuthenticationOnly = true,
                        Username = "LOCALADMIN"
                    };

                    var ss = new SecureString();
                    foreach (var c in "CHANGEME")
                        ss.AppendChar(c);
                    admin.SetPassword(ss);

                    session.Save(admin);

                    _logger.InfoFormat("Created 'LOCALADMIN' administrator with password 'CHANGEME'.  Please authenticate locally and change your password with a 'changepass' control message");
                }

                session.Close();
            }

            _logger.InfoFormat("Initial control group 'freenews.config' created.");
        }
    }
}
