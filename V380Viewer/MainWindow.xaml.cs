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
        
        public MainWindow()
        {
            InitializeComponent();
            _discovery = new V380Discovery();
            _discovery.CameraDiscovered += OnCameraDiscovered;
            _onvifDiscovery = new OnvifDiscovery();
            _onvifDiscovery.CameraDiscovered += OnCameraDiscovered;
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

        protected override void OnClosed(EventArgs e)
        {
            _discovery.Dispose();
            StopAllCameras();
            foreach (var view in _cameraViews)
                view.MediaPlayer?.Dispose();
            _libVLC?.Dispose();
            base.OnClosed(e);
        }
    }
}
