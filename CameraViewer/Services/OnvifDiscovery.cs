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

namespace CameraViewer.Services
{
    /// <summary>
    /// Servicio para descubrir cámaras ONVIF en la red local usando WS-Discovery
    /// </summary>
    public class OnvifDiscovery : IDisposable
    {
        private const string MULTICAST_ADDRESS = "239.255.255.250";
        private const int MULTICAST_PORT = 3702;
        private const int DISCOVERY_TIMEOUT = 3000; // 3 segundos

        public event EventHandler<CameraInfo>? CameraDiscovered;

        public async Task<List<CameraInfo>> DiscoverCamerasAsync(CancellationToken cancellationToken = default)
        {
            var cameras = new List<CameraInfo>();
            var discoveredAddresses = new HashSet<string>();

            try
            {
                using var client = new UdpClient();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                client.Client.ReceiveTimeout = 500;
                
                // IMPORTANTE: Unirse al grupo multicast para recibir respuestas
                var multicastAddress = IPAddress.Parse(MULTICAST_ADDRESS);
                client.JoinMulticastGroup(multicastAddress);
                
                // Habilitar multicast loopback para recibir respuestas en la misma máquina
                client.MulticastLoopback = true;

                // Construir mensaje WS-Discovery SOAP
                var probeMessage = BuildProbeMessage();
                var probeBytes = Encoding.UTF8.GetBytes(probeMessage);

                // Enviar a multicast - enviar múltiples veces para mayor confiabilidad
                var multicastEndpoint = new IPEndPoint(multicastAddress, MULTICAST_PORT);
                
                System.Diagnostics.Debug.WriteLine($"[ONVIF] Sending discovery probe to {MULTICAST_ADDRESS}:{MULTICAST_PORT}");
                
                // Enviar 3 veces con pequeño delay para mayor confiabilidad
                await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
                await Task.Delay(100, cancellationToken);
                await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
                await Task.Delay(100, cancellationToken);
                await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
                
                // También intentar broadcast para cámaras que no responden a multicast
                try
                {
                    client.EnableBroadcast = true;
                    var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, MULTICAST_PORT);
                    System.Diagnostics.Debug.WriteLine($"[ONVIF] Sending discovery probe via broadcast");
                    await client.SendAsync(probeBytes, probeBytes.Length, broadcastEndpoint);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ONVIF] Broadcast failed: {ex.Message}");
                }

                // Escuchar respuestas por más tiempo (5 segundos en lugar de 3)
                var startTime = DateTime.Now;
                var discoveryTimeout = 5000; // 5 segundos
                
                System.Diagnostics.Debug.WriteLine($"[ONVIF] Listening for responses ({discoveryTimeout}ms)...");
                
