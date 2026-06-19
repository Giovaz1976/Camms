using CameraViewer.Models;

namespace CameraViewer.Interfaces.Parsing
{
    /// <summary>
    /// Interface for parsing ONVIF SOAP responses.
    /// Follows Single Responsibility Principle - only responsible for response parsing.
    /// </summary>
    public interface IOnvifResponseParser
    {
        /// <summary>
        /// Parses a ProbeMatch response from WS-Discovery.
        /// </summary>
        /// <param name="response">SOAP XML response as string.</param>
        /// <param name="ipAddress">IP address of the responding device.</param>
        /// <returns>CameraInfo object or null if parsing fails.</returns>
        CameraInfo? ParseProbeMatch(string response, string ipAddress);

        /// <summary>
        /// Extracts the camera name from ONVIF scopes.
        /// </summary>
        /// <param name="scopes">Scopes string from ONVIF response.</param>
        /// <returns>Camera name or null if not found.</returns>
        string? ExtractScopeName(string scopes);
    }
}
