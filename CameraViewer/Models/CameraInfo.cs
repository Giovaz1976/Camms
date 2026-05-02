using System;

namespace CameraViewer.Models
{
    public class CameraInfo
    {
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 32108; // Puerto típico de V380
        public string DeviceId { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public DateTime DiscoveredAt { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        
        // Credenciales para ONVIF/PTZ
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "";
        
        // Credenciales para RTSP (streaming)
        public string RtspUsername { get; set; } = "admin";
        public string RtspPassword { get; set; } = "";
        
        // URL RTSP personalizada (obtenida desde ONVIF GetStreamUri)
        public string? CustomRtspUrl { get; set; } = null;
        public bool UseCustomRtspUrl { get; set; } = false;
    }
}
