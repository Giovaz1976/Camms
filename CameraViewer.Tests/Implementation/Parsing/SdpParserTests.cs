using CameraViewer.Implementation.Parsing;

namespace CameraViewer.Tests.Implementation.Parsing
{
    [TestFixture]
    public class SdpParserTests
    {
        private SdpParser _parser = null!;

        [SetUp]
        public void Setup()
        {
            _parser = new SdpParser();
        }

        [Test]
        public void ParseBackchannelCapability_WithSendonlyAudioMedia_ShouldReturnSupportedCapability()
        {
            // Arrange
            var sdp = "v=0\r\n" +
                "o=- 0 0 IN IP4 192.168.1.81\r\n" +
                "s=Media Presentation\r\n" +
                "t=0 0\r\n" +
                "m=video 0 RTP/AVP 96\r\n" +
                "a=control:trackID=1\r\n" +
                "m=audio 0 RTP/AVP 0\r\n" +
                "a=control:trackID=2\r\n" +
                "a=rtpmap:0 PCMU/8000\r\n" +
                "a=sendonly\r\n";
            const string baseUrl = "rtsp://192.168.1.81:554/onvif1/";

            // Act
            var result = _parser.ParseBackchannelCapability(sdp, baseUrl);

            // Assert
            Assert.That(result.Supported, Is.True);
            Assert.That(result.PayloadType, Is.EqualTo(0));
            Assert.That(result.Codec, Is.EqualTo("PCMU"));
            Assert.That(result.ControlUrl, Is.EqualTo("rtsp://192.168.1.81:554/onvif1/trackID=2"));
        }

        [Test]
        public void ParseBackchannelCapability_WithAbsoluteControlUrl_ShouldReturnThatUrlUnchanged()
        {
            // Arrange
            var sdp = "v=0\r\n" +
                "m=audio 0 RTP/AVP 8\r\n" +
                "a=rtpmap:8 PCMA/8000\r\n" +
                "a=control:rtsp://192.168.1.81:554/trackID=7\r\n" +
                "a=sendonly\r\n";
            const string baseUrl = "rtsp://192.168.1.81:554/onvif1/";

            // Act
            var result = _parser.ParseBackchannelCapability(sdp, baseUrl);

            // Assert
            Assert.That(result.Supported, Is.True);
            Assert.That(result.PayloadType, Is.EqualTo(8));
            Assert.That(result.Codec, Is.EqualTo("PCMA"));
            Assert.That(result.ControlUrl, Is.EqualTo("rtsp://192.168.1.81:554/trackID=7"));
        }

        [Test]
        public void ParseBackchannelCapability_WithControlStar_ShouldReturnBaseUrl()
        {
            // Arrange
            var sdp = "v=0\r\n" +
                "m=audio 0 RTP/AVP 0\r\n" +
                "a=rtpmap:0 PCMU/8000\r\n" +
                "a=control:*\r\n" +
                "a=sendonly\r\n";
            const string baseUrl = "rtsp://192.168.1.81:554/onvif1/";

            // Act
            var result = _parser.ParseBackchannelCapability(sdp, baseUrl);

            // Assert
            Assert.That(result.Supported, Is.True);
            Assert.That(result.ControlUrl, Is.EqualTo(baseUrl));
        }

        [Test]
        public void ParseBackchannelCapability_WithSendrecvAudio_ShouldReturnNotSupported()
        {
            // Arrange - a camera that only exposes its own outgoing audio (sendrecv/recvonly,
            // the one you LISTEN to), with no sendonly media for the client to talk back on.
            var sdp = "v=0\r\n" +
                "m=video 0 RTP/AVP 96\r\n" +
                "a=control:trackID=1\r\n" +
                "m=audio 0 RTP/AVP 0\r\n" +
                "a=control:trackID=2\r\n" +
                "a=rtpmap:0 PCMU/8000\r\n" +
                "a=sendrecv\r\n";

            // Act
            var result = _parser.ParseBackchannelCapability(sdp, "rtsp://192.168.1.81:554/onvif1/");

            // Assert
            Assert.That(result.Supported, Is.False);
            Assert.That(result.Reason, Does.Contain("sendonly"));
        }

        [Test]
        public void ParseBackchannelCapability_WithVideoOnlySdp_ShouldReturnNotSupported()
        {
            // Arrange
            var sdp = "v=0\r\nm=video 0 RTP/AVP 96\r\na=control:trackID=1\r\n";

            // Act
            var result = _parser.ParseBackchannelCapability(sdp, "rtsp://192.168.1.81:554/onvif1/");

            // Assert
            Assert.That(result.Supported, Is.False);
            Assert.That(result.ControlUrl, Is.Null);
        }

        [Test]
        public void ParseBackchannelCapability_WithEmptySdp_ShouldReturnNotSupported()
        {
            // Act
            var result = _parser.ParseBackchannelCapability(string.Empty, "rtsp://192.168.1.81:554/onvif1/");

            // Assert
            Assert.That(result.Supported, Is.False);
            Assert.That(result.Reason, Is.EqualTo("Empty SDP response"));
        }
    }
}
