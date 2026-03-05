# Script para obtener URL RTSP desde ONVIF GetStreamUri
# IP: 192.168.1.76 Puerto: 10080 Usuario: admin Password: Wegata76

$ip = "192.168.1.76"
$port = 10080
$username = "admin"
$password = "Wegata76"

Write-Host "========================================"
Write-Host "Get RTSP URL from ONVIF"
Write-Host "========================================"
Write-Host "IP: $ip | Port: $port"
Write-Host ""

$authBytes = [System.Text.Encoding]::ASCII.GetBytes("$username`:$password")
$authHeader = [System.Convert]::ToBase64String($authBytes)

# 1. Get ProfileToken
Write-Host "[1/2] Getting ProfileToken..."

$getProfilesRequest = '<?xml version="1.0" encoding="UTF-8"?><s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl"><s:Body><trt:GetProfiles/></s:Body></s:Envelope>'

$mediaUrl = "http://$ip`:$port/onvif/device_service"
$profileToken = $null

try {
    $response = Invoke-WebRequest -Uri $mediaUrl -Method POST -Body $getProfilesRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 5 -UseBasicParsing
    
    if ($response.Content -match 'token="([^"]+)"') {
        $profileToken = $matches[1]
        Write-Host "  OK: ProfileToken = $profileToken" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: No ProfileToken found" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 2. Get Stream URI
Write-Host "[2/2] Getting Stream URI..."

$getStreamUriRequest = "<?xml version=`"1.0`" encoding=`"UTF-8`"?><s:Envelope xmlns:s=`"http://www.w3.org/2003/05/soap-envelope`" xmlns:trt=`"http://www.onvif.org/ver10/media/wsdl`" xmlns:tt=`"http://www.onvif.org/ver10/schema`"><s:Body><trt:GetStreamUri><trt:StreamSetup><tt:Stream>RTP-Unicast</tt:Stream><tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport></trt:StreamSetup><trt:ProfileToken>$profileToken</trt:ProfileToken></trt:GetStreamUri></s:Body></s:Envelope>"

try {
    $response = Invoke-WebRequest -Uri $mediaUrl -Method POST -Body $getStreamUriRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 5 -UseBasicParsing
    
    Write-Host "  OK: Response received" -ForegroundColor Green
    Write-Host ""
    Write-Host "Full Response:" -ForegroundColor Cyan
    Write-Host $response.Content
    Write-Host ""
    
    # Extract RTSP URL
    if ($response.Content -match '<tt:Uri>([^<]+)</tt:Uri>') {
        $rtspUrl = $matches[1]
        Write-Host "========================================"
        Write-Host "RTSP URL FOUND!" -ForegroundColor Green
        Write-Host "========================================"
        Write-Host $rtspUrl -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Use this URL in your application:" -ForegroundColor Cyan
        Write-Host $rtspUrl -ForegroundColor White
    } elseif ($response.Content -match '<Uri>([^<]+)</Uri>') {
        $rtspUrl = $matches[1]
        Write-Host "========================================"
        Write-Host "RTSP URL FOUND!" -ForegroundColor Green
        Write-Host "========================================"
        Write-Host $rtspUrl -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Use this URL in your application:" -ForegroundColor Cyan
        Write-Host $rtspUrl -ForegroundColor White
    } else {
        Write-Host "ERROR: Could not extract RTSP URL from response" -ForegroundColor Red
    }
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================"
