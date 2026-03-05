# Script para probar conexion ONVIF en puerto 10080
# IP: 192.168.1.76 Puerto: 10080 Usuario: admin Password: Wegata76

$ip = "192.168.1.76"
$port = 10080
$username = "admin"
$password = "Wegata76"

Write-Host "========================================"
Write-Host "ONVIF Connection Test"
Write-Host "========================================"
Write-Host "IP: $ip | Port: $port | User: $username"
Write-Host ""

# 1. Test TCP Connection
Write-Host "[1/4] Testing TCP connection..."
try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.ReceiveTimeout = 3000
    $tcpClient.SendTimeout = 3000
    $tcpClient.Connect($ip, $port)
    
    if ($tcpClient.Connected) {
        Write-Host "  OK: TCP connection successful"
        $tcpClient.Close()
    }
} catch {
    Write-Host "  ERROR: TCP connection failed"
    Write-Host "  Camera is not reachable on port $port"
    exit 1
}

Write-Host ""

# 2. Test GetDeviceInformation
Write-Host "[2/4] Testing GetDeviceInformation..."

$getDeviceInfoRequest = '<?xml version="1.0" encoding="UTF-8"?><s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:tds="http://www.onvif.org/ver10/device/wsdl"><s:Body><tds:GetDeviceInformation/></s:Body></s:Envelope>'

$url = "http://$ip`:$port/onvif/device_service"
$authBytes = [System.Text.Encoding]::ASCII.GetBytes("$username`:$password")
$authHeader = [System.Convert]::ToBase64String($authBytes)

try {
    $response = Invoke-WebRequest -Uri $url -Method POST -Body $getDeviceInfoRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 5
    
    Write-Host "  OK: Status $($response.StatusCode)"
    Write-Host "  Response (first 500 chars):"
    Write-Host $response.Content.Substring(0, [Math]::Min(500, $response.Content.Length))
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)"
}

Write-Host ""

# 3. Test GetProfiles
Write-Host "[3/4] Testing GetProfiles..."

$getProfilesRequest = '<?xml version="1.0" encoding="UTF-8"?><s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl"><s:Body><trt:GetProfiles/></s:Body></s:Envelope>'

$mediaUrl = "http://$ip`:$port/onvif/device_service"
$profileToken = $null

try {
    $response = Invoke-WebRequest -Uri $mediaUrl -Method POST -Body $getProfilesRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 5
    
    Write-Host "  OK: Status $($response.StatusCode)"
    
    # Extract ProfileToken
    if ($response.Content -match 'token="([^"]+)"') {
        $profileToken = $matches[1]
        Write-Host "  OK: ProfileToken found: $profileToken"
    } else {
        Write-Host "  WARNING: No ProfileToken found"
    }
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)"
}

Write-Host ""

# 4. Test PTZ
Write-Host "[4/4] Testing PTZ Capabilities..."

if ($profileToken) {
    $relativeMoveRequest = "<?xml version=`"1.0`" encoding=`"UTF-8`"?><s:Envelope xmlns:s=`"http://www.w3.org/2003/05/soap-envelope`" xmlns:tptz=`"http://www.onvif.org/ver20/ptz/wsdl`" xmlns:tt=`"http://www.onvif.org/ver10/schema`"><s:Body><tptz:RelativeMove><tptz:ProfileToken>$profileToken</tptz:ProfileToken><tptz:Translation><tt:PanTilt x=`"0.1`" y=`"0.0`" space=`"http://www.onvif.org/ver10/tptz/PanTiltSpaces/TranslationGenericSpace`"/><tt:Zoom x=`"0.0`" space=`"http://www.onvif.org/ver10/tptz/ZoomSpaces/TranslationGenericSpace`"/></tptz:Translation></tptz:RelativeMove></s:Body></s:Envelope>"

    $ptzUrl = "http://$ip`:$port/onvif/ptz_service"
    
    try {
        $response = Invoke-WebRequest -Uri $ptzUrl -Method POST -Body $relativeMoveRequest -ContentType "application/soap+xml" -Headers @{Authorization="Basic $authHeader"} -TimeoutSec 5
        
        Write-Host "  OK: Status $($response.StatusCode)"
        Write-Host "  OK: PTZ command accepted!"
        Write-Host "  Response (first 500 chars):"
        Write-Host $response.Content.Substring(0, [Math]::Min(500, $response.Content.Length))
    } catch {
        Write-Host "  ERROR: $($_.Exception.Message)"
    }
} else {
    Write-Host "  SKIPPED: No ProfileToken available"
}

Write-Host ""
Write-Host "========================================"
Write-Host "Test Complete"
Write-Host "========================================"
