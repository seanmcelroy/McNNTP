// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ArticleNewsgroup.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   The join table that links an article to a newsgroup for a post.  Articles may
//   have multiple <c>ArticleNewsgroup</c> entries when they are cross-posted to
//   multiple newsgroups.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// The join table that links an article to a newsgroup for a post.  Articles may
    /// have multiple <c>ArticleNewsgroup</c> entries when they are cross-posted to
    /// multiple newsgroups.
    /// </summary>
    [PublicAPI]
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ArticleNewsgroup
    {
        /// <summary>
        /// Gets or sets the auto-incrementing primary key identify for this entity
        /// </summary>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        [PublicAPI]
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the article that is posted to the newsgroup
        /// </summary>
        [NotNull]
        public virtual Article Article { get; set; }

        /// <summary>
        /// Gets or sets the newsgroup to which the article is posted
        /// </summary>
        [NotNull]
        public virtual Newsgroup Newsgroup { get; set; }

        /// <summary>
        /// Gets or sets the message number in the newsgroup for this message
        /// </summary>
        public virtual int Number { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the message has been cancelled through a control message
        /// </summary>
        public virtual bool Cancelled { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the message is pending approval from a moderator
        /// </summary>
        public virtual bool Pending { get; set; }
    }
}
