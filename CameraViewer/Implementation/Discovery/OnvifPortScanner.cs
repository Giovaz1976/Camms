using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Interfaces.Configuration;
using CameraViewer.Interfaces.Discovery;
using CameraViewer.Interfaces.Logging;
using CameraViewer.Interfaces.Network;
using CameraViewer.Models;

namespace CameraViewer.Implementation.Discovery
{
    /// <summary>
    /// ONVIF camera discovery using TCP port scanning on alternative ports.
    /// Implements Single Responsibility Principle - only handles port scanning.
    /// Uses Dependency Injection for all dependencies.
    /// </summary>
    public class OnvifPortScanner : IOnvifPortScanner
    {
        private readonly ILogger _logger;
        private readonly ITcpClientFactory _tcpClientFactory;
        private readonly IOnvifDiscoveryConfiguration _config;

        public event EventHandler<CameraInfo>? CameraDiscovered;

        /// <summary>
        /// Creates a new OnvifPortScanner with injected dependencies.
        /// </summary>
        /// <param name="logger">Logger for debug/error messages.</param>
        /// <param name="tcpClientFactory">Factory for creating TCP clients.</param>
        /// <param name="config">Configuration for scanning parameters.</param>
        public OnvifPortScanner(
            ILogger logger,
            ITcpClientFactory tcpClientFactory,
            IOnvifDiscoveryConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tcpClientFactory = tcpClientFactory ?? throw new ArgumentNullException(nameof(tcpClientFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<List<CameraInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            // For DiscoverAsync without subnet, we can't scan
            // This method is here to satisfy ICameraDiscovery interface
            _logger.LogWarning("DiscoverAsync called without subnet. Use ScanSubnetAsync instead.");
            return new List<CameraInfo>();
        }

        public async Task<List<CameraInfo>> ScanSubnetAsync(string subnet, CancellationToken cancellationToken = default)
        {
            var cameras = new List<CameraInfo>();
            var discoveredAddresses = new HashSet<string>();

            try
            {
                _logger.LogDebug($"Starting port scan on subnet {subnet}.x");
                _logger.LogDebug($"Ports to scan: {string.Join(", ", _config.AlternativePorts)}");

                // Scan each IP range
                foreach (var (start, count) in _config.IpRanges)
                {
                    _logger.LogDebug($"Scanning range {subnet}.{start} to {subnet}.{start + count - 1}");

                    for (int i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var lastOctet = start + i;
                        var ip = $"{subnet}.{lastOctet}";

                        // Try each alternative port
                        foreach (var port in _config.AlternativePorts)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                using var tcpClient = _tcpClientFactory.Create();
                                var connectTask = tcpClient.ConnectAsync(ip, port);

                                // Wait for connection with timeout
                                if (await Task.WhenAny(connectTask, Task.Delay(_config.TcpConnectionTimeoutMs, cancellationToken)) == connectTask)
                                {
                                    if (tcpClient.Connected)
                                    {
                                        _logger.LogDebug($"Found open port at {ip}:{port}");

                                        // Only add if we haven't discovered this IP yet
                                        if (!discoveredAddresses.Contains(ip))
                                        {
                                            var camera = new CameraInfo
                                            {
                                                Name = $"ONVIF Camera ({ip}:{port})",
                                                IpAddress = ip,
                                                Port = port,
                                                DeviceId = $"ONVIF-{ip}-{port}",
                                                LastSeen = DateTime.Now
                                            };

                                            discoveredAddresses.Add(ip);
                                            cameras.Add(camera);
                                            _logger.LogInfo($"Camera discovered: {camera.Name}");
                                            CameraDiscovered?.Invoke(this, camera);

                                            // Found camera on this IP, no need to try other ports
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Port not accessible, continue
                            }
                        }
                    }
                }

                _logger.LogInfo($"Port scan complete. Found {cameras.Count} camera(s)");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Port scan cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Port scan error", ex);
            }

            return cameras;
        }
    }
}
