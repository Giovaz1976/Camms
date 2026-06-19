namespace CameraViewer.Interfaces.Parsing
{
    /// <summary>
    /// Interface for building ONVIF SOAP messages.
    /// Follows Single Responsibility Principle - only responsible for message construction.
    /// </summary>
    public interface IOnvifMessageBuilder
    {
        /// <summary>
        /// Builds a WS-Discovery Probe message for ONVIF camera discovery.
        /// </summary>
        /// <returns>SOAP XML message as string.</returns>
        string BuildProbeMessage();

        /// <summary>
        /// Builds a GetDeviceInformation SOAP message.
        /// </summary>
        /// <returns>SOAP XML message as string.</returns>
        string BuildGetDeviceInformationMessage();
    }
}
