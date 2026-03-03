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
            // Limpiar recursos
            StopAllCameras();
            _libVLC?.Dispose();
        }

        private async void BtnScanCameras_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está escaneando, cancelar
            if (_scanCancellationTokenSource != null)
            {
                _scanCancellationTokenSource.Cancel();
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

        private void OnCameraDiscovered(object? sender, CameraInfo camera)
        {
            Dispatcher.Invoke(() =>
            {
                LstCameras.Items.Add(camera);
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

        private void SetupGridLayout(int gridSize)
        {
            _currentLayout = gridSize;
            
            // Limpiar vistas anteriores
            foreach (var view in _cameraViews)
            {
                view.MediaPlayer?.Stop();
                view.MediaPlayer?.Dispose();
            }
            
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
        }
        
        private void UpdateLayoutButtons()
        {
            BtnLayout1.Background = new SolidColorBrush(_currentLayout == 1 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout2.Background = new SolidColorBrush(_currentLayout == 2 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout4.Background = new SolidColorBrush(_currentLayout == 4 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
            BtnLayout9.Background = new SolidColorBrush(_currentLayout == 9 ? Color.FromRgb(39, 174, 96) : Color.FromRgb(52, 73, 94));
        }
        
        private void StartCamera(CameraView cameraView, CameraInfo camera, StreamQuality? quality = null)
        {
            try
            {
                cameraView.Camera = camera;
                
                // Usar calidad especificada o global
                var streamQuality = quality ?? _globalStreamQuality;
                int channel = (int)streamQuality; // 0 = main (HD), 1 = sub (SD)
                
                string rtspUrl = $"rtsp://admin:@{camera.IpAddress}/live/ch00_{channel}";
                var media = new Media(_libVLC, new Uri(rtspUrl));
                cameraView.CurrentMedia = media;
                cameraView.MediaPlayer.Play(media);
                
                System.Diagnostics.Debug.WriteLine($"Started {camera.Name} with {streamQuality} quality: {rtspUrl}");
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
                var success1 = await _ptzService.MoveAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, 0.3f, 0, 0);
                await Task.Delay(1000);
                await _ptzService.StopAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password);
                
                await Task.Delay(500);
                
                // Test 2: Pequeño movimiento a la izquierda (volver)
                Console.WriteLine("Test 2: Moving left...");
                var success2 = await _ptzService.MoveAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password, -0.3f, 0, 0);
                await Task.Delay(1000);
                await _ptzService.StopAsync(_activePtzCamera.IpAddress, _activePtzCamera.Username, _activePtzCamera.Password);
                
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
        
        private async Task SendPtzCommand(float panSpeed, float tiltSpeed, float zoomSpeed)
        {
            if (_activePtzCamera == null)
            {
                TxtStatus.Text = "No active PTZ camera";
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Sending PTZ command: Pan={panSpeed}, Tilt={tiltSpeed}, Zoom={zoomSpeed}");
            
            var success = await _ptzService.MoveAsync(
                _activePtzCamera.IpAddress, 
                _activePtzCamera.Username, 
                _activePtzCamera.Password, 
                panSpeed, 
                tiltSpeed, 
                zoomSpeed
            );
            
            if (success)
            {
                TxtStatus.Text = $"PTZ: Moving camera {_activePtzCamera.Name}...";
            }
            else
            {
                TxtStatus.Text = $"PTZ command failed - Check camera supports PTZ";
                System.Diagnostics.Debug.WriteLine($"✗ PTZ command failed for {_activePtzCamera.IpAddress}");
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
