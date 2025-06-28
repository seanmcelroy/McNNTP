namespace McNNTP.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// A message is a single communication posted to a catalog.  Posting a message to a catalog may mean it is further redistributed or transmitted
    /// to a remote system.
    /// </summary>
    public interface IMessageDetail
    {
        /// <summary>
        /// Gets the unique identifier for the message within a catalog.
        /// </summary>
        /// <remarks>This field must be unique.</remarks>
        [NotNull]
        string Id { get; }

        /// <summary>
        /// Gets the unique identifier of the catalog containing the message.
        /// </summary>
        string CatalogId { get; }

        /// <summary>
        /// Gets the unique identifier of the message to which this detail applies.
        /// </summary>
        string MessageId { get; }

        /// <summary>
        /// Gets the unique identifier of the user to which any contextual elements of this metadata record apply.
        /// </summary>
        string IdentityId { get; }

        /// <summary>
        /// Gets or sets the date the user responded to the message.
        /// </summary>
        DateTime? Answered { get; set; }

        /// <summary>
        /// Gets or sets the date the user deleted to the message.
        /// </summary>
        DateTime? Deleted { get; set; }

        /// <summary>
        /// Gets or sets the date the user marked the message as important to the message.
        /// </summary>
        DateTime? Important { get; set; }

        /// <summary>
        /// Gets or sets the date the user first saw to the message.
        /// </summary>
        DateTime? Seen { get; set; }
    }
}
