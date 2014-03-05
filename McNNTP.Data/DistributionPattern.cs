using JetBrains.Annotations;

namespace McNNTP.Data
{
    public class DistributionPattern
    {
        public virtual int Id { get; set; }
        public virtual int Weight { get; set; }

        [NotNull]
        public virtual string Wildmat { get; set; }

        [NotNull]
        public virtual string Distribution { get; set; }
    }
}