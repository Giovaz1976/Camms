using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Resumen legible (un endpoint probado por línea: éxito, SOAP fault, 401, excepción) del
        /// último intento. CameraViewer.exe es WinExe (sin consola adjunta) — Console.WriteLine
        /// solo se ve bajo un depurador adjunto, así que esto es lo único que el usuario puede
        /// revisar en uso normal. Lo llena Log(); el llamador debe limpiarlo con ClearLog() antes
        /// de una nueva secuencia de prueba (p. ej. BtnTestPtz_Click en MainWindow).
        /// </summary>
        public List<string> LastAttemptLog { get; } = new();

        public void ClearLog() => LastAttemptLog.Clear();

        private void Log(string message)
        {
            Console.WriteLine(message);
            LastAttemptLog.Add(message);
        }

        // Recuerda, por IP de cámara, qué URL de endpoint respondió bien la última vez (PTZ y
        // GetProfiles por separado, porque algunas cámaras los separan en puertos distintos). Sin
        // esto, cada comando PTZ —incluido mantener presionada una flecha— vuelve a probar las 9
        // URLs candidatas desde cero, una por una con 5s de timeout cada una: hasta 45s solo para
        // encontrar el endpoint correcto, en CADA pulsación. Confirmado con una cámara inalcanzable
        // de prueba: el botón "Test PTZ" tardó ~165s en reportar el fallo. Con el endpoint ya
        // conocido, los comandos siguientes son prácticamente instantáneos (un solo POST).
        private readonly Dictionary<string, string> _knownPtzEndpoints = new();
        private readonly Dictionary<string, string> _knownProfileEndpoints = new();

        // Cámaras para las que ya se confirmó que ContinuousMove falla en los 8 candidatos.
        // Medido con un servidor de prueba local: cuando ninguno de los candidatos "equivocados"
        // devuelve ECONNREFUSED rápido (pasó en este entorno — Windows/loopback se quedó esperando
        // el timeout completo en vez de rechazar al instante), agotar los 8 candidatos de
        // ContinuousMove puede tardar ~10s (5 candidatos × 2s de timeout). Sin este caché, ESE
        // costo se pagaría de nuevo en cada pulsación de flecha para una cámara que nunca va a
        // soportar ContinuousMove — contraproducente justo para el problema que se quería arreglar
        // (ver sección "Diagnóstico de PTZ" del SKILL). Con el caché, el costo se paga una sola vez
        // por cámara por sesión; las pulsaciones siguientes van directo a RelativeMove.
        private readonly HashSet<string> _continuousMoveUnsupported = new();

        public OnvifPtzService()
        {
            _httpClient = new HttpClient
            {
                // 5s por intento era demasiado para una LAN: una cámara que sí escucha en un puerto
                // responde en milisegundos, así que no hace falta esperar tanto para descartar un
                // puerto equivocado. 2s sigue siendo generoso para red local.
                Timeout = TimeSpan.FromSeconds(2)
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

                // ContinuousMove es el comando correcto para una UI de "mantener presionado para
                // mover, soltar para detener" (como los botones de flecha del panel PTZ): la
                // cámara se mueve a la velocidad indicada hasta que llega un Stop. RelativeMove, en
                // cambio, es un movimiento de una sola vez por una cantidad fija — no tiene sentido
                // "sostenerlo" ni combina bien con un Stop inmediato al soltar el botón (el Stop
                // puede llegar antes de que el movimiento relativo termine de ejecutarse). Se
                // prueba primero porque es lo semánticamente correcto — pero solo si no se sabe ya
                // que esta cámara no lo soporta (ver _continuousMoveUnsupported): agotar los 8
                // candidatos de ContinuousMove en una cámara que nunca lo soporta puede tardar
                // varios segundos, y eso no hay que pagarlo en cada pulsación.
                bool success = false;

                if (!_continuousMoveUnsupported.Contains(cameraIp))
                {
                    var continuousRequest = BuildContinuousVelocityMoveRequest(panSpeed, tiltSpeed, zoomSpeed, token);
                    success = await SendPtzCommandAsync(cameraIp, username, password, continuousRequest);

                    if (!success)
                    {
                        Console.WriteLine($"ContinuousMove not supported by {cameraIp}, won't retry it this session — falling back to RelativeMove");
                        _continuousMoveUnsupported.Add(cameraIp);
                    }
                }

                var soapRequest = string.Empty;
                if (!success)
                {
                    soapRequest = BuildRelativeMoveRequest(panSpeed, tiltSpeed, zoomSpeed, token);
                    success = await SendPtzCommandAsync(cameraIp, username, password, soapRequest);
                }

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

        private string BuildContinuousVelocityMoveRequest(float panSpeed, float tiltSpeed, float zoomSpeed, string profileToken)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:tptz=""http://www.onvif.org/ver20/ptz/wsdl"" xmlns:tt=""http://www.onvif.org/ver10/schema"">
    <s:Body>
        <tptz:ContinuousMove>
            <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
            <tptz:Velocity>
                <tt:PanTilt x=""{panSpeed:F1}"" y=""{tiltSpeed:F1}"" space=""http://www.onvif.org/ver10/tptz/PanTiltSpaces/VelocityGenericSpace""/>
                <tt:Zoom x=""{zoomSpeed:F1}"" space=""http://www.onvif.org/ver10/tptz/ZoomSpaces/VelocityGenericSpace""/>
            </tptz:Velocity>
        </tptz:ContinuousMove>
    </s:Body>
</s:Envelope>";
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
            var endpoints = new List<string>
            {
                $"http://{cameraIp}:8899/onvif/ptz_service",      // V380 usa puerto 8899
                $"http://{cameraIp}:8899/onvif/device_service",
                $"http://{cameraIp}:8899/onvif/PTZ",
                $"http://{cameraIp}:10080/onvif/ptz_service",     // Puerto 10080 para otras cámaras
                $"http://{cameraIp}:10080/onvif/device_service",
                $"http://{cameraIp}/onvif/ptz_service",
                $"http://{cameraIp}/onvif/device_service",
                $"http://{cameraIp}:8889/onvif/ptz_service"
            };

            // Si ya sabemos qué endpoint funcionó la última vez para esta cámara, probarlo primero
            // — evita recorrer la lista completa en cada comando (ver comentario del diccionario).
            if (_knownPtzEndpoints.TryGetValue(cameraIp, out var knownEndpoint) && endpoints.Remove(knownEndpoint))
            {
                endpoints.Insert(0, knownEndpoint);
            }

            foreach (var url in endpoints)
            {
                try
                {
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

                    // El dump crudo de la respuesta solo va a Console (puede ser SOAP XML largo);
                    // el resumen de la línea siguiente sí va al log visible para el usuario.
                    Console.WriteLine($"PTZ Response Status [{url}]: {response.StatusCode}");
                    Console.WriteLine($"PTZ Response: {responseContent[..Math.Min(1000, responseContent.Length)]}");

                    // Verificar si hay errores SOAP dentro de la respuesta
                    bool hasSoapFault = responseContent.Contains("Fault") || responseContent.Contains("faultcode");

                    if (response.IsSuccessStatusCode && !hasSoapFault)
                    {
                        _knownPtzEndpoints[cameraIp] = url;
                        Log($"✓ PTZ command sent successfully to {url}");
                        return true;
                    }
                    else if (hasSoapFault)
                    {
                        // Extraer mensaje de error si existe
                        var faultMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<.*?faultstring.*?>(.*?)</.*?faultstring>");
                        Log(faultMatch.Success
                            ? $"✗ SOAP Fault from {url}: {faultMatch.Groups[1].Value}"
                            : $"✗ SOAP Fault from {url} (sin faultstring)");
                        continue;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Log($"✗ Authentication failed (401) for {url}");
                        continue; // Probar siguiente endpoint
                    }
                    else
                    {
                        Log($"✗ PTZ command failed for {url}: {response.StatusCode}");
                        // Continuar probando otros endpoints
                    }
                }
                catch (Exception ex)
                {
                    Log($"✗ PTZ HTTP error for {url}: {ex.Message}");
                    continue; // Probar siguiente endpoint
                }
            }

            Log("✗ All PTZ endpoints failed");
            return false;
        }

        private string BuildRelativeMoveRequest(float panSpeed, float tiltSpeed, float zoomSpeed, string profileToken)
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
            var endpoints = new List<string>
            {
                $"http://{cameraIp}:8899/onvif/device_service",   // V380
                $"http://{cameraIp}:10080/onvif/device_service",  // Otras cámaras
                $"http://{cameraIp}:80/onvif/device_service",     // Puerto estándar
                $"http://{cameraIp}:8080/onvif/device_service"    // Alternativo
            };

            // Igual que en SendPtzCommandAsync: probar primero el endpoint que ya funcionó antes
            if (_knownProfileEndpoints.TryGetValue(cameraIp, out var knownProfileEndpoint) && endpoints.Remove(knownProfileEndpoint))
            {
                endpoints.Insert(0, knownProfileEndpoint);
            }

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

                    // El dump crudo va solo a Console; el resumen de abajo sí es visible al usuario.
                    Console.WriteLine($"GetProfiles Response [{url}]: {responseContent[..Math.Min(500, responseContent.Length)]}");

                    if (response.IsSuccessStatusCode)
                    {
                        // Buscar ProfileToken en la respuesta
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<.*?:?Profiles.*?token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            _knownProfileEndpoints[cameraIp] = url;
                            Log($"✓ Found ProfileToken: {token} (from {url})");
                            return token;
                        }

                        Log($"⚠ {url} respondió {response.StatusCode} pero no se encontró ProfileToken en el XML (formato no reconocido)");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Log($"✗ Authentication failed (401) for {url}");
                    }
                    else
                    {
                        Log($"✗ GetProfiles failed for {url}: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"✗ GetProfileToken failed for {url}: {ex.Message}");
                    continue; // Probar siguiente endpoint
                }
            }

            Log("✗ Could not get ProfileToken from any endpoint");
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
