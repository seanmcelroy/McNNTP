using JetBrains.Annotations;
using System.Collections.Generic;

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

        /// <summary>
        /// Whether or not the administrator can Approve moderated messages
        /// in any group
        /// </summary>
        public virtual bool CanApproveAny { get; set; }
        public virtual bool CanCancel { get; set; }
        public virtual bool CanCreateGroup { get; set; }
        public virtual bool CanDeleteGroup { get; set; }
        public virtual bool CanCheckGroups { get; set; }
        /// <summary>
        /// Indicates the credential can operate as a server, such as usiing the IHAVE command
        /// </summary>
        public virtual bool CanInject { get; set; }

        public virtual IList<Newsgroup> Moderates { get; set; }
    }
}
