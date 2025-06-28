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
                var parts = cidr.Split('/');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int prefixLength) || prefixLength < 0 || prefixLength > 32)
                    return false;

                if (!IPAddress.TryParse(parts[0], out IPAddress? network) || network.AddressFamily != AddressFamily.InterNetwork)
                    return false;

                var networkBytes = network.GetAddressBytes();
                var addressBytes = address.GetAddressBytes();

                // IPv4 addresses are 4 bytes (32 bits)
                if (networkBytes.Length != 4 || addressBytes.Length != 4)
                    return false;

                // Calculate how many full bytes to compare
                int fullBytes = prefixLength / 8;
                int remainingBits = prefixLength % 8;

                // Compare full bytes
                for (int i = 0; i < fullBytes; i++)
                {
                    if (networkBytes[i] != addressBytes[i])
                        return false;
                }

                // Compare remaining bits in the partial byte
                if (remainingBits > 0 && fullBytes < 4)
                {
                    byte mask = (byte)(0xFF << (8 - remainingBits));
                    if ((networkBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                        return false;
                }

                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var parts = cidr.Split('/');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int prefixLength) || prefixLength < 0 || prefixLength > 128)
                    return false;

                var networkBytes = IPAddress.Parse(parts[0]).GetAddressBytes();
                var addressBytes = address.GetAddressBytes();

                // IPv6 addresses are 16 bytes (128 bits)
                if (networkBytes.Length != 16 || addressBytes.Length != 16)
                    return false;

                // Calculate how many full bytes to compare
                int fullBytes = prefixLength / 8;
                int remainingBits = prefixLength % 8;

                // Compare full bytes
                for (int i = 0; i < fullBytes; i++)
                {
                    if (networkBytes[i] != addressBytes[i])
                        return false;
                }

                // Compare remaining bits in the partial byte
                if (remainingBits > 0 && fullBytes < 16)
                {
                    byte mask = (byte)(0xFF << (8 - remainingBits));
                    if ((networkBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                        return false;
                }

                return true;
            }

            return false;
        }
    }
}
