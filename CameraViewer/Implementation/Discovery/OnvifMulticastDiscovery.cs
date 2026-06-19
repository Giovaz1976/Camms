using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Interfaces.Configuration;
using CameraViewer.Interfaces.Discovery;
using CameraViewer.Interfaces.Logging;
using CameraViewer.Interfaces.Network;
using CameraViewer.Interfaces.Parsing;
using CameraViewer.Models;

namespace CameraViewer.Implementation.Discovery
{
    /// <summary>
    /// ONVIF camera discovery using WS-Discovery multicast/broadcast protocol.
    /// Implements Single Responsibility Principle - only handles multicast discovery.
    /// Uses Dependency Injection for all dependencies.
    /// </summary>
    public class OnvifMulticastDiscovery : IOnvifMulticastDiscovery
    {
        private readonly ILogger _logger;
        private readonly IOnvifMessageBuilder _messageBuilder;
        private readonly IOnvifResponseParser _responseParser;
        private readonly IOnvifDiscoveryConfiguration _config;

        public event EventHandler<CameraInfo>? CameraDiscovered;

        /// <summary>
        /// Creates a new OnvifMulticastDiscovery with injected dependencies.
        /// </summary>
        /// <param name="logger">Logger for debug/error messages.</param>
        /// <param name="messageBuilder">Builder for SOAP messages.</param>
        /// <param name="responseParser">Parser for SOAP responses.</param>
        /// <param name="config">Configuration for discovery parameters.</param>
        public OnvifMulticastDiscovery(
            ILogger logger,
            IOnvifMessageBuilder messageBuilder,
            IOnvifResponseParser responseParser,
            IOnvifDiscoveryConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageBuilder = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));
            _responseParser = responseParser ?? throw new ArgumentNullException(nameof(responseParser));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<List<CameraInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            var cameras = new List<CameraInfo>();
            var discoveredAddresses = new HashSet<string>();

            try
            {
                _logger.LogDebug($"Starting multicast discovery on {_config.MulticastAddress}:{_config.MulticastPort}");

                // Create network client
                using var client = new UdpClient();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                client.Client.ReceiveTimeout = 500;

                // Join multicast group
                var multicastAddress = IPAddress.Parse(_config.MulticastAddress);
                client.JoinMulticastGroup(multicastAddress);
                client.MulticastLoopback = true;

                // Build probe message
                var probeMessage = _messageBuilder.BuildProbeMessage();
                var probeBytes = Encoding.UTF8.GetBytes(probeMessage);
                var multicastEndpoint = new IPEndPoint(multicastAddress, _config.MulticastPort);

                // Send probes multiple times for reliability
                _logger.LogDebug($"Sending {_config.ProbeRetries} probe(s) to multicast group");
                for (int i = 0; i < _config.ProbeRetries; i++)
                {
                    await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
                    if (i < _config.ProbeRetries - 1)
                    {
                        await Task.Delay(_config.ProbeDelayMs, cancellationToken);
                    }
                }

                // Also try broadcast for cameras that don't respond to multicast
                try
                {
                    client.EnableBroadcast = true;
                    var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _config.MulticastPort);
                    _logger.LogDebug("Sending probe via broadcast");
                    await client.SendAsync(probeBytes, probeBytes.Length, broadcastEndpoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Broadcast failed: {ex.Message}");
                }

                // Listen for responses
                var startTime = DateTime.Now;
                _logger.LogDebug($"Listening for responses ({_config.DiscoveryTimeoutMs}ms)...");

                while ((DateTime.Now - startTime).TotalMilliseconds < _config.DiscoveryTimeoutMs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await client.ReceiveAsync();
                        var response = Encoding.UTF8.GetString(result.Buffer);
                        var remoteIp = result.RemoteEndPoint.Address.ToString();

                        _logger.LogDebug($"Received response from {remoteIp}");

                        var camera = _responseParser.ParseProbeMatch(response, remoteIp);
                        if (camera != null && !discoveredAddresses.Contains(camera.IpAddress))
                        {
                            discoveredAddresses.Add(camera.IpAddress);
                            cameras.Add(camera);
                            _logger.LogInfo($"Camera discovered: {camera.Name} at {camera.IpAddress}");
                            CameraDiscovered?.Invoke(this, camera);
                        }
                    }
                    catch (SocketException)
                    {
                        // Timeout or no more responses - continue waiting
                        await Task.Delay(100, cancellationToken);
                    }
                }

                _logger.LogInfo($"Multicast discovery complete. Found {cameras.Count} camera(s)");

                // Leave multicast group
                client.DropMulticastGroup(multicastAddress);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Discovery cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Discovery error", ex);
            }

            return cameras;
        }
    }
}
