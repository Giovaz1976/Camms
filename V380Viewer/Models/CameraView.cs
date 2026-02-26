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

        public CameraView(CameraInfo camera, MediaPlayer mediaPlayer)
        {
            Camera = camera;
            MediaPlayer = mediaPlayer;
        }

        public void Dispose()
        {
            CurrentMedia?.Dispose();
            MediaPlayer?.Stop();
            MediaPlayer?.Dispose();
        }
    }
}
