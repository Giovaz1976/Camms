using CameraViewer.Interfaces.Network;

namespace CameraViewer.Implementation.Network
{
    /// <summary>
    /// Factory for creating TCP client instances.
    /// Implements Factory Pattern for dependency injection.
    /// </summary>
    public class TcpClientFactory : ITcpClientFactory
    {
        public ITcpClient Create()
        {
            return new TcpClientAdapter();
        }
    }
}
