using LibVLCSharp.Shared;

namespace V380Viewer.Models
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

        public CameraView(CameraInfo camera, MediaPlayer mediaPlayer)
        {
            Camera = camera;
            MediaPlayer = mediaPlayer;
            IsRecording = false;
        }

        public void Dispose()
        {
            CurrentMedia?.Dispose();
            MediaPlayer?.Stop();
            MediaPlayer?.Dispose();
        }
    }
}
