using CameraViewer.Implementation.Logging;

namespace CameraViewer.Tests.Implementation.Logging
{
    [TestFixture]
    public class NullLoggerTests
    {
        [Test]
        public void LogDebug_ShouldDoNothing()
        {
            // Arrange
            var logger = new NullLogger();

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogDebug("Test message"));
        }

        [Test]
        public void LogInfo_ShouldDoNothing()
        {
            // Arrange
            var logger = new NullLogger();

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogInfo("Test message"));
        }

        [Test]
        public void LogWarning_ShouldDoNothing()
        {
            // Arrange
            var logger = new NullLogger();

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogWarning("Test message"));
        }

        [Test]
        public void LogError_ShouldDoNothing()
        {
            // Arrange
            var logger = new NullLogger();
            var exception = new Exception("Test exception");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogError("Test error", exception));
        }
    }
}
