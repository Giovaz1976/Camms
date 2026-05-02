using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CameraViewer.Services
{
    /// <summary>
    /// Servicio para controlar PTZ (Pan/Tilt/Zoom) de cámaras ONVIF
    /// </summary>
    public class OnvifPtzService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _profileToken = "profile_1"; // Token por defecto

        public OnvifPtzService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        /// <summary>
        /// Mueve la cámara en dirección especificada
        /// </summary>
        public async Task<bool> MoveAsync(string cameraIp, string username, string password, 
            float panSpeed, float tiltSpeed, float zoomSpeed, string? profileToken = null)
        {
            try
            {
                // Usar el ProfileToken proporcionado o el por defecto
                var token = profileToken ?? _profileToken;
                
                // Intentar primero con RelativeMove
                var soapRequest = BuildContinuousMoveRequest(panSpeed, tiltSpeed, zoomSpeed, token);
                var success = await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
                
                // Si falla, intentar con AbsoluteMove
                if (!success)
                {
                    Console.WriteLine("RelativeMove failed, trying AbsoluteMove...");
                    soapRequest = BuildAbsoluteMoveRequest(panSpeed, tiltSpeed, zoomSpeed, token);
                    success = await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PTZ Move error: {ex.Message}");
                return false;
            }
        }
        
        private string BuildAbsoluteMoveRequest(float pan, float tilt, float zoom, string profileToken)
        {
            // AbsoluteMove con espacios de coordenadas
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:AbsoluteMove>
            <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
            <tptz:Position>
                <tt:PanTilt x=""{pan:F1}"" y=""{tilt:F1}"" space=""http://www.onvif.org/ver10/tptz/PanTiltSpaces/PositionGenericSpace""/>
                <tt:Zoom x=""{zoom:F1}"" space=""http://www.onvif.org/ver10/tptz/ZoomSpaces/PositionGenericSpace""/>
            </tptz:Position>
        </tptz:AbsoluteMove>
    </s:Body>
</s:Envelope>";
        }

        /// <summary>
        /// Detiene el movimiento de la cámara
        /// </summary>
        public async Task<bool> StopAsync(string cameraIp, string username, string password, string? profileToken = null)
        {
            try
            {
                var token = profileToken ?? _profileToken;
                var soapRequest = BuildStopRequest(token);
                return await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PTZ Stop error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mueve a una posición absoluta
        /// </summary>
        public async Task<bool> GotoPresetAsync(string cameraIp, string username, string password, string presetToken, string? profileToken = null)
        {
            try
            {
                var token = profileToken ?? _profileToken;
                var soapRequest = BuildGotoPresetRequest(presetToken, token);
                return await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PTZ Preset error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendPtzCommandAsync(string cameraIp, string username, string password, string soapRequest)
        {
            // Lista de endpoints comunes para PTZ ONVIF
            var endpoints = new[]
            {
                $"http://{cameraIp}:8899/onvif/ptz_service",      // V380 usa puerto 8899
                $"http://{cameraIp}:8899/onvif/device_service",
                $"http://{cameraIp}:8899/onvif/PTZ",
                $"http://{cameraIp}:10080/onvif/ptz_service",     // Puerto 10080 para otras cámaras
                $"http://{cameraIp}:10080/onvif/device_service",
                $"http://{cameraIp}/onvif/ptz_service",
                $"http://{cameraIp}/onvif/device_service",
                $"http://{cameraIp}:8889/onvif/ptz_service",
                $"http://{cameraIp}:8889/onvif/ptz_service"
            };

            foreach (var url in endpoints)
            {
                try
                {
                    Console.WriteLine($"Trying PTZ endpoint: {url}");
                    
                    var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");
                    
                    // Limpiar headers anteriores
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                    
                    // Agregar autenticación básica si se proporciona
                    if (!string.IsNullOrEmpty(username))
                    {
                        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                    }

                    var response = await _httpClient.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    Console.WriteLine($"PTZ Response Status: {response.StatusCode}");
                    Console.WriteLine($"PTZ Response: {responseContent[..Math.Min(1000, responseContent.Length)]}");
                    
                    // Verificar si hay errores SOAP dentro de la respuesta
                    bool hasSoapFault = responseContent.Contains("Fault") || responseContent.Contains("faultcode");
                    
                    if (response.IsSuccessStatusCode && !hasSoapFault)
                    {
                        Console.WriteLine($"✓ PTZ command sent successfully to {url}");
                        return true;
                    }
                    else if (hasSoapFault)
                    {
                        Console.WriteLine($"✗ SOAP Fault detected in response");
                        // Extraer mensaje de error si existe
                        var faultMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<.*?faultstring.*?>(.*?)</.*?faultstring>");
                        if (faultMatch.Success)
                        {
                            Console.WriteLine($"   Error: {faultMatch.Groups[1].Value}");
                        }
                        continue;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine($"✗ Authentication failed for {url}");
                        continue; // Probar siguiente endpoint
                    }
                    else
                    {
                        Console.WriteLine($"✗ PTZ command failed: {response.StatusCode}");
                        // Continuar probando otros endpoints
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ PTZ HTTP error for {url}: {ex.Message}");
                    continue; // Probar siguiente endpoint
                }
            }
            
            Console.WriteLine("✗ All PTZ endpoints failed");
            return false;
        }

        private string BuildContinuousMoveRequest(float panSpeed, float tiltSpeed, float zoomSpeed, string profileToken)
        {
            // RelativeMove con espacios de coordenadas según ODM
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:RelativeMove>
            <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
            <tptz:Translation>
                <tt:PanTilt x=""{panSpeed:F1}"" y=""{tiltSpeed:F1}"" space=""http://www.onvif.org/ver10/tptz/PanTiltSpaces/TranslationGenericSpace""/>
                <tt:Zoom x=""{zoomSpeed:F1}"" space=""http://www.onvif.org/ver10/tptz/ZoomSpaces/TranslationGenericSpace""/>
            </tptz:Translation>
        </tptz:RelativeMove>
    </s:Body>
</s:Envelope>";
        }

        private string BuildStopRequest(string profileToken)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"">
    <soap:Body>
        <tptz:Stop>
            <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
            <tptz:PanTilt>true</tptz:PanTilt>
            <tptz:Zoom>true</tptz:Zoom>
        </tptz:Stop>
    </soap:Body>
</soap:Envelope>";
        }

        private string BuildGotoPresetRequest(string presetToken, string profileToken)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"">
    <soap:Body>
        <tptz:GotoPreset>
            <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
            <tptz:PresetToken>{presetToken}</tptz:PresetToken>
        </tptz:GotoPreset>
    </soap:Body>
</soap:Envelope>";
        }
        
        /// <summary>
        /// Obtiene el ProfileToken de la cámara (necesario para PTZ)
        /// </summary>
        public async Task<string?> GetProfileTokenAsync(string cameraIp, string username, string password)
        {
            var getProfilesRequest = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"">
    <soap:Body>
        <trt:GetProfiles/>
    </soap:Body>
</soap:Envelope>";

            // Probar múltiples puertos ONVIF comunes
            var endpoints = new[]
            {
                $"http://{cameraIp}:8899/onvif/device_service",   // V380
                $"http://{cameraIp}:10080/onvif/device_service",  // Otras cámaras
                $"http://{cameraIp}:80/onvif/device_service",     // Puerto estándar
                $"http://{cameraIp}:8080/onvif/device_service"    // Alternativo
            };

            foreach (var url in endpoints)
            {
                try
                {
                    var content = new StringContent(getProfilesRequest, Encoding.UTF8, "application/soap+xml");
                    
                    // Limpiar headers anteriores
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                    
                    if (!string.IsNullOrEmpty(username))
                    {
                        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                    }

                    var response = await _httpClient.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"GetProfiles Response: {responseContent[..Math.Min(500, responseContent.Length)]}");
                        
                        // Buscar ProfileToken en la respuesta
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<.*?:?Profiles.*?token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            Console.WriteLine($"✓ Found ProfileToken: {token} (from {url})");
                            return token;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetProfileToken failed for {url}: {ex.Message}");
                    continue; // Probar siguiente endpoint
                }
            }
            
            Console.WriteLine("✗ Could not get ProfileToken from any endpoint");
            return null;
        }
        
        /// <summary>
        /// Obtiene la URL RTSP desde ONVIF GetStreamUri
        /// </summary>
        public async Task<string?> GetRtspUrlAsync(string cameraIp, string username, string password, string profileToken)
        {
            var getStreamUriRequest = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"" 
            xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <trt:GetStreamUri>
            <trt:StreamSetup>
                <tt:Stream>RTP-Unicast</tt:Stream>
                <tt:Transport>
                    <tt:Protocol>RTSP</tt:Protocol>
                </tt:Transport>
            </trt:StreamSetup>
            <trt:ProfileToken>{profileToken}</trt:ProfileToken>
        </trt:GetStreamUri>
    </s:Body>
</s:Envelope>";

            // Probar múltiples puertos ONVIF comunes
            var endpoints = new[]
            {
                $"http://{cameraIp}:10080/onvif/device_service",  // Puerto 10080
                $"http://{cameraIp}:8899/onvif/device_service",   // V380
                $"http://{cameraIp}:80/onvif/device_service",     // Puerto estándar
                $"http://{cameraIp}:8080/onvif/device_service"    // Alternativo
            };

            foreach (var url in endpoints)
            {
                try
                {
                    var content = new StringContent(getStreamUriRequest, Encoding.UTF8, "application/soap+xml");
                    
                    // Limpiar headers anteriores
                    _httpClient.DefaultRequestHeaders.Authorization = null;
                    
                    if (!string.IsNullOrEmpty(username))
                    {
                        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                    }

                    var response = await _httpClient.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        // Buscar URL RTSP en la respuesta
                        var uriMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<tt:Uri>([^<]+)</tt:Uri>");
                        if (!uriMatch.Success)
                        {
                            uriMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<Uri>([^<]+)</Uri>");
                        }
                        
                        if (uriMatch.Success)
                        {
                            var rtspUrl = uriMatch.Groups[1].Value;
                            
                            // Agregar credenciales si no están en la URL
                            if (!rtspUrl.Contains("@"))
                            {
                                if (!string.IsNullOrEmpty(username))
                                {
                                    // Agregar credenciales (password puede estar vacío)
                                    var credentials = string.IsNullOrEmpty(password) 
                                        ? $"{username}:@" 
                                        : $"{username}:{password}@";
                                    rtspUrl = rtspUrl.Replace("rtsp://", $"rtsp://{credentials}");
                                }
                                else
                                {
                                    Console.WriteLine("⚠ Warning: No credentials provided for RTSP URL");
                                }
                            }
                            
                            Console.WriteLine($"✓ RTSP URL obtained: {rtspUrl} (from {url})");
                            return rtspUrl;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetRtspUrl failed for {url}: {ex.Message}");
                    continue; // Probar siguiente endpoint
                }
            }
            
            Console.WriteLine("✗ Could not get RTSP URL from any endpoint");
            return null;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
