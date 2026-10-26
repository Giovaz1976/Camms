namespace CameraViewer.Models
{
    /// <summary>
    /// Result of probing a camera's RTSP DESCRIBE response for ONVIF backchannel
    /// (two-way audio) support. Produced by ISdpParser, consumed by TwoWayAudioService.
    /// </summary>
    public class BackchannelCapability
    {
        /// <summary>Whether the camera's SDP answer advertises a sendonly audio media.</summary>
        public bool Supported { get; set; }

        /// <summary>
        /// Absolute RTSP control URL for the backchannel media track (resolved from the SDP
        /// "a=control:" attribute, relative to Content-Base/request URL if needed).
        /// Null when Supported is false.
        /// </summary>
        public string? ControlUrl { get; set; }

        /// <summary>RTP payload type for the backchannel media (0 = PCMU, 8 = PCMA).</summary>
        public int PayloadType { get; set; }

        /// <summary>Codec name parsed from the SDP "a=rtpmap:" line (e.g. "PCMU", "PCMA").</summary>
        public string? Codec { get; set; }

        /// <summary>
        /// Human-readable reason, used for diagnostics when Supported is false or probing failed
        /// before a DESCRIBE response was even received (e.g. "401 Unauthorized", "timeout",
        /// "SDP has no sendonly audio media").
        /// </summary>
        public string? Reason { get; set; }
    }
}