                while ((DateTime.Now - startTime).TotalMilliseconds < discoveryTimeout)
                {
                    // Verificar cancelación
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var result = await client.ReceiveAsync();
                        var response = Encoding.UTF8.GetString(result.Buffer);
                        
                        System.Diagnostics.Debug.WriteLine($"[ONVIF] Received response from {result.RemoteEndPoint.Address}");
                        
                        var camera = ParseProbeMatch(response, result.RemoteEndPoint.Address.ToString());
                        if (camera != null && !discoveredAddresses.Contains(camera.IpAddress))
                        {
                            discoveredAddresses.Add(camera.IpAddress);
                            cameras.Add(camera);
                            System.Diagnostics.Debug.WriteLine($"[ONVIF] Camera discovered: {camera.Name} at {camera.IpAddress}");
                            CameraDiscovered?.Invoke(this, camera);
                        }
                    }
                    catch (SocketException)
                    {
                        // Timeout o no más respuestas - continuar esperando
                        await Task.Delay(100, cancellationToken);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[ONVIF] Discovery complete. Found {cameras.Count} camera(s)");
                
                // Salir del grupo multicast
                client.DropMulticastGroup(multicastAddress);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ONVIF] Discovery cancelled");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ONVIF] Discovery error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ONVIF] Stack trace: {ex.StackTrace}");
            }

            return cameras;
        }

        private string BuildProbeMessage()
        {
            var messageId = Guid.NewGuid().ToString();
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:a=""http://schemas.xmlsoap.org/ws/2004/08/addressing"">
    <s:Header>
        <a:Action s:mustUnderstand=""1"">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
        <a:MessageID>uuid:{messageId}</a:MessageID>
        <a:ReplyTo>
            <a:Address>http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address>
        </a:ReplyTo>
        <a:To s:mustUnderstand=""1"">urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
    </s:Header>
    <s:Body>
        <Probe xmlns=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
            <d:Types xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"" 
                     xmlns:dp0=""http://www.onvif.org/ver10/network/wsdl"">dp0:NetworkVideoTransmitter</d:Types>
        </Probe>
    </s:Body>
</s:Envelope>";
        }

        private CameraInfo? ParseProbeMatch(string response, string ipAddress)
        {
            try
            {
                var doc = XDocument.Parse(response);
                XNamespace s = "http://www.w3.org/2003/05/soap-envelope";
                XNamespace d = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
                XNamespace a = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

                var probeMatch = doc.Descendants(d + "ProbeMatch").FirstOrDefault();
                if (probeMatch == null)
                    return null;

                var xAddrs = probeMatch.Element(d + "XAddrs")?.Value;
                if (string.IsNullOrEmpty(xAddrs))
                    return null;

                // Extraer IP de la URL ONVIF
                var onvifUrl = xAddrs.Split(' ').FirstOrDefault();
                if (string.IsNullOrEmpty(onvifUrl))
                    return null;

                var uri = new Uri(onvifUrl);
                var cameraIp = uri.Host;

                // Obtener información adicional si está disponible
                var scopes = probeMatch.Element(d + "Scopes")?.Value ?? "";
                var name = ExtractScopeName(scopes) ?? $"ONVIF Camera ({cameraIp})";

                return new CameraInfo
                {
                    Name = name,
                    IpAddress = cameraIp,
                    Port = 554, // Puerto RTSP estándar
                    DeviceId = $"ONVIF-{cameraIp}",
                    LastSeen = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing probe match: {ex.Message}");
                return null;
            }
        }

        private string? ExtractScopeName(string scopes)
        {
            try
            {
                var scopeList = scopes.Split(' ');
                foreach (var scope in scopeList)
                {
                    if (scope.Contains("name/"))
                    {
                        var parts = scope.Split('/');
                        return Uri.UnescapeDataString(parts.Last());
                    }
                }
            }
            catch { }
            
            return null;
        }

        /// <summary>
        /// Escanea puertos ONVIF alternativos (como 10080) en un rango de IPs
        /// </summary>
        public async Task<List<CameraInfo>> DiscoverCamerasOnAlternativePortsAsync(string subnet, CancellationToken cancellationToken = default)
        {
            var cameras = new List<CameraInfo>();
            var discoveredAddresses = new HashSet<string>();
            
            System.Diagnostics.Debug.WriteLine($"[ONVIF] Scanning alternative ports on subnet {subnet}.x");
            
            // Puertos ONVIF alternativos
            var alternativePorts = new[] { 10080, 8080, 8899 };
            
            // Rango de IPs a escanear (común para cámaras)
            var ipRanges = new[] { 
                Enumerable.Range(64, 27),  // 64-90
                Enumerable.Range(100, 21), // 100-120
                Enumerable.Range(200, 11)  // 200-210
            };
            
            foreach (var range in ipRanges)
            {
                foreach (var lastOctet in range)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var ip = $"{subnet}.{lastOctet}";
                    
                    foreach (var port in alternativePorts)
                    {
                        try
                        {
                            using var tcpClient = new System.Net.Sockets.TcpClient();
                            var connectTask = tcpClient.ConnectAsync(ip, port);
                            
                            if (await Task.WhenAny(connectTask, Task.Delay(500, cancellationToken)) == connectTask)
                            {
                                if (tcpClient.Connected)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ONVIF] Found camera at {ip}:{port}");
                                    
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
                                        CameraDiscovered?.Invoke(this, camera);
                                    }
                                    
                                    break; // Ya encontramos esta cámara, no probar más puertos
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Puerto no accesible, continuar
                        }
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ONVIF] Alternative port scan complete. Found {cameras.Count} camera(s)");
            return cameras;
        }

        public void Dispose()
        {
            // Cleanup si es necesario
        }
    }
}
