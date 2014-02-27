﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace McNNTP.Server.Data
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class Article
    {
        public virtual long Id { get; set; }
        [NotNull]
        public virtual Newsgroup Newsgroup { get; set; }
        // MANDATORY FIELDS
        [NotNull]
        public virtual string Date { get; set; }
        [NotNull]
        public virtual string From { get; set; }
        [NotNull]
        public virtual string MessageId { get; set; }
        [NotNull]
        public virtual string Newsgroups { get; set; }
        [NotNull]
        public virtual string Path { get; set; }
        [NotNull]
        public virtual string Subject { get; set; }
        // OPTIONAL FIELDS

        /// <summary>
        /// The Approved header field indicates the mailing addresses (and
        /// possibly the full names) of the persons or entities approving the
        /// article for posting.  Its principal uses are in moderated articles
        /// and in group control messages; see [RFC5537].
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Approved { get; set; }
        /// <summary>
        /// The Archive header field provides an indication of the poster's
        /// intent regarding preservation of the article in publicly accessible
        /// long-term or permanent storage.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Archive { get; set; }
        [CanBeNull]
        public virtual string ContentDisposition { get; set; }
        [CanBeNull]
        public virtual string ContentLanguage { get; set; }
        [CanBeNull]
        public virtual string ContentTransferEncoding { get; set; }
        [CanBeNull]
        public virtual string ContentType { get; set; }
        /// <summary>
        /// The Control header field marks the article as a control message and
        /// specifies the desired actions (in addition to the usual actions of
        /// storing and/or relaying the article).
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Control { get; set; }
        /// <summary>
        /// The Distribution header field specifies geographic or organizational
        /// limits on an article's propagation.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Distribution { get; set; }
        /// <summary>
        /// The Expires header field specifies a date and time when the poster
        /// deems the article to be no longer relevant and could usefully be
        /// removed ("expired").
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Expires { get; set; }
        /// <summary>
        /// The Followup-To header field specifies to which newsgroup(s) the
        /// poster has requested that followups are to be posted.  The
        /// Followup-To header field SHOULD NOT appear in a message, unless its
        /// content is different from the content of the Newsgroups header field.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string FollowupTo { get; set; }
        /// <summary>
        /// The Injection-Date header field contains the date and time that the
        /// article was injected into the network.  Its purpose is to enable news
        /// servers, when checking for "stale" articles, to use a &lt;date-time&gt;
        /// that was added by a news server at injection time rather than one
        /// added by the user agent at message composition time.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string InjectionDate { get; set; }
        [CanBeNull]
        public virtual string InjectionInfo { get; set; }
        [CanBeNull]
        public virtual string MIMEVersion { get; set; }
        /// <summary>
        /// The Organization header field is a short phrase identifying the
        /// poster's organization.
        /// </summary>
        [CanBeNull]
        public virtual string Organization { get; set; }
        /// <summary>
        /// The message identifier of the original
        /// message and the message identifiers of other messages (for example,
        /// in the case of a reply to a message that was itself a reply).  The
        /// "References:" field may be used to identify a "thread" of
        /// conversation.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5322"/>
        [CanBeNull]
        public virtual string References { get; set; }
        /// <summary>
        /// The Summary header field is a short phrase summarizing the article's
        /// content.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Summary { get; set; }
        /// <summary>
        /// The Supersedes header field contains a message identifier specifying
        /// an article to be superseded upon the arrival of this one.  An article
        /// containing a Supersedes header field is equivalent to a "cancel"
        /// [RFC5537] control message for the specified article, followed
        /// immediately by the new article without the Supersedes header field.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string Supersedes { get; set; }
        /// <summary>
        /// The User-Agent header field contains information about the user agent
        /// (typically a newsreader) generating the article, for statistical
        /// purposes and tracing of standards violations to specific software in
        /// need of correction.
        /// </summary>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        [CanBeNull]
        public virtual string UserAgent { get; set; }
        /// <summary>
        /// The Xref header field indicates where an article was filed by the
        /// last news server to process it.  User agents often use the
        /// information in the Xref header field to avoid multiple processing of
        /// crossposted articles.
        /// </summary>
        [CanBeNull]
        public virtual string Xref { get; set; }
        // FULL HEADERS AND BODY
        [NotNull]
        public virtual string Headers { get; set; }
        [NotNull]
        public virtual string Body { get; set; }
        
        [Pure]
        internal static bool TryParse([NotNull] string block, out Article article)
        {
            article = null;

            var headerLines = block
                    .SeekThroughDelimiters("\r\n")
                    .TakeWhile(s => !string.IsNullOrEmpty(s))
                    .ToArray();

            Dictionary<string, string> headers, headersAndFullLines;
            if (!TryParseHeaders(headerLines, out headers, out headersAndFullLines))
                return false;

            var headerLength = headerLines.Sum(hl => hl.Length + 2);

            if (headers.All(h => string.Compare(h.Key, "From", StringComparison.OrdinalIgnoreCase) != 0))
                return false;

            if (headers.All(h => string.Compare(h.Key, "Newsgroups", StringComparison.OrdinalIgnoreCase) != 0))
                return false;

            if (headers.All(h => string.Compare(h.Key, "Subject", StringComparison.OrdinalIgnoreCase) != 0))
                return false;

            string msgId;
            if (headers.Any(h => string.Compare(h.Key, "Message-ID", StringComparison.OrdinalIgnoreCase) == 0))
            {
                msgId = headers.Single(h => string.Compare(h.Key, "Message-ID", StringComparison.OrdinalIgnoreCase) == 0).Value;
                if (!msgId.IsUsenetMessageId())
                    msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.invalid>";
            }
            else
                msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.auto>";
            
            var newsgroups = headers.Single(h => string.Compare(h.Key, "Newsgroups", StringComparison.OrdinalIgnoreCase) == 0).Value;
            
            article = new Article
            {
                Approved = headers.Where(h => string.Compare(h.Key, "Approved", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Archive = headers.Where(h => string.Compare(h.Key, "Archive", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Body = block.Substring(headerLength),
                ContentDisposition = headers.Where(h => string.Compare(h.Key, "Content-Disposition", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                ContentLanguage = headers.Where(h => string.Compare(h.Key, "Content-Language", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                ContentTransferEncoding = headers.Where(h => string.Compare(h.Key, "Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                ContentType = headers.Where(h => string.Compare(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Control = headers.Where(h => string.Compare(h.Key, "Control", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value.Trim()).SingleOrDefault(),
                Date = DateTime.UtcNow.ToString("r"),
                Distribution = headers.Where(h => string.Compare(h.Key, "Distribution", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Expires = headers.Where(h => string.Compare(h.Key, "Expires", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                FollowupTo = headers.Where(h => string.Compare(h.Key, "Followup-To", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                From = headers.Single(h => string.Compare(h.Key, "From", StringComparison.OrdinalIgnoreCase) == 0).Value,
                Headers = block.Substring(0, headerLength - 2),
                InjectionDate = headers.Where(h => string.Compare(h.Key, "Injection-Date", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                MessageId = msgId,
                MIMEVersion = headers.Where(h => string.Compare(h.Key, "MIME-Version", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Newsgroups = newsgroups,
                Organization = headers.Where(h => string.Compare(h.Key, "Organization", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                References = headers.Where(h => string.Compare(h.Key, "References", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Subject = headers.Single(h => string.Compare(h.Key, "Subject", StringComparison.OrdinalIgnoreCase) == 0).Value,
                Summary = headers.Where(h => string.Compare(h.Key, "Summary", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Supersedes = headers.Where(h => string.Compare(h.Key, "Supersedes", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                UserAgent = headers.Where(h => string.Compare(h.Key, "User-Agent", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Xref = headers.Where(h => string.Compare(h.Key, "Xref", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault()
            };
            
            return true;
        }

        [Pure]
        internal static bool TryParseHeaders([NotNull] string headerBlock, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines)
        {
            var headerLines = headerBlock
                   .SeekThroughDelimiters("\r\n")
                   .TakeWhile(s => !string.IsNullOrEmpty(s))
                   .ToArray();

            return TryParseHeaders(headerLines, out headers, out headersAndFullLines);
        }

        [Pure]
        internal static bool TryParseHeaders([NotNull] string[] headerLines, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines)
        {
            headers = new Dictionary<string, string>();
            headersAndFullLines = new Dictionary<string, string>();

            // Parse headers
            for (var i = 0; i < headerLines.Length; i++)
            {
                var headerLine = headerLines[i];

                var readahead = 1;
                while (i + readahead < headerLines.Length && new[] { ' ', '\t' }.Contains(headerLines[i + readahead][0]))
                {
                    headerLine = headerLine + headerLines[i + readahead].Substring(1);
                    readahead++;
                }
                i += readahead - 1;

                var match = Regex.Match(headerLine, @"(?<key>[\x21-\x7e]+):\s+(?<value>[^\n]+$)");
                if (!match.Success)
                    return false;

                headers.Add(match.Groups["key"].Value, match.Groups["value"].Value);
                headersAndFullLines.Add(match.Groups["key"].Value, headerLine);
            }

            return true;
        }
    }
}