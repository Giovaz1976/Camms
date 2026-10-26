using System;
using System.Threading.Tasks;
using CameraViewer.Models;

namespace CameraViewer.Interfaces.Audio
{
    /// <summary>
    /// A single RTSP connection used to probe for, and later drive, an ONVIF backchannel
    /// (two-way audio) session with a camera. The same TCP connection carries the text RTSP
    /// requests/responses and, once PLAY is sent, the interleaved binary RTP data - so unlike
    /// most network abstractions in this project, implementations talk to the socket directly.
    /// </summary>
    public interface IRtspBackchannelSession : IDisposable
    {
        /// <summary>Opens the TCP connection to the camera's RTSP port.</summary>
        Task ConnectAsync(string host, int port);

        /// <summary>
        /// Sends an RTSP OPTIONS request, retrying once with authentication if the camera
        /// challenges with a 401.
        /// </summary>
        Task<RtspResponse> SendOptionsAsync(string uri, string username, string password);

        /// <summary>
        /// Sends an RTSP DESCRIBE request with the ONVIF backchannel "Require" header, retrying
        /// once with authentication if the camera challenges with a 401.
        /// </summary>
        Task<RtspResponse> SendDescribeAsync(string uri, string username, string password);
    }
}
