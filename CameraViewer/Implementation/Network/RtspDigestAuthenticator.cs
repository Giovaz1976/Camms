using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CameraViewer.Interfaces.Network;

namespace CameraViewer.Implementation.Network
{
    /// <summary>
    /// Builds RTSP/HTTP Digest (RFC 2617, MD5) or Basic Authorization header values from a
    /// server's "WWW-Authenticate" challenge. RTSP cameras almost always challenge with Digest;
    /// Basic is kept only as a fallback for the rare camera that uses it.
    /// </summary>
    public class RtspDigestAuthenticator : IRtspAuthenticator
    {
        // Matches key="value" or key=value pairs inside a WWW-Authenticate challenge, e.g.
        // Digest realm="camera", nonce="abc123", qop="auth"
        private static readonly Regex ChallengePartRegex =
            new(@"(\w+)=(?:""([^""]*)""|([^,\s]+))", RegexOptions.Compiled);

        public string? BuildAuthorizationHeader(string wwwAuthenticate, string method, string uri,
            string username, string password)
        {
            if (string.IsNullOrWhiteSpace(wwwAuthenticate))
                return null;

            var trimmed = wwwAuthenticate.Trim();

            if (trimmed.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
                return BuildDigestHeader(trimmed, method, uri, username, password);

            if (trimmed.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
                return BuildBasicHeader(username, password);

            return null;
        }

        private static string BuildBasicHeader(string username, string password)
        {
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            return $"Basic {authValue}";
        }

        private static string BuildDigestHeader(string challenge, string method, string uri,
            string username, string password)
        {
            var parts = ParseChallengeParts(challenge);

            // Challenge missing the mandatory fields - can't build a Digest response. Falling
            // back to Basic at least gives the server something to reject with a clear reason
            // instead of throwing here.
            if (!parts.TryGetValue("realm", out var realm) || !parts.TryGetValue("nonce", out var nonce))
                return BuildBasicHeader(username, password);

            parts.TryGetValue("qop", out var qop);
            parts.TryGetValue("opaque", out var opaque);

            var ha1 = Md5Hex($"{username}:{realm}:{password}");
            var ha2 = Md5Hex($"{method}:{uri}");

            string response;
            string? cnonce = null;
            const string nc = "00000001";

            if (!string.IsNullOrEmpty(qop))
            {
                cnonce = Guid.NewGuid().ToString("N").Substring(0, 16);
                response = Md5Hex($"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");
            }
            else
            {
                response = Md5Hex($"{ha1}:{nonce}:{ha2}");
            }

            var header = new StringBuilder("Digest ");
            header.Append($"username=\"{username}\", ");
            header.Append($"realm=\"{realm}\", ");
            header.Append($"nonce=\"{nonce}\", ");
            header.Append($"uri=\"{uri}\", ");
            header.Append($"response=\"{response}\"");

            if (!string.IsNullOrEmpty(opaque))
                header.Append($", opaque=\"{opaque}\"");

            if (!string.IsNullOrEmpty(qop))
            {
                header.Append($", qop={qop}");
                header.Append($", nc={nc}");
                header.Append($", cnonce=\"{cnonce}\"");
            }

            return header.ToString();
        }

        private static Dictionary<string, string> ParseChallengeParts(string challenge)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in ChallengePartRegex.Matches(challenge))
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                result[key] = value;
            }
            return result;
        }

        private static string Md5Hex(string input)
        {
            var bytes = MD5.HashData(Encoding.ASCII.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
