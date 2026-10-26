namespace CameraViewer.Configuration
{
    /// <summary>
    /// Configuration settings for two-way audio (ONVIF RTSP backchannel).
    /// Loaded from appsettings.json "Audio" section.
    /// </summary>
    public class AudioSettings
    {
        /// <summary>
        /// Microphone input device index for NAudio WaveIn. -1 = system default device.
        /// Default: -1
        /// </summary>
        public int InputDeviceIndex { get; set; } = -1;

        /// <summary>
        /// Capture sample rate in Hz. G.711 backchannel audio is always 8 kHz.
        /// Default: 8000
        /// </summary>
        public int SampleRate { get; set; } = 8000;

        /// <summary>
        /// Preferred RTP payload codec: "PCMU" (payload type 0) or "PCMA" (payload type 8).
        /// The camera's SDP answer is the actual source of truth; this is only the first guess.
        /// Default: "PCMU"
        /// </summary>
        public string Codec { get; set; } = "PCMU";

        /// <summary>
        /// Audio frame duration in milliseconds. 20ms at 8kHz = 160 samples per frame.
        /// Default: 20
        /// </summary>
        public int FrameMs { get; set; } = 20;

        /// <summary>
        /// Default RTSP port to try first when probing backchannel support.
        /// Default: 554
        /// </summary>
        public int RtspPort { get; set; } = 554;

        /// <summary>
        /// Timeout in seconds for each RTSP connection/request attempt. Kept short (same as
        /// OnvifPtzService) because a LAN camera that is listening responds in milliseconds.
        /// Default: 2
        /// </summary>
        public int TimeoutSeconds { get; set; } = 2;

        /// <summary>
        /// Interval in seconds between RTSP keep-alive requests (GET_PARAMETER/OPTIONS) while
        /// a talk session is open.
        /// Default: 20
        /// </summary>
        public int KeepAliveSeconds { get; set; } = 20;
    }
}
