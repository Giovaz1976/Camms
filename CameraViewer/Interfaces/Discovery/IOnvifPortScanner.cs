using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Models;

namespace CameraViewer.Interfaces.Discovery
{
    /// <summary>
    /// Interface for ONVIF discovery using port scanning on alternative ports.
    /// Segregated interface for port scanning-specific discovery.
    /// </summary>
    public interface IOnvifPortScanner : IOnvifDiscovery
    {
        /// <summary>
        /// Scans a specific subnet for cameras on alternative ONVIF ports.
        /// </summary>
        /// <param name="subnet">Subnet to scan (e.g., "192.168.1").</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>List of discovered cameras.</returns>
        Task<List<CameraInfo>> ScanSubnetAsync(string subnet, CancellationToken cancellationToken = default);
    }
}
