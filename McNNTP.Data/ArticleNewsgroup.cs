using JetBrains.Annotations;

namespace McNNTP.Data
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ArticleNewsgroup
    {
        public virtual int Id { get; set; }
        [NotNull]
        public virtual Article Article { get; set; }
        [NotNull]
        public virtual Newsgroup Newsgroup { get; set; }
        public virtual int Number { get; set; }
        // State
        /// <summary>
        /// The message has been cancelled through a control message
        /// </summary>
        public virtual bool Cancelled { get; set; }
        /// <summary>
        /// The message is pending approval from a moderator
        /// </summary>
        public virtual bool Pending { get; set; }
    }
}
