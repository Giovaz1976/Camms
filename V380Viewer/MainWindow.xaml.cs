using System;
using System.Collections.Generic;
using System.Linq;
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
        private LibVLC? _libVLC;
        private List<CameraView> _cameraViews = new List<CameraView>();
        private int _currentLayout = 1; // 1, 2, 4, or 9
        
        public MainWindow()
        {
            InitializeComponent();
            _discovery = new V380Discovery();
            _discovery.CameraDiscovered += OnCameraDiscovered;
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
            LstCameras.Items.Clear();
            TxtStatus.Text = "Scanning network for V380 cameras...";
            BtnScanCameras.IsEnabled = false;
            
            try
            {
                // Agregar cámara conocida directamente
                var knownCamera = new CameraInfo
                {
                    Name = "V380 Camera (Direct)",
                    IpAddress = "192.168.1.81",
                    Port = 32108,
                    DeviceId = "Direct-Connection"
                };
                LstCameras.Items.Add(knownCamera);
                
                // También intentar discovery por si acaso
                await _discovery.ScanNetworkAsync();
                
                TxtStatus.Text = $"Scan complete. Found {LstCameras.Items.Count} camera(s)";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Scan failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnScanCameras.IsEnabled = true;
            }
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
            VideoGrid.Children.Clear();
            VideoGrid.RowDefinitions.Clear();
            VideoGrid.ColumnDefinitions.Clear();
            _cameraViews.Clear();
            
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
                    
                    var videoView = new VideoView();
                    var mediaPlayer = new VLCMediaPlayer(_libVLC);
                    videoView.MediaPlayer = mediaPlayer;
                    
                    border.Child = videoView;
                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    VideoGrid.Children.Add(border);
                    
                    _cameraViews.Add(new CameraView(new CameraInfo(), mediaPlayer));
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
        
        private void StartCamera(CameraView cameraView, CameraInfo camera)
        {
            try
            {
                cameraView.Camera = camera;
                string rtspUrl = $"rtsp://admin:@{camera.IpAddress}/live/ch00_0";
                var media = new Media(_libVLC, new Uri(rtspUrl));
                cameraView.CurrentMedia = media;
                cameraView.MediaPlayer.Play(media);
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
