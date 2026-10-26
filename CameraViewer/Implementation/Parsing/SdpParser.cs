using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CameraViewer.Interfaces.Parsing;
using CameraViewer.Models;

namespace CameraViewer.Implementation.Parsing
{
    /// <summary>
    /// Parses SDP (RFC 4566) bodies from RTSP DESCRIBE responses, looking specifically for the
    /// ONVIF backchannel audio media: an "m=audio" section marked "a=sendonly" is the direction
    /// the CLIENT is expected to send audio in (the camera's own speaker output).
    /// </summary>
    public class SdpParser : ISdpParser
    {
        private static readonly Regex MediaLineRegex = new(@"^m=(\w+)\s+\S+\s+\S+\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex RtpMapRegex = new(@"^a=rtpmap:(\d+)\s+([^/\s]+)", RegexOptions.Compiled);
        private static readonly Regex ControlRegex = new(@"^a=control:(.+)$", RegexOptions.Compiled);

        public BackchannelCapability ParseBackchannelCapability(string sdp, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(sdp))
                return NotSupported("Empty SDP response");

            foreach (var block in SplitMediaBlocks(sdp))
            {
                var mediaMatch = MediaLineRegex.Match(block[0]);
                if (!mediaMatch.Success ||
                    !mediaMatch.Groups[1].Value.Equals("audio", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isSendOnly = block.Exists(l => l.Equals("a=sendonly", StringComparison.OrdinalIgnoreCase));
                if (!isSendOnly)
                    continue;

                // "m=audio <port> <proto> <fmt> [<fmt> ...]" - the backchannel media only ever
                // advertises one payload type in practice, so take the first one listed.
                var formats = mediaMatch.Groups[2].Value.Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (formats.Length == 0 || !int.TryParse(formats[0], out var payloadType))
                    continue;

                string? codec = null;
                string? controlUrl = null;

                foreach (var line in block)
                {
                    var rtpMapMatch = RtpMapRegex.Match(line);
                    if (rtpMapMatch.Success &&
                        int.TryParse(rtpMapMatch.Groups[1].Value, out var pt) && pt == payloadType)
                    {
                        codec = rtpMapMatch.Groups[2].Value;
                    }

                    var controlMatch = ControlRegex.Match(line);
                    if (controlMatch.Success)
                    {
                        controlUrl = ResolveControlUrl(controlMatch.Groups[1].Value.Trim(), baseUrl);
                    }
                }

                return new BackchannelCapability
                {
                    Supported = true,
                    ControlUrl = controlUrl ?? baseUrl,
                    PayloadType = payloadType,
                    Codec = codec ?? DefaultCodecName(payloadType)
                };
            }

            return NotSupported("SDP has no sendonly audio media (camera does not advertise backchannel)");
        }

        /// <summary>
        /// Splits an SDP body into media-level blocks: everything from an "m=" line up to (but
        /// not including) the next "m=" line. Session-level lines before the first "m=" are
        /// discarded - the backchannel media's own attributes are all that matter here.
        /// </summary>
        private static List<List<string>> SplitMediaBlocks(string sdp)
        {
            var mediaBlocks = new List<List<string>>();
            List<string>? currentBlock = null;

            foreach (var rawLine in sdp.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("m=", StringComparison.Ordinal))
                {
                    currentBlock = new List<string> { line };
                    mediaBlocks.Add(currentBlock);
                }
                else
                {
                    currentBlock?.Add(line);
                }
            }

            return mediaBlocks;
        }

        private static string ResolveControlUrl(string control, string baseUrl)
        {
            // "*" means "use the session-level/request URL as-is" per RFC 2326 C.1.1.
            if (control == "*")
                return baseUrl;

            if (Uri.TryCreate(control, UriKind.Absolute, out _))
                return control;

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, control, out var resolved))
            {
                return resolved.ToString();
            }

            // baseUrl wasn't a well-formed absolute URI (shouldn't happen in practice) - fall
            // back to naive concatenation so the caller at least gets something to try.
            return baseUrl.TrimEnd('/') + "/" + control.TrimStart('/');
        }

        private static string DefaultCodecName(int payloadType) => payloadType switch
        {
            0 => "PCMU",
            8 => "PCMA",
            _ => $"PT{payloadType}"
        };

        private static BackchannelCapability NotSupported(string reason) => new()
        {
            Supported = false,
            Reason = reason
        };
    }
}
