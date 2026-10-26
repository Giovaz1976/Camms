namespace CameraViewer.Interfaces.Network
{
    /// <summary>
    /// Builds RTSP/HTTP "Authorization" header values from a server challenge.
    /// Follows Dependency Inversion Principle - callers depend on this abstraction instead of
    /// a concrete Digest/Basic implementation.
    /// </summary>
    public interface IRtspAuthenticator
    {
        /// <summary>
        /// Builds the "Authorization" header value in response to a 401 challenge.
        /// </summary>
        /// <param name="wwwAuthenticate">The "WWW-Authenticate" header value from the 401 response.</param>
        /// <param name="method">The RTSP/HTTP method being authenticated (e.g. "DESCRIBE").</param>
        /// <param name="uri">
        /// The request URI, WITHOUT embedded credentials (no "user:pass@"). Digest's response hash
        /// is computed over this exact string, so it must match the URI actually sent on the wire.
        /// </param>
        /// <param name="username">Username for authentication.</param>
        /// <param name="password">Password for authentication.</param>
        /// <returns>
        /// The value to send in the "Authorization" header (e.g. "Digest username=... response=...",
        /// or "Basic &lt;base64&gt;"), or null if the challenge scheme is not recognized.
        /// </returns>
        string? BuildAuthorizationHeader(string wwwAuthenticate, string method, string uri,
            string username, string password);
    }
}
