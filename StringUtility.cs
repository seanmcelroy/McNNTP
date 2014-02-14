using System;
using System.Collections.Generic;
using System.Linq;

namespace McNNTP
{
    public static class StringUtility
    {
        public static IEnumerable<string> SeekThroughDelimiters(this string block, string delimiter)
        {
            return block.ToCharArray().SeekThroughDelimiters(delimiter.ToCharArray()).Select(s => new string(s));
        }

        public static IEnumerable<T[]> SeekThroughDelimiters<T>(this T[] block, T[] delimiter)
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
