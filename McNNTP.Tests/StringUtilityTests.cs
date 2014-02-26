using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McNNTP.Tests
{
    [TestClass]
    public class StringUtilityTests
    {
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
    }
}
