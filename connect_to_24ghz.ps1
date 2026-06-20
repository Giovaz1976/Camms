# Script para conectar a red 2.4 GHz y verificar cámaras

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Connecting to 2.4 GHz Network" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Conectar a red 2.4 GHz
Write-Host "Disconnecting from current network..." -ForegroundColor Yellow
netsh wlan disconnect | Out-Null
Start-Sleep -Seconds 2

Write-Host "Connecting to INFINITUM6A0F_2.4..." -ForegroundColor Yellow
netsh wlan connect name="INFINITUM6A0F_2.4"

Write-Host ""
Write-Host "Waiting for connection..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# Verificar conexión
$connected = netsh wlan show interfaces | Select-String "INFINITUM6A0F_2.4"
if ($connected) {
    Write-Host "✓ Connected to 2.4 GHz network!" -ForegroundColor Green
} else {
    Write-Host "✗ Connection failed" -ForegroundColor Red
    Write-Host "You may need to enter the WiFi password manually" -ForegroundColor Yellow
    exit
}

Write-Host ""
Write-Host "Testing cameras..." -ForegroundColor Cyan
Write-Host ""

# Test camera .76
Write-Host "Camera .76 (192.168.1.76): " -NoNewline
if (Test-Connection -ComputerName 192.168.1.76 -Count 1 -Quiet) {
    Write-Host "✓ ONLINE" -ForegroundColor Green
} else {
    Write-Host "✗ OFFLINE" -ForegroundColor Red
}

# Test camera .81
Write-Host "Camera .81 (192.168.1.81): " -NoNewline
if (Test-Connection -ComputerName 192.168.1.81 -Count 1 -Quiet) {
    Write-Host "✓ ONLINE" -ForegroundColor Green
} else {
    Write-Host "✗ OFFLINE (may need to wait or camera is off)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. If both cameras are online, run CameraViewer" -ForegroundColor Gray
Write-Host "2. If .81 still offline, verify it's powered on" -ForegroundColor Gray
Write-Host "3. To switch back to 5 GHz:" -ForegroundColor Gray
Write-Host "   netsh wlan connect name=`"INFINITUM6A0F_5`"" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
