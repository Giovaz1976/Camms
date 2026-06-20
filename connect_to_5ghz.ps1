# Script para volver a red 5 GHz

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Connecting to 5 GHz Network" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Disconnecting from current network..." -ForegroundColor Yellow
netsh wlan disconnect | Out-Null
Start-Sleep -Seconds 2

Write-Host "Connecting to INFINITUM6A0F_5..." -ForegroundColor Yellow
netsh wlan connect name="INFINITUM6A0F_5"

Write-Host ""
Write-Host "Waiting for connection..." -ForegroundColor Gray
Start-Sleep -Seconds 5

$connected = netsh wlan show interfaces | Select-String "INFINITUM6A0F_5"
if ($connected) {
    Write-Host "✓ Connected to 5 GHz network!" -ForegroundColor Green
    Write-Host "  (Faster internet, but camera .81 won't be accessible)" -ForegroundColor Yellow
} else {
    Write-Host "✗ Connection failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing camera .76..." -ForegroundColor Cyan
if (Test-Connection -ComputerName 192.168.1.76 -Count 1 -Quiet) {
    Write-Host "✓ Camera .76 is accessible" -ForegroundColor Green
} else {
    Write-Host "✗ Camera .76 not accessible" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
