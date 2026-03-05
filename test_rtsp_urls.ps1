# Script para detectar URL RTSP correcta
# IP: 192.168.1.76
# Usuario: admin
# Password: Wegata76

$ip = "192.168.1.76"
$username = "admin"
$password = "Wegata76"

Write-Host "========================================"
Write-Host "RTSP URL Detection Test"
Write-Host "========================================"
Write-Host "IP: $ip | User: $username"
Write-Host ""

# URLs RTSP comunes para diferentes marcas
$rtspUrls = @(
    "rtsp://$username`:$password@$ip/live/ch00_0",           # V380 format
    "rtsp://$username`:$password@$ip/live/ch00_1",           # V380 Sub
    "rtsp://$username`:$password@$ip`:554/Streaming/Channels/101",  # Hikvision Main
    "rtsp://$username`:$password@$ip`:554/Streaming/Channels/102",  # Hikvision Sub
    "rtsp://$username`:$password@$ip`:554/cam/realmonitor?channel=1&subtype=0",  # Dahua Main
    "rtsp://$username`:$password@$ip`:554/cam/realmonitor?channel=1&subtype=1",  # Dahua Sub
    "rtsp://$username`:$password@$ip`:554/stream1",         # Generic Main
    "rtsp://$username`:$password@$ip`:554/stream2",         # Generic Sub
    "rtsp://$username`:$password@$ip`:554/h264",            # Generic h264
    "rtsp://$username`:$password@$ip`:554/live",            # Generic live
    "rtsp://$username`:$password@$ip`:554/video",           # Generic video
    "rtsp://$username`:$password@$ip`:554/ch0",             # Generic ch0
    "rtsp://$username`:$password@$ip`:554/ch1",             # Generic ch1
    "rtsp://$username`:$password@$ip`:554/11",              # Some cameras
    "rtsp://$username`:$password@$ip`:554/1",               # Some cameras
    "rtsp://$username`:$password@$ip/onvif1",               # ONVIF
    "rtsp://$username`:$password@$ip/onvif2"                # ONVIF Sub
)

Write-Host "Testing RTSP URLs (this may take a while)..."
Write-Host ""

$workingUrls = @()

foreach ($url in $rtspUrls) {
    Write-Host "Testing: $url" -NoNewline
    
    # Test with ffprobe (if available)
    try {
        $ffprobeTest = & ffprobe -v quiet -print_format json -show_streams -rtsp_transport tcp -stimeout 3000000 $url 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host " -> OK (ffprobe)" -ForegroundColor Green
            $workingUrls += $url
            continue
        }
    } catch {
        # ffprobe not available or failed
    }
    
    # Test with VLC (if available)
    try {
        $vlcPath = "C:\Program Files\VideoLAN\VLC\vlc.exe"
        if (Test-Path $vlcPath) {
            $vlcTest = Start-Process -FilePath $vlcPath -ArgumentList "--intf dummy --run-time=2 --rtsp-tcp $url vlc://quit" -PassThru -WindowStyle Hidden
            Start-Sleep -Seconds 3
            
            if (-not $vlcTest.HasExited) {
                Stop-Process -Id $vlcTest.Id -Force -ErrorAction SilentlyContinue
                Write-Host " -> OK (VLC)" -ForegroundColor Green
                $workingUrls += $url
                continue
            }
        }
    } catch {
        # VLC not available or failed
    }
    
    # Basic TCP test on port 554
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.ReceiveTimeout = 2000
        $tcpClient.SendTimeout = 2000
        $tcpClient.Connect($ip, 554)
        
        if ($tcpClient.Connected) {
            $tcpClient.Close()
            Write-Host " -> Port 554 open (needs manual verification)" -ForegroundColor Yellow
        } else {
            Write-Host " -> Failed" -ForegroundColor Red
        }
    } catch {
        Write-Host " -> Failed" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================"
Write-Host "Results"
Write-Host "========================================"

if ($workingUrls.Count -gt 0) {
    Write-Host "Working RTSP URLs found:" -ForegroundColor Green
    foreach ($url in $workingUrls) {
        Write-Host "  $url" -ForegroundColor Green
    }
} else {
    Write-Host "No working URLs detected automatically." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Try these URLs manually in VLC:" -ForegroundColor Cyan
    Write-Host "  rtsp://admin:Wegata76@192.168.1.76:554/Streaming/Channels/101" -ForegroundColor White
    Write-Host "  rtsp://admin:Wegata76@192.168.1.76:554/cam/realmonitor?channel=1&subtype=0" -ForegroundColor White
    Write-Host "  rtsp://admin:Wegata76@192.168.1.76:554/stream1" -ForegroundColor White
    Write-Host "  rtsp://admin:Wegata76@192.168.1.76:554/h264" -ForegroundColor White
}

Write-Host ""
Write-Host "TIP: You can also get RTSP URL from ONVIF GetStreamUri command" -ForegroundColor Cyan
