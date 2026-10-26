using System.Net;
using System.Net.Sockets;
using System.Text;
using CameraViewer.Implementation.Audio;
using CameraViewer.Implementation.Network;
using CameraViewer.Implementation.Parsing;

namespace CameraViewer.Tests.Implementation.Audio
{
    /// <summary>
    /// Validates RtspBackchannelSession's request/response handling (CSeq, header parsing,
    /// Content-Length body read, and the 401-challenge-then-retry-with-Digest flow) against a
    /// minimal RTSP server mock on a loopback TcpListener. Same precedent as the HttpListener
    /// mock used to validate the ONVIF PTZ pipeline before testing against real hardware.
    /// </summary>
    [TestFixture]
    public class RtspBackchannelSessionTests
    {
        private const string Sdp =
            "v=0\r\n" +
            "m=audio 0 RTP/AVP 0\r\n" +
            "a=rtpmap:0 PCMU/8000\r\n" +
            "a=control:trackID=2\r\n" +
            "a=sendonly\r\n";

        [Test]
        public async Task SendDescribeAsync_WhenChallengedWithDigest_ShouldRetryWithAuthorizationAndSucceed()
        {
            // Arrange
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverTask = RunMockServerAsync(listener);

            var session = new RtspBackchannelSession(new RtspDigestAuthenticator(), TimeSpan.FromSeconds(5));
            var uri = $"rtsp://127.0.0.1:{port}/onvif1";

            try
            {
                // Act
                await session.ConnectAsync("127.0.0.1", port);

                var options = await session.SendOptionsAsync(uri, "admin", "secret");
                var describe = await session.SendDescribeAsync(uri, "admin", "secret");

                // Assert
                Assert.That(options.StatusCode, Is.EqualTo(200));
                Assert.That(describe.StatusCode, Is.EqualTo(200));
                Assert.That(describe.Body, Is.EqualTo(Sdp));

                var capability = new SdpParser().ParseBackchannelCapability(describe.Body, uri);
                Assert.That(capability.Supported, Is.True);
                Assert.That(capability.Codec, Is.EqualTo("PCMU"));
            }
            finally
            {
                session.Dispose();
                listener.Stop();
                await serverTask;
            }
        }

        /// <summary>
        /// Accepts exactly one connection and plays out: OPTIONS -> 200, DESCRIBE (no auth) ->
        /// 401 + Digest challenge, DESCRIBE (with Authorization) -> 200 + SDP body. Verifies the
        /// retry actually carries a non-empty Authorization header before answering 200, so a
        /// session that silently gave up after the 401 would fail this test's own read, not just
        /// the caller's assertions.
        /// </summary>
        private static async Task RunMockServerAsync(TcpListener listener)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            await ReadRequestAsync(stream); // OPTIONS
            await WriteResponseAsync(stream, "RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");

            await ReadRequestAsync(stream); // DESCRIBE, no credentials yet
            await WriteResponseAsync(stream,
                "RTSP/1.0 401 Unauthorized\r\n" +
                "CSeq: 2\r\n" +
                "WWW-Authenticate: Digest realm=\"test\", nonce=\"abc123\", qop=\"auth\"\r\n\r\n");

            var retry = await ReadRequestAsync(stream); // DESCRIBE, retried with Authorization
            Assert.That(retry, Does.Contain("Authorization: Digest"));

            var body = Encoding.ASCII.GetBytes(Sdp);
            var response = "RTSP/1.0 200 OK\r\n" +
                "CSeq: 3\r\n" +
                "Content-Type: application/sdp\r\n" +
                $"Content-Length: {body.Length}\r\n\r\n" +
                Sdp;
            await WriteResponseAsync(stream, response);
        }

        private static async Task<string> ReadRequestAsync(NetworkStream stream)
        {
            var buffer = new List<byte>();
            var singleByte = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(singleByte);
                if (read == 0)
                    break;

                buffer.Add(singleByte[0]);

                if (buffer.Count >= 4 &&
                    buffer[^4] == (byte)'\r' && buffer[^3] == (byte)'\n' &&
                    buffer[^2] == (byte)'\r' && buffer[^1] == (byte)'\n')
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(buffer.ToArray());
        }

        private static async Task WriteResponseAsync(NetworkStream stream, string response)
        {
            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes);
        }
    }
}
