using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CameraViewer.Interfaces.Network;

namespace CameraViewer.Implementation.Network
{
    /// <summary>
    /// UDP network client implementation wrapping System.Net.Sockets.UdpClient.
    /// Provides abstraction for multicast/broadcast operations.
    /// </summary>
    public class UdpNetworkClient : INetworkClient
    {
        private readonly UdpClient _udpClient;
        private bool _disposed;

        public UdpNetworkClient()
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            _udpClient.Client.ReceiveTimeout = 500;
        }

        public bool EnableBroadcast
        {
            get => _udpClient.EnableBroadcast;
            set => _udpClient.EnableBroadcast = value;
        }

        public bool MulticastLoopback
        {
            get => _udpClient.MulticastLoopback;
            set => _udpClient.MulticastLoopback = value;
        }

        public async Task SendAsync(byte[] data, IPEndPoint endpoint)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpNetworkClient));

            await _udpClient.SendAsync(data, data.Length, endpoint);
        }

        public async Task<(byte[] Data, IPEndPoint RemoteEndpoint)> ReceiveAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpNetworkClient));

            var result = await _udpClient.ReceiveAsync();
            return (result.Buffer, result.RemoteEndPoint);
        }

        public void JoinMulticastGroup(IPAddress multicastAddress)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpNetworkClient));

            _udpClient.JoinMulticastGroup(multicastAddress);
        }

        public void DropMulticastGroup(IPAddress multicastAddress)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpNetworkClient));

            _udpClient.DropMulticastGroup(multicastAddress);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _udpClient?.Dispose();
            _disposed = true;
        }
    }
}
