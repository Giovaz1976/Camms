namespace CameraViewer.Interfaces.Network
{
    /// <summary>
    /// Factory interface for creating TCP clients.
    /// Follows Dependency Inversion Principle - allows testable creation of TCP clients.
    /// </summary>
    public interface ITcpClientFactory
    {
        /// <summary>
        /// Creates a new TCP client instance.
        /// </summary>
        /// <returns>New ITcpClient instance.</returns>
        ITcpClient Create();
    }
}
