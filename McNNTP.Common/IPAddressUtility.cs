namespace McNNTP.Common
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Net.Sockets;
    using System.Numerics;

    /// <summary>
    /// A utility class for analyzing and manipulating IP addresses.
    /// </summary>
    public static class IpAddressUtility
    {
        [Pure]
        public static bool MatchesCIDRRange([NotNull] this IPAddress address, [NotNull] string cidr)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var ipAddress = address.ToString();
                var parts = cidr.Split('/');

                var ipAddr = BitConverter.ToInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                var cidrAddr = BitConverter.ToInt32(IPAddress.Parse(ipAddress).GetAddressBytes(), 0);
                var cidrMask = IPAddress.HostToNetworkOrder(-1 << (32 - int.Parse(parts[1])));

                return (ipAddr & cidrMask) == (cidrAddr & cidrMask);
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var ipAddress = address.ToString();
                var parts = cidr.Split('/');

                var ipAddr = new BigInteger(IPAddress.Parse(parts[0]).GetAddressBytes());
                var cidrAddr = new BigInteger(IPAddress.Parse(ipAddress).GetAddressBytes());
                var cidrMask = IPAddress.HostToNetworkOrder(-1 << (128 - int.Parse(parts[1])));

                return (ipAddr & cidrMask) == (cidrAddr & cidrMask);
            }

            return false;
        }
    }
}
