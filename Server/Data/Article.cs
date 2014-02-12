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
        public virtual string Control { get; set; }
        public virtual string Distribution { get; set; }
        public virtual string Expires { get; set; }
        public virtual string FollowupTo { get; set; }
        public virtual string InjectionDate { get; set; }
        public virtual string InjectionInfo { get; set; }
        public virtual string Organization { get; set; }
        public virtual string References { get; set; }
        public virtual string Summary { get; set; }
        public virtual string Supersedes { get; set; }
        public virtual string UserAgent { get; set; }
        public virtual string Xref  { get; set; }
        // FULL BODY
        public virtual string Body { get; set; }

        internal static bool TryParse(string block, out Article article)
        {
            article = null;

            var headers = new Dictionary<string, string>();
            var body = new StringBuilder();

            var inHeaders = true;
            foreach (var line in block.Split(new[] {"\r\n"}, StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(line) && inHeaders)
                {
                    inHeaders = false;
                    continue;
                }

                if (inHeaders)
                {
                    var match = Regex.Match(line, @"(?<key>[^:]+):\s+(?<value>[^\n]+$)");
                    if (!match.Success)
                        return false;
                    headers.Add(match.Groups["key"].Value, match.Groups["value"].Value);
                }
                else
                    body.AppendLine(line);
            }

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
                Body = body.ToString(),
                Date = DateTime.UtcNow.ToString(),
                From = headers.Single(h => string.Compare(h.Key, "From", StringComparison.OrdinalIgnoreCase) == 0).Value,
                MessageId = msgId,
                Newsgroups = headers.Single(h => string.Compare(h.Key, "Newsgroups", StringComparison.OrdinalIgnoreCase) == 0).Value,
                Subject = headers.Single(h => string.Compare(h.Key, "Subject", StringComparison.OrdinalIgnoreCase) == 0).Value
            };

            return true;
        }
    }
}
