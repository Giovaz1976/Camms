using System;
using CameraViewer.Interfaces.Logging;

namespace CameraViewer.Implementation.Logging
{
    /// <summary>
    /// No-op logger implementation for testing or when logging is disabled.
    /// Implements Null Object Pattern.
    /// </summary>
    public class NullLogger : ILogger
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
    }
}
