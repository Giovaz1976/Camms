using System;
using System.Net;
using System.Threading.Tasks;

namespace CameraViewer.Interfaces.Network
{
    /// <summary>
    /// Interface for network communication operations.
    /// Abstracts UDP/TCP operations for testability and flexibility.
    /// </summary>
    public interface INetworkClient : IDisposable
    {
        /// <summary>
        /// Sends data to a specific endpoint.
        /// </summary>
        /// <param name="data">Data to send.</param>
        /// <param name="endpoint">Destination endpoint.</param>
        Task SendAsync(byte[] data, IPEndPoint endpoint);

        /// <summary>
        /// Receives data from the network.
        /// </summary>
        /// <returns>Tuple containing received data and remote endpoint.</returns>
        Task<(byte[] Data, IPEndPoint RemoteEndpoint)> ReceiveAsync();

        /// <summary>
        /// Joins a multicast group.
        /// </summary>
        /// <param name="multicastAddress">Multicast group address.</param>
        void JoinMulticastGroup(IPAddress multicastAddress);

        /// <summary>
        /// Leaves a multicast group.
        /// </summary>
        /// <param name="multicastAddress">Multicast group address.</param>
        void DropMulticastGroup(IPAddress multicastAddress);

        /// <summary>
        /// Enables or disables broadcast mode.
        /// </summary>
        bool EnableBroadcast { get; set; }

        /// <summary>
        /// Enables or disables multicast loopback.
        /// </summary>
        bool MulticastLoopback { get; set; }
    }
}
