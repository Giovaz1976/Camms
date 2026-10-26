using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Interfaces.Audio;
using CameraViewer.Interfaces.Network;
using CameraViewer.Models;

namespace CameraViewer.Implementation.Audio
{
    /// <summary>
    /// Minimal RTSP client over a raw TCP socket. Text-based RTSP requests/responses share the
    /// same TCP stream as the binary interleaved RTP data once PLAY is sent, so this talks to
    /// the socket directly instead of going through ITcpClient/HttpClient.
    /// </summary>
    public class RtspBackchannelSession : IRtspBackchannelSession
    {
        private readonly IRtspAuthenticator _authenticator;
        private readonly TimeSpan _timeout;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private int _cSeq = 1;
        private bool _disposed;

        public RtspBackchannelSession(IRtspAuthenticator authenticator, TimeSpan timeout)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _timeout = timeout;
        }

        public async Task ConnectAsync(string host, int port)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RtspBackchannelSession));

            using var cts = new CancellationTokenSource(_timeout);
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port, cts.Token);
            _stream = _tcpClient.GetStream();
        }

        public Task<RtspResponse> SendOptionsAsync(string uri, string username, string password)
            => SendRequestAsync("OPTIONS", uri, username, password);

        public Task<RtspResponse> SendDescribeAsync(string uri, string username, string password)
            => SendRequestAsync("DESCRIBE", uri, username, password, new Dictionary<string, string>
            {
                ["Require"] = "www.onvif.org/ver20/backchannel",
                ["Accept"] = "application/sdp"
            });

        private async Task<RtspResponse> SendRequestAsync(string method, string uri, string username,
            string password, Dictionary<string, string>? extraHeaders = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RtspBackchannelSession));
            if (_stream == null)
                throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

            var response = await SendOnceAsync(method, uri, extraHeaders, authorization: null);

            // Retry exactly once with credentials if the server challenges us. A second 401
            // after that just means wrong credentials or an unsupported scheme - the caller
            // gets the raw response either way and decides what to report.
            if (response.StatusCode == 401 &&
                response.Headers.TryGetValue("WWW-Authenticate", out var challenge))
            {
                var header = _authenticator.BuildAuthorizationHeader(challenge, method, uri, username, password);
                if (header != null)
                    response = await SendOnceAsync(method, uri, extraHeaders, header);
            }

            return response;
        }

        private async Task<RtspResponse> SendOnceAsync(string method, string uri,
            Dictionary<string, string>? extraHeaders, string? authorization)
        {
            var request = new StringBuilder();
            request.Append($"{method} {uri} RTSP/1.0\r\n");
            request.Append($"CSeq: {_cSeq++}\r\n");
            request.Append("User-Agent: CameraViewer\r\n");

            if (extraHeaders != null)
            {
                foreach (var header in extraHeaders)
                    request.Append($"{header.Key}: {header.Value}\r\n");
            }

            if (authorization != null)
                request.Append($"Authorization: {authorization}\r\n");

            request.Append("\r\n");

            using var cts = new CancellationTokenSource(_timeout);
            var bytes = Encoding.ASCII.GetBytes(request.ToString());
            await _stream!.WriteAsync(bytes, cts.Token);

            return await ReadResponseAsync(cts.Token);
        }

        private async Task<RtspResponse> ReadResponseAsync(CancellationToken cancellationToken)
        {
            // RTSP responses are "status line \r\n headers \r\n\r\n [body]" - read byte by byte
            // until the blank line (responses are small, this isn't a hot path), then read
            // exactly Content-Length bytes for the body.
            var headerText = await ReadUntilBlankLineAsync(cancellationToken);
            var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var response = new RtspResponse();
            if (lines.Length > 0)
            {
                var statusParts = lines[0].Split(' ', 3);
                if (statusParts.Length >= 2 && int.TryParse(statusParts[1], out var statusCode))
                {
                    response.StatusCode = statusCode;
                    response.StatusText = statusParts.Length > 2 ? statusParts[2] : string.Empty;
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var separator = lines[i].IndexOf(':');
                if (separator <= 0)
                    continue;

                response.Headers[lines[i][..separator].Trim()] = lines[i][(separator + 1)..].Trim();
            }

            if (response.Headers.TryGetValue("Content-Length", out var lengthHeader) &&
                int.TryParse(lengthHeader, out var contentLength) && contentLength > 0)
            {
                response.Body = await ReadExactAsync(contentLength, cancellationToken);
            }

            return response;
        }

        private async Task<string> ReadUntilBlankLineAsync(CancellationToken cancellationToken)
        {
            var buffer = new List<byte>();
            var singleByte = new byte[1];

            while (true)
            {
                int read = await _stream!.ReadAsync(singleByte, cancellationToken);
                if (read == 0)
                    break; // connection closed by the camera

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

        private async Task<string> ReadExactAsync(int length, CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await _stream!.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                if (read == 0)
                    break; // connection closed early - return what we got

                offset += read;
            }
            return Encoding.ASCII.GetString(buffer, 0, offset);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _stream?.Dispose();
            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}
