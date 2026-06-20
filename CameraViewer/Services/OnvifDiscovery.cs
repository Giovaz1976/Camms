using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CameraViewer.Models;
using CameraViewer.Implementation.Logging;
using CameraViewer.Implementation.Parsing;
using CameraViewer.Implementation.Configuration;
using CameraViewer.Implementation.Discovery;
using CameraViewer.Interfaces.Discovery;

namespace CameraViewer.Services
{
    /// <summary>
    /// Servicio para descubrir cámaras ONVIF en la red local usando WS-Discovery.
    /// REFACTORED: Now uses SOLID implementations internally for better testability.
    /// Maintains backward compatibility with existing code.
    /// </summary>
    public class OnvifDiscovery : IDisposable
    {
        private const string MULTICAST_ADDRESS = "239.255.255.250";
        private const int MULTICAST_PORT = 3702;
        private const int DISCOVERY_TIMEOUT = 3000; // 3 segundos

        // New SOLID implementations
        private readonly IOnvifMulticastDiscovery _multicastDiscovery;
        private readonly IOnvifPortScanner _portScanner;

        public event EventHandler<CameraInfo>? CameraDiscovered;

        /// <summary>
        /// Default constructor - creates instances with default dependencies.
        /// Maintains backward compatibility.
        /// </summary>
        public OnvifDiscovery()
        {
            // Create default dependencies
            var logger = new DebugLogger("[ONVIF]");
            var messageBuilder = new OnvifSoapMessageBuilder();
            var responseParser = new OnvifResponseParser();
            
            // Create default settings
            var settings = new Configuration.OnvifDiscoverySettings();
            var config = new Implementation.Configuration.OnvifDiscoveryConfiguration(settings);
            var tcpFactory = new Implementation.Network.TcpClientFactory();

            // Create discovery implementations
            _multicastDiscovery = new OnvifMulticastDiscovery(logger, messageBuilder, responseParser, config);
            _portScanner = new OnvifPortScanner(logger, tcpFactory, config);

            // Forward events
            _multicastDiscovery.CameraDiscovered += (s, e) => CameraDiscovered?.Invoke(this, e);
            _portScanner.CameraDiscovered += (s, e) => CameraDiscovered?.Invoke(this, e);
        }

        /// <summary>
        /// Constructor with dependency injection (for testing and advanced scenarios).
        /// </summary>
        public OnvifDiscovery(IOnvifMulticastDiscovery multicastDiscovery, IOnvifPortScanner portScanner)
        {
            _multicastDiscovery = multicastDiscovery ?? throw new ArgumentNullException(nameof(multicastDiscovery));
            _portScanner = portScanner ?? throw new ArgumentNullException(nameof(portScanner));

            // Forward events
            _multicastDiscovery.CameraDiscovered += (s, e) => CameraDiscovered?.Invoke(this, e);
            _portScanner.CameraDiscovered += (s, e) => CameraDiscovered?.Invoke(this, e);
        }

        /// <summary>
        /// Discovers ONVIF cameras using multicast/broadcast.
        /// REFACTORED: Now delegates to OnvifMulticastDiscovery implementation.
        /// </summary>
        public async Task<List<CameraInfo>> DiscoverCamerasAsync(CancellationToken cancellationToken = default)
        {
            // Delegate to new SOLID implementation
            return await _multicastDiscovery.DiscoverAsync(cancellationToken);
        }

        // Helper methods removed - now in specialized classes:
        // - BuildProbeMessage -> OnvifSoapMessageBuilder
        // - ParseProbeMatch -> OnvifResponseParser
        // - ExtractScopeName -> OnvifResponseParser

        /// <summary>
        /// Scans alternative ONVIF ports on a subnet.
        /// REFACTORED: Now delegates to OnvifPortScanner implementation.
        /// </summary>
        public async Task<List<CameraInfo>> DiscoverCamerasOnAlternativePortsAsync(string subnet, CancellationToken cancellationToken = default)
        {
            // Delegate to new SOLID implementation
            return await _portScanner.ScanSubnetAsync(subnet, cancellationToken);
        }

        public void Dispose()
        {
            // Cleanup si es necesario
        }
    }
}
