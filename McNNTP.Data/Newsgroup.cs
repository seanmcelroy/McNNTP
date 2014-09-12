// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Newsgroup.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   A related forum for articles to be posted into and replied within
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using JetBrains.Annotations;

    using McNNTP.Common;

    using NHibernate;

    /// <summary>
    /// A related forum for articles to be posted into and replied within
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class Newsgroup : ICatalog
    {
        /// <summary>
        /// Gets or sets the unique identifier for the catalog within a store
        /// </summary>
        /// <remarks>This field must be unique</remarks>
        // ReSharper disable once MemberCanBeProtected.Global
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the catalog within a store
        /// </summary>
        /// <remarks>This field must be unique</remarks>
        string ICatalog.Id
        {
            get
            {
                return Id.ToString(CultureInfo.InvariantCulture);
            }

            set
            {
                int id;
                if (int.TryParse(value, out id))
                    Id = id;
                Id = -1;
            }
        }

        /// <summary>
        /// Gets or sets the 'owner' of the newsgroup.  Such groups are personal groups,
        /// typically personal mailboxes, and can only be enumerated by the user that owns
        /// them.
        /// </summary>
        [CanBeNull]
        public virtual User Owner { get; set; }

        /// <summary>
        /// Gets the 'owner' of the newsgroup.  Such groups are personal groups,
        /// typically personal mailboxes, and can only be enumerated by the user that owns
        /// them.
        /// </summary>
        IIdentity ICatalog.Owner
        {
            get
            {
                return Owner;
            }
        }

        /// <summary>
        /// Gets or sets the canonical name of the newsgroup
        /// </summary>
        public virtual string Name { get; set; }

        [CanBeNull]
        public virtual string Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the newsgroup is 'moderated' and requires special permission or approval to post
        /// </summary>
        public virtual bool Moderated { get; set; }

        /// <summary>
        /// Gets or sets the number of total messages in the catalog
        /// </summary>
        public virtual int MessageCount { get; set; }

        public virtual int? LowWatermark { get; set; }

        /// <summary>
        /// Gets or sets the highest message number in the catalog
        /// </summary>
        public virtual int? HighWatermark { get; set; }

        /// <summary>
        /// Gets or sets the date and time the catalog was created, if known
        /// </summary>
        public virtual DateTime CreateDate { get; set; }

        /// <summary>
        /// Gets or sets the date and time the catalog was created, if known
        /// </summary>
        DateTime? ICatalog.CreateDateUtc
        {
            get
            {
                return CreateDate;
            }

            set
            {
                CreateDate = value ?? new DateTime(1970, 1, 1);
            }
        }

        [CanBeNull]
        public virtual string CreatorEntity { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether local connections may post to this group at all
        /// </summary>
        public virtual bool DenyLocalPosting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether peers may post to this group at all
        /// </summary>
        public virtual bool DenyPeerPosting { get; set; }

        public virtual IList<User> ModeratedBy { get; set; }

        [NotNull, Pure]
        public virtual Newsgroup GetMetaCancelledGroup([NotNull] ISession session)
        {
            var counts = (object[])session.CreateQuery(
                    "select min(an.Number), max(an.Number), count(an.Id) from ArticleNewsgroup an where an.Cancelled = 1 and an.Newsgroup.Name = :NewsgroupName")
                    .SetParameter("NewsgroupName", Name)
                    .UniqueResult();

            return new Newsgroup
            {
                CreateDate = CreateDate,
                CreatorEntity = CreatorEntity,
                Description = "Cancelled posts for " + Name,
                HighWatermark = counts[1] == null ? 0 : (int)counts[1],
                Id = 0,
                LowWatermark = counts[0] == null ? 0 : (int)counts[0],
                MessageCount = Convert.ToInt32(counts[2]),
                Name = Name + ".deleted",
                Moderated = true
            };
        }

        [NotNull, Pure]
        public virtual Newsgroup GetMetaPendingGroup([NotNull] ISession session)
        {
            var counts = (object[])session.CreateQuery(
                   "select min(an.Number), max(an.Number), count(an.Id) from ArticleNewsgroup an where an.Pending = 1 and an.Newsgroup.Name = :NewsgroupName")
                   .SetParameter("NewsgroupName", Name)
                   .UniqueResult();

            return new Newsgroup
            {
                CreateDate = CreateDate,
                CreatorEntity = CreatorEntity,
                Description = "Pending posts for " + Name,
                HighWatermark = counts[1] == null ? 0 : (int)counts[1],
                Id = 0,
                LowWatermark = counts[0] == null ? 0 : (int)counts[0],
                MessageCount = Convert.ToInt32(counts[2]),
                Name = Name + ".pending",
                Moderated = true
            };
        }
    }
}
