using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace McNNTP.Server.Data
{
    public class Article
    {
        public virtual long Id { get; set; }
        public virtual Newsgroup Newsgroup { get; set; }
        // MANDATORY FIELDS
        public virtual string Date { get; set; }
        public virtual string From { get; set; }
        public virtual string MessageId { get; set; }
        public virtual string Newsgroups { get; set; }
        public virtual string Path { get; set; }
        public virtual string Subject { get; set; }
        // OPTIONAL FIELDS
        public virtual string Approved { get; set; }
        public virtual string Archive { get; set; }
        public virtual string ContentDisposition { get; set; }
        public virtual string ContentLanguage { get; set; }
        public virtual string ContentTransferEncoding { get; set; }
        public virtual string ContentType { get; set; }
        public virtual string Control { get; set; }
        public virtual string Distribution { get; set; }
        public virtual string Expires { get; set; }
        public virtual string FollowupTo { get; set; }
        public virtual string InjectionDate { get; set; }
        public virtual string InjectionInfo { get; set; }
        public virtual string MIMEVersion { get; set; }
        public virtual string Organization { get; set; }
        public virtual string References { get; set; }
        public virtual string Summary { get; set; }
        public virtual string Supersedes { get; set; }
        public virtual string UserAgent { get; set; }
        public virtual string Xref  { get; set; }
        // FULL HEADERS AND BODY
        public virtual string Headers { get; set; }
        public virtual string Body { get; set; }

        internal static bool TryParse(string block, out Article article)
        {
            article = null;

            var headerLines = block
                    .SeekThroughDelimiters("\r\n")
                    .TakeWhile(s => !string.IsNullOrEmpty(s))
                    .ToArray();

            Dictionary<string, string> headers;
            if (!TryParseHeaders(headerLines, out headers))
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
                msgId = headers.Single(h => string.Compare(h.Key, "Message-ID", StringComparison.OrdinalIgnoreCase) != 0).Value;
                if (!msgId.IsUsenetMessageId())
                    msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.invalid>";
            }
            else
                msgId = "<" + Guid.NewGuid().ToString("N").ToUpperInvariant() + "@mcnttp.auto>";

            article = new Article
            {
                Body = block.Substring(headerLength),
                Date = DateTime.UtcNow.ToString("r"),
                From = headers.Single(h => string.Compare(h.Key, "From", StringComparison.OrdinalIgnoreCase) == 0).Value,
                Headers = block.Substring(0, headerLength - 2),
                MessageId = msgId,
                Newsgroups = headers.Single(h => string.Compare(h.Key, "Newsgroups", StringComparison.OrdinalIgnoreCase) == 0).Value,
                Subject = headers.Single(h => string.Compare(h.Key, "Subject", StringComparison.OrdinalIgnoreCase) == 0).Value,
            };

            return true;
        }

        internal static bool TryParseHeaders(string headerBlock, out Dictionary<string, string> headers)
        {
            var headerLines = headerBlock
                   .SeekThroughDelimiters("\r\n")
                   .TakeWhile(s => !string.IsNullOrEmpty(s))
                   .ToArray();

            return TryParseHeaders(headerLines, out headers);
        }

        internal static bool TryParseHeaders(string[] headerLines, out Dictionary<string, string> headers)
        {
            headers = new Dictionary<string, string>();

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
            }

            return true;
        }
    }
}
