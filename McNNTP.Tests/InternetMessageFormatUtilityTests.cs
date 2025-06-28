using McNNTP.Common;

namespace McNNTP.Tests
{
    [TestClass]
    public class InternetMessageFormatUtilityTests
    {
        [TestMethod]
        public void IsAText_ValidCharacters_ReturnsTrue()
        {
            Assert.IsTrue("A".IsAText());
            Assert.IsTrue("z".IsAText());
            Assert.IsTrue("0".IsAText());
            Assert.IsTrue("9".IsAText());
            Assert.IsTrue("!".IsAText());
            Assert.IsTrue("#".IsAText());
            Assert.IsTrue("$".IsAText());
            Assert.IsTrue("%".IsAText());
            Assert.IsTrue("&".IsAText());
            Assert.IsTrue("'".IsAText());
            Assert.IsTrue("*".IsAText());
            Assert.IsTrue("+".IsAText());
            Assert.IsTrue("-".IsAText());
            Assert.IsTrue("/".IsAText());
            Assert.IsTrue("=".IsAText());
            Assert.IsTrue("?".IsAText());
            Assert.IsTrue("^".IsAText());
            Assert.IsTrue("_".IsAText());
            Assert.IsTrue("`".IsAText());
            Assert.IsTrue("{".IsAText());
            Assert.IsTrue("|".IsAText());
            Assert.IsTrue("}".IsAText());
            Assert.IsTrue("~".IsAText());
        }

        [TestMethod]
        public void IsAText_InvalidCharacters_ReturnsFalse()
        {
            Assert.IsFalse(" ".IsAText());
            Assert.IsFalse("@".IsAText());
            Assert.IsFalse("[".IsAText());
            Assert.IsFalse("]".IsAText());
            Assert.IsFalse("\\".IsAText());
            Assert.IsFalse("\"".IsAText());
            Assert.IsFalse("(".IsAText());
            Assert.IsFalse(")".IsAText());
            Assert.IsFalse(",".IsAText());
            Assert.IsFalse(":".IsAText());
            Assert.IsFalse(";".IsAText());
            Assert.IsFalse("<".IsAText());
            Assert.IsFalse(">".IsAText());
            Assert.IsFalse(".".IsAText());
        }

        [TestMethod]
        public void IsAtom_ValidAtom_ReturnsTrue()
        {
            Assert.IsTrue("test".IsAtom());
            Assert.IsTrue("example123".IsAtom());
            Assert.IsTrue("user_name".IsAtom());
        }

        [TestMethod]
        public void IsAtom_InvalidAtom_ReturnsFalse()
        {
            Assert.IsFalse("test@domain".IsAtom());
            Assert.IsFalse("test.domain".IsAtom());
            Assert.IsFalse("test domain".IsAtom());
        }

        [TestMethod]
        public void IsUsenetMessageId_ValidMessageId_ReturnsTrue()
        {
            Assert.IsTrue("<test@example.com>".IsUsenetMessageId());
            Assert.IsTrue("<123abc@domain.org>".IsUsenetMessageId());
            Assert.IsTrue("<unique-id@server.net>".IsUsenetMessageId());
        }

        [TestMethod]
        public void IsUsenetMessageId_ValidMessageIdWithIPAddress_ReturnsTrue()
        {
            Assert.IsTrue("<test@[192.168.1.1]>".IsUsenetMessageId());
            Assert.IsTrue("<message@[10.0.0.1]>".IsUsenetMessageId());
        }

        [TestMethod]
        public void IsUsenetMessageId_InvalidMessageId_ReturnsFalse()
        {
            Assert.IsFalse("test@example.com".IsUsenetMessageId());
            Assert.IsFalse("<test>".IsUsenetMessageId());
            Assert.IsFalse("<@example.com>".IsUsenetMessageId());
            Assert.IsFalse("<test@>".IsUsenetMessageId());
            Assert.IsFalse("test@example.com>".IsUsenetMessageId());
            Assert.IsFalse("<test@example.com".IsUsenetMessageId());
        }

        [TestMethod]
        public void IsUsenetMessageId_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(string.Empty.IsUsenetMessageId());
        }

        [TestMethod]
        public void IsUsenetMessageId_ComplexValidMessageId_ReturnsTrue()
        {
            Assert.IsTrue("<2023.01.01.123456@news.example.com>".IsUsenetMessageId());
            Assert.IsTrue("<article_123_abc@server-name.domain.tld>".IsUsenetMessageId());
        }

        [TestMethod]
        public void IsUsenetMessageId_InvalidCharactersInMessageId_ReturnsFalse()
        {
            Assert.IsFalse("<test with spaces@example.com>".IsUsenetMessageId());
            Assert.IsFalse("<test\"quote@example.com>".IsUsenetMessageId());
            Assert.IsFalse("<test<nested>@example.com>".IsUsenetMessageId());
        }

        [TestMethod]
        public void IsAtom_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(string.Empty.IsAtom());
        }

        [TestMethod]
        public void IsAText_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(string.Empty.IsAText());
        }

        [TestMethod]
        public void IsAText_MultipleCharacters_ValidatesFirstCharacterOnly()
        {
            Assert.IsTrue("A@".IsAText());
            Assert.IsTrue("test".IsAText());
        }
    }
}