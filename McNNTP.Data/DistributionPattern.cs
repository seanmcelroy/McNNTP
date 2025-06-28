namespace McNNTP.Data
{
    using System.Diagnostics.CodeAnalysis;

    public class DistributionPattern
    {
        public virtual int Id { get; set; }

        public virtual int Weight { get; set; }

        [NotNull]
        public virtual string Wildmat { get; set; }

        [NotNull]
        public virtual string Distribution { get; set; }

        public virtual string? Description { get; set; }
    }
}