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
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using McNNTP.Common;

    /// <summary>
    /// The join table that links an article to a newsgroup for a post.  Articles may
    /// have multiple <c>ArticleNewsgroup</c> entries when they are cross-posted to
    /// multiple newsgroups.
    /// </summary>
    public class ArticleNewsgroup : IMessage
    {
        /// <summary>
        /// Gets or sets the auto-incrementing primary key identify for this entity.
        /// </summary>
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the catalog within a store.
        /// </summary>
        /// <remarks>This field must be unique.</remarks>
        string IMessage.Id => Number.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets or sets the article that is posted to the newsgroup.
        /// </summary>
        [NotNull]
        public virtual Article Article { get; set; }

        /// <summary>
        /// Gets or sets the newsgroup to which the article is posted.
        /// </summary>
        [NotNull]
        public virtual Newsgroup Newsgroup { get; set; }

        /// <summary>
        /// Gets or sets the message number in the newsgroup for this message.
        /// </summary>
        public virtual int Number { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the message has been cancelled through a control message
        /// </summary>
        public virtual bool Cancelled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the message is pending approval from a moderator.
        /// </summary>
        public virtual bool Pending { get; set; }

        /// <summary>
        /// Gets a dictionary of headers, keyed by the header name, and containing a tuple of just the header value, and the raw header line that includes the full header.
        /// </summary>
        Dictionary<string, Tuple<string, string>>? IMessage.Headers
        {
            get
            {
                Dictionary<string, string> headers, headersAndFullLines;
                return !Article.TryParseHeaders(this.Article.Headers, out headers, out headersAndFullLines)
                    ? null
                    : headersAndFullLines.ToDictionary(k => k.Key, v => new Tuple<string, string>(headers[v.Key], v.Value));
            }
        }

        /// <summary>
        /// Gets the raw header block for this message.
        /// </summary>
        string IMessage.HeaderRaw => Article.Headers;

        /// <summary>
        /// Gets the body of the message.
        /// </summary>
        string IMessage.Body => Article.Body;
    }
}
