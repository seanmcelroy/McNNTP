// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SessionUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A utility class that provides assistance managing and consuming NHibernate database sessions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Core.Database
{
    using System;

    using JetBrains.Annotations;

    using Data;

    using NHibernate;
    using NHibernate.Cfg;

    /// <summary>
    /// A utility class that provides assistance managing and consuming NHibernate database sessions
    /// </summary>
    public static class SessionUtility
    {
        /// <summary>
        /// A singleton instance of an NHibernate <see cref="ISessionFactory"/> built from the
        /// configuration of the application.
        /// </summary>
        private static readonly Lazy<ISessionFactory> _SessionFactory = new Lazy<ISessionFactory>(() =>
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            
            return configuration.BuildSessionFactory();
        });

        /// <summary>
        /// Builds a new session from the NHibernate session factory
        /// </summary>
        /// <returns>A new session from the NHibernate session factory</returns>
        /// <exception cref="MemberAccessException">Thrown when an error occurs trying to access the NHibernate session factory</exception>
        /// <exception cref="MissingMemberException">Thrown when an error occurs trying to access the NHibernate session factory</exception>
        /// <exception cref="InvalidOperationException">Thrown when an error occurs trying to access the NHibernate session factory</exception>
        [NotNull, Pure]
        public static ISession OpenSession()
        {
            return _SessionFactory.Value.OpenSession();
        }
    }
}
