using CameraViewer.Implementation.Parsing;

namespace CameraViewer.Tests.Implementation.Parsing
{
    [TestFixture]
    public class OnvifSoapMessageBuilderTests
    {
        private OnvifSoapMessageBuilder _builder = null!;

        [SetUp]
        public void Setup()
        {
            _builder = new OnvifSoapMessageBuilder();
        }

        [Test]
        public void BuildProbeMessage_ShouldReturnValidXml()
        {
            // Act
            var result = _builder.BuildProbeMessage();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Does.Contain("<?xml version=\"1.0\""));
            Assert.That(result, Does.Contain("s:Envelope"));
        }

        [Test]
        public void BuildProbeMessage_ShouldContainProbeAction()
        {
            // Act
            var result = _builder.BuildProbeMessage();

            // Assert
            Assert.That(result, Does.Contain("http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe"));
        }

        [Test]
        public void BuildProbeMessage_ShouldContainNetworkVideoTransmitter()
        {
            // Act
            var result = _builder.BuildProbeMessage();

            // Assert
            Assert.That(result, Does.Contain("NetworkVideoTransmitter"));
        }

        [Test]
        public void BuildProbeMessage_ShouldContainUniqueMessageId()
        {
            // Act
            var result1 = _builder.BuildProbeMessage();
            var result2 = _builder.BuildProbeMessage();

            // Assert
            Assert.That(result1, Is.Not.EqualTo(result2), "Each message should have a unique ID");
        }

        [Test]
        public void BuildGetDeviceInformationMessage_ShouldReturnValidXml()
        {
            // Act
            var result = _builder.BuildGetDeviceInformationMessage();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Does.Contain("<?xml version=\"1.0\""));
            Assert.That(result, Does.Contain("s:Envelope"));
        }

        [Test]
        public void BuildGetDeviceInformationMessage_ShouldContainGetDeviceInformation()
        {
            // Act
            var result = _builder.BuildGetDeviceInformationMessage();

            // Assert
            Assert.That(result, Does.Contain("GetDeviceInformation"));
        }
    }
}
