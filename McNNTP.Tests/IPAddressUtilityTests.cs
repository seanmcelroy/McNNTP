using System.Net;
using McNNTP.Common;

namespace McNNTP.Tests
{
    [TestClass]
    public class IPAddressUtilityTests
    {
        [TestMethod]
        public void MatchesCIDRRange_IPv4_ValidRange_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("192.168.1.0/24");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_OutsideRange_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.2.100");
            var result = address.MatchesCIDRRange("192.168.1.0/24");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_EdgeCase_FirstAddress_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.168.1.0");
            var result = address.MatchesCIDRRange("192.168.1.0/24");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_EdgeCase_LastAddress_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.168.1.255");
            var result = address.MatchesCIDRRange("192.168.1.0/24");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_Prefix16_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.168.50.100");
            var result = address.MatchesCIDRRange("192.168.0.0/16");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_Prefix8_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.100.50.100");
            var result = address.MatchesCIDRRange("192.0.0.0/8");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_Prefix32_ExactMatch_ReturnsTrue()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("192.168.1.100/32");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv4_Prefix32_NoMatch_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.1.101");
            var result = address.MatchesCIDRRange("192.168.1.100/32");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv6_ValidRange_ReturnsTrue()
        {
            var address = IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348");
            var result = address.MatchesCIDRRange("2001:db8:85a3:8d3::/64");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv6_OutsideRange_ReturnsFalse()
        {
            var address = IPAddress.Parse("2001:db8:85a3:8d4:1319:8a2e:370:7348");
            var result = address.MatchesCIDRRange("2001:db8:85a3:8d3::/64");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv6_Prefix128_ExactMatch_ReturnsTrue()
        {
            var address = IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348");
            var result = address.MatchesCIDRRange("2001:db8:85a3:8d3:1319:8a2e:370:7348/128");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_InvalidFormat_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("invalid/format");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_InvalidPrefix_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("192.168.1.0/33");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_NegativePrefix_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("192.168.1.0/-1");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_IPv6_InvalidPrefix_ReturnsFalse()
        {
            var address = IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348");
            var result = address.MatchesCIDRRange("2001:db8:85a3:8d3::/129");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void MatchesCIDRRange_MismatchedAddressFamilies_ReturnsFalse()
        {
            var address = IPAddress.Parse("192.168.1.100");
            var result = address.MatchesCIDRRange("2001:db8::/32");
            Assert.IsFalse(result);
        }
    }
}