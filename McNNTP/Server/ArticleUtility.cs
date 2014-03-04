using System;
using System.Collections.Generic;
using System.Linq;
using McNNTP.Server.Data;
using JetBrains.Annotations;
using MoreLinq;

namespace McNNTP.Server
{
    public static class ArticleUtility
    {
        internal static void ChangeHeader([NotNull] this Article article, [NotNull] string headerName, [NotNull] string headerValue)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                foreach (var hfl in headersAndFullLines.Where(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
                    article.Headers = article.Headers.Replace(hfl.Value + "\r\n", string.Format("{0}: {1}\r\n", hfl.Key, headerValue));
            }
            else
            {
                article.Headers = string.Format("{0}\r\n{1}: {2}", article.Headers, headerName, headerValue);
            }
        }
        [CanBeNull, Pure]
        internal static string GetHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.Any(
                    hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                var fullHeader = headersAndFullLines.Where(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0).Select(hfl => hfl.Value).FirstOrDefault();
                if (fullHeader == null)
                    return null;
                if (fullHeader.Contains(": "))
                    return fullHeader.Substring(fullHeader.IndexOf(": ", StringComparison.OrdinalIgnoreCase) + 2);
                return fullHeader;
            }

            return null;
        }

        internal static void RemoveHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
            {
                foreach (var hfl in headersAndFullLines.Where(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
                    article.Headers = article.Headers.Replace(hfl.Value + "\r\n", string.Empty);
            }
        }
    }
}
