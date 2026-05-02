using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CameraViewer.Models;

namespace CameraViewer.Services
{
    /// <summary>
    /// Maneja la conexión TCP/UDP con una cámara V380 individual
    /// Implementa el protocolo P2P propietario de V380
    /// </summary>
    public class V380Connection : IDisposable
    {
        private readonly CameraInfo _camera;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private bool _isConnected;
        private CancellationTokenSource? _cancellationTokenSource;

        // Comandos V380 (ejemplos - necesitan ser capturados)
        private const byte CMD_LOGIN = 0x01;
        private const byte CMD_START_STREAM = 0x02;
        private const byte CMD_STOP_STREAM = 0x03;
        private const byte CMD_HEARTBEAT = 0x04;

        public event EventHandler<byte[]>? VideoDataReceived;
        public event EventHandler<string>? ConnectionStatusChanged;

        public V380Connection(CameraInfo camera)
        {
            _camera = camera;
        }

        public async Task<bool> ConnectAsync(string username = "admin", string password = "")
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_camera.IpAddress, _camera.Port);
                _stream = _tcpClient.GetStream();
                
                ConnectionStatusChanged?.Invoke(this, "Connected to camera");

                // Enviar comando de login
                bool loginSuccess = await SendLoginCommandAsync(username, password);
                if (!loginSuccess)
                {
                    ConnectionStatusChanged?.Invoke(this, "Login failed");
                    return false;
                }

                _isConnected = true;
                ConnectionStatusChanged?.Invoke(this, "Login successful");

                // Iniciar heartbeat
                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => HeartbeatLoopAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Connection error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartStreamAsync()
        {
            if (!_isConnected || _stream == null)
                return false;

            try
            {
                byte[] startStreamCmd = BuildStartStreamCommand();
                await _stream.WriteAsync(startStreamCmd, 0, startStreamCmd.Length);

                // Iniciar recepción de datos de video
                _ = Task.Run(() => ReceiveVideoDataAsync(_cancellationTokenSource!.Token));

                return true;
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Stream start error: {ex.Message}");
                return false;
            }
        }

        public async Task StopStreamAsync()
        {
            if (!_isConnected || _stream == null)
                return;

            try
            {
                byte[] stopStreamCmd = BuildStopStreamCommand();
                await _stream.WriteAsync(stopStreamCmd, 0, stopStreamCmd.Length);
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Stream stop error: {ex.Message}");
            }
        }

        private async Task<bool> SendLoginCommandAsync(string username, string password)
        {
            if (_stream == null)
                return false;

            try
            {
                byte[] loginPacket = BuildLoginPacket(username, password);
                await _stream.WriteAsync(loginPacket, 0, loginPacket.Length);

                // Esperar respuesta de login
                byte[] response = new byte[1024];
                int bytesRead = await _stream.ReadAsync(response, 0, response.Length);

                // Parsear respuesta (placeholder)
                return bytesRead > 0 && response[0] == 0x44; // Ejemplo
            }
            catch
            {
                return false;
            }
        }

        private byte[] BuildLoginPacket(string username, string password)
        {
            // Estructura de paquete de login V380 (placeholder)
            List<byte> packet = new List<byte>();
            
            // Header
            packet.AddRange(new byte[] { 0x44, 0x48, 0x00, 0x01 });
            
            // Command: Login
            packet.Add(CMD_LOGIN);
            packet.AddRange(new byte[] { 0x00, 0x00, 0x00 });
            
            // Username (32 bytes, null-padded)
            byte[] userBytes = new byte[32];
            Encoding.ASCII.GetBytes(username).CopyTo(userBytes, 0);
            packet.AddRange(userBytes);
            
            // Password (32 bytes, null-padded)
            byte[] passBytes = new byte[32];
            Encoding.ASCII.GetBytes(password).CopyTo(passBytes, 0);
            packet.AddRange(passBytes);
            
            // Checksum
            packet.AddRange(CalculateChecksum(packet.ToArray()));
            
            return packet.ToArray();
        }

        private byte[] BuildStartStreamCommand()
        {
            List<byte> packet = new List<byte>();
            
            // Header
            packet.AddRange(new byte[] { 0x44, 0x48, 0x00, 0x01 });
            
            // Command: Start Stream
            packet.Add(CMD_START_STREAM);
            packet.AddRange(new byte[] { 0x00, 0x00, 0x00 });
            
            // Stream type: Main stream (0) or Sub stream (1)
            packet.Add(0x00);
            
            // Checksum
            packet.AddRange(CalculateChecksum(packet.ToArray()));
            
            return packet.ToArray();
        }

        private byte[] BuildStopStreamCommand()
        {
            List<byte> packet = new List<byte>();
            
            packet.AddRange(new byte[] { 0x44, 0x48, 0x00, 0x01 });
            packet.Add(CMD_STOP_STREAM);
            packet.AddRange(new byte[] { 0x00, 0x00, 0x00 });
            packet.AddRange(CalculateChecksum(packet.ToArray()));
            
            return packet.ToArray();
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isConnected && _stream != null)
            {
                try
                {
                    byte[] heartbeat = BuildHeartbeatPacket();
                    await _stream.WriteAsync(heartbeat, 0, heartbeat.Length, cancellationToken);
                    await Task.Delay(10000, cancellationToken); // Cada 10 segundos
                }
                catch
                {
                    break;
                }
            }
        }

        private byte[] BuildHeartbeatPacket()
        {
            return new byte[] { 0x44, 0x48, 0x00, 0x01, CMD_HEARTBEAT, 0x00, 0x00, 0x00 };
        }

        private async Task ReceiveVideoDataAsync(CancellationToken cancellationToken)
        {
            if (_stream == null)
                return;

            byte[] buffer = new byte[65536]; // 64KB buffer

            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead > 0)
                    {
                        byte[] videoData = new byte[bytesRead];
                        Array.Copy(buffer, videoData, bytesRead);
                        VideoDataReceived?.Invoke(this, videoData);
                    }
                    else
                    {
                        // Conexión cerrada
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ConnectionStatusChanged?.Invoke(this, $"Receive error: {ex.Message}");
                    break;
                }
            }
        }

        private byte[] CalculateChecksum(byte[] data)
        {
            // Checksum simple (placeholder - implementar el algoritmo real)
            ushort sum = 0;
            foreach (byte b in data)
            {
                sum += b;
            }
            return BitConverter.GetBytes(sum);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            _isConnected = false;
        }
    }
}
