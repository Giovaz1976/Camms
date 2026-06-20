using System.Collections.Generic;

namespace CameraViewer.Configuration
{
    /// <summary>
    /// Configuration settings for ONVIF camera discovery.
    /// Loaded from appsettings.json "OnvifDiscovery" section.
    /// </summary>
    public class OnvifDiscoverySettings
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
        public int DiscoveryTimeout { get; set; } = 5000;

        /// <summary>
        /// Alternative ports to scan if multicast discovery fails.
        /// Default: [10080, 8080, 8899]
        /// </summary>
        public List<int> AlternativePorts { get; set; } = new() { 10080, 8080, 8899 };

        /// <summary>
        /// IP ranges to scan for cameras.
        /// </summary>
        public List<ScanRange> ScanRanges { get; set; } = new()
        {
            new ScanRange { StartOffset = 64, Count = 27 },
            new ScanRange { StartOffset = 100, Count = 21 },
            new ScanRange { StartOffset = 200, Count = 11 }
        };
    }

    /// <summary>
    /// Represents an IP range to scan.
    /// </summary>
    public class ScanRange
    {
        /// <summary>
        /// Starting offset in the subnet (e.g., 64 for x.x.x.64).
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Number of IPs to scan from the start offset.
        /// </summary>
        public int Count { get; set; }
    }
}
