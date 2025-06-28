using System.Text;
using McNNTP.Common;

namespace McNNTP.Tests
{
    using System;

    [TestClass]
    public class StringUtilityTests
    {
        [TestMethod]
        public void ParseDates()
        {
            DateTime dateTime;
            Assert.IsTrue("Mon, 10 Mar 2014 22:23:31 -0500".TryParseNewsgroupDateHeader(out dateTime));

        }

        [TestMethod]
        public void SeekThroughDelimiters()
        {
            Assert.AreEqual("a", "a".SeekThroughDelimiters(",").Skip(0).Take(1).Single());
            Assert.AreEqual("a", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(0).Take(1).Single());
            Assert.AreEqual("b","a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(1).Take(1).Single());
            Assert.AreEqual("c", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(2).Take(1).Single());
            Assert.AreEqual("d", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(3).Take(1).Single());
            Assert.AreEqual("e", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(4).Take(1).Single());
            Assert.AreEqual("f", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(5).Take(1).Single());
            Assert.AreEqual("f", "a,b,c,d,e,f,".SeekThroughDelimiters(",").Skip(5).Take(1).Single());
            Assert.AreEqual("g", "a,b,c,d,e,f,g".SeekThroughDelimiters(",").Skip(6).Take(1).Single());
        }

        [TestMethod]
        public void SeekThroughDelimiters_Multicharacter()
        {
            Assert.AreEqual("b", "a\r\nb".SeekThroughDelimiters("\r\n").Skip(1).Take(1).Single());
            Assert.AreEqual("a", "a\r\n".SeekThroughDelimiters("\r\n").Skip(0).Take(1).Single());
        }

        [TestMethod]
        public void Wildmat()
        {
            Assert.IsTrue("foo".MatchesWildmat("*foo*"));
            Assert.IsTrue("mini".MatchesWildmat("mini*"));
            Assert.IsTrue("minibus".MatchesWildmat("mini*"));
            Assert.IsFalse("ab".MatchesWildmat("???*"));
            Assert.IsTrue("abc".MatchesWildmat("???*"));
            Assert.IsTrue("abcd".MatchesWildmat("???*"));
            Assert.IsTrue("abcde".MatchesWildmat("???*"));

            Assert.IsTrue("abc".MatchesWildmat("abc"));
            Assert.IsTrue("abc".MatchesWildmat("abc,def"));
            Assert.IsTrue("def".MatchesWildmat("abc,def"));
            Assert.IsTrue("abb".MatchesWildmat("a*b"));
            Assert.IsFalse("abc".MatchesWildmat("a*b"));
            Assert.IsTrue("apple".MatchesWildmat("a*,*b"));
            Assert.IsTrue("rhubarb".MatchesWildmat("a*,*b"));
            Assert.IsFalse("carrot".MatchesWildmat("a*,*b"));

            Assert.IsTrue("apple".MatchesWildmat("a*,!*b"));
            Assert.IsFalse("anub".MatchesWildmat("a*,!*b"));

            Assert.IsTrue("apple".MatchesWildmat("a*,!*b,c*"));
            Assert.IsFalse("anub".MatchesWildmat("a*,!*b,c*"));
            Assert.IsTrue("cabal".MatchesWildmat("a*,!*b,c*"));
            Assert.IsTrue("curb".MatchesWildmat("a*,!*b,c*"));

            Assert.IsTrue("apple".MatchesWildmat("a*,c*,!*b"));
            Assert.IsTrue("carrot".MatchesWildmat("a*,c*,!*b"));
            Assert.IsFalse("appleb".MatchesWildmat("a*,c*,!*b"));
            Assert.IsFalse("carrotb".MatchesWildmat("a*,c*,!*b"));

            Assert.IsTrue("rake".MatchesWildmat("?a*"));
            Assert.IsFalse("snake".MatchesWildmat("?a*"));
            Assert.IsTrue("prake".MatchesWildmat("??a*"));
            Assert.IsFalse("psnake".MatchesWildmat("??a*"));
            Assert.IsTrue("sbat".MatchesWildmat("*a?"));
            Assert.IsFalse("sbatt".MatchesWildmat("*a?"));
        }

        [TestMethod]
        public async Task ZlibDeflate_ValidString_ReturnsCompressedData()
        {
            var input = "Hello, World! This is a test string for compression. Hello, World! This is a test string for compression.";
            var result = await input.ZlibDeflate(CancellationToken.None);
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            var inputLength = Encoding.UTF8.GetBytes(input).Length;
            Assert.IsTrue(result.Length < inputLength, $"Expected deflated length {result.Length} to be less than input length {inputLength}.");
        }

        [TestMethod]
        public async Task ZlibInflate_CompressedData_ReturnsOriginalString()
        {
            var input = "Hello, World! This is a test string for compression and decompression.";
            var compressed = await input.ZlibDeflate(CancellationToken.None);
            var result = await compressed.ZlibInflate(CancellationToken.None);
            
            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public async Task ZlibRoundTrip_LargeString_PreservesData()
        {
            var input = string.Concat(Enumerable.Repeat("This is a longer test string for compression testing. ", 100));
            var compressed = await input.ZlibDeflate(CancellationToken.None);
            var result = await compressed.ZlibInflate(CancellationToken.None);
            
            Assert.AreEqual(input, result);
            Assert.IsTrue(compressed.Length < Encoding.UTF8.GetBytes(input).Length);
        }

        [TestMethod]
        public void MatchesAsIPInCIDRRange_ValidIPInRange_ReturnsTrue()
        {
            Assert.IsTrue("192.168.1.100".MatchesAsIPInCIDRRange("192.168.1.0/24"));
            Assert.IsTrue("10.0.0.1".MatchesAsIPInCIDRRange("10.0.0.0/8"));
        }

        [TestMethod]
        public void MatchesAsIPInCIDRRange_ValidIPOutsideRange_ReturnsFalse()
        {
            Assert.IsFalse("192.168.2.100".MatchesAsIPInCIDRRange("192.168.1.0/24"));
            Assert.IsFalse("172.16.0.1".MatchesAsIPInCIDRRange("10.0.0.0/8"));
        }

        [TestMethod]
        public void MatchesAsIPInCIDRRange_InvalidIP_ReturnsFalse()
        {
            Assert.IsFalse("not.an.ip.address".MatchesAsIPInCIDRRange("192.168.1.0/24"));
            Assert.IsFalse("256.256.256.256".MatchesAsIPInCIDRRange("192.168.1.0/24"));
        }

        [TestMethod]
        public void MatchesWildchar_SimpleWildcard_ReturnsTrue()
        {
            Assert.IsTrue("test.txt".MatchesWildchar("*.txt"));
            Assert.IsTrue("file123.doc".MatchesWildchar("file???.doc"));
            Assert.IsTrue("anything".MatchesWildchar("*"));
        }

        [TestMethod]
        public void MatchesWildchar_NoMatch_ReturnsFalse()
        {
            Assert.IsFalse("test.doc".MatchesWildchar("*.txt"));
            Assert.IsFalse("file1234.doc".MatchesWildchar("file???.doc"));
        }

        [TestMethod]
        public void TryParseNewsgroupDateHeader_AdditionalFormats_ReturnsTrue()
        {
            Assert.IsTrue("01 Jan 2023 12:00:00 +0000".TryParseNewsgroupDateHeader(out var date1));
            Assert.AreEqual(new DateTime(2023, 1, 1, 0, 0, 0), date1.Date);

            Assert.IsTrue("01 Jan 2023 12:00:00".TryParseNewsgroupDateHeader(out var date2));
            Assert.AreEqual(new DateTime(2023, 1, 1, 0, 0, 0), date2.Date);
        }

        [TestMethod]
        public void TryParseNewsgroupDateHeader_InvalidDate_ReturnsFalse()
        {
            Assert.IsFalse("invalid date".TryParseNewsgroupDateHeader(out var date1));
            Assert.AreEqual(DateTime.MinValue, date1);

            Assert.IsFalse("32 Jan 2023 12:00:00".TryParseNewsgroupDateHeader(out var date2));
            Assert.AreEqual(DateTime.MinValue, date2);

            Assert.IsFalse(string.Empty.TryParseNewsgroupDateHeader(out var date3));
            Assert.AreEqual(DateTime.MinValue, date3);

            Assert.IsFalse(((string?)null).TryParseNewsgroupDateHeader(out var date4));
            Assert.AreEqual(DateTime.MinValue, date4);
        }

        [TestMethod]
        public void MatchesWildmat_EdgeCases_ReturnsCorrectResult()
        {
            Assert.IsTrue("anything".MatchesWildmat(""));
            Assert.IsTrue("test".MatchesWildmat(null));
            
            Assert.ThrowsException<ArgumentNullException>(() => 
                string.Empty.MatchesWildmat("pattern"));
        }

        [TestMethod]
        public void MatchesWildmat_CaseInsensitive_ReturnsTrue()
        {
            Assert.IsTrue("COMP.LANG.C".MatchesWildmat("comp.lang.*"));
            Assert.IsTrue("comp.lang.c".MatchesWildmat("COMP.LANG.*"));
        }
    }
}
