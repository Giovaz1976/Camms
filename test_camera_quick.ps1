# Quick Camera Test
param(
    [string]$IP = "192.168.1.81"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Quick Camera Test for $IP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test ping
Write-Host "[1/3] Ping test..." -NoNewline
$ping = Test-Connection -ComputerName $IP -Count 1 -Quiet
if ($ping) {
    Write-Host " OK" -ForegroundColor Green
} else {
    Write-Host " FAILED" -ForegroundColor Red
    exit
}

# Test RTSP port
Write-Host "[2/3] RTSP port 554..." -NoNewline
$rtsp = Test-NetConnection -ComputerName $IP -Port 554 -WarningAction SilentlyContinue -InformationLevel Quiet
if ($rtsp) {
    Write-Host " OPEN" -ForegroundColor Green
} else {
    Write-Host " CLOSED" -ForegroundColor Red
}

# Test HTTP ports
Write-Host "[3/3] HTTP ports..."
$ports = @(80, 8080, 10080)
foreach ($p in $ports) {
    Write-Host "  Port $p..." -NoNewline
    $test = Test-NetConnection -ComputerName $IP -Port $p -WarningAction SilentlyContinue -InformationLevel Quiet
    if ($test) {
        Write-Host " OPEN" -ForegroundColor Green
        Write-Host "    Try: http://${IP}:${p}" -ForegroundColor Cyan
    } else {
        Write-Host " closed" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Suggested RTSP URLs:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. rtsp://admin:@${IP}:554/live/ch00_0" -ForegroundColor Cyan
Write-Host "2. rtsp://admin:@${IP}:554/stream1" -ForegroundColor Cyan
Write-Host "3. rtsp://admin:@${IP}:554/onvif1" -ForegroundColor Cyan
Write-Host "4. rtsp://admin:@${IP}:554/Streaming/Channels/101" -ForegroundColor Cyan
Write-Host ""
Write-Host "Test these URLs in VLC: Media -> Open Network Stream" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
