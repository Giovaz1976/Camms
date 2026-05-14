# Simple Network Scanner
Write-Host "=== Network Device Scanner ===" -ForegroundColor Cyan
Write-Host "Your IP: 192.168.1.77" -ForegroundColor Green
Write-Host "Scanning subnet: 192.168.1.x" -ForegroundColor Yellow
Write-Host ""

$subnet = "192.168.1"
$found = @()

# Escanear IPs comunes de cámaras
$ips = 1..20 + 100..120 + 200..210

Write-Host "Scanning $($ips.Count) addresses..." -ForegroundColor Cyan
Write-Host ""

foreach ($i in $ips) {
    $ip = "$subnet.$i"
    Write-Host "Testing $ip..." -NoNewline
    
    if (Test-Connection -ComputerName $ip -Count 1 -Quiet) {
        Write-Host " FOUND!" -ForegroundColor Green
        $found += $ip
        
        # Test RTSP port
        $rtsp = Test-NetConnection -ComputerName $ip -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
        if ($rtsp) {
            Write-Host "  -> Port 554 (RTSP) OPEN - Likely a camera!" -ForegroundColor Yellow
        }
        
        # Test HTTP port
        $http = Test-NetConnection -ComputerName $ip -Port 80 -WarningAction SilentlyContinue -InformationLevel Quiet
        if ($http) {
            Write-Host "  -> Port 80 (HTTP) OPEN" -ForegroundColor Cyan
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
    $found | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
}
