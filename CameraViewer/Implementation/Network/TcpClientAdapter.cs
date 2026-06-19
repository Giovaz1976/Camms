using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using CameraViewer.Interfaces.Network;

namespace CameraViewer.Implementation.Network
{
    /// <summary>
    /// Adapter for System.Net.Sockets.TcpClient implementing ITcpClient interface.
    /// Provides abstraction for TCP operations.
    /// </summary>
    public class TcpClientAdapter : ITcpClient
    {
        private readonly TcpClient _tcpClient;
        private bool _disposed;

        public TcpClientAdapter()
        {
            _tcpClient = new TcpClient();
        }

        public bool Connected => _tcpClient.Connected;

        public async Task ConnectAsync(string host, int port)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            await _tcpClient.ConnectAsync(host, port);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _tcpClient?.Dispose();
            _disposed = true;
        }
    }
}
