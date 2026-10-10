using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using CameraViewer.Models;
using CameraViewer.Services;
using CameraViewer.ViewModels;
using VLCMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace CameraViewer
{
    public partial class MainWindow : Window
    {
        private readonly V380Discovery _discovery;
        private readonly OnvifDiscovery _onvifDiscovery;
        private readonly OnvifPtzService _ptzService;
        private readonly MainViewModel _viewModel;
        private LibVLC? _libVLC;
        private List<CameraView> _cameraViews = new List<CameraView>();
        private int _currentLayout = 1; // 1, 2, 4, or 9
        private CancellationTokenSource? _scanCancellationTokenSource;
        private StreamQuality _globalStreamQuality = StreamQuality.Main; // HD por defecto
        private CameraInfo? _activePtzCamera; // Cámara actualmente controlada por PTZ
        private bool _isDarkTheme = true; // Tema actual (false = normal, true = oscuro). Oscuro por defecto.
        private bool _globalAudioMuted = true; // Estado global de audio (true = muted por defecto)
        private Point _cameraDragStartPoint; // Para iniciar drag desde la lista de cámaras
        private Point _panelDragStartPoint; // Para iniciar drag desde un panel de video
        
        /// <summary>
        /// Constructor with Dependency Injection.
        /// Services and ViewModel are injected by the DI container.
        /// </summary>
        public MainWindow(
            V380Discovery discovery,
            OnvifDiscovery onvifDiscovery,
            OnvifPtzService ptzService,
            MainViewModel viewModel)
        {
            InitializeComponent();
            
            // Injected dependencies
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            _onvifDiscovery = onvifDiscovery ?? throw new ArgumentNullException(nameof(onvifDiscovery));
            _ptzService = ptzService ?? throw new ArgumentNullException(nameof(ptzService));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            
            // Set DataContext for MVVM binding
            DataContext = _viewModel;
            
            // Subscribe to events
            _discovery.CameraDiscovered += OnCameraDiscovered;
            _onvifDiscovery.CameraDiscovered += OnCameraDiscovered;
            
            // Actualizar texto de velocidad PTZ cuando cambia el slider
            SliderPtzSpeed.ValueChanged += (s, e) => 
            {
                TxtPtzSpeed.Text = $"Speed: {SliderPtzSpeed.Value:F1}";
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Inicializar LibVLC con configuración balanceada para baja latencia
            Core.Initialize();
            
            var options = new string[]
            {
                "--network-caching=50",          // Buffer mínimo pero estable (50ms)
                "--rtsp-tcp",                    // RTSP sobre TCP (más estable)
                "--live-caching=50",             // Cache mínimo para live
                "--avcodec-hurry-up",            // Decodificación rápida
                "--avcodec-fast",                // Codec rápido
                "--avcodec-threads=2",           // Threads reducidos para múltiples streams
                "--drop-late-frames",            // Descartar frames tardíos
                "--skip-frames",                 // Saltar frames si es necesario
                "--vout=direct3d11",             // Aceleración por hardware
                // Opciones de audio
                "--audio-desync=0",              // Sincronización de audio
                "--audio-time-stretch",          // Ajuste de tiempo de audio
                "--audio-resampler=soxr"         // Resampler de alta calidad
            };
            
            _libVLC = new LibVLC(options);
            SetupGridLayout(_currentLayout);

            // Aplicar tema oscuro por defecto al iniciar la ventana
            ApplyTheme(_isDarkTheme);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CleanupResources();
        }

        /// <summary>
        /// Libera, en el orden correcto, todas las conexiones activas hacia las cámaras:
        /// detiene cada MediaPlayer (lo que dispara el TEARDOWN de RTSP hacia la cámara),
        /// los dispone junto con su Media, y solo DESPUÉS dispone el LibVLC core del que
        /// dependen. Disponer _libVLC antes de que sus MediaPlayers estén detenidos/disponibles
        /// es lo que antes causaba un AccessViolationException nativo. Idempotente vía
        /// _isDisposing porque tanto Window_Closing como OnClosed la invocan (por si el cierre
        /// no dispara Closing, p. ej. al matar el proceso).
        /// </summary>
        private void CleanupResources()
        {
            if (_isDisposing)
                return;
            _isDisposing = true;

            try
            {
                // 1. Cancelar cualquier escaneo en progreso
                if (_scanCancellationTokenSource != null && !_scanCancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine("Cancelling scan due to window closing...");
                    _scanCancellationTokenSource.Cancel();
                }

                // 2. Detener grabaciones SIN reiniciar el stream (la app se está cerrando,
                // no tiene sentido reconectar para seguir viendo en vivo)
                Console.WriteLine("Stopping all recordings...");
                StopAllRecordings(restartStream: false);

                // 3. Detener reproducción de cada panel (dispara el TEARDOWN RTSP hacia cada cámara)
                Console.WriteLine("Stopping all cameras...");
                foreach (var view in _cameraViews)
                {
                    try { view.MediaPlayer?.Stop(); }
                    catch (Exception ex) { Console.WriteLine($"Error stopping camera: {ex.Message}"); }
                }

                // Dar tiempo a LibVLC para completar el teardown de red antes de disponer
                System.Threading.Thread.Sleep(200);

                // 4. Disponer Media y MediaPlayer de cada panel
                foreach (var view in _cameraViews)
                {
                    try
                    {
                        view.CurrentMedia?.Dispose();
                        view.MediaPlayer?.Dispose();
                    }
                    catch (Exception ex) { Console.WriteLine($"Error disposing camera view: {ex.Message}"); }
                }
                _cameraViews.Clear();

                // 5. Disponer el LibVLC core solo después de que ya no quede ningún
                // MediaPlayer vivo que dependa de él
                try
                {
                    Console.WriteLine("Disposing LibVLC...");
                    _libVLC?.Dispose();
                }
                catch (Exception ex) { Console.WriteLine($"Error disposing LibVLC: {ex.Message}"); }
                _libVLC = null;

                // 6. Disponer servicios de discovery/PTZ (cierran sus sockets UDP/HTTP)
                try { _discovery?.Dispose(); } catch (Exception ex) { Console.WriteLine($"Error disposing V380Discovery: {ex.Message}"); }
                try { _onvifDiscovery?.Dispose(); } catch (Exception ex) { Console.WriteLine($"Error disposing OnvifDiscovery: {ex.Message}"); }
                try { _ptzService?.Dispose(); } catch (Exception ex) { Console.WriteLine($"Error disposing OnvifPtzService: {ex.Message}"); }

                Console.WriteLine("All camera connections released, window closed successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        private async void BtnScanCameras_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está escaneando, cancelar
            if (_scanCancellationTokenSource != null)
            {
                _scanCancellationTokenSource.Cancel();
                
                // Restaurar botón inmediatamente
                BtnScanCameras.Content = "🔍 Scan Cameras";
                BtnScanCameras.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Azul
                TxtStatus.Text = "Scan cancelled by user.";
                
                return;
            }
            
            LstCameras.Items.Clear();
            TxtStatus.Text = "Scanning network for ONVIF cameras...";
            
            // Cambiar botón a modo "Cancelar"
            BtnScanCameras.Content = "⏹ Cancel Scan";
            BtnScanCameras.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Rojo
            
            _scanCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _scanCancellationTokenSource.Token;
            
            try
            {
                // Usar ONVIF Discovery para encontrar cámaras automáticamente
                var cameras = await _onvifDiscovery.DiscoverCamerasAsync(cancellationToken);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    foreach (var camera in cameras)
                    {
                        LstCameras.Items.Add(camera);
                    }
                    
                    // Si no se encontraron cámaras con multicast, intentar escaneo de puertos alternativos
                    if (LstCameras.Items.Count == 0)
                    {
                        TxtStatus.Text = "Scanning alternative ONVIF ports...";
                        
                        // Obtener subnet local (ej: "192.168.1")
                        var localIP = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                            .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                            .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork 
                                && !System.Net.IPAddress.IsLoopback(addr.Address)
                                && !addr.Address.ToString().StartsWith("169.254"));
                        
                        if (localIP != null)
                        {
                            var ipParts = localIP.Address.ToString().Split('.');
                            var subnet = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                            
                            var altCameras = await _onvifDiscovery.DiscoverCamerasOnAlternativePortsAsync(subnet, cancellationToken);
                            
                            foreach (var camera in altCameras)
                            {
                                LstCameras.Items.Add(camera);
                            }
                        }
                    }
                    
                    TxtStatus.Text = $"Scan complete. Found {LstCameras.Items.Count} camera(s)";
                    
                    if (LstCameras.Items.Count == 0)
                    {
                        TxtStatus.Text = "No cameras found. Try 'Add Camera' to add manually.";
                    }
                }
                else
                {
                    TxtStatus.Text = "Scan cancelled by user.";
                }
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    TxtStatus.Text = $"Error: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Scan error: {ex}");
                    MessageBox.Show($"Scan failed: {ex.Message}\n\nDetails: {ex.GetType().Name}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    TxtStatus.Text = "Scan cancelled.";
                }
            }
            finally
            {
                // Restaurar botón a modo "Escanear"
                BtnScanCameras.Content = "🔍 Scan Cameras";
                BtnScanCameras.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Azul
                _scanCancellationTokenSource?.Dispose();
                _scanCancellationTokenSource = null;
            }
        }
        
        private void BtnAddCamera_Click(object sender, RoutedEventArgs e)
        {
            // Crear ventana de diálogo simple para ingresar IP
            var dialog = new Window
            {
                Title = "Add Camera Manually",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)), // #2C3E50 - mismo tono que los paneles de la ventana principal
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblIp = new TextBlock { Text = "Camera IP Address:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) };
            var txtIp = new TextBox { Height = 32, Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(8) };

            var lblName = new TextBlock { Text = "Camera Name (optional):", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) };
            var txtName = new TextBox { Height = 32, Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(8) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Add", Width = 80, Height = 30, Margin = new Thickness(5, 0, 0, 0), Style = (Style)Application.Current.Resources["SuccessButton"] };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(5, 0, 0, 0), Style = (Style)Application.Current.Resources["DangerButton"] };
            
            btnOk.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtIp.Text))
                {
                    MessageBox.Show("Please enter an IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Validar formato de IP
                if (!System.Net.IPAddress.TryParse(txtIp.Text.Trim(), out _))
                {
                    MessageBox.Show("Invalid IP address format.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var camera = new CameraInfo
                {
                    Name = string.IsNullOrWhiteSpace(txtName.Text) ? $"Camera ({txtIp.Text.Trim()})" : txtName.Text.Trim(),
                    IpAddress = txtIp.Text.Trim(),
                    Port = 554,
                    DeviceId = $"Manual-{txtIp.Text.Trim()}"
                };
                
                LstCameras.Items.Add(camera);
                TxtStatus.Text = $"Camera added: {camera.Name}";
                dialog.DialogResult = true;
                dialog.Close();
            };
            
            btnCancel.Click += (s, args) => { dialog.Close(); };
            
            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            
            Grid.SetRow(lblIp, 0);
            Grid.SetRow(txtIp, 1);
            Grid.SetRow(lblName, 2);
            Grid.SetRow(txtName, 3);
            Grid.SetRow(btnPanel, 4);
            
            grid.Children.Add(lblIp);
            grid.Children.Add(txtIp);
            grid.Children.Add(lblName);
            grid.Children.Add(txtName);
            grid.Children.Add(btnPanel);
            
            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private async void OnCameraDiscovered(object? sender, CameraInfo camera)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // Verificar si el escaneo fue cancelado
                if (_scanCancellationTokenSource?.IsCancellationRequested == true)
                {
                    Console.WriteLine($"Scan cancelled, skipping camera {camera.IpAddress}");
                    return;
                }
                
                // Verificar si la cámara ya existe en la lista
                var existingCamera = LstCameras.Items.Cast<CameraInfo>()
                    .FirstOrDefault(c => c.IpAddress == camera.IpAddress);
                
                if (existingCamera != null)
                {
                    Console.WriteLine($"Camera {camera.IpAddress} already in list, skipping");
                    return;
                }
                
                // Intentar con credenciales por defecto primero
                var defaultCredentials = new[] 
                { 
                    new { User = "admin", Pass = "" },
                    new { User = "admin", Pass = "admin" },
                    new { User = "admin", Pass = "888888" },
                    new { User = "admin", Pass = "12345" }
                };
                
                bool authenticated = false;
                
                foreach (var cred in defaultCredentials)
                {
                    // Verificar cancelación antes de cada intento
                    if (_scanCancellationTokenSource?.IsCancellationRequested == true)
                    {
                        Console.WriteLine($"Scan cancelled during authentication for {camera.IpAddress}");
                        return;
                    }
                    
                    Console.WriteLine($"Trying credentials {cred.User}/{(string.IsNullOrEmpty(cred.Pass) ? "(empty)" : "***")} for {camera.IpAddress}...");
                    
                    var profileToken = await _ptzService.GetProfileTokenAsync(camera.IpAddress, cred.User, cred.Pass);
                    
                    if (!string.IsNullOrEmpty(profileToken))
                    {
                        // Credenciales válidas para ONVIF
                        camera.Username = cred.User;
                        camera.Password = cred.Pass;
                        camera.RtspUsername = cred.User;
                        camera.RtspPassword = cred.Pass;
                        authenticated = true;
                        Console.WriteLine($"✓ Authenticated with {cred.User}/{(string.IsNullOrEmpty(cred.Pass) ? "(empty)" : "***")}");
                        
                        // Si la contraseña está vacía, puede que RTSP requiera contraseña real
                        // Solicitar credenciales para RTSP
                        if (string.IsNullOrEmpty(cred.Pass))
                        {
                            Console.WriteLine($"⚠ ONVIF accepted empty password, but RTSP may require password");
                            Console.WriteLine($"  Requesting RTSP credentials for {camera.IpAddress}...");
                            
                            var rtspResult = await ShowRtspCredentialsDialogAsync(camera);
                            
                            if (rtspResult.Success)
                            {
                                camera.RtspUsername = rtspResult.Username;
                                camera.RtspPassword = rtspResult.Password;
                                Console.WriteLine($"✓ RTSP credentials configured: {rtspResult.Username}/***");
                            }
                            else
                            {
                                Console.WriteLine($"⚠ User skipped RTSP credentials, using empty password");
                            }
                        }
                        
                        break;
                    }
                }
                
                // Si no se autenticó con credenciales por defecto, solicitar manualmente
                if (!authenticated)
                {
                    Console.WriteLine($"⚠ Default credentials failed for {camera.IpAddress}, requesting manual input...");
                    
                    var result = await ShowCredentialsDialogAsync(camera);
                    
                    if (!result.Success)
                    {
                        Console.WriteLine($"✗ User cancelled credentials for {camera.IpAddress}");
                        TxtStatus.Text = $"Camera {camera.IpAddress} skipped (no credentials)";
                        return;
                    }
                    
                    // Validar credenciales ingresadas
                    var profileToken = await _ptzService.GetProfileTokenAsync(camera.IpAddress, result.Username, result.Password);
                    
                    if (string.IsNullOrEmpty(profileToken))
                    {
                        MessageBox.Show($"Invalid credentials for {camera.IpAddress}\nONVIF authentication failed.", 
                            "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        Console.WriteLine($"✗ Invalid credentials for {camera.IpAddress}");
                        return;
                    }
                    
                    camera.Username = result.Username;
                    camera.Password = result.Password;
                    camera.RtspUsername = result.Username;
                    camera.RtspPassword = result.Password;
                    Console.WriteLine($"✓ Authenticated with manual credentials for {camera.IpAddress}");
                }
                
                // Agregar cámara a la lista
                LstCameras.Items.Add(camera);
                TxtStatus.Text = $"Camera added: {camera.Name} ({camera.IpAddress})";
                Console.WriteLine($"✓ Camera {camera.Name} added to list");
            });
        }

        private void LstCameras_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
            
            if (selectedCameras.Count == 0)
                return;
            
            // Verificar que no exceda el layout actual
            if (selectedCameras.Count > _currentLayout)
            {
                MessageBox.Show($"Current layout supports maximum {_currentLayout} camera(s).\nChange layout or deselect some cameras.",
                    "Too many cameras", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Detener todas las cámaras actuales
            StopAllCameras();
            
            // Iniciar streams para las cámaras seleccionadas
            for (int i = 0; i < selectedCameras.Count && i < _cameraViews.Count; i++)
            {
                StartCamera(_cameraViews[i], selectedCameras[i]);
            }
            
            TxtVideoStatus.Text = "";
            TxtConnectionInfo.Text = $"Connected: {selectedCameras.Count} camera(s)";
        }

        // --- Fix visual: fondo blanco del VideoView mientras no hay video reproduciéndose ---
        //
        // LibVLCSharp.WPF.VideoView no dibuja con el compositor de WPF: internamente crea una
        // ventana nativa (campo privado "_videoHwndHost", de tipo VideoHwndHost : HwndHost) y le
        // entrega el handle a libvlc para que pinte el video ahí directamente ("airspace"). Hasta
        // que no hay un frame de video real, esa ventana nativa se repinta con el brush de fondo
        // por defecto de su clase de ventana (gris claro/blanco de Windows) cada vez que Windows
        // le manda WM_ERASEBKGND — lo cual pasa en CUALQUIER resize (confirmado: ocurre igual
        // redimensionando la ventana a mano, sin tocar el botón de ocultar/mostrar lista; con
        // SetupGridLayout no se nota porque se recrean los Border de cada celda desde cero).
        // Como no hay forma pública de fijar ese color, se cambia el brush de fondo de la CLASE
        // de ventana nativa vía SetClassLongPtr (GCLP_HBRBACKGROUND) apenas el VideoView obtiene
        // su handle (evento Loaded) — accede a _videoHwndHost por reflexión ya que no es público.
        // Todo con try/catch: si la versión de LibVLCSharp cambia esos detalles internos, esto
        // simplemente no hace nada y la app sigue funcionando igual que antes.
        private static IntPtr _darkVideoClassBrush = IntPtr.Zero;
        private const int GCLP_HBRBACKGROUND = -10;

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint crColor);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        private static void DarkenVideoViewNativeBackground(VideoView videoView)
        {
            videoView.Loaded += (s, e) =>
            {
                try
                {
                    var field = typeof(VideoView).GetField("_videoHwndHost", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field?.GetValue(videoView) is HwndHost hwndHost && hwndHost.Handle != IntPtr.Zero)
                    {
                        if (_darkVideoClassBrush == IntPtr.Zero)
                        {
                            // COLORREF 0x00BBGGRR; #252525 tiene los 3 canales iguales así que
                            // el orden de bytes no importa acá (coincide con el fondo de cada celda).
                            _darkVideoClassBrush = CreateSolidBrush(0x00252525);
                        }

                        if (_darkVideoClassBrush != IntPtr.Zero)
                        {
                            SetClassLongPtr(hwndHost.Handle, GCLP_HBRBACKGROUND, _darkVideoClassBrush);
                            InvalidateRect(hwndHost.Handle, IntPtr.Zero, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No se pudo oscurecer el fondo nativo del VideoView: {ex.Message}");
                }
            };
        }

        private async void SetupGridLayout(int gridSize)
        {
            _currentLayout = gridSize;
            
            // Guardar referencias a cámaras activas antes de limpiar
            var activeCameras = _cameraViews
                .Where(v => v.Camera != null && !string.IsNullOrEmpty(v.Camera.Name))
                .Select(v => v.Camera)
                .ToList();
            
            // Limpiar vistas anteriores de forma segura en background
            await Task.Run(() =>
            {
                foreach (var view in _cameraViews.ToList())
                {
                    try
                    {
                        if (view.MediaPlayer != null)
                        {
                            view.MediaPlayer.Stop();
                            view.CurrentMedia?.Dispose();
                            view.MediaPlayer.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing MediaPlayer: {ex.Message}");
                    }
                }
            });
            
            VideoGrid.Children.Clear();
            VideoGrid.RowDefinitions.Clear();
            VideoGrid.ColumnDefinitions.Clear();
            _cameraViews.Clear();
            
            // Verificar que LibVLC esté inicializado
            if (_libVLC == null)
            {
                System.Diagnostics.Debug.WriteLine("LibVLC no está inicializado");
                return;
            }
            
            int rows = gridSize == 1 ? 1 : (gridSize == 2 ? 1 : (gridSize == 4 ? 2 : 3));
            int cols = gridSize == 1 ? 1 : (gridSize == 2 ? 2 : (gridSize == 4 ? 2 : 3));
            
            // Crear filas y columnas
            for (int i = 0; i < rows; i++)
                VideoGrid.RowDefinitions.Add(new RowDefinition());
            
            for (int i = 0; i < cols; i++)
                VideoGrid.ColumnDefinitions.Add(new ColumnDefinition());
            
            // Crear VideoViews para cada celda
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(37, 37, 37)), // #252525
                        BorderBrush = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(2)
                    };
                    
                    try
                    {
                        // Crear un Grid para contener el video y el overlay del botón de audio
                        var cellGrid = new Grid();
                        
                        var videoView = new VideoView();
                        var mediaPlayer = new VLCMediaPlayer(_libVLC);
                        videoView.MediaPlayer = mediaPlayer;
                        DarkenVideoViewNativeBackground(videoView);

                        // Agregar video al grid con ZIndex 0 (fondo)
                        Panel.SetZIndex(videoView, 0);
                        cellGrid.Children.Add(videoView);
                        
                        // Crear botón de audio con icono de bocina
                        var audioButton = new Button
                        {
                            Width = 35,
                            Height = 35,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(0, 0, 10, 10),
                            Background = new SolidColorBrush(Color.FromArgb(180, 220, 53, 69)), // Rojo semi-transparente (muted)
                            BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand,
                            FontSize = 18,
                            Content = "🔇", // Bocina con X (muted)
                            Foreground = Brushes.White,
                            ToolTip = "Click to unmute audio",
                            Focusable = false, // Evitar que robe el foco del video
                            IsTabStop = false  // Excluir de navegación por teclado
                        };
                        
                        // Crear CameraView antes de asignar el evento
                        var cameraView = new CameraView(new CameraInfo(), mediaPlayer);
                        cameraView.AudioButton = audioButton; // Guardar referencia al botón
                        _cameraViews.Add(cameraView);
                        int panelIndex = _cameraViews.Count - 1;

                        // Evento click para toggle audio
                        audioButton.Click += (s, e) => ToggleAudio(cameraView, audioButton);

                        // Agregar botón al grid con ZIndex 10 (encima del video)
                        Panel.SetZIndex(audioButton, 10);
                        cellGrid.Children.Add(audioButton);

                        // Overlay transparente para iniciar el arrastre de la cámara activa.
                        // Se usa un overlay en vez del VideoView directamente porque el control de
                        // video puede no propagar los eventos de mouse de WPF de forma confiable.
                        var dragOverlay = new Border
                        {
                            Background = Brushes.Transparent,
                            Tag = panelIndex
                        };
                        dragOverlay.PreviewMouseLeftButtonDown += VideoPanel_PreviewMouseLeftButtonDown;
                        dragOverlay.PreviewMouseMove += VideoPanel_PreviewMouseMove;
                        Panel.SetZIndex(dragOverlay, 5);
                        cellGrid.Children.Add(dragOverlay);

                        border.Child = cellGrid;
                        border.Tag = panelIndex;
                        border.AllowDrop = true;
                        border.Drop += VideoPanel_Drop;
                        border.DragEnter += VideoPanel_DragEnter;
                        border.DragLeave += VideoPanel_DragLeave;
                        Grid.SetRow(border, row);
                        Grid.SetColumn(border, col);
                        VideoGrid.Children.Add(border);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating MediaPlayer: {ex.Message}");
                        // Agregar un placeholder en caso de error
                        var errorText = new TextBlock
                        {
                            Text = "Error",
                            Foreground = new SolidColorBrush(Colors.Red),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        border.Child = errorText;
                        Grid.SetRow(border, row);
                        Grid.SetColumn(border, col);
                        VideoGrid.Children.Add(border);
                    }
                }
            }
            
            // Actualizar botones de layout
            UpdateLayoutButtons();
            
            // Restaurar cámaras activas en el nuevo layout
            if (activeCameras.Count > 0)
            {
                Console.WriteLine($"Restoring {activeCameras.Count} active camera(s) in new layout...");
                
                for (int i = 0; i < Math.Min(activeCameras.Count, _cameraViews.Count); i++)
                {
                    try
                    {
                        StartCamera(_cameraViews[i], activeCameras[i]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error restoring camera {activeCameras[i].Name}: {ex.Message}");
                    }
                }
            }
        }
        
        private void UpdateLayoutButtons()
        {
            BtnLayout1.Background = new SolidColorBrush(_currentLayout == 1 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout2.Background = new SolidColorBrush(_currentLayout == 2 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout4.Background = new SolidColorBrush(_currentLayout == 4 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout9.Background = new SolidColorBrush(_currentLayout == 9 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
        }
        
        private async void StartCamera(CameraView cameraView, CameraInfo camera, StreamQuality? quality = null)
        {
            // Evita que una llamada "fire-and-forget" en vuelo (p. ej. esperando una respuesta
            // ONVIF que nunca llega) reanude su continuación después de que la ventana ya empezó
            // a cerrarse: antes esto podía abrir un MessageBox sin dueño tras CleanupResources(),
            // dejando el proceso colgado (visible en tasklist, sin ninguna ventana a la vista).
            if (_isDisposing)
                return;

            try
            {
                cameraView.Camera = camera;
                
                // Usar calidad especificada o global
                var streamQuality = quality ?? _globalStreamQuality;
                int channel = (int)streamQuality; // 0 = main (HD), 1 = sub (SD)
                
                string rtspUrl;
                
                // Si la cámara tiene URL RTSP personalizada desde ONVIF, usarla
                if (camera.UseCustomRtspUrl && !string.IsNullOrEmpty(camera.CustomRtspUrl))
                {
                    rtspUrl = camera.CustomRtspUrl;
                    Console.WriteLine($"Using custom RTSP URL: {rtspUrl}");
                }
                // Si no, intentar obtenerla desde ONVIF
                else if (!string.IsNullOrEmpty(camera.Username))
                {
                    Console.WriteLine($"Attempting to get RTSP URL from ONVIF for {camera.Name}...");
                    
                    // Obtener ProfileToken
                    var profileToken = await _ptzService.GetProfileTokenAsync(camera.IpAddress, camera.Username, camera.Password);
                    
                    if (!string.IsNullOrEmpty(profileToken))
                    {
                        // Usar credenciales RTSP, o ONVIF como fallback si RTSP está vacío
                        var rtspUser = string.IsNullOrEmpty(camera.RtspUsername) ? camera.Username : camera.RtspUsername;
                        var rtspPass = string.IsNullOrEmpty(camera.RtspPassword) && string.IsNullOrEmpty(camera.RtspUsername) 
                            ? camera.Password 
                            : camera.RtspPassword;
                        
                        // Obtener URL RTSP desde ONVIF
                        var onvifRtspUrl = await _ptzService.GetRtspUrlAsync(camera.IpAddress, rtspUser, rtspPass, profileToken);
                        
                        if (!string.IsNullOrEmpty(onvifRtspUrl))
                        {
                            // Guardar URL para futuros usos
                            camera.CustomRtspUrl = onvifRtspUrl;
                            camera.UseCustomRtspUrl = true;
                            rtspUrl = onvifRtspUrl;
                            Console.WriteLine($"✓ Got RTSP URL from ONVIF: {rtspUrl}");
                        }
                        else
                        {
                            // Fallback a formato V380
                            Console.WriteLine("⚠ Could not get RTSP URL from ONVIF, using V380 format");
                            string credentials = string.IsNullOrEmpty(camera.RtspPassword) 
                                ? $"{camera.RtspUsername}:@" 
                                : $"{camera.RtspUsername}:{camera.RtspPassword}@";
                            rtspUrl = $"rtsp://{credentials}{camera.IpAddress}/live/ch00_{channel}";
                        }
                    }
                    else
                    {
                        // Fallback a formato V380
                        Console.WriteLine("⚠ Could not get ProfileToken, using V380 format");
                        string credentials = string.IsNullOrEmpty(camera.RtspPassword) 
                            ? $"{camera.RtspUsername}:@" 
                            : $"{camera.RtspUsername}:{camera.RtspPassword}@";
                        rtspUrl = $"rtsp://{credentials}{camera.IpAddress}/live/ch00_{channel}";
                    }
                }
                else
                {
                    // Formato V380 por defecto
                    string credentials = string.IsNullOrEmpty(camera.RtspPassword) 
                        ? $"{camera.RtspUsername}:@" 
                        : $"{camera.RtspUsername}:{camera.RtspPassword}@";
                    rtspUrl = $"rtsp://{credentials}{camera.IpAddress}/live/ch00_{channel}";
                }
                
                // La ventana pudo haberse empezado a cerrar mientras esperábamos las llamadas
                // ONVIF de arriba (await). Si ya estamos limpiando recursos, no intentar
                // reproducir ni mostrar diálogos: _libVLC puede estar disponiéndose o ya en null.
                if (_isDisposing)
                    return;

                if (_libVLC == null)
                {
                    MessageBox.Show("LibVLC no está inicializado", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var media = new Media(_libVLC, new Uri(rtspUrl));
                cameraView.CurrentMedia = media;
                cameraView.MediaPlayer.Play(media);
                
                // Ensure audio is muted by default
                cameraView.MediaPlayer.Mute = cameraView.IsMuted;
                
                Console.WriteLine($"Started {camera.Name} with {streamQuality} quality: {rtspUrl}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start {camera.Name}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StopAllCameras()
        {
            foreach (var view in _cameraViews)
            {
                view.MediaPlayer?.Stop();
                view.CurrentMedia?.Dispose();
                view.CurrentMedia = null;
            }
        }

        private void StopCamera(CameraView view)
        {
            try
            {
                view.MediaPlayer?.Stop();
                view.CurrentMedia?.Dispose();
                view.CurrentMedia = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping camera: {ex.Message}");
            }
            finally
            {
                view.Camera = new CameraInfo();
            }
        }

        private void AssignCameraToPanel(CameraView view, CameraInfo? camera)
        {
            StopCamera(view);

            if (camera != null && !string.IsNullOrEmpty(camera.Name))
            {
                StartCamera(view, camera);
            }
        }

        // --- Drag & drop: mover cámaras entre paneles de video ---

        private void LstCameras_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _cameraDragStartPoint = e.GetPosition(null);
        }

        private void LstCameras_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var current = e.GetPosition(null);
            if (Math.Abs(current.X - _cameraDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _cameraDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is CameraInfo camera)
            {
                DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(CameraInfo), camera), DragDropEffects.Move);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void VideoPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _panelDragStartPoint = e.GetPosition(null);
        }

        private void VideoPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (sender is not Border overlay || overlay.Tag is not int index || index < 0 || index >= _cameraViews.Count)
                return;

            var cameraView = _cameraViews[index];
            if (string.IsNullOrEmpty(cameraView.Camera?.Name))
                return; // Panel vacío, nada que arrastrar

            var current = e.GetPosition(null);
            if (Math.Abs(current.X - _panelDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _panelDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            DragDrop.DoDragDrop(overlay, new DataObject(typeof(CameraView), cameraView), DragDropEffects.Move);
        }

        private void VideoPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && (e.Data.GetDataPresent(typeof(CameraInfo)) || e.Data.GetDataPresent(typeof(CameraView))))
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(102, 192, 244)); // SteamBlue: feedback de drop válido
            }
        }

        private void VideoPanel_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 73, 94));
            }
        }

        private void VideoPanel_Drop(object sender, DragEventArgs e)
        {
            if (sender is not Border targetBorder || targetBorder.Tag is not int targetIndex)
                return;

            targetBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 73, 94));

            if (targetIndex < 0 || targetIndex >= _cameraViews.Count)
                return;

            var targetView = _cameraViews[targetIndex];

            if (e.Data.GetData(typeof(CameraView)) is CameraView sourceView)
            {
                if (sourceView == targetView)
                    return;

                var sourceCamera = sourceView.Camera;
                var targetCamera = targetView.Camera;

                AssignCameraToPanel(sourceView, targetCamera);
                AssignCameraToPanel(targetView, sourceCamera);

                TxtStatus.Text = string.IsNullOrEmpty(targetCamera?.Name)
                    ? $"{sourceCamera?.Name} moved to panel"
                    : $"Swapped {sourceCamera?.Name} and {targetCamera.Name}";
            }
            else if (e.Data.GetData(typeof(CameraInfo)) is CameraInfo droppedCamera)
            {
                AssignCameraToPanel(targetView, droppedCamera);
                TxtStatus.Text = $"{droppedCamera.Name} connected to panel";
            }
        }

        private void BtnLayout1_Click(object sender, RoutedEventArgs e)
        {
            SetupGridLayout(1);
            LstCameras.SelectedItems.Clear();
        }
        
        private void BtnLayout2_Click(object sender, RoutedEventArgs e)
        {
            SetupGridLayout(2);
            LstCameras.SelectedItems.Clear();
        }
        
        private void BtnLayout4_Click(object sender, RoutedEventArgs e)
        {
            SetupGridLayout(4);
            LstCameras.SelectedItems.Clear();
        }
        
        private void BtnLayout9_Click(object sender, RoutedEventArgs e)
        {
            SetupGridLayout(9);
            LstCameras.SelectedItems.Clear();
        }
        
        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme);
        }
        
        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                // Aplicar tema oscuro Steam
                // Panel superior
                TopPanel.Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)); // #171A21
                
                // Panel de lista de cámaras - MANTENER CONSISTENTE
                CameraListPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                CameraListPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 37, 47)); // #1A252F
                
                // Título de lista - BLANCO
                TxtCameraListTitle.Foreground = new SolidColorBrush(Colors.White);
                
                // Lista de cámaras - MANTENER CONSISTENTE
                LstCameras.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                
                // Panel PTZ
                PtzPanel.Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)); // #171A21
                
                // Botones de layout inactivos
                BtnLayout2.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                BtnLayout4.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                BtnLayout9.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                
                // Actualizar botón de tema (clic -> vuelve al tema normal)
                BtnTheme.Content = "☀️ Normal";
                BtnTheme.Background = new SolidColorBrush(Color.FromRgb(102, 192, 244)); // SteamBlue

                Console.WriteLine("✓ Steam Dark Theme activated");
            }
            else
            {
                // Aplicar tema normal (manteniendo paneles oscuros por diseño)
                // Panel superior
                TopPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                
                // Panel de lista de cámaras - MANTENER OSCURO
                CameraListPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                CameraListPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 37, 47)); // #1A252F
                
                // Título de lista - MANTENER BLANCO
                TxtCameraListTitle.Foreground = new SolidColorBrush(Colors.White);
                
                // Lista de cámaras - MANTENER OSCURO
                LstCameras.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                
                // Panel PTZ
                PtzPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                
                // Botones de layout inactivos
                BtnLayout2.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                BtnLayout4.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                BtnLayout9.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                
                // Actualizar botón de tema (clic -> vuelve al tema oscuro)
                BtnTheme.Content = "🌙 Oscuro";
                BtnTheme.Background = new SolidColorBrush(Color.FromRgb(102, 192, 244)); // SteamBlue

                Console.WriteLine("✓ Normal Theme activated");
            }
            
            // Actualizar botones de layout activos
            UpdateLayoutButtons();
        }
        
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            // Cerrar la aplicación
            Close();
        }
        
        private void BtnQuality_Click(object sender, RoutedEventArgs e)
        {
            // Alternar calidad
            _globalStreamQuality = _globalStreamQuality == StreamQuality.Main 
                ? StreamQuality.Sub 
                : StreamQuality.Main;
            
            // Actualizar texto del botón
            BtnQuality.Content = _globalStreamQuality == StreamQuality.Main ? "⚙️ HD" : "⚙️ SD";
            BtnQuality.Background = new SolidColorBrush(_globalStreamQuality == StreamQuality.Main 
                ? Color.FromRgb(155, 89, 182)   // Morado para HD
                : Color.FromRgb(230, 126, 34)); // Naranja para SD
            
            // Reiniciar streams activos con nueva calidad
            RestartActiveStreams();
            
            TxtStatus.Text = $"Quality changed to {(_globalStreamQuality == StreamQuality.Main ? "HD (Main Stream)" : "SD (Sub Stream)")}";
        }
        
        private void RestartActiveStreams()
        {
            var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
            
            if (selectedCameras.Count == 0)
                return;
            
            // Detener todos
            StopAllCameras();
            
            // Reiniciar con nueva calidad
            for (int i = 0; i < selectedCameras.Count && i < _cameraViews.Count; i++)
            {
                StartCamera(_cameraViews[i], selectedCameras[i]);
            }
        }
        
        // ==================== GLOBAL AUDIO CONTROL ====================
        
        private void BtnGlobalAudio_Click(object sender, RoutedEventArgs e)
        {
            // Toggle global audio state
            _globalAudioMuted = !_globalAudioMuted;
            
            // Update global button appearance
            if (_globalAudioMuted)
            {
                BtnGlobalAudio.Content = "🔇 Audio";
                BtnGlobalAudio.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Rojo
                BtnGlobalAudio.ToolTip = "Audio muted - Click to enable audio for all cameras";
            }
            else
            {
                BtnGlobalAudio.Content = "🔊 Audio";
                BtnGlobalAudio.Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Verde
                BtnGlobalAudio.ToolTip = "Audio enabled - Click to mute all cameras";
            }
            
            // Apply to all active camera views
            int affectedCameras = 0;
            foreach (var cameraView in _cameraViews)
            {
                if (cameraView.MediaPlayer != null && cameraView.Camera != null && !string.IsNullOrEmpty(cameraView.Camera.Name))
                {
                    cameraView.IsMuted = _globalAudioMuted;
                    cameraView.MediaPlayer.Mute = _globalAudioMuted;
                    
                    // Update individual button appearance
                    if (cameraView.AudioButton != null)
                    {
                        if (_globalAudioMuted)
                        {
                            cameraView.AudioButton.Background = new SolidColorBrush(Color.FromArgb(180, 220, 53, 69));
                            cameraView.AudioButton.Content = "🔇";
                            cameraView.AudioButton.ToolTip = "Click to unmute audio";
                        }
                        else
                        {
                            cameraView.AudioButton.Background = new SolidColorBrush(Color.FromArgb(180, 39, 174, 96));
                            cameraView.AudioButton.Content = "🔊";
                            cameraView.AudioButton.ToolTip = "Click to mute audio";
                        }
                    }
                    affectedCameras++;
                }
            }
            
            TxtStatus.Text = _globalAudioMuted 
                ? $"Audio muted for {affectedCameras} camera(s)" 
                : $"Audio enabled for {affectedCameras} camera(s)";
            
            Console.WriteLine($"Global audio {(_globalAudioMuted ? "muted" : "enabled")} for {affectedCameras} camera(s)");
        }
        
        // ==================== VIDEO RECORDING ====================
        
        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            // Verificar si hay cámaras activas
            var activeCameras = _cameraViews.Where(v => v.Camera != null && !string.IsNullOrEmpty(v.Camera.Name)).ToList();
            
            if (activeCameras.Count == 0)
            {
                MessageBox.Show("Please select and start a camera first.", "No Active Camera", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // Verificar si alguna cámara está grabando
            var recordingCameras = activeCameras.Where(v => v.IsRecording).ToList();
            
            if (recordingCameras.Count > 0)
            {
                // Detener todas las grabaciones
                StopAllRecordings();
                BtnRecord.Content = "⏺ Record";
                BtnRecord.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Rojo
                TxtStatus.Text = $"Recording stopped for {recordingCameras.Count} camera(s)";
            }
            else
            {
                // Iniciar grabación para todas las cámaras activas
                StartAllRecordings(activeCameras);
                BtnRecord.Content = "⏹ Stop";
                BtnRecord.Background = new SolidColorBrush(Color.FromRgb(192, 57, 43)); // Rojo oscuro
                TxtStatus.Text = $"Recording started for {activeCameras.Count} camera(s)";
            }
        }
        
        private void StartAllRecordings(List<CameraView> cameras)
        {
            // Crear carpeta de grabaciones si no existe
            string recordingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "V380Recordings");
            Directory.CreateDirectory(recordingsFolder);
            
            int successCount = 0;
            
            foreach (var cameraView in cameras)
            {
                if (cameraView.MediaPlayer != null && cameraView.Camera != null)
                {
                    try
                    {
                        if (_libVLC == null)
                        {
                            Console.WriteLine("✗ LibVLC not initialized");
                            continue;
                        }
                        
                        // Generar nombre de archivo con timestamp
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string safeFileName = string.Join("_", cameraView.Camera.Name.Split(Path.GetInvalidFileNameChars()));
                        string fileName = $"{safeFileName}_{timestamp}.mp4";
                        string filePath = Path.Combine(recordingsFolder, fileName);
                        
                        // Detener el stream actual
                        cameraView.MediaPlayer.Stop();
                        cameraView.CurrentMedia?.Dispose();
                        
                        // Obtener la URL RTSP actual
                        string rtspUrl = GetRtspUrl(cameraView.Camera);
                        
                        if (_libVLC == null)
                        {
                            Console.WriteLine("⚠ LibVLC no está inicializado");
                            continue;
                        }
                        
                        // Crear nuevo media con opciones de grabación
                        var media = new Media(_libVLC, new Uri(rtspUrl));
                        
                        // Agregar opciones de grabación usando duplicate para mostrar Y grabar
                        string soutOption = $":sout=#duplicate{{dst=display,dst=std{{access=file,mux=mp4,dst={filePath}}}}}";
                        media.AddOption(soutOption);
                        media.AddOption(":sout-keep");
                        
                        cameraView.CurrentMedia = media;
                        cameraView.MediaPlayer.Play(media);
                        
                        // Preserve mute state
                        cameraView.MediaPlayer.Mute = cameraView.IsMuted;
                        
                        cameraView.IsRecording = true;
                        cameraView.RecordingPath = filePath;
                        
                        Console.WriteLine($"✓ Recording started: {filePath}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed to start recording for {cameraView.Camera.Name}: {ex.Message}");
                        MessageBox.Show($"Failed to start recording for {cameraView.Camera.Name}: {ex.Message}", 
                            "Recording Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
            if (successCount > 0)
            {
                // Mostrar mensaje con ubicación de grabaciones
                MessageBox.Show($"Recording started for {successCount} camera(s)\n\nSaving to: {recordingsFolder}", 
                    "Recording Started", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private string GetRtspUrl(CameraInfo camera)
        {
            // Usar calidad global
            int channel = (int)_globalStreamQuality; // 0 = main (HD), 1 = sub (SD)
            
            // Si la cámara tiene URL RTSP personalizada, usarla
            if (camera.UseCustomRtspUrl && !string.IsNullOrEmpty(camera.CustomRtspUrl))
            {
                return camera.CustomRtspUrl;
            }
            
            // Formato V380 por defecto
            string credentials = string.IsNullOrEmpty(camera.RtspPassword) 
                ? $"{camera.RtspUsername}:@" 
                : $"{camera.RtspUsername}:{camera.RtspPassword}@";
            
            return $"rtsp://{credentials}{camera.IpAddress}/live/ch00_{channel}";
        }
        
        /// <summary>
        /// Detiene las grabaciones activas. Por defecto reinicia el stream sin las opciones de
        /// grabación para seguir viendo la cámara en vivo (uso normal desde BtnRecord_Click).
        /// Con restartStream=false (cierre de la app) NO reconecta nada — el llamador se encarga
        /// de detener/disponer los MediaPlayer justo después; reiniciar streams al cerrar la
        /// ventana abriría conexiones nuevas justo cuando se está intentando cerrar todo.
        /// </summary>
        private void StopAllRecordings(bool restartStream = true)
        {
            var recordingCameras = _cameraViews.Where(v => v.IsRecording).ToList();

            foreach (var cameraView in recordingCameras)
            {
                try
                {
                    cameraView.IsRecording = false;

                    if (!string.IsNullOrEmpty(cameraView.RecordingPath))
                    {
                        Console.WriteLine($"✓ Recording stopped: {cameraView.RecordingPath}");

                        if (restartStream && cameraView.Camera != null)
                        {
                            // Reiniciar el stream sin las opciones de grabación para seguir viendo en vivo
                            cameraView.MediaPlayer?.Stop();
                            cameraView.CurrentMedia?.Dispose();
                            StartCamera(cameraView, cameraView.Camera);
                        }

                        cameraView.RecordingPath = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error stopping recording: {ex.Message}");
                }
            }
        }
        
        // ==================== PTZ CONTROLS ====================
        
        private void BtnShowPtz_Click(object sender, RoutedEventArgs e)
        {
            // Click derecho para configurar credenciales
            if (System.Windows.Input.Mouse.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                ConfigurePtzCredentials();
            }
            else
            {
                TogglePtzPanel();
            }
        }
        
        private void ConfigurePtzCredentials()
        {
            var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
            if (selectedCameras.Count == 0)
            {
                MessageBox.Show("Please select a camera first.", "PTZ Credentials", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var camera = selectedCameras[0];
            
            var inputDialog = new Window
            {
                Title = $"PTZ Credentials - {camera.Name}",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                ResizeMode = ResizeMode.NoResize
            };
            
            // Crear StackPanel principal
            var mainPanel = new StackPanel { Margin = new Thickness(20) };
            
            // Username
            var lblUsername = new TextBlock 
            { 
                Text = "Username:", 
                Foreground = Brushes.White, 
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtUsername = new TextBox 
            { 
                Text = camera.Username, 
                Height = 32,
                Padding = new Thickness(8),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            // Password
            var lblPassword = new TextBlock 
            { 
                Text = "Password:", 
                Foreground = Brushes.White, 
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtPassword = new TextBox 
            { 
                Text = camera.Password,  // Usar TextBox normal para ver la contraseña
                Height = 32,
                Padding = new Thickness(8),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // Hint
            var lblHint = new TextBlock 
            { 
                Text = "Common passwords: (empty), admin, 888888", 
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), 
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            // Botones
            var btnPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var btnOk = new Button 
            { 
                Content = "Save", 
                Width = 90, 
                Height = 35, 
                Margin = new Thickness(0, 0, 10, 0), 
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            var btnCancel = new Button 
            { 
                Content = "Cancel", 
                Width = 90, 
                Height = 35, 
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            btnOk.Click += (s, e) => 
            { 
                camera.Username = txtUsername.Text; 
                camera.Password = txtPassword.Text; 
                inputDialog.DialogResult = true; 
            };
            
            btnCancel.Click += (s, e) => 
            { 
                inputDialog.DialogResult = false; 
            };
            
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            
            // Agregar todo al panel principal
            mainPanel.Children.Add(lblUsername);
            mainPanel.Children.Add(txtUsername);
            mainPanel.Children.Add(lblPassword);
            mainPanel.Children.Add(txtPassword);
            mainPanel.Children.Add(lblHint);
            mainPanel.Children.Add(btnPanel);
            
            inputDialog.Content = mainPanel;
            
            if (inputDialog.ShowDialog() == true)
            {
                TxtStatus.Text = $"Credentials updated: {camera.Username} / {(string.IsNullOrEmpty(camera.Password) ? "(empty)" : "***")}";
                System.Diagnostics.Debug.WriteLine($"PTZ Credentials updated for {camera.Name}: {camera.Username} / {camera.Password}");
            }
        }
        
        private void MenuConfigureCredentials_Click(object sender, RoutedEventArgs e)
        {
            var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
            if (selectedCameras.Count == 0)
            {
                MessageBox.Show("Please select a camera first.", "Configure Credentials", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var camera = selectedCameras[0];
            
            var inputDialog = new Window
            {
                Title = $"Camera Credentials - {camera.Name}",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                ResizeMode = ResizeMode.NoResize
            };
            
            var mainPanel = new StackPanel { Margin = new Thickness(20) };
            
            // Título RTSP
            var lblRtspTitle = new TextBlock 
            { 
                Text = "📹 RTSP Streaming Credentials", 
                Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)), 
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10) 
            };
            
            // RTSP Username
            var lblRtspUsername = new TextBlock 
            { 
                Text = "Username:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtRtspUsername = new TextBox 
            { 
                Text = camera.RtspUsername, 
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // RTSP Password
            var lblRtspPassword = new TextBlock 
            { 
                Text = "Password:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtRtspPassword = new TextBox 
            { 
                Text = camera.RtspPassword,
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            // Separador
            var separator = new Separator 
            { 
                Margin = new Thickness(0, 10, 0, 15),
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
            
            // Título ONVIF/PTZ
            var lblOnvifTitle = new TextBlock 
            { 
                Text = "🎮 ONVIF/PTZ Credentials", 
                Foreground = new SolidColorBrush(Color.FromRgb(155, 89, 182)), 
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10) 
            };
            
            // ONVIF Username
            var lblOnvifUsername = new TextBlock 
            { 
                Text = "Username:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtOnvifUsername = new TextBox 
            { 
                Text = camera.Username, 
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // ONVIF Password
            var lblOnvifPassword = new TextBlock 
            { 
                Text = "Password:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtOnvifPassword = new TextBox 
            { 
                Text = camera.Password,
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // Hint
            var lblHint = new TextBlock 
            { 
                Text = "💡 Common: admin/(empty), admin/admin, admin/888888", 
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)), 
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 5, 0, 15)
            };
            
            // Botones
            var btnPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var btnOk = new Button
            {
                Content = "Save",
                Width = 90,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Style = (Style)Application.Current.Resources["SuccessButton"],
                FontSize = 12
            };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 90,
                Height = 35,
                Style = (Style)Application.Current.Resources["DangerButton"],
                FontSize = 12
            };

            btnOk.Click += (s, e) =>
            {
                camera.RtspUsername = txtRtspUsername.Text;
                camera.RtspPassword = txtRtspPassword.Text; 
                camera.Username = txtOnvifUsername.Text; 
                camera.Password = txtOnvifPassword.Text;
                
                // Si RTSP está vacío, copiar credenciales ONVIF
                if (string.IsNullOrEmpty(camera.RtspUsername) && !string.IsNullOrEmpty(camera.Username))
                {
                    camera.RtspUsername = camera.Username;
                    camera.RtspPassword = camera.Password;
                    Console.WriteLine($"✓ Copied ONVIF credentials to RTSP for {camera.Name}");
                }
                
                // Limpiar URL RTSP en caché para forzar re-detección
                camera.UseCustomRtspUrl = false;
                camera.CustomRtspUrl = null;
                
                inputDialog.DialogResult = true; 
            };
            
            btnCancel.Click += (s, e) => 
            { 
                inputDialog.DialogResult = false; 
            };
            
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            
            // Agregar todo al panel principal
            mainPanel.Children.Add(lblRtspTitle);
            mainPanel.Children.Add(lblRtspUsername);
            mainPanel.Children.Add(txtRtspUsername);
            mainPanel.Children.Add(lblRtspPassword);
            mainPanel.Children.Add(txtRtspPassword);
            mainPanel.Children.Add(separator);
            mainPanel.Children.Add(lblOnvifTitle);
            mainPanel.Children.Add(lblOnvifUsername);
            mainPanel.Children.Add(txtOnvifUsername);
            mainPanel.Children.Add(lblOnvifPassword);
            mainPanel.Children.Add(txtOnvifPassword);
            mainPanel.Children.Add(lblHint);
            mainPanel.Children.Add(btnPanel);
            
            inputDialog.Content = mainPanel;
            
            if (inputDialog.ShowDialog() == true)
            {
                TxtStatus.Text = $"✓ Credentials updated for {camera.Name}";
                Console.WriteLine($"Credentials updated for {camera.Name}:");
                Console.WriteLine($"  RTSP: {camera.RtspUsername} / {(string.IsNullOrEmpty(camera.RtspPassword) ? "(empty)" : "***")}");
                Console.WriteLine($"  ONVIF: {camera.Username} / {(string.IsNullOrEmpty(camera.Password) ? "(empty)" : "***")}");
                
                // Reiniciar stream si la cámara está activa
                var activeView = _cameraViews.FirstOrDefault(v => v.Camera?.IpAddress == camera.IpAddress);
                if (activeView != null)
                {
                    activeView.MediaPlayer?.Stop();
                    StartCamera(activeView, camera);
                    TxtStatus.Text = $"✓ Credentials updated and stream restarted for {camera.Name}";
                }
            }
        }
        
        private void MenuRemoveCamera_Click(object sender, RoutedEventArgs e)
        {
            var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
            if (selectedCameras.Count == 0)
            {
                MessageBox.Show("Please select a camera to remove.", "Remove Camera", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var result = MessageBox.Show(
                $"Are you sure you want to remove {selectedCameras.Count} camera(s)?", 
                "Confirm Removal", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                foreach (var camera in selectedCameras)
                {
                    // Detener stream si está activo
                    var activeView = _cameraViews.FirstOrDefault(v => v.Camera?.IpAddress == camera.IpAddress);
                    if (activeView != null)
                    {
                        activeView.MediaPlayer?.Stop();
                    }
                    
                    LstCameras.Items.Remove(camera);
                }
                
                TxtStatus.Text = $"✓ Removed {selectedCameras.Count} camera(s)";
            }
        }
        
        /// <summary>
        /// Muestra un diálogo para solicitar solo credenciales RTSP
        /// </summary>
        private Task<(bool Success, string Username, string Password)> ShowRtspCredentialsDialogAsync(CameraInfo camera)
        {
            var tcs = new TaskCompletionSource<(bool Success, string Username, string Password)>();
            
            var dialog = new Window
            {
                Title = $"RTSP Credentials - {camera.IpAddress}",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            
            var mainPanel = new StackPanel { Margin = new Thickness(20) };
            
            // Mensaje
            var lblMessage = new TextBlock
            {
                Text = $"ONVIF connected successfully!\nHowever, RTSP streaming may require a password.\n\nEnter RTSP credentials (or Skip to use empty password):",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            
            // Username
            var lblUsername = new TextBlock 
            { 
                Text = "RTSP Username:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtUsername = new TextBox 
            { 
                Text = "admin",
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            // Password
            var lblPassword = new TextBlock 
            { 
                Text = "RTSP Password:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtPassword = new PasswordBox 
            { 
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            // Botones
            var btnPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var btnSkip = new Button 
            { 
                Content = "Skip", 
                Width = 90, 
                Height = 35, 
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            var btnOk = new Button 
            { 
                Content = "Save", 
                Width = 90, 
                Height = 35, 
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            btnOk.Click += (s, e) => 
            { 
                tcs.SetResult((true, txtUsername.Text, txtPassword.Password));
                dialog.Close();
            };
            
            btnSkip.Click += (s, e) => 
            { 
                tcs.SetResult((false, "", ""));
                dialog.Close();
            };
            
            dialog.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult((false, "", ""));
                }
            };
            
            btnPanel.Children.Add(btnSkip);
            btnPanel.Children.Add(btnOk);
            
            mainPanel.Children.Add(lblMessage);
            mainPanel.Children.Add(lblUsername);
            mainPanel.Children.Add(txtUsername);
            mainPanel.Children.Add(lblPassword);
            mainPanel.Children.Add(txtPassword);
            mainPanel.Children.Add(btnPanel);
            
            dialog.Content = mainPanel;
            dialog.ShowDialog();
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Muestra un diálogo para solicitar credenciales durante el escaneo
        /// </summary>
        private Task<(bool Success, string Username, string Password)> ShowCredentialsDialogAsync(CameraInfo camera)
        {
            var tcs = new TaskCompletionSource<(bool Success, string Username, string Password)>();
            
            var dialog = new Window
            {
                Title = $"Credentials Required - {camera.IpAddress}",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            };
            
            var mainPanel = new StackPanel { Margin = new Thickness(20) };
            
            // Mensaje
            var lblMessage = new TextBlock
            {
                Text = $"Camera found at {camera.IpAddress}\nDefault credentials failed. Please enter credentials:",
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap
            };
            
            // Username
            var lblUsername = new TextBlock 
            { 
                Text = "Username:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtUsername = new TextBox 
            { 
                Text = "admin",
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            // Password
            var lblPassword = new TextBlock 
            { 
                Text = "Password:", 
                Foreground = Brushes.White, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            
            var txtPassword = new PasswordBox 
            { 
                Height = 30,
                Padding = new Thickness(8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            // Botones
            var btnPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var btnSkip = new Button 
            { 
                Content = "Skip", 
                Width = 90, 
                Height = 35, 
                Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            var btnOk = new Button 
            { 
                Content = "Connect", 
                Width = 90, 
                Height = 35, 
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            btnOk.Click += (s, e) => 
            { 
                tcs.SetResult((true, txtUsername.Text, txtPassword.Password));
                dialog.Close();
            };
            
            btnSkip.Click += (s, e) => 
            { 
                tcs.SetResult((false, "", ""));
                dialog.Close();
            };
            
            dialog.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult((false, "", ""));
                }
            };
            
            btnPanel.Children.Add(btnSkip);
            btnPanel.Children.Add(btnOk);
            
            mainPanel.Children.Add(lblMessage);
            mainPanel.Children.Add(lblUsername);
            mainPanel.Children.Add(txtUsername);
            mainPanel.Children.Add(lblPassword);
            mainPanel.Children.Add(txtPassword);
            mainPanel.Children.Add(btnPanel);
            
            dialog.Content = mainPanel;
            dialog.ShowDialog();
            
            return tcs.Task;
        }
        
        private void BtnTogglePtz_Click(object sender, RoutedEventArgs e)
        {
            TogglePtzPanel();
        }

        private void BtnToggleCameraList_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = CameraListPanel.Visibility == Visibility.Visible;

            if (isVisible)
            {
                CameraListPanel.Visibility = Visibility.Collapsed;
                ColCameraList.Width = new GridLength(0);
                BtnToggleCameraList.Content = "📋 Show List";
            }
            else
            {
                CameraListPanel.Visibility = Visibility.Visible;
                ColCameraList.Width = new GridLength(250);
                BtnToggleCameraList.Content = "📋 Hide List";
            }
        }
        
        private void TogglePtzPanel()
        {
            if (PtzPanel.Visibility == Visibility.Collapsed)
            {
                // Verificar que haya una cámara seleccionada
                var selectedCameras = LstCameras.SelectedItems.Cast<CameraInfo>().ToList();
                if (selectedCameras.Count == 0)
                {
                    MessageBox.Show("Please select a camera first to use PTZ controls.", "No Camera Selected", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // Usar la primera cámara seleccionada para PTZ
                _activePtzCamera = selectedCameras[0];
                PtzPanel.Visibility = Visibility.Visible;
                BtnShowPtz.Content = "🎮 PTZ ✓";
                BtnShowPtz.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Verde
                BtnTogglePtz.Content = "Hide PTZ";
                TxtStatus.Text = $"PTZ control active for {_activePtzCamera.Name}";
            }
            else
            {
                PtzPanel.Visibility = Visibility.Collapsed;
                BtnShowPtz.Content = "🎮 PTZ";
                BtnShowPtz.Background = new SolidColorBrush(Color.FromRgb(142, 68, 173)); // Morado
                _activePtzCamera = null;
                _cachedProfileToken = null; // Limpiar caché
                TxtStatus.Text = "PTZ control hidden";
            }
        }
        
        private async void BtnPtzUp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand(0, (float)SliderPtzSpeed.Value, 0);
        }
        
        private async void BtnPtzDown_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand(0, -(float)SliderPtzSpeed.Value, 0);
        }
        
        private async void BtnPtzLeft_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand(-(float)SliderPtzSpeed.Value, 0, 0);
        }
        
        private async void BtnPtzRight_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand((float)SliderPtzSpeed.Value, 0, 0);
        }
        
        private async void BtnZoomIn_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand(0, 0, (float)SliderPtzSpeed.Value);
        }
        
        private async void BtnZoomOut_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await SendPtzCommand(0, 0, -(float)SliderPtzSpeed.Value);
        }
        
        private async void BtnPtz_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Detener movimiento PTZ
            if (_activePtzCamera != null)
            {
                await _ptzService.StopAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password);
            }
        }
        
        private async void BtnPtzHome_Click(object sender, RoutedEventArgs e)
        {
            if (_activePtzCamera != null)
            {
                // Ir a preset "Home" (preset 1 es común para home)
                var success = await _ptzService.GotoPresetAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, "1");
                TxtStatus.Text = success ? "Returning to home position..." : "Failed to return home";
            }
        }
        
        private void BtnDebugPtz_Click(object sender, RoutedEventArgs e)
        {
            if (_activePtzCamera == null)
            {
                MessageBox.Show("No active PTZ camera selected.", "PTZ Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine($"Camera: {_activePtzCamera.Name}");
            debugInfo.AppendLine($"IP: {_activePtzCamera.IpAddress}");
            debugInfo.AppendLine($"Username: {_activePtzCamera.Username}");
            debugInfo.AppendLine($"Password: {(string.IsNullOrEmpty(_activePtzCamera.Password) ? "(empty)" : "***")}");
            debugInfo.AppendLine();
            debugInfo.AppendLine("=== SOAP Command (RelativeMove Right) ===");
            debugInfo.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:RelativeMove>
            <tptz:ProfileToken>profile_1</tptz:ProfileToken>
            <tptz:Translation>
                <tt:PanTilt x=""0.3"" y=""0""/>
                <tt:Zoom x=""0""/>
            </tptz:Translation>
        </tptz:RelativeMove>
    </s:Body>
</s:Envelope>");
            debugInfo.AppendLine();
            debugInfo.AppendLine("Check Debug Output window for actual responses.");
            
            MessageBox.Show(debugInfo.ToString(), "PTZ Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private async void BtnTestPtz_Click(object sender, RoutedEventArgs e)
        {
            if (_activePtzCamera == null)
            {
                MessageBox.Show("No active PTZ camera selected.", "PTZ Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            TxtStatus.Text = "Testing PTZ... Please wait";
            BtnTestPtz.IsEnabled = false;
            
            try
            {
                Console.WriteLine($"\n========== PTZ TEST START ==========");
                Console.WriteLine($"Camera: {_activePtzCamera.Name}");
                Console.WriteLine($"IP: {_activePtzCamera.IpAddress}");
                Console.WriteLine($"====================================\n");
                
                // Obtener ProfileToken primero
                Console.WriteLine("Getting ProfileToken...");
                var profileToken = await _ptzService.GetProfileTokenAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password);
                
                if (!string.IsNullOrEmpty(profileToken))
                {
                    Console.WriteLine($"✓ ProfileToken obtained: {profileToken}");
                }
                else
                {
                    Console.WriteLine("✗ Failed to get ProfileToken, using default");
                }
                
                // Test 1: Pequeño movimiento a la derecha
                Console.WriteLine("Test 1: Moving right...");
                Console.WriteLine($"Using credentials: {_activePtzCamera.Username} / {(string.IsNullOrEmpty(_activePtzCamera.Password) ? "(empty)" : "***")}");
                var success1 = await _ptzService.MoveAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, 0.3f, 0, 0, profileToken);
                await Task.Delay(1000);
                await _ptzService.StopAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, profileToken);
                
                await Task.Delay(500);
                
                // Test 2: Pequeño movimiento a la izquierda (volver)
                Console.WriteLine("Test 2: Moving left...");
                var success2 = await _ptzService.MoveAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, -0.3f, 0, 0, profileToken);
                await Task.Delay(1000);
                await _ptzService.StopAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, profileToken);
                
                Console.WriteLine($"\n========== PTZ TEST END ==========");
                Console.WriteLine($"Test 1 (Right): {(success1 ? "✓ SUCCESS" : "✗ FAILED")}");
                Console.WriteLine($"Test 2 (Left): {(success2 ? "✓ SUCCESS" : "✗ FAILED")}");
                Console.WriteLine($"==================================\n");
                
                if (success1 || success2)
                {
                    MessageBox.Show(
                        $"PTZ Test Results:\\n\\n" +
                        $"Camera: {_activePtzCamera.Name}\\n" +
                        $"IP: {_activePtzCamera.IpAddress}\\n\\n" +
                        $"Move Right: {(success1 ? "✓ SUCCESS" : "✗ FAILED")}\\n" +
                        $"Move Left: {(success2 ? "✓ SUCCESS" : "✗ FAILED")}\\n\\n" +
                        $"PTZ is working! Check Debug output for details.",
                        "PTZ Test - Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    TxtStatus.Text = "PTZ test completed successfully!";
                }
                else
                {
                    var result = MessageBox.Show(
                        $"PTZ Test Failed\\n\\n" +
                        $"Camera: {_activePtzCamera.Name}\\n" +
                        $"IP: {_activePtzCamera.IpAddress}\\n" +
                        $"Credentials: {_activePtzCamera.Username} / {(string.IsNullOrEmpty(_activePtzCamera.Password) ? "(empty)" : "***")}\\n\\n" +
                        $"Possible reasons:\\n" +
                        $"• Camera doesn't support PTZ\\n" +
                        $"• Wrong credentials (401 Unauthorized)\\n" +
                        $"• PTZ not enabled in camera settings\\n" +
                        $"• ONVIF not properly configured\\n\\n" +
                        $"Do you want to configure credentials?\\n" +
                        $"(Right-click PTZ button to configure)",
                        "PTZ Test - Failed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        ConfigurePtzCredentials();
                    }
                    TxtStatus.Text = "PTZ test failed - Camera may not support PTZ";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PTZ test error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = $"PTZ test error: {ex.Message}";
            }
            finally
            {
                BtnTestPtz.IsEnabled = true;
            }
        }
        
        private string? _cachedProfileToken = null;
        
        private async Task SendPtzCommand(float panSpeed, float tiltSpeed, float zoomSpeed)
        {
            if (_activePtzCamera == null)
            {
                TxtStatus.Text = "No active PTZ camera";
                return;
            }
            
            try
            {
                // Obtener ProfileToken si no está en caché
                if (string.IsNullOrEmpty(_cachedProfileToken))
                {
                    _cachedProfileToken = await _ptzService.GetProfileTokenAsync(
                        _activePtzCamera.IpAddress, 
                        _activePtzCamera.Username, 
                        _activePtzCamera.Password
                    );
                    
                    if (!string.IsNullOrEmpty(_cachedProfileToken))
                    {
                        Console.WriteLine($"ProfileToken cached: {_cachedProfileToken}");
                    }
                }
                
                Console.WriteLine($"Sending PTZ command: Pan={panSpeed}, Tilt={tiltSpeed}, Zoom={zoomSpeed}");
                
                var success = await _ptzService.MoveAsync(
                    _activePtzCamera.IpAddress, 
                    _activePtzCamera.Username, 
                    _activePtzCamera.Password, 
                    panSpeed, 
                    tiltSpeed, 
                    zoomSpeed,
                    _cachedProfileToken
                );
                
                if (success)
                {
                    TxtStatus.Text = $"PTZ: Moving camera {_activePtzCamera.Name}...";
                }
                else
                {
                    TxtStatus.Text = $"PTZ command failed - Check camera supports PTZ";
                    Console.WriteLine($"✗ PTZ command failed for {_activePtzCamera.IpAddress}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PTZ command error: {ex.Message}");
                TxtStatus.Text = $"PTZ error: {ex.Message}";
            }
        }

        /// <summary>
        /// Toggle audio mute/unmute for a camera view and update visual indicator
        /// </summary>
        private void ToggleAudio(CameraView cameraView, Button audioButton)
        {
            if (cameraView.MediaPlayer == null)
                return;
            
            try
            {
                // Toggle mute state
                cameraView.IsMuted = !cameraView.IsMuted;
                
                // Apply mute to MediaPlayer
                cameraView.MediaPlayer.Mute = cameraView.IsMuted;
                
                // Update button appearance based on mute state
                if (cameraView.IsMuted)
                {
                    // Muted: Red background with muted speaker icon
                    audioButton.Background = new SolidColorBrush(Color.FromArgb(180, 220, 53, 69)); // Rojo semi-transparente
                    audioButton.Content = "🔇"; // Bocina con X
                    audioButton.ToolTip = "Click to unmute audio";
                }
                else
                {
                    // Unmuted: Green background with active speaker icon
                    audioButton.Background = new SolidColorBrush(Color.FromArgb(180, 39, 174, 96)); // Verde semi-transparente
                    audioButton.Content = "🔊"; // Bocina activa
                    audioButton.ToolTip = "Click to mute audio";
                }
                
                Console.WriteLine($"Audio {(cameraView.IsMuted ? "muted" : "unmuted")} for camera: {cameraView.Camera?.Name ?? "Unknown"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error toggling audio: {ex.Message}");
            }
        }

        private bool _isDisposing = false;

        protected override void OnClosed(EventArgs e)
        {
            // Red de seguridad: si por algún motivo Window_Closing no llegó a ejecutarse,
            // CleanupResources() es idempotente (guardia _isDisposing) así que no duplica trabajo.
            CleanupResources();
            base.OnClosed(e);
        }
    }
}
