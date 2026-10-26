using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CameraViewer.Configuration;
using CameraViewer.Implementation.Audio;
using CameraViewer.Interfaces.Network;
using CameraViewer.Interfaces.Parsing;
using CameraViewer.Models;

namespace CameraViewer.Services
{
    /// <summary>
    /// Orchestrates ONVIF RTSP backchannel (two-way audio / push-to-talk) for cameras: probing
    /// whether a camera supports it today, and (once a talk UI lands) opening/closing a talk
    /// session. Registered as a singleton so the per-IP capability cache survives for the whole
    /// app session - re-probing on every click would repeat several seconds of timeouts per
    /// attempt, exactly the mistake that made PTZ "feel broken" before endpoint caching was
    /// added there (see OnvifPtzService._knownPtzEndpoints).
    /// </summary>
    public class TwoWayAudioService : IDisposable
    {
        private readonly IRtspAuthenticator _authenticator;
        private readonly ISdpParser _sdpParser;
        private readonly AudioSettings _settings;
        private readonly TimeSpan _timeout;

        private readonly Dictionary<string, BackchannelCapability> _capabilities = new();
        private bool _disposed;

        /// <summary>
        /// Human-readable log of the last ProbeAsync call (one line per endpoint/port tried).
        /// CameraViewer.exe is WinExe (no attached console), so this - together with the
        /// MessageBox the caller builds from it - is the only way the user can see what actually
        /// happened. Mirrors OnvifPtzService.LastAttemptLog.
        /// </summary>
        public List<string> LastAttemptLog { get; } = new();

        public void ClearLog() => LastAttemptLog.Clear();

        private void Log(string message)
        {
            Console.WriteLine(message);
            LastAttemptLog.Add(message);
        }

        public TwoWayAudioService(IRtspAuthenticator authenticator, ISdpParser sdpParser, AudioSettings settings)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _sdpParser = sdpParser ?? throw new ArgumentNullException(nameof(sdpParser));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
        }

        /// <summary>
        /// Probes whether a camera supports ONVIF backchannel. Cached per IP - safe to call
        /// again any time, it only hits the network once per camera per app session.
        /// </summary>
        public async Task<BackchannelCapability> ProbeAsync(CameraInfo camera)
        {
            if (_capabilities.TryGetValue(camera.IpAddress, out var cached))
            {
                Log($"Using cached result for {camera.IpAddress}: Supported={cached.Supported}");
                return cached;
            }

            var (username, password) = ResolveCredentials(camera);

            foreach (var (host, port, path) in BuildCandidateUrls(camera))
            {
                var uri = $"rtsp://{host}:{port}{path}";
                Log($"Trying {uri} ...");

                using var session = new RtspBackchannelSession(_authenticator, _timeout);

                try
                {
                    await session.ConnectAsync(host, port);
                    await session.SendOptionsAsync(uri, username, password);

                    var describe = await session.SendDescribeAsync(uri, username, password);

                    if (describe.StatusCode == 401)
                    {
                        Log($"✗ {uri}: 401 Unauthorized (check credentials)");
                        continue;
                    }

                    if (!describe.IsSuccess)
                    {
                        Log($"✗ {uri}: DESCRIBE failed ({describe.StatusCode} {describe.StatusText})");
                        continue;
                    }

                    var capability = _sdpParser.ParseBackchannelCapability(describe.Body, uri);

                    if (capability.Supported)
                    {
                        Log($"✓ {uri}: backchannel supported (codec={capability.Codec}, PT={capability.PayloadType})");
                        _capabilities[camera.IpAddress] = capability;
                        return capability;
                    }

                    Log($"✗ {uri}: {capability.Reason}");
                }
                catch (OperationCanceledException)
                {
                    Log($"✗ {uri}: timeout");
                }
                catch (Exception ex)
                {
                    Log($"✗ {uri}: {ex.Message}");
                }
            }

            var notSupported = new BackchannelCapability
            {
                Supported = false,
                Reason = "No endpoint advertised ONVIF backchannel - see log for details"
            };
            _capabilities[camera.IpAddress] = notSupported;
            return notSupported;
        }

        private static (string Username, string Password) ResolveCredentials(CameraInfo camera)
        {
            // Same RTSP-with-ONVIF-fallback rule StartCamera already uses (MainWindow.xaml.cs).
            var username = string.IsNullOrEmpty(camera.RtspUsername) ? camera.Username : camera.RtspUsername;
            var password = string.IsNullOrEmpty(camera.RtspUsername) && string.IsNullOrEmpty(camera.RtspPassword)
                ? camera.Password
                : camera.RtspPassword;
            return (username, password);
        }

        private List<(string Host, int Port, string Path)> BuildCandidateUrls(CameraInfo camera)
        {
            var host = camera.IpAddress;
            var candidates = new List<(string Host, int Port, string Path)>();

            if (camera.UseCustomRtspUrl && !string.IsNullOrEmpty(camera.CustomRtspUrl) &&
                Uri.TryCreate(StripCredentials(camera.CustomRtspUrl), UriKind.Absolute, out var customUri))
            {
                candidates.Add((host, customUri.Port > 0 ? customUri.Port : _settings.RtspPort, customUri.PathAndQuery));
            }

            foreach (var port in new[] { _settings.RtspPort, 8554, 10554 })
            {
                if (!candidates.Exists(c => c.Port == port))
                    candidates.Add((host, port, "/live/ch00_0"));
            }

            return candidates;
        }

        /// <summary>
        /// Removes "user:pass@" from a URL if present. ONVIF's GetStreamUri embeds credentials
        /// in CustomRtspUrl (see OnvifPtzService.GetRtspUrlAsync) but Digest's response hash must
        /// be computed over the plain request URI - embedding credentials there would make every
        /// Digest response wrong and the camera would 401 forever.
        /// </summary>
        private static string StripCredentials(string url)
        {
            var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
            var atIndex = url.IndexOf('@');
            if (atIndex < 0 || schemeEnd < 0 || atIndex < schemeEnd)
                return url;

            return url[..(schemeEnd + 3)] + url[(atIndex + 1)..];
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
        }
    }
}
