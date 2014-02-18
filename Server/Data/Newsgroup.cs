using System;
using JetBrains.Annotations;

namespace McNNTP.Server.Data
{
    public class Newsgroup
    {
        public virtual long Id { get; set; }
        [NotNull]
        public virtual string Name { get; set; }
        [CanBeNull]
        public virtual string Description { get; set; }
        public virtual int PostCount { get; set; }
        public virtual int? LowWatermark { get; set; }
        public virtual int? HighWatermark { get; set; }
        public virtual DateTime CreateDate { get; set; }
    }
}
