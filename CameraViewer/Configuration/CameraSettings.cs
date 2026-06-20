namespace CameraViewer.Configuration
{
    /// <summary>
    /// Configuration settings for camera connections.
    /// Loaded from appsettings.json "Camera" section.
    /// </summary>
    public class CameraSettings
    {
        /// <summary>
        /// Default username for camera authentication.
        /// Default: "admin"
        /// </summary>
        public string DefaultUsername { get; set; } = "admin";

        /// <summary>
        /// Default password for camera authentication.
        /// Default: "" (empty)
        /// </summary>
        public string DefaultPassword { get; set; } = "";

        /// <summary>
        /// Connection timeout in milliseconds.
        /// Default: 5000 (5 seconds)
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// Stream timeout in milliseconds.
        /// Default: 30000 (30 seconds)
        /// </summary>
        public int StreamTimeout { get; set; } = 30000;

        /// <summary>
        /// Number of retry attempts for failed connections.
        /// Default: 3
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts in milliseconds.
        /// Default: 1000 (1 second)
        /// </summary>
        public int RetryDelay { get; set; } = 1000;
    }
}
