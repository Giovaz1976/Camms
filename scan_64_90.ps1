# Network Scanner - Rango 192.168.1.64-90
Write-Host "=== Scanning 192.168.1.64 - 192.168.1.90 ===" -ForegroundColor Cyan
Write-Host ""

$subnet = "192.168.1"
$found = @()

# Escanear rango específico
64..90 | ForEach-Object {
    $ip = "$subnet.$_"
    Write-Host "Testing $ip..." -NoNewline
    
    if (Test-Connection -ComputerName $ip -Count 1 -Quiet) {
        Write-Host " FOUND!" -ForegroundColor Green
        $found += $ip
        
        # Test RTSP port (554)
        Write-Host "  Checking ports..." -ForegroundColor Cyan
        $rtsp = Test-NetConnection -ComputerName $ip -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
        if ($rtsp) {
            Write-Host "  -> Port 554 (RTSP) OPEN - This is likely a camera!" -ForegroundColor Yellow
        }
        
        # Test HTTP port (80)
        $http = Test-NetConnection -ComputerName $ip -Port 80 -WarningAction SilentlyContinue -InformationLevel Quiet
        if ($http) {
            Write-Host "  -> Port 80 (HTTP) OPEN" -ForegroundColor Cyan
        }
        
        # Test ONVIF port (8080)
        $onvif = Test-NetConnection -ComputerName $ip -Port 8080 -WarningAction SilentlyContinue -InformationLevel Quiet
        if ($onvif) {
            Write-Host "  -> Port 8080 (ONVIF) OPEN" -ForegroundColor Cyan
        }
    } else {
        Write-Host " -" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "=== Results ===" -ForegroundColor Cyan
Write-Host "Devices found: $($found.Count)" -ForegroundColor $(if ($found.Count -gt 0) { "Green" } else { "Red" })

if ($found.Count -gt 0) {
    Write-Host ""
    Write-Host "Found devices:" -ForegroundColor Yellow
    $found | ForEach-Object { 
        Write-Host "  $_" -ForegroundColor White
        Write-Host "    You can add this manually in CameraViewer using 'Add Camera' button" -ForegroundColor Gray
    }
}
