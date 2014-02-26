using System.Collections.Generic;
using McNNTP.Server.Data;

namespace McNNTP.Server
{
    public static class ArticleUtility
    {
        internal static void ChangeHeader(this Article article, string headerName, string headerValue)
        {
            Dictionary<string, string> headers, headersAndFullLines;
            if (Article.TryParseHeaders(article.Headers, out headers, out headersAndFullLines) &&
                headersAndFullLines.ContainsKey(headerName))
            {
                article.Headers = article.Headers.Replace(headersAndFullLines[headerName] + "\r\n", string.Format("{0}: {1}\r\n", headerName, headerValue));
            }
        }

        internal static void RemoveHeader(this Article article, string headerName)
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
