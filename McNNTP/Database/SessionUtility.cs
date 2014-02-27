using McNNTP.Server.Data;
using NHibernate;
using NHibernate.Cfg;

namespace McNNTP.Database
{
    public class SessionUtility
    {
        private static readonly object _sessionFactoryLock = new object();
        private static ISessionFactory _sessionFactory;

        public static ISession OpenSession()
        {
            lock (_sessionFactoryLock)
            {
                if (_sessionFactory == null)
                {
                    var configuration = new Configuration();
                    configuration.AddAssembly(typeof(Newsgroup).Assembly);
                    _sessionFactory = configuration.BuildSessionFactory();
                }
            }
            return _sessionFactory.OpenSession();
        }
    }
}
