# Verificar puertos de posibles cámaras
Write-Host "=== Checking Devices 192.168.1.74 and 192.168.1.76 ===" -ForegroundColor Cyan
Write-Host ""

$devices = @("192.168.1.74", "192.168.1.76")

# Puertos comunes de cámaras
$ports = @{
    554 = "RTSP (Standard streaming)"
    80 = "HTTP (Web interface)"
    8080 = "HTTP Alt (ONVIF/Web)"
    8000 = "HTTP Alt (Hikvision)"
    37777 = "TCP (Dahua)"
    34567 = "TCP (V380/XMEye)"
    6688 = "TCP (V380)"
    8899 = "TCP (V380)"
    9527 = "TCP (V380)"
}

foreach ($ip in $devices) {
    Write-Host "Checking $ip..." -ForegroundColor Yellow
    Write-Host "----------------------------------------"
    
    $openPorts = @()
    
    foreach ($port in $ports.Keys | Sort-Object) {
        Write-Host "  Port $port ($($ports[$port]))..." -NoNewline
        
        $result = Test-NetConnection -ComputerName $ip -Port $port -WarningAction SilentlyContinue -InformationLevel Quiet
        
        if ($result) {
            Write-Host " OPEN" -ForegroundColor Green
            $openPorts += $port
        } else {
            Write-Host " closed" -ForegroundColor DarkGray
        }
    }
    
    Write-Host ""
    
    if ($openPorts.Count -gt 0) {
        Write-Host "  Summary: $($openPorts.Count) port(s) open" -ForegroundColor Green
        
        # Determinar tipo de cámara
        if ($openPorts -contains 554) {
            Write-Host "  -> Likely ONVIF/Standard IP Camera (RTSP available)" -ForegroundColor Yellow
        }
        if ($openPorts -contains 34567 -or $openPorts -contains 6688 -or $openPorts -contains 8899) {
            Write-Host "  -> Likely V380/XMEye Camera" -ForegroundColor Yellow
        }
        if ($openPorts -contains 37777) {
            Write-Host "  -> Likely Dahua Camera" -ForegroundColor Yellow
        }
        if ($openPorts -contains 8000) {
            Write-Host "  -> Likely Hikvision Camera" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "  Recommendation: Add manually in CameraViewer" -ForegroundColor Cyan
        Write-Host "  IP: $ip" -ForegroundColor White
        
        if ($openPorts -contains 80) {
            Write-Host "  Web Interface: http://$ip" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  No camera ports detected" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "=== Done ===" -ForegroundColor Cyan
