// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IIdentity.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   An identity represents a user that can authenticate to the system and perform actions against a store
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace mcnntp.common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An identity represents a user that can authenticate to the system and perform actions against a store
    /// </summary>
    public interface IIdentity
    {
        /// <summary>
        /// Gets or sets the unique identifier for the identity
        /// </summary>
        string Id { get; set; }
        
        /// <summary>
        /// Gets or sets the unique username for the identity
        /// </summary>
        string Username { get; set; }

        string PasswordHash { get; set; }

        string PasswordSalt { get; set; }

        /// <summary>
        /// Whether or not the user can only authenticate from localhost
        /// </summary>
        bool LocalAuthenticationOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user can Approve moderated messages in any group
        /// </summary>
        bool CanApproveAny { get; set; }

        bool CanCancel { get; set; }

        bool CanCreateCatalogs { get; set; }

        bool CanDeleteCatalogs { get; set; }

        bool CanCheckCatalogs { get; set; }

        IEnumerable<ICatalog> Moderates { get; }

        DateTime? LastLogin { get; set; }
    }
}
