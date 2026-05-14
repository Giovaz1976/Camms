# Quick ONVIF Discovery Test
Write-Host "=== Quick ONVIF Test ===" -ForegroundColor Cyan

$multicastAddress = "239.255.255.250"
$multicastPort = 3702

$probeMessage = @"
<?xml version="1.0" encoding="UTF-8"?>
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing">
<s:Header>
<a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
<a:MessageID>uuid:$([Guid]::NewGuid().ToString())</a:MessageID>
<a:ReplyTo><a:Address>http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address></a:ReplyTo>
<a:To s:mustUnderstand="1">urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
</s:Header>
<s:Body>
<Probe xmlns="http://schemas.xmlsoap.org/ws/2005/04/discovery">
<d:Types xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" xmlns:dp0="http://www.onvif.org/ver10/network/wsdl">dp0:NetworkVideoTransmitter</d:Types>
</Probe>
</s:Body>
</s:Envelope>
"@

try {
    $udpClient = New-Object System.Net.Sockets.UdpClient
    $udpClient.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket, [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)
    $udpClient.Client.Bind([System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0))
    $udpClient.Client.ReceiveTimeout = 500
    
    $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse($multicastAddress), $multicastPort)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($probeMessage)
    
    Write-Host "Sending probe..." -ForegroundColor Yellow
    $udpClient.Send($bytes, $bytes.Length, $endpoint) | Out-Null
    
    Write-Host "Listening for 3 seconds..." -ForegroundColor Yellow
    $responses = 0
    $startTime = Get-Date
    
    while (((Get-Date) - $startTime).TotalSeconds -lt 3) {
        try {
            $remoteEndpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $receivedBytes = $udpClient.Receive([ref]$remoteEndpoint)
            $response = [System.Text.Encoding]::UTF8.GetString($receivedBytes)
            
            if ($response -match "ProbeMatch") {
                $responses++
                Write-Host "Response from: $($remoteEndpoint.Address)" -ForegroundColor Green
                
                if ($response -match "http://([0-9.]+)") {
                    Write-Host "  Camera IP: $($matches[1])" -ForegroundColor Cyan
                }
            }
        } catch {
            # Timeout
        }
    }
    
    Write-Host "`nTotal responses: $responses" -ForegroundColor $(if ($responses -gt 0) { "Green" } else { "Red" })
    $udpClient.Close()
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
