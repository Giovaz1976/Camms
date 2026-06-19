namespace CameraViewer.Interfaces.Discovery
{
    /// <summary>
    /// Interface for ONVIF discovery using WS-Discovery multicast protocol.
    /// Segregated interface for multicast-specific discovery.
    /// </summary>
    public interface IOnvifMulticastDiscovery : IOnvifDiscovery
    {
        // Inherits DiscoverAsync from ICameraDiscovery
        // Specific to multicast/broadcast discovery on standard ONVIF ports
    }
}
