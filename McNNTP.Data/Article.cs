﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Article.cs" company="Copyright Sean McElroy">
//   Copyright Sean McElroy
// </copyright>
// <summary>
//   Defines the Article type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text.RegularExpressions;

    using McNNTP.Common;

    public class Article
    {
        /// <summary>
        /// Gets or sets the auto-incrementing primary key identify for this entity.
        /// </summary>
        public virtual int Id { get; set; }

        /// <summary>
        /// Gets or sets the Date header field that indicates the date the article was composed.
        /// </summary>
        /// <remarks>This is a required header.</remarks>
        [NotNull]
        public virtual string Date { get; set; }

        /// <summary>
        /// Gets or sets the date as a native value, parsed from the Date header.
        /// </summary>
        public virtual DateTime? DateTimeParsed { get; set; }

        /// <summary>
        /// Gets or sets the From header field that indicates who authored the article.
        /// </summary>
        /// <remarks>This is a required header.</remarks>
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

        /// <summary>
        /// Gets or sets the Approved header field indicates the mailing addresses (and
        /// possibly the full names) of the persons or entities approving the
        /// article for posting.  Its principal uses are in moderated articles
        /// and in group control messages; see [RFC5537].
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Approved { get; set; }

        /// <summary>
        /// Gets or sets the Archive header field provides an indication of the poster's
        /// intent regarding preservation of the article in publicly accessible
        /// long-term or permanent storage.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Archive { get; set; }

        public virtual string? ContentDisposition { get; set; }

        public virtual string? ContentLanguage { get; set; }

        public virtual string? ContentTransferEncoding { get; set; }

        public virtual string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the Control header field marks the article as a control message and
        /// specifies the desired actions (in addition to the usual actions of
        /// storing and/or relaying the article).
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Control { get; set; }

        /// <summary>
        /// Gets or sets the Distribution header field specifies geographic or organizational
        /// limits on an article's propagation.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Distribution { get; set; }

        /// <summary>
        /// Gets or sets the Expires header field specifies a date and time when the poster
        /// deems the article to be no longer relevant and could usefully be
        /// removed ("expired").
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Expires { get; set; }

        /// <summary>
        /// Gets or sets the Followup-To header field specifies to which newsgroup(s) the
        /// poster has requested that followups are to be posted.  The
        /// Followup-To header field SHOULD NOT appear in a message, unless its
        /// content is different from the content of the Newsgroups header field.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? FollowupTo { get; set; }

        /// <summary>
        /// Gets or sets the Injection-Date header field contains the date and time that the
        /// article was injected into the network.  Its purpose is to enable news
        /// servers, when checking for "stale" articles, to use a &lt;date-time&gt;
        /// that was added by a news server at injection time rather than one
        /// added by the user agent at message composition time.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? InjectionDate { get; set; }

        public virtual string? InjectionInfo { get; set; }

        public virtual string? MIMEVersion { get; set; }

        /// <summary>
        /// Gets or sets the Organization header field is a short phrase identifying the
        /// poster's organization.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        public virtual string? Organization { get; set; }

        /// <summary>
        /// Gets or sets the message identifier of the original
        /// message and the message identifiers of other messages (for example,
        /// in the case of a reply to a message that was itself a reply).  The
        /// "References:" field may be used to identify a "thread" of
        /// conversation.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5322"/>
        public virtual string? References { get; set; }

        /// <summary>
        /// Gets or sets the Summary header field is a short phrase summarizing the article's
        /// content.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Summary { get; set; }

        /// <summary>
        /// Gets or sets the Supersedes header field contains a message identifier specifying
        /// an article to be superseded upon the arrival of this one.  An article
        /// containing a Supersedes header field is equivalent to a "cancel"
        /// [RFC5537] control message for the specified article, followed
        /// immediately by the new article without the Supersedes header field.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? Supersedes { get; set; }

        /// <summary>
        /// Gets or sets the User-Agent header field contains information about the user agent
        /// (typically a newsreader) generating the article, for statistical
        /// purposes and tracing of standards violations to specific software in
        /// need of correction.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        /// <seealso cref="http://tools.ietf.org/html/rfc5536#section-3.2"/>
        public virtual string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the Xref header field indicates where an article was filed by the
        /// last news server to process it.  User agents often use the
        /// information in the Xref header field to avoid multiple processing of
        /// crossposted articles.
        /// </summary>
        /// <remarks>This is an optional header.</remarks>
        public virtual string Xref { get; set; }

        // FULL HEADERS AND BODY
        [NotNull]
        public virtual string Headers { get; set; }

        [NotNull]
        public virtual string Body { get; set; }

        /// <summary>
        /// Gets or sets the newsgroups to which this message has been posted
        /// </summary>
        public virtual ICollection<ArticleNewsgroup> ArticleNewsgroups { get; set; }

        public virtual string? GetHeader(string headerName)
        {
            switch (headerName.ToUpperInvariant())
            {
                case "APPROVED":
                    return this.Approved;
                case "CONTROL":
                    return this.Control;
                case "INJECTIONDATE":
                    return this.InjectionDate;
                case "DATE":
                    return this.Date;
                case "DISTRIBUTION":
                    return this.Distribution;
                case "FROM":
                    return this.From;
                case "MESSAGE-ID":
                    return this.MessageId;
                case "ORGANIZATION":
                    return this.Organization;
                case "REFERENCES":
                    return this.References;
                case "SUBJECT":
                    return this.Subject;
                case "USERAGENT":
                    return this.UserAgent;
                case "XREF":
                    return this.Xref;
                default:
                    {
                        Dictionary<string, string> headers, headersAndFullLines;
                        if (!TryParseHeaders(this.Headers, out headers, out headersAndFullLines))
                        {
                            return null;
                        }

                        if (headers.ContainsKey(headerName.ToUpperInvariant()))
                        {
                            return headers[headerName.ToUpperInvariant()];
                        }

                        return null;
                    }
            }
        }

        public virtual string GetHeaderFullLine(string headerName)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (!TryParseHeaders(this.Headers, out headers, out headersAndFullLines))
            {
                return null;
            }

            return headers.ContainsKey(headerName.ToUpperInvariant()) ? headersAndFullLines[headerName.ToUpperInvariant()] : null;
        }

        [Pure]
        public static bool TryParse([NotNull] string block, out Article article)
        {
            article = null;

            var headerLines = block
                    .SeekThroughDelimiters("\r\n")
                    .TakeWhile(s => !string.IsNullOrEmpty(s))
                    .ToArray();

            Dictionary<string, string> headers, headersAndFullLines;
            if (!TryParseHeaders(headerLines, out headers, out headersAndFullLines))
            {
                return false;
            }

            var headerLength = headerLines.Sum(hl => hl.Length + 2);

            if (!headers.TryGetValue("FROM", out string? value))
            {
                return false;
            }

            // Validate From: header against RFC 5322 3.4
            if (!Regex.IsMatch(value, @"((\s*\w+)*\s+<[^@]+@[^>]+>|[^@]+@[^>]+)(\s*,\s*((\s*\w+)*\s+<[^@]+@[^>]+>|[^@]+@[^>]+))*"))
            {
                return false;
            }

            if (!headers.ContainsKey("NEWSGROUPS"))
            {
                return false;
            }

            if (!headers.ContainsKey("SUBJECT"))
            {
                return false;
            }

            string msgId;
            if (headers.ContainsKey("MESSAGE-ID"))
            {
                msgId = headers.Single(h => string.Equals(h.Key, "Message-ID", StringComparison.OrdinalIgnoreCase)).Value;
                if (!msgId.IsUsenetMessageId())
                {
                    msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.invalid>";
                }
            }
            else
            {
                msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.auto>";
            }

            var newsgroups = headers.Single(h => string.Equals(h.Key, "Newsgroups", StringComparison.OrdinalIgnoreCase)).Value;

            var body = (block.Length <= headerLength + 2)
                ? string.Empty // Usenet explorer can post a completely empty body.
                : block.Substring(headerLength + 2); // Eliminate headers + terminating \r\n

            article = new Article
            {
                Approved = headers.Where(h => string.Equals(h.Key, "Approved", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Archive = headers.Where(h => string.Equals(h.Key, "Archive", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Body = body,
                ContentDisposition = headers.Where(h => string.Equals(h.Key, "Content-Disposition", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                ContentLanguage = headers.Where(h => string.Equals(h.Key, "Content-Language", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                ContentTransferEncoding = headers.Where(h => string.Equals(h.Key, "Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                ContentType = headers.Where(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Control = headers.Where(h => string.Equals(h.Key, "Control", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value.Trim()).SingleOrDefault(),
                Date = headers.Where(h => string.Equals(h.Key, "Date", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value.Trim()).SingleOrDefault() ?? DateTime.UtcNow.ToString("dd MMM yyyy HH:mm:ss") + " +0000",
                Distribution = headers.Where(h => string.Compare(h.Key, "Distribution", StringComparison.OrdinalIgnoreCase) == 0).Select(h => h.Value).SingleOrDefault(),
                Expires = headers.Where(h => string.Equals(h.Key, "Expires", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                FollowupTo = headers.Where(h => string.Equals(h.Key, "Followup-To", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                From = headers.Single(h => string.Equals(h.Key, "From", StringComparison.OrdinalIgnoreCase)).Value,
                Headers = block.Substring(0, headerLength - 2),
                InjectionDate = headers.Where(h => string.Equals(h.Key, "Injection-Date", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                MessageId = msgId,
                MIMEVersion = headers.Where(h => string.Equals(h.Key, "MIME-Version", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Newsgroups = newsgroups,
                Organization = headers.Where(h => string.Equals(h.Key, "Organization", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                References = headers.Where(h => string.Equals(h.Key, "References", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Subject = headers.Single(h => string.Equals(h.Key, "Subject", StringComparison.OrdinalIgnoreCase)).Value,
                Summary = headers.Where(h => string.Equals(h.Key, "Summary", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Supersedes = headers.Where(h => string.Equals(h.Key, "Supersedes", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                UserAgent = headers.Where(h => string.Equals(h.Key, "User-Agent", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
                Xref = headers.Where(h => string.Equals(h.Key, "Xref", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).SingleOrDefault(),
            };

            if (!headers.ContainsKey("DATE"))
            {
                article.ChangeHeader("Date", article.Date);
            }

            if (article.Date.TryParseNewsgroupDateHeader(out DateTime parsedDateTime))
            {
                article.DateTimeParsed = parsedDateTime.ToUniversalTime();
            }

            article.ChangeHeader("Message-ID", msgId);

            return true;
        }

        [Pure]
        public static bool TryParseHeaders([NotNull] string headerBlock, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines)
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
            for (var i = 0L; i < headerLines.LongLength; i++)
            {
                var headerLine = headerLines[i];

                var readahead = 1;
                while (i + readahead < headerLines.LongLength && new[] { ' ', '\t' }.Contains(headerLines[i + readahead][0]))
                {
                    headerLine += headerLines[i + readahead].Substring(1);
                    readahead++;
                }

                i += readahead - 1;

                var match = Regex.Match(headerLine, @"(?<key>[\x21-\x7e]+):\s+(?<value>[^\n]+$)");
                if (!match.Success)
                {
                    return false;
                }

                headers.Add(match.Groups["key"].Value.ToUpperInvariant(), match.Groups["value"].Value);
                headersAndFullLines.Add(match.Groups["key"].Value.ToUpperInvariant(), headerLine);
            }

            return true;
        }
    }
}
