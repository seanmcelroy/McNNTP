// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Subscription.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   A record that indicates a user has identified a newsgroup as 'active' or 'subscribed'
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using JetBrains.Annotations;

    /// <summary>
    /// A record that indicates a user has identified a newsgroup as 'active' or 'subscribed'
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class Subscription
    {
        /// <summary>
        /// Gets or sets the unique identifier for the subscription record
        /// </summary>
        /// <remarks>This field must be unique</remarks>
        // ReSharper disable once MemberCanBeProtected.Global
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the 'owner' of the newsgroup.  Such groups are personal groups,
        /// typically personal mailboxes, and can only be enumerated by the user that owns
        /// them.
        /// </summary>
        [NotNull]
        public virtual User Owner { get; set; }

        /// <summary>
        /// Gets or sets the name of the newsgroup to which the user is subscribed
        /// </summary>
        /// <remarks>
        /// This is provided as a string and not a reference to a newsgroup object due to RFC 3501 6.3.9:
        /// The server MUST NOT unilaterally remove an existing mailbox name from the subscription list even if a mailbox by that name no longer exists.
        /// </remarks>
        [NotNull]
        public virtual string Newsgroup { get; set; }
    }
}
