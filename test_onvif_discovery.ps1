# Script de diagnóstico para ONVIF Discovery
# Verifica si el escaneo ONVIF está funcionando correctamente

Write-Host "=== ONVIF Discovery Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar firewall
Write-Host "1. Checking Windows Firewall..." -ForegroundColor Yellow
$firewallRules = Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*CameraViewer*" }
if ($firewallRules) {
    Write-Host "   ✓ Firewall rules found for CameraViewer" -ForegroundColor Green
    $firewallRules | ForEach-Object { Write-Host "     - $($_.DisplayName)" }
} else {
    Write-Host "   ⚠ No firewall rules found for CameraViewer" -ForegroundColor Red
    Write-Host "     This may block ONVIF discovery responses" -ForegroundColor Red
}
Write-Host ""

# 2. Verificar puerto multicast
Write-Host "2. Checking multicast port 3702..." -ForegroundColor Yellow
$port3702 = Get-NetUDPEndpoint | Where-Object { $_.LocalPort -eq 3702 }
if ($port3702) {
    Write-Host "   ✓ Port 3702 is in use:" -ForegroundColor Green
    $port3702 | ForEach-Object { Write-Host "     - Process: $($_.OwningProcess)" }
} else {
    Write-Host "   ℹ Port 3702 is not currently in use (normal when not scanning)" -ForegroundColor Cyan
}
Write-Host ""

# 3. Test ONVIF multicast
Write-Host "3. Testing ONVIF multicast discovery..." -ForegroundColor Yellow
$multicastAddress = "239.255.255.250"
$multicastPort = 3702

$probeMessage = @"
<?xml version="1.0" encoding="UTF-8"?>
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" 
            xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing">
    <s:Header>
        <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
        <a:MessageID>uuid:$([Guid]::NewGuid().ToString())</a:MessageID>
        <a:ReplyTo>
            <a:Address>http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address>
        </a:ReplyTo>
        <a:To s:mustUnderstand="1">urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
    </s:Header>
    <s:Body>
        <Probe xmlns="http://schemas.xmlsoap.org/ws/2005/04/discovery">
            <d:Types xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" 
                     xmlns:dp0="http://www.onvif.org/ver10/network/wsdl">dp0:NetworkVideoTransmitter</d:Types>
        </Probe>
    </s:Body>
</s:Envelope>
"@

try {
    $udpClient = New-Object System.Net.Sockets.UdpClient
    $udpClient.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket, 
                                       [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)
    $udpClient.Client.Bind([System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0))
    $udpClient.Client.ReceiveTimeout = 500
    
    $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse($multicastAddress), $multicastPort)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($probeMessage)
    
    Write-Host "   Sending ONVIF probe to $multicastAddress`:$multicastPort..." -ForegroundColor Cyan
    $udpClient.Send($bytes, $bytes.Length, $endpoint) | Out-Null
    
    Write-Host "   Listening for responses (3 seconds)..." -ForegroundColor Cyan
    $responses = 0
    $startTime = Get-Date
    
    while (((Get-Date) - $startTime).TotalSeconds -lt 3) {
        try {
            $remoteEndpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $receivedBytes = $udpClient.Receive([ref]$remoteEndpoint)
            $response = [System.Text.Encoding]::UTF8.GetString($receivedBytes)
            
            if ($response -match "ProbeMatch") {
                $responses++
                Write-Host "   ✓ Response $responses from $($remoteEndpoint.Address)" -ForegroundColor Green
                
                # Extraer IP de la respuesta
                if ($response -match "http://([0-9.]+)") {
                    Write-Host "     Camera IP: $($matches[1])" -ForegroundColor Cyan
                }
            }
        } catch {
            # Timeout - continuar
        }
    }
    
    if ($responses -eq 0) {
        Write-Host "   ⚠ No ONVIF cameras responded" -ForegroundColor Red
        Write-Host "     Possible reasons:" -ForegroundColor Yellow
        Write-Host "     - No ONVIF cameras on network" -ForegroundColor Yellow
        Write-Host "     - Cameras are on different subnet" -ForegroundColor Yellow
        Write-Host "     - Firewall blocking multicast" -ForegroundColor Yellow
        Write-Host "     - Network switch blocking multicast" -ForegroundColor Yellow
    } else {
        Write-Host "   ✓ Found $responses ONVIF camera(s)" -ForegroundColor Green
    }
    
    $udpClient.Close()
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 4. Verificar interfaces de red
Write-Host "4. Network interfaces:" -ForegroundColor Yellow
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" } | ForEach-Object {
    Write-Host "   - $($_.IPAddress) ($($_.InterfaceAlias))" -ForegroundColor Cyan
}
Write-Host ""

# 5. Verificar si hay cámaras conocidas en la red
Write-Host "5. Checking for known camera IPs (common ranges)..." -ForegroundColor Yellow
$localIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" } | Select-Object -First 1).IPAddress
if ($localIP) {
    $subnet = $localIP.Substring(0, $localIP.LastIndexOf('.'))
    Write-Host "   Local subnet: $subnet.x" -ForegroundColor Cyan
    Write-Host "   Testing common camera IPs..." -ForegroundColor Cyan
    
    $commonIPs = @(100, 101, 102, 103, 104, 105, 200, 201, 202)
    $found = 0
    foreach ($ip in $commonIPs) {
        $testIP = "$subnet.$ip"
        if (Test-Connection -ComputerName $testIP -Count 1 -Quiet -TimeoutSeconds 1) {
            Write-Host "   ✓ Device found at $testIP" -ForegroundColor Green
            $found++
        }
    }
    
    if ($found -eq 0) {
        Write-Host "   ℹ No devices found on common camera IPs" -ForegroundColor Cyan
    }
}
Write-Host ""

Write-Host "=== Diagnostic Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Recommendations:" -ForegroundColor Yellow
Write-Host "1. Ensure cameras are powered on and connected to network" -ForegroundColor White
Write-Host "2. Check if cameras are on same subnet as PC" -ForegroundColor White
Write-Host "3. Verify Windows Firewall allows CameraViewer" -ForegroundColor White
Write-Host "4. Try adding camera manually if auto-discovery fails" -ForegroundColor White
