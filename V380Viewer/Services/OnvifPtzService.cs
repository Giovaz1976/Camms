using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace V380Viewer.Services
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
            float panSpeed, float tiltSpeed, float zoomSpeed)
        {
            try
            {
                // Intentar primero con RelativeMove
                var soapRequest = BuildContinuousMoveRequest(panSpeed, tiltSpeed, zoomSpeed);
                var success = await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
                
                // Si falla, intentar con AbsoluteMove
                if (!success)
                {
                    Console.WriteLine("RelativeMove failed, trying AbsoluteMove...");
                    soapRequest = BuildAbsoluteMoveRequest(panSpeed, tiltSpeed, zoomSpeed);
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
        
        private string BuildAbsoluteMoveRequest(float pan, float tilt, float zoom)
        {
            // AbsoluteMove - el más simple y compatible
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:AbsoluteMove>
            <tptz:ProfileToken>{_profileToken}</tptz:ProfileToken>
            <tptz:Position>
                <tt:PanTilt x=""{pan}"" y=""{tilt}""/>
                <tt:Zoom x=""{zoom}""/>
            </tptz:Position>
        </tptz:AbsoluteMove>
    </s:Body>
</s:Envelope>";
        }

        /// <summary>
        /// Detiene el movimiento de la cámara
        /// </summary>
        public async Task<bool> StopAsync(string cameraIp, string username, string password)
        {
            try
            {
                var soapRequest = BuildStopRequest();
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
        public async Task<bool> GotoPresetAsync(string cameraIp, string username, string password, string presetToken)
        {
            try
            {
                var soapRequest = BuildGotoPresetRequest(presetToken);
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
                    Console.WriteLine($"PTZ Response: {responseContent[..Math.Min(500, responseContent.Length)]}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✓ PTZ command sent successfully to {url}");
                        return true;
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

        private string BuildContinuousMoveRequest(float panSpeed, float tiltSpeed, float zoomSpeed)
        {
            // Intentar primero con RelativeMove (más compatible)
            if (Math.Abs(panSpeed) > 0.01f || Math.Abs(tiltSpeed) > 0.01f)
            {
                return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:RelativeMove>
            <tptz:ProfileToken>{_profileToken}</tptz:ProfileToken>
            <tptz:Translation>
                <tt:PanTilt x=""{panSpeed}"" y=""{tiltSpeed}""/>
                <tt:Zoom x=""{zoomSpeed}""/>
            </tptz:Translation>
        </tptz:RelativeMove>
    </s:Body>
</s:Envelope>";
            }
            else
            {
                // Si es solo zoom, usar comando de zoom
                return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:RelativeMove>
            <tptz:ProfileToken>{_profileToken}</tptz:ProfileToken>
            <tptz:Translation>
                <tt:PanTilt x=""0"" y=""0""/>
                <tt:Zoom x=""{zoomSpeed}""/>
            </tptz:Translation>
        </tptz:RelativeMove>
    </s:Body>
</s:Envelope>";
            }
        }

        private string BuildStopRequest()
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"">
    <soap:Body>
        <tptz:Stop>
            <tptz:ProfileToken>{_profileToken}</tptz:ProfileToken>
            <tptz:PanTilt>true</tptz:PanTilt>
            <tptz:Zoom>true</tptz:Zoom>
        </tptz:Stop>
    </soap:Body>
</soap:Envelope>";
        }

        private string BuildGotoPresetRequest(string presetToken)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"">
    <soap:Body>
        <tptz:GotoPreset>
            <tptz:ProfileToken>{_profileToken}</tptz:ProfileToken>
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
            try
            {
                var getProfilesRequest = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" 
               xmlns:trt=""http://www.onvif.org/ver10/media/wsdl"">
    <soap:Body>
        <trt:GetProfiles/>
    </soap:Body>
</soap:Envelope>";

                var url = $"http://{cameraIp}:8899/onvif/device_service";  // V380 usa puerto 8899
                var content = new StringContent(getProfilesRequest, Encoding.UTF8, "application/soap+xml");
                
                if (!string.IsNullOrEmpty(username))
                {
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                }

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"GetProfiles Response: {responseContent[..Math.Min(500, responseContent.Length)]}");
                
                // Buscar ProfileToken en la respuesta
                var tokenMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<.*?:?Profiles.*?token=""([^""]+)""");
                if (tokenMatch.Success)
                {
                    var token = tokenMatch.Groups[1].Value;
                    Console.WriteLine($"✓ Found ProfileToken: {token}");
                    return token;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting ProfileToken: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
