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

        [CanBeNull]
        public virtual string CanApproveGroups { get; set; }
        public virtual bool CanCancel { get; set; }
        public virtual bool CanCreateGroup { get; set; }
        public virtual bool CanDeleteGroup { get; set; }
        public virtual bool CanCheckGroups { get; set; }
        /// <summary>
        /// Indicates the credential can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public virtual bool CanInject { get; set; }
    }
}
