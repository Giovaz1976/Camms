using System.Net;

namespace CameraViewer.Interfaces.Network
{
    /// <summary>
    /// Interface for network interface operations.
    /// Provides network information and utilities.
    /// </summary>
    public interface INetworkInterfaceHelper
    {
        /// <summary>
        /// Gets the local subnet (e.g., "192.168.1").
        /// </summary>
        /// <returns>Subnet string or null if not found.</returns>
        string? GetLocalSubnet();

        /// <summary>
        /// Gets the local IP address.
        /// </summary>
        /// <returns>Local IP address or null if not found.</returns>
        IPAddress? GetLocalIPAddress();
    }
}
