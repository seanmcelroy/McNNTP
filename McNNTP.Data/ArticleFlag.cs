// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ArticleFlag.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   A record that indicates one user's interaction with a message
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using System;
    using System.Globalization;

    using JetBrains.Annotations;

    using McNNTP.Common;

    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    
    /// <summary>
    /// A record that indicates one user's interaction with a message
    /// </summary>
    [PublicAPI]
    public class ArticleFlag : IMessageDetail
    {
        /// <summary>
        /// Gets or sets the auto-incrementing primary key identify for this entity
        /// </summary>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        [PublicAPI]
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets the unique identifier for the message within a catalog
        /// </summary>
        string IMessageDetail.Id
        {
            get
            {
                return Id.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets or sets the newsgroup for the article
        /// </summary>
        [PublicAPI]
        public virtual int NewsgroupId { get; set; }

        /// <summary>
        /// Gets the unique identifier of the catalog containing the message
        /// </summary>
        string IMessageDetail.CatalogId
        {
            get
            {
                return NewsgroupId.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets or sets the unique identifier for the article
        /// </summary>
        [PublicAPI]
        public virtual int ArticleId { get; set; }

        /// <summary>
        /// Gets the unique identifier of the message to which this detail applies
        /// </summary>
        string IMessageDetail.MessageId
        {
            get
            {
                return ArticleId.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets or sets the unique identifier for the user
        /// </summary>
        [PublicAPI]
        public virtual int UserId { get; set; }

        /// <summary>
        /// Gets the unique identifier of the user to which any contextual elements of this metadata record apply
        /// </summary>
        string IMessageDetail.IdentityId
        {
            get
            {
                return UserId.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Gets or sets the date the user responded to the message
        /// </summary>
        [PublicAPI]
        public virtual DateTime? Answered { get; set; }

        /// <summary>
        /// Gets or sets the date the user deleted to the message
        /// </summary>
        [PublicAPI]
        public virtual DateTime? Deleted { get; set; }

        /// <summary>
        /// Gets or sets the date the user marked the message as important to the message
        /// </summary>
        [PublicAPI]
        public virtual DateTime? Important { get; set; }

        /// <summary>
        /// Gets or sets the date the user first saw to the message
        /// </summary>
        [PublicAPI]
        public virtual DateTime? Seen { get; set; }
    }
}
