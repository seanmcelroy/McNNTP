using Microsoft.Extensions.Logging;
using McNNTP.Common;
using Moq;

namespace McNNTP.Tests
{
    [TestClass]
    public class LogUtilityTests
    {
        private Mock<ILogger> _mockLogger;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [TestMethod]
        public void TraceFormat_CallsLogTraceWithFormatting()
        {
            var format = "Test {0} message {1}";
            var args = new object[] { "trace", 123 };
            
            _mockLogger.Object.TraceFormat(format, args);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Trace,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void Verbose_CallsLogDebug()
        {
            var message = "Test verbose message";
            
            _mockLogger.Object.Verbose(message);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == message),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void VerboseFormat_CallsLogDebugWithFormatting()
        {
            var format = "Test {0} verbose {1}";
            var args = new object[] { "formatted", 456 };
            
            _mockLogger.Object.VerboseFormat(format, args);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public void TraceFormat_WithNullFormat_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => 
                _mockLogger.Object.TraceFormat(null!, "arg"));
        }

        [TestMethod]
        public void Verbose_WithNullMessage_ThrowsException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => 
                _mockLogger.Object.Verbose(null!));
        }

        [TestMethod]
        public void VerboseFormat_WithNullFormat_ThrowsException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => 
                _mockLogger.Object.VerboseFormat(null!, "arg"));
        }
    }
}