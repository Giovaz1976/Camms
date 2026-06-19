using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Models;

namespace CameraViewer.Interfaces.Discovery
{
    /// <summary>
    /// Base interface for camera discovery mechanisms.
    /// Follows Interface Segregation Principle - clients depend only on discovery capability.
    /// </summary>
    public interface ICameraDiscovery
    {
        /// <summary>
        /// Event raised when a camera is discovered during scanning.
        /// </summary>
        event EventHandler<CameraInfo>? CameraDiscovered;

        /// <summary>
        /// Discovers cameras on the network.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the discovery operation.</param>
        /// <returns>List of discovered cameras. Never returns null.</returns>
        Task<List<CameraInfo>> DiscoverAsync(CancellationToken cancellationToken = default);
    }
}
