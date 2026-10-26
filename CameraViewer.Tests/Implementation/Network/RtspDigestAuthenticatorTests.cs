using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CameraViewer.Implementation.Network;

namespace CameraViewer.Tests.Implementation.Network
{
    [TestFixture]
    public class RtspDigestAuthenticatorTests
    {
        private RtspDigestAuthenticator _authenticator = null!;

        private static readonly Regex FieldRegex =
            new(@"(\w+)=(?:""([^""]*)""|([^,\s]+))", RegexOptions.Compiled);

        [SetUp]
        public void Setup()
        {
            _authenticator = new RtspDigestAuthenticator();
        }

        [Test]
        public void BuildAuthorizationHeader_WithQop_ShouldProduceConsistentDigestResponse()
        {
            // Arrange
            const string challenge = "Digest realm=\"testrealm@host.com\", " +
                "nonce=\"dcd98b7102dd2f0e8b11d0f600bbdc7\", qop=\"auth\", " +
                "opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"";
            const string method = "DESCRIBE";
            const string uri = "rtsp://192.168.1.81:554/onvif1";
            const string username = "admin";
            const string password = "Circle Of Life";

            // Act
            var header = _authenticator.BuildAuthorizationHeader(challenge, method, uri, username, password);

            // Assert
            Assert.That(header, Is.Not.Null);
            Assert.That(header, Does.StartWith("Digest "));

            var fields = ParseFields(header!);
            Assert.That(fields["uri"], Is.EqualTo(uri));
            Assert.That(fields["realm"], Is.EqualTo("testrealm@host.com"));
            Assert.That(fields["nonce"], Is.EqualTo("dcd98b7102dd2f0e8b11d0f600bbdc7"));
            Assert.That(fields["qop"], Is.EqualTo("auth"));
            Assert.That(fields.ContainsKey("cnonce"), Is.True);
            Assert.That(fields["nc"], Is.EqualTo("00000001"));

            // Independently recompute the RFC 2617 digest chain from the fields the header
            // actually produced (including the server-unpredictable client nonce) and verify
            // the "response" field matches - this catches a wrong field order or separator
            // without depending on a hand-copied magic hash constant.
            var ha1 = Md5Hex($"{username}:{fields["realm"]}:{password}");
            var ha2 = Md5Hex($"{method}:{uri}");
            var expectedResponse = Md5Hex($"{ha1}:{fields["nonce"]}:{fields["nc"]}:{fields["cnonce"]}:{fields["qop"]}:{ha2}");

            Assert.That(fields["response"], Is.EqualTo(expectedResponse));
        }

        [Test]
        public void BuildAuthorizationHeader_WithoutQop_ShouldProduceConsistentDigestResponse()
        {
            // Arrange - RFC 2069 style challenge, no qop
            const string challenge = "Digest realm=\"camera\", nonce=\"abc123nonce\"";
            const string method = "OPTIONS";
            const string uri = "rtsp://192.168.1.81:554/onvif1";
            const string username = "admin";
            const string password = "secret";

            // Act
            var header = _authenticator.BuildAuthorizationHeader(challenge, method, uri, username, password);

            // Assert
            Assert.That(header, Is.Not.Null);
            var fields = ParseFields(header!);
            Assert.That(fields.ContainsKey("cnonce"), Is.False);
            Assert.That(fields.ContainsKey("qop"), Is.False);

            var ha1 = Md5Hex($"{username}:camera:{password}");
            var ha2 = Md5Hex($"{method}:{uri}");
            var expectedResponse = Md5Hex($"{ha1}:abc123nonce:{ha2}");

            Assert.That(fields["response"], Is.EqualTo(expectedResponse));
        }

        [Test]
        public void BuildAuthorizationHeader_WithBasicChallenge_ShouldReturnBasicHeader()
        {
            // Arrange
            const string challenge = "Basic realm=\"camera\"";
            const string username = "admin";
            const string password = "secret";
            var expected = "Basic " + System.Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

            // Act
            var header = _authenticator.BuildAuthorizationHeader(challenge, "DESCRIBE",
                "rtsp://192.168.1.81/onvif1", username, password);

            // Assert
            Assert.That(header, Is.EqualTo(expected));
        }

        [Test]
        public void BuildAuthorizationHeader_WithUnknownScheme_ShouldReturnNull()
        {
            // Arrange
            const string challenge = "Negotiate abc123";

            // Act
            var header = _authenticator.BuildAuthorizationHeader(challenge, "DESCRIBE",
                "rtsp://192.168.1.81/onvif1", "admin", "secret");

            // Assert
            Assert.That(header, Is.Null);
        }

        [Test]
        public void BuildAuthorizationHeader_WithEmptyChallenge_ShouldReturnNull()
        {
            // Act
            var header = _authenticator.BuildAuthorizationHeader(string.Empty, "DESCRIBE",
                "rtsp://192.168.1.81/onvif1", "admin", "secret");

            // Assert
            Assert.That(header, Is.Null);
        }

        private static Dictionary<string, string> ParseFields(string header)
        {
            var result = new Dictionary<string, string>();
            foreach (Match match in FieldRegex.Matches(header))
            {
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                result[match.Groups[1].Value] = value;
            }
            return result;
        }

        private static string Md5Hex(string input)
        {
            var bytes = MD5.HashData(Encoding.ASCII.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
