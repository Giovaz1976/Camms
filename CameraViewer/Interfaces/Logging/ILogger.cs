using System;

namespace CameraViewer.Interfaces.Logging
{
    /// <summary>
    /// Interface for logging operations.
    /// Follows Dependency Inversion Principle - high-level modules depend on this abstraction.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogDebug(string message);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">Optional exception details.</param>
        void LogError(string message, Exception? exception = null);
    }
}
