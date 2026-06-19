using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Models;

namespace CameraViewer.Interfaces.Discovery
{
    /// <summary>
    /// Interface for ONVIF-specific camera discovery.
    /// Extends ICameraDiscovery with ONVIF-specific capabilities.
    /// </summary>
    public interface IOnvifDiscovery : ICameraDiscovery
    {
        // Base discovery is inherited from ICameraDiscovery
        // This interface can be extended with ONVIF-specific methods if needed
    }
}
