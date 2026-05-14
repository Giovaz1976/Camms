# Network Scanner - Buscar dispositivos en subnet local
Write-Host "=== Network Device Scanner ===" -ForegroundColor Cyan
Write-Host "Your IP: 192.168.1.77" -ForegroundColor Green
Write-Host "Scanning subnet: 192.168.1.x" -ForegroundColor Yellow
Write-Host ""

$subnet = "192.168.1"
$found = @()

# Escanear rango común de cámaras
$ranges = @(
    @{Start=1; End=50; Name="Router/Gateway range"},
    @{Start=100; End=120; Name="Common camera range"},
    @{Start=200; End=220; Name="Alternative camera range"}
)

foreach ($range in $ranges) {
    Write-Host "Scanning $($range.Name) ($subnet.$($range.Start)-$($range.End))..." -ForegroundColor Cyan
    
    $range.Start..$range.End | ForEach-Object {
        $ip = "$subnet.$_"
        if (Test-Connection -ComputerName $ip -Count 1 -Quiet -TimeoutSeconds 1) {
            $found += $ip
            Write-Host "  [+] Device found: $ip" -ForegroundColor Green
            
            # Intentar identificar si es cámara (puerto 554 RTSP)
            $rtspTest = Test-NetConnection -ComputerName $ip -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
            if ($rtspTest) {
                Write-Host "      -> RTSP port 554 OPEN (likely a camera!)" -ForegroundColor Yellow
            }
            
            # Intentar identificar si es cámara (puerto 80 HTTP)
            $httpTest = Test-NetConnection -ComputerName $ip -Port 80 -WarningAction SilentlyContinue -InformationLevel Quiet
            if ($httpTest) {
                Write-Host "      -> HTTP port 80 OPEN" -ForegroundColor Cyan
            }
        }
    }
}

Write-Host ""
Write-Host "=== Scan Complete ===" -ForegroundColor Cyan
Write-Host "Total devices found: $($found.Count)" -ForegroundColor $(if ($found.Count -gt 0) { "Green" } else { "Red" })

if ($found.Count -gt 0) {
    Write-Host ""
    Write-Host "Found devices:" -ForegroundColor Yellow
    $found | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
}
