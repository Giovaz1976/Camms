using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CameraViewer.Models;
using CameraViewer.Services;

namespace CameraViewer.ViewModels
{
    /// <summary>
    /// Main ViewModel for the CameraViewer application.
    /// Handles camera discovery, management, and UI state.
    /// Follows MVVM pattern - separates business logic from UI.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly OnvifDiscovery _onvifDiscovery;
        private readonly V380Discovery _v380Discovery;
        private CancellationTokenSource? _scanCancellationTokenSource;

        // Observable collections for data binding
        private ObservableCollection<CameraInfo> _discoveredCameras;
        private bool _isScanning;
        private string _scanButtonText;
        private string _statusMessage;

        public MainViewModel(OnvifDiscovery onvifDiscovery, V380Discovery v380Discovery)
        {
            _onvifDiscovery = onvifDiscovery ?? throw new ArgumentNullException(nameof(onvifDiscovery));
            _v380Discovery = v380Discovery ?? throw new ArgumentNullException(nameof(v380Discovery));

            // Initialize collections
            _discoveredCameras = new ObservableCollection<CameraInfo>();
            _scanButtonText = "🔍 Scan Cameras";
            _statusMessage = "Ready to scan";

            // Subscribe to discovery events
            _onvifDiscovery.CameraDiscovered += OnCameraDiscovered;
            _v380Discovery.CameraDiscovered += OnCameraDiscovered;
        }

        /// <summary>
        /// Collection of discovered cameras (bound to UI).
        /// </summary>
        public ObservableCollection<CameraInfo> DiscoveredCameras
        {
            get => _discoveredCameras;
            set => SetProperty(ref _discoveredCameras, value);
        }

        /// <summary>
        /// Indicates if a scan is currently in progress.
        /// </summary>
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    // Update scan button text when scanning state changes
                    ScanButtonText = value ? "⏹ Stop Scan" : "🔍 Scan Cameras";
                    
                    // Notify commands to re-evaluate CanExecute
                    ((RelayCommand)ScanCamerasCommand).NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Text for the scan button (changes based on state).
        /// </summary>
        public string ScanButtonText
        {
            get => _scanButtonText;
            set => SetProperty(ref _scanButtonText, value);
        }

        /// <summary>
        /// Status message displayed in the UI.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Command to start/stop camera scanning.
        /// </summary>
        public ICommand ScanCamerasCommand => new RelayCommand(
            execute: async () => await ScanCamerasAsync(),
            canExecute: () => true
        );

        /// <summary>
        /// Scans for cameras on the network.
        /// </summary>
        private async Task ScanCamerasAsync()
        {
            if (IsScanning)
            {
                // Stop scanning
                _scanCancellationTokenSource?.Cancel();
                StatusMessage = "Scan cancelled";
                IsScanning = false;
                return;
            }

            try
            {
                IsScanning = true;
                DiscoveredCameras.Clear();
                StatusMessage = "Scanning for cameras...";

                _scanCancellationTokenSource = new CancellationTokenSource();
                var token = _scanCancellationTokenSource.Token;

                // Start ONVIF discovery
                var onvifCameras = await _onvifDiscovery.DiscoverCamerasAsync(token);

                // Note: V380Discovery uses event-based discovery, not async
                // Cameras will be added via OnCameraDiscovered event handler

                StatusMessage = $"Scan complete. Found {DiscoveredCameras.Count} camera(s)";

                // If no cameras found via multicast, try alternative ports
                if (DiscoveredCameras.Count == 0)
                {
                    StatusMessage = "No cameras found via multicast. Trying alternative ports...";
                    
                    // Get local subnet
                    var subnet = GetLocalSubnet();
                    if (!string.IsNullOrEmpty(subnet))
                    {
                        var altCameras = await _onvifDiscovery.DiscoverCamerasOnAlternativePortsAsync(subnet, token);
                        StatusMessage = $"Scan complete. Found {DiscoveredCameras.Count} camera(s) on alternative ports";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Scan cancelled by user";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during scan: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                _scanCancellationTokenSource?.Dispose();
                _scanCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Event handler for when a camera is discovered.
        /// </summary>
        private void OnCameraDiscovered(object? sender, CameraInfo camera)
        {
            // Add to collection on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Check if camera already exists (by IP)
                var existing = DiscoveredCameras.FirstOrDefault(c => c.IpAddress == camera.IpAddress);
                if (existing == null)
                {
                    DiscoveredCameras.Add(camera);
                    StatusMessage = $"Found: {camera.Name} at {camera.IpAddress}";
                }
            });
        }

        /// <summary>
        /// Gets the local subnet for port scanning.
        /// </summary>
        private string? GetLocalSubnet()
        {
            try
            {
                var localIP = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .FirstOrDefault(addr =>
                        addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(addr.Address) &&
                        !addr.Address.ToString().StartsWith("169.254"));

                if (localIP != null)
                {
                    var ipParts = localIP.Address.ToString().Split('.');
                    return $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Cleanup resources.
        /// </summary>
        public void Dispose()
        {
            _scanCancellationTokenSource?.Cancel();
            _scanCancellationTokenSource?.Dispose();
            
            _onvifDiscovery.CameraDiscovered -= OnCameraDiscovered;
            _v380Discovery.CameraDiscovered -= OnCameraDiscovered;
        }
    }
}
