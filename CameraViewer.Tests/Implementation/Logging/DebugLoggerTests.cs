using CameraViewer.Implementation.Logging;

namespace CameraViewer.Tests.Implementation.Logging
{
    [TestFixture]
    public class DebugLoggerTests
    {
        [Test]
        public void Constructor_WithPrefix_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new DebugLogger("[TEST]"));
        }

        [Test]
        public void Constructor_WithoutPrefix_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => new DebugLogger());
        }

        [Test]
        public void LogDebug_ShouldNotThrow()
        {
            // Arrange
            var logger = new DebugLogger("[TEST]");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogDebug("Test message"));
        }

        [Test]
        public void LogInfo_ShouldNotThrow()
        {
            // Arrange
            var logger = new DebugLogger("[TEST]");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogInfo("Test message"));
        }

        [Test]
        public void LogWarning_ShouldNotThrow()
        {
            // Arrange
            var logger = new DebugLogger("[TEST]");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogWarning("Test message"));
        }

        [Test]
        public void LogError_WithoutException_ShouldNotThrow()
        {
            // Arrange
            var logger = new DebugLogger("[TEST]");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogError("Test error"));
        }

        [Test]
        public void LogError_WithException_ShouldNotThrow()
        {
            // Arrange
            var logger = new DebugLogger("[TEST]");
            var exception = new Exception("Test exception");

            // Act & Assert
            Assert.DoesNotThrow(() => logger.LogError("Test error", exception));
        }
    }
}
