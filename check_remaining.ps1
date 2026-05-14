# Verificar dispositivos restantes
Write-Host "=== Checking Remaining Devices (.64, .67, .79) ===" -ForegroundColor Cyan
Write-Host ""

$devices = @("192.168.1.64", "192.168.1.67", "192.168.1.79")

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
        Write-Host "  Open ports: $($openPorts -join ', ')" -ForegroundColor White
        
        # Determinar tipo de cámara
        $isCamera = $false
        
        if ($openPorts -contains 554) {
            Write-Host "  -> CAMERA DETECTED: ONVIF/Standard IP Camera (RTSP available)" -ForegroundColor Yellow
            $isCamera = $true
        }
        if ($openPorts -contains 34567 -or $openPorts -contains 6688 -or $openPorts -contains 8899 -or $openPorts -contains 9527) {
            Write-Host "  -> CAMERA DETECTED: V380/XMEye Camera" -ForegroundColor Yellow
            $isCamera = $true
        }
        if ($openPorts -contains 37777) {
            Write-Host "  -> CAMERA DETECTED: Dahua Camera" -ForegroundColor Yellow
            $isCamera = $true
        }
        if ($openPorts -contains 8000) {
            Write-Host "  -> CAMERA DETECTED: Hikvision Camera" -ForegroundColor Yellow
            $isCamera = $true
        }
        
        if ($isCamera) {
            Write-Host ""
            Write-Host "  ACTION REQUIRED: Add this camera manually!" -ForegroundColor Green
            Write-Host "  IP: $ip" -ForegroundColor White
            
            if ($openPorts -contains 80) {
                Write-Host "  Web Interface: http://$ip" -ForegroundColor Cyan
            }
            if ($openPorts -contains 8080) {
                Write-Host "  Alt Web Interface: http://$ip`:8080" -ForegroundColor Cyan
            }
        } else {
            Write-Host "  -> Not a camera (generic network device)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  No camera ports detected - Not a camera" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Scan complete. Check results above for cameras to add manually." -ForegroundColor White
