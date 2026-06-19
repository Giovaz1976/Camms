using System;
using System.Threading.Tasks;

namespace CameraViewer.Interfaces.Network
{
    /// <summary>
    /// Interface for TCP client operations.
    /// Abstracts TCP connection for port scanning and testing.
    /// </summary>
    public interface ITcpClient : IDisposable
    {
        /// <summary>
        /// Connects to a remote host.
        /// </summary>
        /// <param name="host">Host address.</param>
        /// <param name="port">Port number.</param>
        Task ConnectAsync(string host, int port);

        /// <summary>
        /// Gets whether the client is connected.
        /// </summary>
        bool Connected { get; }
    }
}
