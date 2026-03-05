# Test si la cámara acepta credenciales vacías
$ip = "192.168.1.76"
$port = 10080

Write-Host "Testing authentication with empty password..."
Write-Host ""

# Test 1: Sin credenciales
Write-Host "[1] No credentials:"
$getProfilesRequest = '<?xml version="1.0" encoding="UTF-8"?><s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl"><s:Body><trt:GetProfiles/></s:Body></s:Envelope>'

try {
    $response = Invoke-WebRequest -Uri "http://$ip`:$port/onvif/device_service" -Method POST -Body $getProfilesRequest -ContentType "application/soap+xml" -TimeoutSec 3 -UseBasicParsing
    Write-Host "  Result: $($response.StatusCode) - ACCEPTED (no auth required?)" -ForegroundColor Yellow
} catch {
    Write-Host "  Result: REJECTED - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 2: admin con password vacío
Write-Host "[2] admin / (empty):"
$authBytes = [System.Text.Encoding]::ASCII.GetBytes("admin:")
$authHeader = [System.Convert]::ToBase64String($authBytes)

try {
    $response = Invoke-WebRequest -Uri "http://$ip`:$port/onvif/device_service" -Method POST -Body $getProfilesRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 3 -UseBasicParsing
    Write-Host "  Result: $($response.StatusCode) - ACCEPTED" -ForegroundColor Green
} catch {
    Write-Host "  Result: REJECTED - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: admin con password correcto
Write-Host "[3] admin / Wegata76:"
$authBytes = [System.Text.Encoding]::ASCII.GetBytes("admin:Wegata76")
$authHeader = [System.Convert]::ToBase64String($authBytes)

try {
    $response = Invoke-WebRequest -Uri "http://$ip`:$port/onvif/device_service" -Method POST -Body $getProfilesRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 3 -UseBasicParsing
    Write-Host "  Result: $($response.StatusCode) - ACCEPTED" -ForegroundColor Green
} catch {
    Write-Host "  Result: REJECTED - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================"
Write-Host "Conclusion:"
Write-Host "If both [2] and [3] are ACCEPTED, the camera"
Write-Host "accepts any password for ONVIF but requires"
Write-Host "the correct password for RTSP streaming."
Write-Host "========================================"
