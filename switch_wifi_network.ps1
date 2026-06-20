# Script para cambiar entre redes WiFi 2.4 GHz y 5 GHz

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  WiFi Network Switcher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Mostrar red actual
Write-Host "Current WiFi connection:" -ForegroundColor Yellow
$currentWifi = netsh wlan show interfaces | Select-String "SSID"
if ($currentWifi) {
    $currentWifi | ForEach-Object { Write-Host "  $_" -ForegroundColor Cyan }
}

Write-Host ""

# Escanear redes disponibles
Write-Host "Scanning available networks..." -ForegroundColor Green
netsh wlan show networks mode=bssid | Out-Null
Start-Sleep -Seconds 2

# Mostrar redes disponibles
Write-Host ""
Write-Host "Available WiFi networks:" -ForegroundColor Yellow
Write-Host ""

$networks = netsh wlan show networks
$ssids = @()
$currentIndex = 0

foreach ($line in $networks) {
    if ($line -match "SSID \d+ : (.+)") {
        $ssidName = $matches[1].Trim()
        if ($ssidName -ne "") {
            $currentIndex++
            $ssids += $ssidName
            
            # Detectar si es 2.4 o 5 GHz por el nombre
            $band = "Unknown"
            if ($ssidName -match "2\.?4G?") {
                $band = "2.4 GHz"
                Write-Host "  [$currentIndex] $ssidName" -ForegroundColor Green -NoNewline
                Write-Host " ($band)" -ForegroundColor Gray
            } elseif ($ssidName -match "5G") {
                $band = "5 GHz"
                Write-Host "  [$currentIndex] $ssidName" -ForegroundColor Cyan -NoNewline
                Write-Host " ($band)" -ForegroundColor Gray
            } else {
                Write-Host "  [$currentIndex] $ssidName" -ForegroundColor White
            }
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To connect to a network:" -ForegroundColor Yellow
Write-Host "  netsh wlan connect name=`"NETWORK_NAME`"" -ForegroundColor Gray
Write-Host ""
Write-Host "Example for 2.4 GHz network (to access camera .81):" -ForegroundColor Yellow
Write-Host "  netsh wlan connect name=`"YourNetwork-2.4G`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "After connecting, verify cameras:" -ForegroundColor Yellow
Write-Host "  .\check_wifi_band.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
