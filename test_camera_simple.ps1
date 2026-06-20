# Script simple para verificar credenciales de cámara
# No requiere herramientas externas

param(
    [string]$CameraIP = "192.168.1.81",
    [string]$Username = "admin",
    [string]$Password = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Simple Camera Tester" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Test connectivity
Write-Host "[1/3] Testing connectivity to $CameraIP..." -ForegroundColor Green
$ping = Test-Connection -ComputerName $CameraIP -Count 1 -Quiet
if ($ping) {
    Write-Host "  ✓ Camera is online" -ForegroundColor Green
} else {
    Write-Host "  ✗ Camera is offline or unreachable" -ForegroundColor Red
    exit 1
}

# 2. Test RTSP port
Write-Host ""
Write-Host "[2/3] Testing RTSP port 554..." -ForegroundColor Green
$port = Test-NetConnection -ComputerName $CameraIP -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
if ($port) {
    Write-Host "  ✓ Port 554 is open" -ForegroundColor Green
} else {
    Write-Host "  ✗ Port 554 is closed" -ForegroundColor Red
}

# 3. Test HTTP access (web interface)
Write-Host ""
Write-Host "[3/3] Testing HTTP access..." -ForegroundColor Green

$httpPorts = @(80, 8080, 10080)
$openHttpPort = $null

foreach ($httpPort in $httpPorts) {
    $httpTest = Test-NetConnection -ComputerName $CameraIP -Port $httpPort -WarningAction SilentlyContinue -InformationLevel Quiet
    if ($httpTest) {
        Write-Host "  ✓ Port $httpPort is open" -ForegroundColor Green
        $openHttpPort = $httpPort
        break
    }
}

if ($openHttpPort) {
    Write-Host ""
    Write-Host "Camera web interface may be available at:" -ForegroundColor Yellow
    Write-Host "  http://${CameraIP}:${openHttpPort}" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Open this URL in your browser to:" -ForegroundColor Gray
    Write-Host "  • Verify credentials" -ForegroundColor Gray
    Write-Host "  • Find the correct RTSP URL" -ForegroundColor Gray
    Write-Host "  • Check camera settings" -ForegroundColor Gray
}

# 4. Generate test URLs
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Suggested RTSP URLs to try:" -ForegroundColor Yellow
Write-Host ""

$credString = if ($Password) { "${Username}:${Password}" } else { "${Username}:" }

$suggestedUrls = @(
    "rtsp://${credString}@${CameraIP}:554/live/ch00_0",
    "rtsp://${credString}@${CameraIP}:554/stream1",
    "rtsp://${credString}@${CameraIP}:554/onvif1",
    "rtsp://${credString}@${CameraIP}:554/Streaming/Channels/101"
)

$counter = 1
foreach ($url in $suggestedUrls) {
    $displayUrl = if ($Password) { $url -replace ":${Password}@", ":****@" } else { $url }
    Write-Host "  $counter. $displayUrl" -ForegroundColor Cyan
    $counter++
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Try these URLs in VLC Media Player:" -ForegroundColor Gray
Write-Host "   Media → Open Network Stream → Paste URL" -ForegroundColor Gray
Write-Host ""
Write-Host "2. If none work, access the web interface to find the correct URL" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Common passwords to try:" -ForegroundColor Gray
Write-Host "   • (empty)" -ForegroundColor Gray
Write-Host "   • admin" -ForegroundColor Gray
Write-Host "   • 888888" -ForegroundColor Gray
Write-Host "   • 12345" -ForegroundColor Gray
Write-Host "   • Wegata76 (from your previous test)" -ForegroundColor Gray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Save URLs to file
$suggestedUrls | Out-File -FilePath "suggested_rtsp_urls.txt" -Encoding UTF8
Write-Host "✓ URLs saved to: suggested_rtsp_urls.txt" -ForegroundColor Green
