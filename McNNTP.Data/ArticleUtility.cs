namespace McNNTP.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Linq;

    public static class ArticleUtility
    {
        public static void ChangeHeader([NotNull] this Article article, [NotNull] string headerName, [NotNull] string headerValue)
        {
            if (Article.TryParseHeaders(article.Headers, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var hfl in headersAndFullLines.Where(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)))
                {
                    article.Headers = article.Headers.Replace(hfl.Value + "\r\n", string.Format("{0}: {1}\r\n", hfl.Key, headerValue));
                }
            }
            else
            {
                article.Headers = string.Format("{0}\r\n{1}: {2}", article.Headers, headerName, headerValue);
            }
        }

        [Pure]
        public static string? GetHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            if (Article.TryParseHeaders(article.Headers, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)))
            {
                var fullHeader = headersAndFullLines.Where(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)).Select(hfl => hfl.Value).FirstOrDefault();
                if (fullHeader == null)
                {
                    return null;
                }

                return fullHeader.Contains(": ")
                    ? fullHeader[(fullHeader.IndexOf(": ", StringComparison.OrdinalIgnoreCase) + 2) ..]
                    : fullHeader;
            }

            return null;
        }

        public static void RemoveHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            if (Article.TryParseHeaders(article.Headers, out Dictionary<string, string> headers, out Dictionary<string, string> headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var hfl in headersAndFullLines.Where(hfl => string.Equals(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase)))
                {
                    article.Headers = article.Headers.Replace(hfl.Value + "\r\n", string.Empty);
                }
            }
        }
    }
}
