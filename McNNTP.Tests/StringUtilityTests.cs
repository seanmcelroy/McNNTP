using System.Linq;
using McNNTP.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
