using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using V380Viewer.Models;

namespace V380Viewer.Services
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
                client.Client.ReceiveTimeout = 500; // Timeout más corto para verificar cancelación más frecuentemente

                // Construir mensaje WS-Discovery SOAP
                var probeMessage = BuildProbeMessage();
                var probeBytes = Encoding.UTF8.GetBytes(probeMessage);

                // Enviar a multicast
                var multicastEndpoint = new IPEndPoint(IPAddress.Parse(MULTICAST_ADDRESS), MULTICAST_PORT);
                await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);

                // Escuchar respuestas
                var startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalMilliseconds < DISCOVERY_TIMEOUT)
                {
                    // Verificar cancelación
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var result = await client.ReceiveAsync();
                        var response = Encoding.UTF8.GetString(result.Buffer);
                        
                        var camera = ParseProbeMatch(response, result.RemoteEndPoint.Address.ToString());
                        if (camera != null && !discoveredAddresses.Contains(camera.IpAddress))
                        {
                            discoveredAddresses.Add(camera.IpAddress);
                            cameras.Add(camera);
                            CameraDiscovered?.Invoke(this, camera);
                        }
                    }
                    catch (SocketException)
                    {
                        // Timeout o no más respuestas - continuar esperando
                        await Task.Delay(100, cancellationToken); // Pequeña pausa antes de reintentar
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelación solicitada - propagar la excepción
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ONVIF Discovery error: {ex.Message}");
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

        public void Dispose()
        {
            // Cleanup si es necesario
        }
    }
}
