namespace CameraViewer.Configuration
{
    /// <summary>
    /// Configuration settings for video streaming.
    /// Loaded from appsettings.json "Streaming" section.
    /// </summary>
    public class StreamingSettings
    {
        /// <summary>
        /// VLC caching in milliseconds.
        /// Default: 300
        /// </summary>
        public int CachingMs { get; set; } = 300;

        /// <summary>
        /// Network caching in milliseconds.
        /// Default: 1000
        /// </summary>
        public int NetworkCachingMs { get; set; } = 1000;

        /// <summary>
        /// Enable hardware decoding (D3D11VA, etc.).
        /// Default: true
        /// </summary>
        public bool EnableHardwareDecoding { get; set; } = true;

        /// <summary>
        /// Preferred video codec.
        /// Default: "h264"
        /// </summary>
        public string PreferredCodec { get; set; } = "h264";

        /// <summary>
        /// Buffer size for streaming.
        /// Default: 8192
        /// </summary>
        public int BufferSize { get; set; } = 8192;
    }
}
