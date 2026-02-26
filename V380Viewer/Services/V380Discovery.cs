using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using V380Viewer.Models;

namespace V380Viewer.Services
{
    /// <summary>
    /// Servicio para descubrir cámaras V380 en la red local mediante UDP broadcast
    /// Basado en ingeniería inversa del protocolo de descubrimiento V380
    /// </summary>
    public class V380Discovery : IDisposable
    {
        private const int DISCOVERY_PORT = 32108; // Puerto de descubrimiento V380
        private const int TIMEOUT_MS = 3000;
        private UdpClient? _udpClient;
        
        public event EventHandler<CameraInfo>? CameraDiscovered;

        public async Task ScanNetworkAsync()
        {
            _udpClient?.Close();
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.ReceiveTimeout = TIMEOUT_MS;

            try
            {
                // Paquete de descubrimiento V380
                byte[] discoveryPacket = BuildDiscoveryPacket();
                
                // DEBUG: Mostrar paquete que se enviará
                System.Diagnostics.Debug.WriteLine($"Sending discovery packet ({discoveryPacket.Length} bytes): {BitConverter.ToString(discoveryPacket)}");
                
                // Enviar a broadcast Y a IP específica conocida
                IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
                await _udpClient.SendAsync(discoveryPacket, discoveryPacket.Length, broadcastEndpoint);
                System.Diagnostics.Debug.WriteLine("Sent to broadcast");
                
                // También enviar directamente a la cámara conocida
                IPEndPoint cameraEndpoint = new IPEndPoint(IPAddress.Parse("192.168.1.81"), DISCOVERY_PORT);
                await _udpClient.SendAsync(discoveryPacket, discoveryPacket.Length, cameraEndpoint);
                System.Diagnostics.Debug.WriteLine("Sent to 192.168.1.81");

                // Escuchar respuestas
                DateTime startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalMilliseconds < TIMEOUT_MS)
                {
                    try
                    {
                        if (_udpClient.Available > 0)
                        {
                            IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                            byte[] response = _udpClient.Receive(ref remoteEndpoint);
                            
                            // DEBUG: Mostrar respuesta recibida
                            System.Diagnostics.Debug.WriteLine($"Received {response.Length} bytes from {remoteEndpoint.Address}: {BitConverter.ToString(response.Take(Math.Min(32, response.Length)).ToArray())}");
                            
                            CameraInfo? camera = ParseDiscoveryResponse(response, remoteEndpoint.Address);
                            if (camera != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Camera found: {camera.Name} at {camera.IpAddress}");
                                CameraDiscovered?.Invoke(this, camera);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Response did not match V380 protocol");
                            }
                        }
                        await Task.Delay(100);
                    }
                    catch (SocketException)
                    {
                        // Timeout en receive, continuar
                    }
                }
            }
            finally
            {
                _udpClient?.Close();
            }
        }

        /// <summary>
        /// Construye el paquete de descubrimiento V380
        /// Basado en captura real del protocolo V380
        /// </summary>
        private byte[] BuildDiscoveryPacket()
        {
            // Estructura del protocolo V380 capturado:
            // [Magic: 4 bytes] [Command: 4 bytes] [Reserved: 4 bytes] [Length: 4 bytes] [Payload]
            List<byte> packet = new List<byte>();
            
            // Magic bytes V380 (capturados del tráfico real)
            packet.AddRange(new byte[] { 0x18, 0x08, 0x03, 0x38 });
            
            // Probar comando de discovery (variante 1: solicitud)
            // Basado en que las respuestas usan 01 02 02 00, la solicitud podría usar:
            // 01 01 00 00 (comando de búsqueda/query)
            packet.AddRange(new byte[] { 0x01, 0x01, 0x00, 0x00 });
            
            // Reserved bytes
            packet.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            // Payload length (0 para discovery, little-endian)
            packet.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            
            return packet.ToArray();
        }

        /// <summary>
        /// Parsea la respuesta de descubrimiento de una cámara V380
        /// Basado en captura real del protocolo
        /// </summary>
        private CameraInfo? ParseDiscoveryResponse(byte[] data, IPAddress ipAddress)
        {
            try
            {
                // Verificar tamaño mínimo (header = 16 bytes)
                if (data.Length < 16)
                    return null;

                // Verificar magic bytes V380 reales
                if (data[0] != 0x18 || data[1] != 0x08 || data[2] != 0x03 || data[3] != 0x38)
                    return null;

                CameraInfo camera = new CameraInfo
                {
                    IpAddress = ipAddress.ToString(),
                    Name = $"V380 Camera ({ipAddress})",
                    Port = DISCOVERY_PORT
                };

                // Extraer Device ID si está disponible
                if (data.Length > 20)
                {
                    // Ejemplo: Device ID en bytes 16-28
                    camera.DeviceId = BitConverter.ToString(data, 16, Math.Min(12, data.Length - 16))
                        .Replace("-", "");
                }

                // Extraer MAC address si está disponible
                if (data.Length > 32)
                {
                    camera.MacAddress = BitConverter.ToString(data, 28, 6).Replace("-", ":");
                }

                return camera;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
    }
}
