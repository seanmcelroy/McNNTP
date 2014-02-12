using System;

namespace McNNTP.Server.Data
{
    public class Newsgroup
    {
        public virtual long Id { get; set; }
        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public virtual int PostCount { get; set; }
        public virtual int? LowWatermark { get; set; }
        public virtual int? HighWatermark { get; set; }
        public virtual DateTime CreateDate { get; set; }
    }
}
