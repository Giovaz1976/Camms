namespace CameraViewer.Configuration
{
    /// <summary>
    /// Root configuration class containing all application settings.
    /// </summary>
    public class AppSettings
    {
        public OnvifDiscoverySettings OnvifDiscovery { get; set; } = new();
        public CameraSettings Camera { get; set; } = new();
        public StreamingSettings Streaming { get; set; } = new();
        public PtzSettings PTZ { get; set; } = new();
        public UISettings UI { get; set; } = new();
        public NetworkSettings Network { get; set; } = new();
    }

    /// <summary>
    /// PTZ control settings.
    /// </summary>
    public class PtzSettings
    {
        public int DefaultSpeed { get; set; } = 50;
        public int MinSpeed { get; set; } = 1;
        public int MaxSpeed { get; set; } = 100;
        public int StepSize { get; set; } = 10;
    }

    /// <summary>
    /// UI settings.
    /// </summary>
    public class UISettings
    {
        public int AutoRefreshInterval { get; set; } = 30000;
        public int MaxCamerasPerRow { get; set; } = 4;
        public string DefaultQuality { get; set; } = "High";
        public bool ShowDebugInfo { get; set; } = false;
    }

    /// <summary>
    /// Network settings.
    /// </summary>
    public class NetworkSettings
    {
        public bool EnableIPv6 { get; set; } = false;
        public bool BindToLocalAddress { get; set; } = true;
        public int MaxConcurrentConnections { get; set; } = 10;
    }
}
