using LibVLCSharp.Shared;
using System.Windows.Controls;

namespace CameraViewer.Models
{
    /// <summary>
    /// Representa una vista de cámara individual con su reproductor
    /// </summary>
    public class CameraView
    {
        public CameraInfo Camera { get; set; }
        public MediaPlayer MediaPlayer { get; set; }
        public Media? CurrentMedia { get; set; }
        public bool IsRecording { get; set; }
        public string? RecordingPath { get; set; }
        public bool IsMuted { get; set; }
        public Button? AudioButton { get; set; } // Referencia al botón de audio individual

        public CameraView(CameraInfo camera, MediaPlayer mediaPlayer)
        {
            Camera = camera;
            MediaPlayer = mediaPlayer;
            IsRecording = false;
            IsMuted = true; // Start muted by default
        }

        public void Dispose()
        {
            CurrentMedia?.Dispose();
            MediaPlayer?.Stop();
            MediaPlayer?.Dispose();
        }
    }
}
