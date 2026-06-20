using CameraViewer.Implementation.Parsing;

namespace CameraViewer.Tests.Implementation.Parsing
{
    [TestFixture]
    public class OnvifResponseParserTests
    {
        private OnvifResponseParser _parser = null!;

        [SetUp]
        public void Setup()
        {
            _parser = new OnvifResponseParser();
        }

        [Test]
        public void ParseProbeMatch_WithValidResponse_ShouldReturnCameraInfo()
        {
            // Arrange
            var validResponse = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
    <s:Body>
        <d:ProbeMatches>
            <d:ProbeMatch>
                <d:XAddrs>http://192.168.1.100:80/onvif/device_service</d:XAddrs>
                <d:Scopes>onvif://www.onvif.org/name/TestCamera</d:Scopes>
            </d:ProbeMatch>
        </d:ProbeMatches>
    </s:Body>
</s:Envelope>";

            // Act
            var result = _parser.ParseProbeMatch(validResponse, "192.168.1.100");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.IpAddress, Is.EqualTo("192.168.1.100"));
            Assert.That(result.Port, Is.EqualTo(554)); // Default RTSP port
            Assert.That(result.Name, Does.Contain("TestCamera"));
        }

        [Test]
        public void ParseProbeMatch_WithInvalidXml_ShouldReturnNull()
        {
            // Arrange
            var invalidResponse = "This is not XML";

            // Act
            var result = _parser.ParseProbeMatch(invalidResponse, "192.168.1.100");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseProbeMatch_WithMissingProbeMatch_ShouldReturnNull()
        {
            // Arrange
            var responseWithoutProbeMatch = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"">
    <s:Body>
    </s:Body>
</s:Envelope>";

            // Act
            var result = _parser.ParseProbeMatch(responseWithoutProbeMatch, "192.168.1.100");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseProbeMatch_WithMissingXAddrs_ShouldReturnNull()
        {
            // Arrange
            var responseWithoutXAddrs = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
    <s:Body>
        <d:ProbeMatches>
            <d:ProbeMatch>
            </d:ProbeMatch>
        </d:ProbeMatches>
    </s:Body>
</s:Envelope>";

            // Act
            var result = _parser.ParseProbeMatch(responseWithoutXAddrs, "192.168.1.100");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ExtractScopeName_WithValidScope_ShouldReturnName()
        {
            // Arrange
            var scopes = "onvif://www.onvif.org/type/NetworkVideoTransmitter onvif://www.onvif.org/name/MyCamera";

            // Act
            var result = _parser.ExtractScopeName(scopes);

            // Assert
            Assert.That(result, Is.EqualTo("MyCamera"));
        }

        [Test]
        public void ExtractScopeName_WithoutNameScope_ShouldReturnNull()
        {
            // Arrange
            var scopes = "onvif://www.onvif.org/type/NetworkVideoTransmitter";

            // Act
            var result = _parser.ExtractScopeName(scopes);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ExtractScopeName_WithEmptyString_ShouldReturnNull()
        {
            // Act
            var result = _parser.ExtractScopeName("");

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}
