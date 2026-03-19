using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using V380Viewer.Models;
using V380Viewer.Services;
using VLCMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace V380Viewer
{
    public partial class MainWindow : Window
    {
        private readonly V380Discovery _discovery;
        private readonly OnvifDiscovery _onvifDiscovery;
        private LibVLC? _libVLC;
        private List<CameraView> _cameraViews = new List<CameraView>();
        private int _currentLayout = 1; // 1, 2, 4, or 9
        private CancellationTokenSource? _scanCancellationTokenSource;
        private StreamQuality _globalStreamQuality = StreamQuality.Main; // HD por defecto
        private readonly OnvifPtzService _ptzService;
        private CameraInfo? _activePtzCamera; // Cámara actualmente controlada por PTZ
        private bool _isDarkTheme = false; // Tema actual (false = claro, true = oscuro)
        
        public MainWindow()
        {
            InitializeComponent();
            _discovery = new V380Discovery();
            _discovery.CameraDiscovered += OnCameraDiscovered;
            _onvifDiscovery = new OnvifDiscovery();
            _onvifDiscovery.CameraDiscovered += OnCameraDiscovered;
            _ptzService = new OnvifPtzService();
            
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
                "--no-audio",                    // Desactivar audio
                "--live-caching=50",             // Cache mínimo para live
                "--avcodec-hurry-up",            // Decodificación rápida
                "--avcodec-fast",                // Codec rápido
                "--avcodec-threads=2",           // Threads reducidos para múltiples streams
                "--drop-late-frames",            // Descartar frames tardíos
                "--skip-frames",                 // Saltar frames si es necesario
                "--vout=direct3d11"              // Aceleración por hardware
            };
            
            _libVLC = new LibVLC(options);
            SetupGridLayout(_currentLayout);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancelar cualquier escaneo en progreso
            if (_scanCancellationTokenSource != null && !_scanCancellationTokenSource.IsCancellationRequested)
            {
                Console.WriteLine("Cancelling scan due to window closing...");
                _scanCancellationTokenSource.Cancel();
            }
            
            // Limpiar recursos
            Console.WriteLine("Stopping all cameras...");
            StopAllCameras();
            
            Console.WriteLine("Disposing LibVLC...");
            _libVLC?.Dispose();
            
            // Limpiar servicios
            _ptzService?.Dispose();
            _onvifDiscovery?.Dispose();
            
            Console.WriteLine("Window closed successfully");
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
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var lblIp = new TextBlock { Text = "Camera IP Address:", Margin = new Thickness(0, 0, 0, 5) };
            var txtIp = new TextBox { Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(5) };
            
            var lblName = new TextBlock { Text = "Camera Name (optional):", Margin = new Thickness(0, 0, 0, 5) };
            var txtName = new TextBox { Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(5) };
            
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Add", Width = 80, Height = 30, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)), Foreground = new SolidColorBrush(Colors.White), BorderThickness = new Thickness(0) };
            var btnCancel = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)), Foreground = new SolidColorBrush(Colors.White), BorderThickness = new Thickness(0) };
            
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
            Grid.SetRow(btnPanel, 5);
            
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
                        var videoView = new VideoView();
                        var mediaPlayer = new VLCMediaPlayer(_libVLC);
                        videoView.MediaPlayer = mediaPlayer;
                        
                        border.Child = videoView;
                        Grid.SetRow(border, row);
                        Grid.SetColumn(border, col);
                        VideoGrid.Children.Add(border);
                        
                        _cameraViews.Add(new CameraView(new CameraInfo(), mediaPlayer));
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
                
                var media = new Media(_libVLC, new Uri(rtspUrl));
                cameraView.CurrentMedia = media;
                cameraView.MediaPlayer.Play(media);
                
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
                
                // Panel de lista de cámaras
                CameraListPanel.Background = new SolidColorBrush(Color.FromRgb(27, 40, 56)); // #1B2838
                CameraListPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(61, 68, 80)); // #3D4450
                
                // Título de lista
                TxtCameraListTitle.Foreground = new SolidColorBrush(Color.FromRgb(199, 213, 224)); // #C7D5E0
                
                // Lista de cámaras
                LstCameras.Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)); // #171A21
                
                // Panel PTZ
                PtzPanel.Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)); // #171A21
                
                // Botones de layout inactivos
                BtnLayout2.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                BtnLayout4.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                BtnLayout9.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)); // #2A475E
                
                // Actualizar botón de tema
                BtnTheme.Content = "☀️ Light";
                BtnTheme.Background = new SolidColorBrush(Color.FromRgb(102, 192, 244)); // SteamBlue
                
                Console.WriteLine("✓ Steam Dark Theme activated");
            }
            else
            {
                // Aplicar tema claro (original)
                // Panel superior
                TopPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                
                // Panel de lista de cámaras
                CameraListPanel.Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)); // #ECF0F1
                CameraListPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)); // #BDC3C7
                
                // Título de lista
                TxtCameraListTitle.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                
                // Lista de cámaras
                LstCameras.Background = new SolidColorBrush(Colors.White);
                
                // Panel PTZ
                PtzPanel.Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // #2C3E50
                
                // Botones de layout inactivos
                BtnLayout2.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                BtnLayout4.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                BtnLayout9.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // #34495E
                
                // Actualizar botón de tema
                BtnTheme.Content = "🌙 Dark";
                BtnTheme.Background = new SolidColorBrush(Color.FromRgb(102, 192, 244)); // SteamBlue
                
                Console.WriteLine("✓ Light Theme activated");
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
                Height = 280,
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
                Height = 450,
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
                Background = new SolidColorBrush(Color.FromRgb(39, 174, 96)), 
                Foreground = Brushes.White, 
                BorderThickness = new Thickness(0),
                FontSize = 12,
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
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
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
                Height = 340,
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
                Height = 350,
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

        private bool _isDisposing = false;
        
        protected override void OnClosed(EventArgs e)
        {
            if (_isDisposing)
                return;
                
            _isDisposing = true;
            
            try
            {
                // 1. Detener todos los streams primero
                foreach (var view in _cameraViews)
                {
                    try
                    {
                        view.MediaPlayer?.Stop();
                    }
                    catch { }
                }
                
                // 2. Pequeña pausa
                System.Threading.Thread.Sleep(200);
                
                // 3. Disponer MediaPlayers
                foreach (var view in _cameraViews)
                {
                    try
                    {
                        view.MediaPlayer?.Dispose();
                    }
                    catch { }
                }
                
                // 4. Limpiar lista
                _cameraViews.Clear();
                
                // 5. NO disponer LibVLC explícitamente - dejar que el GC lo maneje
                // Esto evita el AccessViolationException
                _libVLC = null;
                
                // 6. Disponer servicios
                try
                {
                    _discovery?.Dispose();
                }
                catch { }
                
                try
                {
                    _ptzService?.Dispose();
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnClosed: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
