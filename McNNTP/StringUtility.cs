using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace McNNTP
{
    public static class StringUtility
    {
        public static string GZipCompress(this string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gZipStream.Write(buffer, 0, buffer.Length);
                }

                memoryStream.Position = 0;

                compressedData = new byte[memoryStream.Length];
                memoryStream.Read(compressedData, 0, compressedData.Length);
            }

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }

        public static string GZipUncompress(this string compressedText)
        {
            var gZipBuffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream())
            {
                var dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                var buffer = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
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
