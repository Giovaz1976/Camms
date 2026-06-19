using System;
using CameraViewer.Interfaces.Logging;

namespace CameraViewer.Implementation.Logging
{
    /// <summary>
    /// Logger implementation that writes to System.Diagnostics.Debug.
    /// Useful for development and debugging.
    /// </summary>
    public class DebugLogger : ILogger
    {
        private readonly string _prefix;

        /// <summary>
        /// Creates a new DebugLogger with optional prefix.
        /// </summary>
        /// <param name="prefix">Prefix for all log messages (e.g., "[ONVIF]").</param>
        public DebugLogger(string prefix = "")
        {
            _prefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix} ";
        }

        public void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{_prefix}{message}");
        }

        public void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{_prefix}INFO: {message}");
        }

        public void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{_prefix}WARNING: {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            System.Diagnostics.Debug.WriteLine($"{_prefix}ERROR: {message}");
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"{_prefix}Exception: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"{_prefix}Stack trace: {exception.StackTrace}");
            }
        }
    }
}
