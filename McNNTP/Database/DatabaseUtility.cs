using McNNTP.Server.Data;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace McNNTP.Database
{
    public static class DatabaseUtility
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(DatabaseUtility));

        public static void InitializeDatabase()
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

            using (var connection = new SQLiteConnection(configuration.GetProperty("connection.connection_string")))
            {
                connection.Open();
                try
                {
                    UpdateSchema();

                    // Update failed..  recreate it.
                    if (!VerifyDatabase())
                        RebuildSchema();
                    else
                        WriteBaselineData();
                }
                finally
                {
                    connection.Close();
                }
            }
        }
        public static void RebuildSchema()
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
        }
        public static bool VerifyDatabase()
        {
            try
            {
                using (var session = SessionUtility.OpenSession())
                {
                    var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                    _logger.InfoFormat("Verified database has {0} newsgroups", newsgroupCount);

                    var articleCount = session.Query<Article>().Count(a => a.Headers != null);
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
                var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                if (newsgroupCount == 0)
                    session.Save(new Newsgroup
                    {
                        CreateDate = DateTime.UtcNow,
                        Description = "Control group for the repository",
                        Moderated = true,
                        Name = "freenews.config",
                    });

                session.Close();
            }

            _logger.InfoFormat("Initial control group 'freenews.config' created.");
        }
    }
}
