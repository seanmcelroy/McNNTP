using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace McNNTP
{
    public static class StringUtility
    {
        public static byte[] GZipCompress(this string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream(buffer))
            using (var gzs = new Ionic.Zlib.ZlibStream(ms, Ionic.Zlib.CompressionMode.Compress, true))
            using (var msOut = new MemoryStream())
            {
                gzs.CopyTo(msOut);
                var array = msOut.ToArray();
                return array;
            }
        }

        public static string GZipUncompress(this string compressedText)
        {
            var buffer = Encoding.UTF8.GetBytes(compressedText);
            return GZipUncompress(buffer);
        }

        public static string GZipUncompress(this byte[] gZipBuffer)
        {
            using (var ms = new MemoryStream(gZipBuffer))
            using (var gzs = new Ionic.Zlib.ZlibStream(ms, Ionic.Zlib.CompressionMode.Decompress, true))
            using (var msOut = new MemoryStream())
            {
                gzs.CopyTo(msOut);
                var array = msOut.ToArray();
                var str = Encoding.UTF8.GetString(array);
                return str;
            }
        }

        [Pure]
        public static bool MatchesWildmat([NotNull] this string test, string wildmat)
        {
            if (string.IsNullOrEmpty(wildmat))
                return true;

            // RFC 3977 4.2 - Right most part that matches wins
            var wildmatPatterns = wildmat.Split(',').Reverse();
            foreach (var wildmatPattern in wildmatPatterns)
            {
                var negate = false;
                var wildmatPattern2 = wildmatPattern;
                if (wildmatPattern2.StartsWith("!"))
                {
                    negate = true;
                    wildmatPattern2 = wildmatPattern2.Substring(1);
                }

                var regexPattern = "^" + Regex.Escape(wildmatPattern2).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                if (Regex.IsMatch(test, regexPattern, RegexOptions.IgnoreCase))
                    return !negate;
            }

            return false;
        }


        [NotNull, Pure]
        public static IEnumerable<string> SeekThroughDelimiters([NotNull] this string block, [NotNull] string delimiter)
        {
            return block.ToCharArray().SeekThroughDelimiters(delimiter.ToCharArray()).Select(s => new string(s));
        }

        [NotNull, Pure]
        public static IEnumerable<T[]> SeekThroughDelimiters<T>([NotNull] this T[] block, [NotNull] T[] delimiter)
            where T : IComparable
        {
            var start = 0;
            var b = 0;

            while (b < block.Length)
            {
                var match = false;
                var bx = b;
                var d = 0;
                while (b < block.Length && d < delimiter.Length && block[b].Equals(delimiter[d]))
                {
                    match = true;
                    b++;
                    d++;
                }

                if (match && d == delimiter.Length)
                {
                    yield return block.Skip(start).Take(b - delimiter.Length - start).ToArray();
                    start = b;
                }
                else
                    b = bx + 1;
            }

            if (start < block.Length)
                yield return block.Skip(start).ToArray();
        }
    }
}
