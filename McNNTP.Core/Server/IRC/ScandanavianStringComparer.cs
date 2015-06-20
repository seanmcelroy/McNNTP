namespace McNNTP.Core.Server.IRC
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Because of IRC's Scandinavian origin, the characters {}|^ are
    /// considered to be the lower case equivalents of the characters []\~,
    /// respectively. This is a critical issue when determining the
    /// equivalence of two nicknames or channel names.
    /// https://tools.ietf.org/html/rfc2812#section-2.2
    /// </summary>
    public class ScandanavianStringComparison : IComparer<string>, IEqualityComparer<string>
    {
        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <returns>
        /// A signed integer that indicates the relative values of <paramref name="x"/> and <paramref name="y"/>, as shown in the following table.Value Meaning Less than zero<paramref name="x"/> is less than <paramref name="y"/>.Zero<paramref name="x"/> equals <paramref name="y"/>.Greater than zero<paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        /// <param name="x">The first object to compare.</param><param name="y">The second object to compare.</param>
        public int Compare(string x, string y)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;

            var xs = x == null ? null : x.Replace('[', '{').Replace(']', '}').Replace('\\', '|').Replace('~', '^');
            var ys = y == null ? null : y.Replace('[', '{').Replace(']', '}').Replace('\\', '|').Replace('~', '^');

            return comparer.Compare(xs, ys);
        }

        /// <summary>
        /// Determines whether the specified objects are equal.
        /// </summary>
        /// <returns>
        /// true if the specified objects are equal; otherwise, false.
        /// </returns>
        /// <param name="x">The first object of type string to compare.</param><param name="y">The second object of type string to compare.</param>
        public bool Equals(string x, string y)
        {
            return this.Compare(x, y) == 0;
        }

        /// <summary>
        /// Returns a hash code for the specified object.
        /// </summary>
        /// <returns>
        /// A hash code for the specified object.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> for which a hash code is to be returned.</param><exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
        public int GetHashCode(string obj)
        {
            var xs = obj == null ? null : obj.Replace('[', '{').Replace(']', '}').Replace('\\', '|').Replace('~', '^');
            return xs == null ? 0 : xs.GetHashCode();
        }
    }
}
