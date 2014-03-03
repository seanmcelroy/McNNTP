using System;
using System.Linq;
using JetBrains.Annotations;
using System.Collections.Generic;
using NHibernate;
using NHibernate.Linq;

namespace McNNTP.Server.Data
{
    public class Newsgroup
    {
        public virtual long Id { get; set; }
        [NotNull]
        public virtual string Name { get; set; }
        [CanBeNull]
        public virtual string Description { get; set; }
        public virtual bool Moderated { get; set; }
        public virtual int PostCount { get; set; }
        public virtual int? LowWatermark { get; set; }
        public virtual int? HighWatermark { get; set; }
        public virtual DateTime CreateDate { get; set; }
        [CanBeNull]
        public virtual string CreatorEntity { get; set; }

        public virtual IList<Newsgroup> ModeratedBy { get; set; }


        [NotNull, Pure]
        public virtual Newsgroup GetMetaCancelledGroup([NotNull] ISession session)
        {
            return new Newsgroup
            {
                CreateDate = CreateDate,
                CreatorEntity = CreatorEntity,
                Description = "Cancelled posts for " + Name,
                HighWatermark = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name && a.Cancelled).Max(a => (int?)a.Number) ?? 0,
                Id = 0,
                LowWatermark = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name && a.Cancelled).Min(a => (int?)a.Number) ?? 0,
                PostCount = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name).Count(a => a.Cancelled),
                Name = Name + ".deleted",
                Moderated = true
            };
        }

        [NotNull, Pure]
        public virtual Newsgroup GetMetaPendinGroup([NotNull] ISession session)
        {
            return new Newsgroup
            {
                CreateDate = CreateDate,
                CreatorEntity = CreatorEntity,
                Description = "Pending posts for " + Name,
                HighWatermark = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name && a.Pending).Max(a => (int?)a.Number) ?? 0,
                Id = 0,
                LowWatermark = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name && a.Pending).Min(a => (int?)a.Number) ?? 0,
                PostCount = session.Query<Article>().Fetch(a => a.Newsgroup).Where(a => a.Newsgroup.Name == Name).Count(a => a.Pending),
                Name = Name + ".pending",
                Moderated = true
            };
        }
    }
}
