namespace CameraViewer.Interfaces.Configuration
{
    /// <summary>
    /// Interface for ONVIF discovery configuration.
    /// Follows Open/Closed Principle - configuration can be changed without modifying code.
    /// </summary>
    public interface IOnvifDiscoveryConfiguration
    {
        /// <summary>
        /// Multicast address for ONVIF discovery.
        /// </summary>
        string MulticastAddress { get; }

        /// <summary>
        /// Multicast port for ONVIF discovery.
        /// </summary>
        int MulticastPort { get; }

        /// <summary>
        /// Discovery timeout in milliseconds.
        /// </summary>
        int DiscoveryTimeoutMs { get; }

        /// <summary>
        /// Number of probe retries for reliability.
        /// </summary>
        int ProbeRetries { get; }

        /// <summary>
        /// Delay between probe retries in milliseconds.
        /// </summary>
        int ProbeDelayMs { get; }

        /// <summary>
        /// Alternative ONVIF ports to scan.
        /// </summary>
        int[] AlternativePorts { get; }

        /// <summary>
        /// IP ranges to scan (start, count).
        /// </summary>
        (int Start, int Count)[] IpRanges { get; }

        /// <summary>
        /// Timeout for TCP connection attempts in milliseconds.
        /// </summary>
        int TcpConnectionTimeoutMs { get; }
    }
}
