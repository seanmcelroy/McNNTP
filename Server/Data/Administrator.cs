using JetBrains.Annotations;

namespace McNNTP.Server.Data
{
    public class Administrator
    {
        public virtual long Id { get; set; }
        [NotNull]
        public virtual string Username { get; set; }
        [NotNull]
        public virtual string PasswordHash { get; set; }
        [NotNull]
        public virtual string PasswordSalt { get; set; }

        public virtual bool CanCancel { get; set; }
        public virtual bool CanCreateGroup { get; set; }
        public virtual bool CanDeleteGroup { get; set; }
        public virtual bool CanCheckGroups { get; set; }
    }
}
