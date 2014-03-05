using McNNTP.Data;
using NHibernate;
using NHibernate.Cfg;
using System;

namespace McNNTP.Core.Database
{
    public static class SessionUtility
    {
        private static readonly Lazy<ISessionFactory> _sessionFactory = new Lazy<ISessionFactory>(() =>
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            return configuration.BuildSessionFactory();
        });

        public static ISession OpenSession()
        {
            return _sessionFactory.Value.OpenSession();
        }
    }
}
