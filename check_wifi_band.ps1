# Check WiFi Band (2.4 GHz vs 5 GHz)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "WiFi Band Checker" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get WiFi info
$wifiInfo = netsh wlan show interfaces

if ($wifiInfo -match "SSID\s+:\s+(.+)") {
    $ssid = $matches[1].Trim()
    Write-Host "Connected to: $ssid" -ForegroundColor Yellow
}

if ($wifiInfo -match "Channel\s+:\s+(\d+)") {
    $channel = [int]$matches[1]
    Write-Host "Channel: $channel" -ForegroundColor Yellow
    
    if ($channel -le 14) {
        Write-Host ""
        Write-Host "Band: 2.4 GHz" -ForegroundColor Green
        Write-Host "Status: OK for cameras" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "Band: 5 GHz" -ForegroundColor Red
        Write-Host "Status: PROBLEM - Cameras likely on 2.4 GHz!" -ForegroundColor Red
        Write-Host ""
        Write-Host "SOLUTION:" -ForegroundColor Yellow
        Write-Host "1. Disconnect from current WiFi" -ForegroundColor Gray
        Write-Host "2. Look for network ending in '-2.4G' or similar" -ForegroundColor Gray
        Write-Host "3. Connect to 2.4 GHz network" -ForegroundColor Gray
        Write-Host "4. OR use Ethernet cable (best option)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Testing cameras..." -ForegroundColor Cyan
Write-Host "Camera .76: " -NoNewline
if (Test-Connection -ComputerName 192.168.1.76 -Count 1 -Quiet) {
    Write-Host "ONLINE" -ForegroundColor Green
} else {
    Write-Host "OFFLINE" -ForegroundColor Red
}

Write-Host "Camera .81: " -NoNewline
if (Test-Connection -ComputerName 192.168.1.81 -Count 1 -Quiet) {
    Write-Host "ONLINE" -ForegroundColor Green
} else {
    Write-Host "OFFLINE" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
