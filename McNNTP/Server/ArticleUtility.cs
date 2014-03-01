using System;
using System.Collections.Generic;
using System.Linq;
using McNNTP.Server.Data;
using JetBrains.Annotations;

namespace McNNTP.Server
{
    public static class ArticleUtility
    {
        internal static void ChangeHeader([NotNull] this Article article, [NotNull] string headerName, [NotNull] string headerValue)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.ContainsKey(headerName))
            {
                article.Headers = article.Headers.Replace(headersAndFullLines[headerName] + "\r\n", string.Format("{0}: {1}\r\n", headerName, headerValue));
            }
        }
        [CanBeNull, Pure]
        internal static string GetHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.Any(hfl => string.Compare(hfl.Key, headerName, StringComparison.OrdinalIgnoreCase) == 0))
                return headersAndFullLines[headerName];

            return null;
        }

        internal static void RemoveHeader([NotNull] this Article article, [NotNull] string headerName)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.ContainsKey(headerName))
            {
                article.Headers = article.Headers.Replace(headersAndFullLines[headerName] + "\r\n", string.Empty);
            }
        }
    }
}
