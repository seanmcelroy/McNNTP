// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LdapUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A utility class that provides helper methods for dealing with the Lightweight Directory Access Protocol (LDAP)
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Common
{
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.DirectoryServices.AccountManagement;
    using System.Linq;

    using JetBrains.Annotations;
    using log4net;

    /// <summary>
    /// A utility class that provides helper methods for dealing with the Lightweight Directory Access Protocol (LDAP)
    /// </summary>
    public static class LdapUtility
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LdapUtility));
        
        /// <summary>
        /// Retrieves the distinguished name (DN) of the specified user from the LDAP server
        /// </summary>
        /// <param name="ldapServer">The hostname or IP address of the LDAP server to query</param>
        /// <param name="searchPath">The search path for the LDAP query</param>
        /// <param name="lookupUsername">The username to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="lookupPassword">The password to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="searchUser">The username to find in the LDAP server</param>
        /// <returns>If the user was found in the LDAP server, the distinguished name of the user is returned.  Otherwise, 'null' is returned.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null</exception>
        [CanBeNull, Pure, UsedImplicitly]
        public static string GetUserDistinguishedName(
            [NotNull] string ldapServer,
            [CanBeNull] string searchPath,
            [NotNull] string lookupUsername,
            [NotNull] string lookupPassword,
            [NotNull] string searchUser)
        {
            if (string.IsNullOrWhiteSpace(ldapServer))
                throw new ArgumentNullException("ldapServer");
            if (string.IsNullOrWhiteSpace(lookupUsername))
                throw new ArgumentNullException("lookupUsername");
            if (string.IsNullOrWhiteSpace(lookupPassword))
                throw new ArgumentNullException("lookupPassword");
            if (string.IsNullOrWhiteSpace(searchUser))
                throw new ArgumentNullException("searchUser");

            using (var entry = new DirectoryEntry(
                searchPath == null
                    ? string.Format("LDAP://{0}", ldapServer)
                    : string.Format("LDAP://{0}/{1}", ldapServer, searchPath),
                lookupUsername, 
                lookupPassword))
            {
                var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(&(objectClass=User)(sAMAccountName=" + searchUser + "))",
                    SearchScope = SearchScope.Subtree
                };

                try
                {
                    var result = searcher.FindOne();
                    if (result == null)
                        return null;
                    var userEntry = result.GetDirectoryEntry();
                    return (string)userEntry.Properties["distinguishedName"].Value;
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to connect to configured LDAP directory", ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Retrieves the list of group names of which the user is a member in the LDAP server instance
        /// </summary>
        /// <param name="ldapServer">The hostname or IP address of the LDAP server to query</param>
        /// <param name="lookupUsername">The username to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="lookupPassword">The password to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="searchUser">The username to find in the LDAP server</param>
        /// <returns>The list of group names of which the user is a member in the LDAP server instance</returns>
        /// <exception cref="MultipleMatchesException">Thrown when more than one user principal exists in the LDAP server instance for the given <paramref name="searchUser"/></exception>
        /// <exception cref="PrincipalOperationException">Thrown when the authentication creates a secondary token to the LDAP server that cannot be used to authenticate for LDAP</exception>
        [NotNull, Pure]
        public static IEnumerable<string> GetUserGroupMemberships(
            string ldapServer,
            string lookupUsername, 
            string lookupPassword, 
            string searchUser)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, ldapServer, lookupUsername, lookupPassword))
                using (var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, searchUser))
                {
                    if (user == null) return new List<string>();

                    var groups = user.GetAuthorizationGroups();
                    var principals = groups.OfType<GroupPrincipal>();
                    return principals.Select(g => g.Name).ToList();
                }
            }
            catch (ArgumentNullException)
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Checks whether the specified username exists in the LDAP directory
        /// </summary>
        /// <param name="ldapServer">The hostname or IP address of the LDAP server to query</param>
        /// <param name="searchPath">The search path for the LDAP query</param>
        /// <param name="lookupUsername">The username to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="lookupPassword">The password to use to authenticate to the LDAP server to perform the query</param>
        /// <param name="searchUser">The username to find in the LDAP server</param>
        /// <returns>A value indicating whether or not the user exists in the LDAP directory</returns>
        [Pure]
        public static bool UserExists(
            string ldapServer,
            string searchPath,
            string lookupUsername, 
            string lookupPassword, 
            string searchUser)
        {
            return GetUserDistinguishedName(ldapServer, searchPath, lookupUsername, lookupPassword, searchUser) != null;
        }

        /// <summary>
        /// Attempts to perform username and password authentication against an LDAP server instance
        /// </summary>
        /// <param name="ldapServer">The hostname or IP address of the LDAP server to query</param>
        /// <param name="searchPath">The search path for the LDAP query</param>
        /// <param name="searchUser">The username to use to authenticate to the LDAP server</param>
        /// <param name="searchPassword">The password to use to authenticate to the LDAP server</param>
        /// <returns>True if the credentials provided successfully authenticated to the LDAP server; otherwise, false.</returns>
        public static bool AuthenticateUser(string ldapServer, string searchPath, string searchUser, string searchPassword)
        {
            try
            {
                var entry =
                    new DirectoryEntry(
                        searchPath == null
                            ? string.Format("LDAP://{0}", ldapServer)
                            : string.Format("LDAP://{0}/{1}", ldapServer, searchPath),
                        searchUser,
                        searchPassword);

                object obj;
                try
                {
                    obj = entry.NativeObject;
                }
                catch (DirectoryServicesCOMException)
                {
                    return false;
                }

                return obj != null;
            }
            catch (ArgumentNullException ane)
            {
                Logger.Error("Unable to authenticate the user due to a system or program error", ane);
            }
            catch (ArgumentException ae)
            {
                Logger.Error("Unable to authenticate the user due to a system or program error", ae);
            }
            catch (FormatException fe)
            {
                Logger.Error("Unable to authenticate the user due to a system or program error", fe);
            }

            return false;
        }
    }
}
