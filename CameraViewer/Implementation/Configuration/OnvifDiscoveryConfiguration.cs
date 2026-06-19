using CameraViewer.Interfaces.Configuration;

namespace CameraViewer.Implementation.Configuration
{
    /// <summary>
    /// Configuration for ONVIF discovery operations.
    /// Provides default values that can be overridden.
    /// </summary>
    public class OnvifDiscoveryConfiguration : IOnvifDiscoveryConfiguration
    {
        /// <summary>
        /// Multicast address for ONVIF WS-Discovery.
        /// Default: 239.255.255.250
        /// </summary>
        public string MulticastAddress { get; set; } = "239.255.255.250";

        /// <summary>
        /// Multicast port for ONVIF WS-Discovery.
        /// Default: 3702
        /// </summary>
        public int MulticastPort { get; set; } = 3702;

        /// <summary>
        /// Discovery timeout in milliseconds.
        /// Default: 5000 (5 seconds)
        /// </summary>
        public int DiscoveryTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Number of probe retries for reliability.
        /// Default: 3
        /// </summary>
        public int ProbeRetries { get; set; } = 3;

        /// <summary>
        /// Delay between probe retries in milliseconds.
        /// Default: 100
        /// </summary>
        public int ProbeDelayMs { get; set; } = 100;

        /// <summary>
        /// Alternative ONVIF ports to scan.
        /// Default: 10080, 8080, 8899
        /// </summary>
        public int[] AlternativePorts { get; set; } = new[] { 10080, 8080, 8899 };

        /// <summary>
        /// IP ranges to scan (start, count).
        /// Default: 64-90, 100-120, 200-210
        /// </summary>
        public (int Start, int Count)[] IpRanges { get; set; } = new[]
        {
            (64, 27),   // 64-90
            (100, 21),  // 100-120
            (200, 11)   // 200-210
        };

        /// <summary>
        /// Timeout for TCP connection attempts in milliseconds.
        /// Default: 500
        /// </summary>
        public int TcpConnectionTimeoutMs { get; set; } = 500;
    }
}
