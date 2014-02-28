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

            return true;
        }
        public static void UpdateSchema()
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

            using (var connection = new SQLiteConnection(configuration.GetProperty("connection.connection_string")))
            {
                connection.Open();
                try
                {
                    var update = new SchemaUpdate(configuration);
                    update.Execute(false, true);
                }
                catch (Exception ex)
                {
                    _logger.Error("Unable to update the database schema", ex);
                    throw;
                }
                finally
                {
                    connection.Close();
                }
            }

            _logger.InfoFormat("Updated the database schema");

            WriteBaselineData();
        }
        public static bool VerifyDatabase()
        {
            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                    var all = !session.Query<Newsgroup>().Any(n => n.Name == "freenews.config");
                    _logger.InfoFormat("Verified database has {0} newsgroups", newsgroupCount);

                    var articleCount = session.Query<Article>().Count(a => a.Headers != null);
                    var article = session.Query<Article>().FirstOrDefault();
                    if (article != null)
                    {
                        var an = article.Number;
                        article.Number = -1;
                        session.Save(article);
                        article.Number = an;
                        session.Save(article);
                    }
                    _logger.InfoFormat("Verified database has {0} articles", articleCount);

                    var adminCount = session.Query<Administrator>().Count(a => a.CanInject);
                    _logger.InfoFormat("Verified database has {0} local admins", adminCount);

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

                session.Close();
            }

            _logger.InfoFormat("Initial control group 'freenews.config' created.");
        }
    }
}
