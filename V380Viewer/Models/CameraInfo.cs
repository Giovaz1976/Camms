using System;

namespace V380Viewer.Models
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
    }
}
