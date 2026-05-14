# Escaneo profundo de 192.168.1.76
Write-Host "=== Deep Scan: 192.168.1.76 ===" -ForegroundColor Cyan
Write-Host ""

$ip = "192.168.1.76"

# Verificar conectividad básica
Write-Host "1. Testing basic connectivity..." -ForegroundColor Yellow
if (Test-Connection -ComputerName $ip -Count 2 -Quiet) {
    Write-Host "   Device is ONLINE" -ForegroundColor Green
} else {
    Write-Host "   Device is OFFLINE" -ForegroundColor Red
    exit
}
Write-Host ""

# Puertos comunes de cámaras IP (extendido)
$ports = @{
    # RTSP
    554 = "RTSP (Standard)"
    8554 = "RTSP (Alternative)"
    7447 = "RTSP (Alternative 2)"
    
    # HTTP/Web
    80 = "HTTP (Web interface)"
    8080 = "HTTP (Alternative)"
    8000 = "HTTP (Hikvision)"
    8081 = "HTTP (Alternative 2)"
    
    # ONVIF
    3702 = "ONVIF Discovery"
    8899 = "ONVIF (Alternative)"
    10080 = "ONVIF (Alternative 2)"
    
    # Marcas específicas
    37777 = "Dahua TCP"
    34567 = "V380/XMEye"
    6688 = "V380"
    9527 = "V380"
    15961 = "V380"
    
    # Otros
    443 = "HTTPS"
    21 = "FTP"
    23 = "Telnet"
    22 = "SSH"
    5000 = "UPnP"
}

Write-Host "2. Scanning ports..." -ForegroundColor Yellow
$openPorts = @()

foreach ($port in $ports.Keys | Sort-Object) {
    Write-Host "   Port $port ($($ports[$port]))..." -NoNewline
    
    $result = Test-NetConnection -ComputerName $ip -Port $port -WarningAction SilentlyContinue -InformationLevel Quiet
    
    if ($result) {
        Write-Host " OPEN" -ForegroundColor Green
        $openPorts += @{Port=$port; Service=$ports[$port]}
    } else {
        Write-Host " closed" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "=== Results ===" -ForegroundColor Cyan

if ($openPorts.Count -gt 0) {
    Write-Host "Open ports found: $($openPorts.Count)" -ForegroundColor Green
    Write-Host ""
    
    foreach ($p in $openPorts) {
        Write-Host "  Port $($p.Port) - $($p.Service)" -ForegroundColor White
    }
    
    Write-Host ""
    
    # Análisis
    $hasRTSP = $openPorts | Where-Object { $_.Port -in @(554, 8554, 7447) }
    $hasHTTP = $openPorts | Where-Object { $_.Port -in @(80, 8080, 8000, 8081) }
    $hasV380 = $openPorts | Where-Object { $_.Port -in @(34567, 6688, 8899, 9527, 15961) }
    $hasONVIF = $openPorts | Where-Object { $_.Port -in @(3702, 10080) }
    
    Write-Host "Analysis:" -ForegroundColor Yellow
    
    if ($hasRTSP) {
        Write-Host "  Camera Type: Standard IP Camera (RTSP available)" -ForegroundColor Green
        Write-Host "  Action: Add manually in CameraViewer" -ForegroundColor Cyan
        Write-Host "  RTSP URL: rtsp://admin:@$ip`:$($hasRTSP[0].Port)/..." -ForegroundColor White
    }
    
    if ($hasV380) {
        Write-Host "  Camera Type: V380/XMEye Camera" -ForegroundColor Green
        Write-Host "  Action: Use V380 app or add manually with V380 RTSP format" -ForegroundColor Cyan
    }
    
    if ($hasHTTP) {
        Write-Host "  Web Interface: http://$ip`:$($hasHTTP[0].Port)" -ForegroundColor Cyan
        Write-Host "  Try accessing in browser to configure" -ForegroundColor White
    }
    
    if ($hasONVIF) {
        Write-Host "  ONVIF: Port $($hasONVIF[0].Port) is open" -ForegroundColor Green
        Write-Host "  Camera should support ONVIF discovery" -ForegroundColor Cyan
    }
    
    if (-not $hasRTSP -and -not $hasV380) {
        Write-Host "  This device does NOT appear to be a camera" -ForegroundColor Red
        Write-Host "  Likely: PC, smartphone, tablet, or other network device" -ForegroundColor Yellow
    }
    
} else {
    Write-Host "NO open ports detected" -ForegroundColor Red
    Write-Host ""
    Write-Host "This device is:" -ForegroundColor Yellow
    Write-Host "  - Online (responds to ping)" -ForegroundColor White
    Write-Host "  - But has NO camera ports open" -ForegroundColor White
    Write-Host "  - Likely NOT a camera" -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible device types:" -ForegroundColor Yellow
    Write-Host "  - Smartphone/Tablet" -ForegroundColor White
    Write-Host "  - Laptop/PC with firewall" -ForegroundColor White
    Write-Host "  - Smart TV" -ForegroundColor White
    Write-Host "  - IoT device" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Additional Info ===" -ForegroundColor Cyan

# Intentar obtener hostname
try {
    $hostname = [System.Net.Dns]::GetHostEntry($ip).HostName
    Write-Host "Hostname: $hostname" -ForegroundColor White
} catch {
    Write-Host "Hostname: Unable to resolve" -ForegroundColor Gray
}

# Intentar obtener MAC (requiere estar en la misma subnet)
try {
    $arp = arp -a $ip | Select-String $ip
    if ($arp) {
        Write-Host "ARP Entry: $arp" -ForegroundColor White
    }
} catch {
    Write-Host "MAC Address: Unable to determine" -ForegroundColor Gray
}
