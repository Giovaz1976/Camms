using System.Linq;
using CameraViewer.Interfaces.Configuration;

namespace CameraViewer.Implementation.Configuration
{
    /// <summary>
    /// Configuration for ONVIF discovery operations.
    /// Wraps OnvifDiscoverySettings from appsettings.json and adapts it to IOnvifDiscoveryConfiguration interface.
    /// </summary>
    public class OnvifDiscoveryConfiguration : IOnvifDiscoveryConfiguration
    {
        private readonly CameraViewer.Configuration.OnvifDiscoverySettings _settings;

        /// <summary>
        /// Creates configuration from OnvifDiscoverySettings loaded from appsettings.json.
        /// </summary>
        public OnvifDiscoveryConfiguration(CameraViewer.Configuration.OnvifDiscoverySettings settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        public string MulticastAddress => _settings.MulticastAddress;
        public int MulticastPort => _settings.MulticastPort;
        public int DiscoveryTimeoutMs => _settings.DiscoveryTimeout;
        public int ProbeRetries { get; set; } = 3;  // Not in appsettings yet
        public int ProbeDelayMs { get; set; } = 100;  // Not in appsettings yet
        public int[] AlternativePorts => _settings.AlternativePorts.ToArray();
        
        public (int Start, int Count)[] IpRanges => _settings.ScanRanges
            .Select(r => (r.StartOffset, r.Count))
            .ToArray();
        
        public int TcpConnectionTimeoutMs { get; set; } = 500;  // Not in appsettings yet
    }
}
