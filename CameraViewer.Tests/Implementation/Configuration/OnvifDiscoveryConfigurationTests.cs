using CameraViewer.Implementation.Configuration;

namespace CameraViewer.Tests.Implementation.Configuration
{
    [TestFixture]
    public class OnvifDiscoveryConfigurationTests
    {
        [Test]
        public void DefaultValues_ShouldBeSet()
        {
            // Arrange
            var settings = new CameraViewer.Configuration.OnvifDiscoverySettings();
            
            // Act
            var config = new OnvifDiscoveryConfiguration(settings);

            // Assert
            Assert.That(config.MulticastAddress, Is.EqualTo("239.255.255.250"));
            Assert.That(config.MulticastPort, Is.EqualTo(3702));
            Assert.That(config.DiscoveryTimeoutMs, Is.EqualTo(5000));
            Assert.That(config.ProbeRetries, Is.EqualTo(3));
            Assert.That(config.ProbeDelayMs, Is.EqualTo(100));
            Assert.That(config.TcpConnectionTimeoutMs, Is.EqualTo(500));
        }

        [Test]
        public void AlternativePorts_ShouldContainDefaultPorts()
        {
            // Arrange
            var settings = new CameraViewer.Configuration.OnvifDiscoverySettings();
            
            // Act
            var config = new OnvifDiscoveryConfiguration(settings);

            // Assert
            Assert.That(config.AlternativePorts, Does.Contain(10080));
            Assert.That(config.AlternativePorts, Does.Contain(8080));
            Assert.That(config.AlternativePorts, Does.Contain(8899));
        }

        [Test]
        public void IpRanges_ShouldContainDefaultRanges()
        {
            // Arrange
            var settings = new CameraViewer.Configuration.OnvifDiscoverySettings();
            
            // Act
            var config = new OnvifDiscoveryConfiguration(settings);

            // Assert
            Assert.That(config.IpRanges, Has.Length.EqualTo(3));
            Assert.That(config.IpRanges[0], Is.EqualTo((64, 27)));
            Assert.That(config.IpRanges[1], Is.EqualTo((100, 21)));
            Assert.That(config.IpRanges[2], Is.EqualTo((200, 11)));
        }

        [Test]
        public void Settings_ShouldReflectChanges()
        {
            // Arrange
            var settings = new CameraViewer.Configuration.OnvifDiscoverySettings
            {
                DiscoveryTimeout = 10000,
                AlternativePorts = new System.Collections.Generic.List<int> { 8000, 9000 }
            };

            // Act
            var config = new OnvifDiscoveryConfiguration(settings);

            // Assert
            Assert.That(config.DiscoveryTimeoutMs, Is.EqualTo(10000));
            Assert.That(config.AlternativePorts, Has.Length.EqualTo(2));
        }
    }
}
