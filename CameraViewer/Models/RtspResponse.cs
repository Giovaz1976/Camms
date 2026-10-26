using System;
using System.Collections.Generic;

namespace CameraViewer.Models
{
    /// <summary>
    /// A parsed RTSP response (status line + headers + optional body), as returned by
    /// RtspBackchannelSession.
    /// </summary>
    public class RtspResponse
    {
        public int StatusCode { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; set; } = string.Empty;

        public bool IsSuccess => StatusCode is >= 200 and < 300;
    }
}
