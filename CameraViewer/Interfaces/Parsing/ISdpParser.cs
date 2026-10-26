using CameraViewer.Models;

namespace CameraViewer.Interfaces.Parsing
{
    /// <summary>
    /// Parses an RTSP DESCRIBE response body (SDP) to detect ONVIF backchannel
    /// (two-way audio) support.
    /// </summary>
    public interface ISdpParser
    {
        /// <summary>
        /// Looks for an "m=audio" media section marked "a=sendonly" (the direction the camera
        /// expects the client to send audio in) and extracts its RTP payload type, codec and
        /// control URL.
        /// </summary>
        /// <param name="sdp">The SDP body returned by the camera's DESCRIBE response.</param>
        /// <param name="baseUrl">
        /// Base URL to resolve a relative "a=control:" attribute against (the request URL, or
        /// the response's "Content-Base"/"Content-Location" header if present).
        /// </param>
        /// <returns>
        /// A BackchannelCapability with Supported=true and the resolved details if a sendonly
        /// audio media was found; otherwise Supported=false with a human-readable Reason.
        /// </returns>
        BackchannelCapability ParseBackchannelCapability(string sdp, string baseUrl);
    }
}
