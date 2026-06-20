# Script para verificar credenciales y URLs RTSP de cámaras
# Prueba múltiples combinaciones de URLs y credenciales

param(
    [string]$CameraIP = "192.168.1.81",
    [string]$Username = "admin",
    [string]$Password = ""  # Dejar vacío para probar sin contraseña primero
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Camera Credentials & URL Tester" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Camera IP: $CameraIP" -ForegroundColor Yellow
Write-Host "Username: $Username" -ForegroundColor Yellow
Write-Host "Password: $(if ($Password) { '*' * $Password.Length } else { '(empty)' })" -ForegroundColor Yellow
Write-Host ""

# 1. Test basic connectivity
Write-Host "[1/4] Testing network connectivity..." -ForegroundColor Green
$pingResult = Test-Connection -ComputerName $CameraIP -Count 1 -Quiet
if ($pingResult) {
    Write-Host "  ✓ Camera is reachable" -ForegroundColor Green
} else {
    Write-Host "  ✗ Camera is NOT reachable" -ForegroundColor Red
    Write-Host "  Please check if camera is powered on and on the same network" -ForegroundColor Yellow
    exit 1
}

# 2. Test RTSP port
Write-Host ""
Write-Host "[2/4] Testing RTSP port 554..." -ForegroundColor Green
$portTest = Test-NetConnection -ComputerName $CameraIP -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
if ($portTest) {
    Write-Host "  ✓ Port 554 is OPEN" -ForegroundColor Green
} else {
    Write-Host "  ✗ Port 554 is CLOSED" -ForegroundColor Red
    Write-Host "  Camera may not support RTSP or is using a different port" -ForegroundColor Yellow
}

# 3. Test common RTSP URLs
Write-Host ""
Write-Host "[3/4] Testing common RTSP URLs..." -ForegroundColor Green
Write-Host "  (This will try to connect with VLC for 3 seconds each)" -ForegroundColor Gray
Write-Host ""

# Build credential string
$credString = if ($Password) { "${Username}:${Password}" } else { $Username }

# Common RTSP URL patterns
$rtspUrls = @(
    "rtsp://${credString}@${CameraIP}:554/live/ch00_0",
    "rtsp://${credString}@${CameraIP}:554/stream1",
    "rtsp://${credString}@${CameraIP}:554/stream2",
    "rtsp://${credString}@${CameraIP}:554/onvif1",
    "rtsp://${credString}@${CameraIP}:554/onvif2",
    "rtsp://${credString}@${CameraIP}:554/Streaming/Channels/101",
    "rtsp://${credString}@${CameraIP}:554/Streaming/Channels/102",
    "rtsp://${credString}@${CameraIP}:554/cam/realmonitor?channel=1&subtype=0",
    "rtsp://${credString}@${CameraIP}:554/cam/realmonitor?channel=1&subtype=1",
    "rtsp://${credString}@${CameraIP}:554/h264",
    "rtsp://${credString}@${CameraIP}:554/h264_stream",
    "rtsp://${credString}@${CameraIP}:554/video1",
    "rtsp://${credString}@${CameraIP}:554/mpeg4",
    "rtsp://${credString}@${CameraIP}:554/1",
    "rtsp://${credString}@${CameraIP}:554/11"
)

$workingUrls = @()

foreach ($url in $rtspUrls) {
    # Display URL (hide password)
    $displayUrl = $url -replace ":${Password}@", ":****@"
    Write-Host "  Testing: $displayUrl" -NoNewline
    
    # Try to connect with ffprobe (faster than VLC)
    try {
        $ffprobeResult = & ffprobe -v quiet -print_format json -show_streams -rtsp_transport tcp -timeout 3000000 $url 2>&1
        
        if ($LASTEXITCODE -eq 0 -and $ffprobeResult) {
            Write-Host " ✓ WORKS!" -ForegroundColor Green
            $workingUrls += $url
        } else {
            Write-Host " ✗ Failed" -ForegroundColor Red
        }
    } catch {
        Write-Host " ✗ Failed" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 500
}

# 4. Summary
Write-Host ""
Write-Host "[4/4] Summary" -ForegroundColor Green
Write-Host ""

if ($workingUrls.Count -gt 0) {
    Write-Host "✓ Found $($workingUrls.Count) working URL(s):" -ForegroundColor Green
    Write-Host ""
    foreach ($url in $workingUrls) {
        $displayUrl = $url -replace ":${Password}@", ":****@"
        Write-Host "  • $displayUrl" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "Recommended URL for CameraViewer:" -ForegroundColor Yellow
    $recommendedUrl = $workingUrls[0] -replace ":${Password}@", ":****@"
    Write-Host "  $recommendedUrl" -ForegroundColor White
    Write-Host ""
    
    # Save to file
    $workingUrls[0] | Out-File -FilePath "working_rtsp_url.txt" -Encoding UTF8
    Write-Host "✓ URL saved to: working_rtsp_url.txt" -ForegroundColor Green
    
} else {
    Write-Host "✗ No working URLs found" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting suggestions:" -ForegroundColor Yellow
    Write-Host "  1. Verify camera credentials (username/password)" -ForegroundColor Gray
    Write-Host "  2. Check camera's web interface for RTSP settings" -ForegroundColor Gray
    Write-Host "  3. Try accessing camera via web browser: http://$CameraIP" -ForegroundColor Gray
    Write-Host "  4. Check camera manual for correct RTSP URL format" -ForegroundColor Gray
    Write-Host "  5. Try different passwords (empty, 'admin', '888888', '12345')" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
