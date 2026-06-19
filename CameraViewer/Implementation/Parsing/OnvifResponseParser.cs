using System;
using System.Linq;
using System.Xml.Linq;
using CameraViewer.Interfaces.Parsing;
using CameraViewer.Models;

namespace CameraViewer.Implementation.Parsing
{
    /// <summary>
    /// Parser for ONVIF SOAP responses.
    /// Extracts camera information from WS-Discovery ProbeMatch responses.
    /// </summary>
    public class OnvifResponseParser : IOnvifResponseParser
    {
        public CameraInfo? ParseProbeMatch(string response, string ipAddress)
        {
            try
            {
                var doc = XDocument.Parse(response);
                XNamespace s = "http://www.w3.org/2003/05/soap-envelope";
                XNamespace d = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
                XNamespace a = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

                var probeMatch = doc.Descendants(d + "ProbeMatch").FirstOrDefault();
                if (probeMatch == null)
                    return null;

                var xAddrs = probeMatch.Element(d + "XAddrs")?.Value;
                if (string.IsNullOrEmpty(xAddrs))
                    return null;

                // Extraer IP de la URL ONVIF
                var onvifUrl = xAddrs.Split(' ').FirstOrDefault();
                if (string.IsNullOrEmpty(onvifUrl))
                    return null;

                var uri = new Uri(onvifUrl);
                var cameraIp = uri.Host;

                // Obtener información adicional si está disponible
                var scopes = probeMatch.Element(d + "Scopes")?.Value ?? "";
                var name = ExtractScopeName(scopes) ?? $"ONVIF Camera ({cameraIp})";

                return new CameraInfo
                {
                    Name = name,
                    IpAddress = cameraIp,
                    Port = 554, // Puerto RTSP estándar
                    DeviceId = $"ONVIF-{cameraIp}",
                    LastSeen = DateTime.Now
                };
            }
            catch (Exception)
            {
                // Failed to parse - return null
                return null;
            }
        }

        public string? ExtractScopeName(string scopes)
        {
            try
            {
                var scopeList = scopes.Split(' ');
                foreach (var scope in scopeList)
                {
                    if (scope.Contains("name/"))
                    {
                        var parts = scope.Split('/');
                        return Uri.UnescapeDataString(parts.Last());
                    }
                }
            }
            catch
            {
                // Failed to extract name
            }
            
            return null;
        }
    }
}
