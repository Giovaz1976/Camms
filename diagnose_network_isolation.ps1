# Script para diagnosticar aislamiento entre redes 2.4 GHz y 5 GHz

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Network Isolation Diagnostic" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Obtener información de la conexión WiFi actual
Write-Host "[1/4] Checking your WiFi connection..." -ForegroundColor Green

try {
    $wifiInfo = netsh wlan show interfaces
    
    if ($wifiInfo -match "SSID\s+:\s+(.+)") {
        $ssid = $matches[1].Trim()
        Write-Host "  Connected to: $ssid" -ForegroundColor Yellow
    }
    
    if ($wifiInfo -match "Radio type\s+:\s+(.+)") {
        $radioType = $matches[1].Trim()
        Write-Host "  Radio type: $radioType" -ForegroundColor Yellow
        
        if ($radioType -like "*802.11ac*" -or $radioType -like "*802.11ax*") {
            Write-Host "  ⚠️  You are likely on 5 GHz network" -ForegroundColor Yellow
        } elseif ($radioType -like "*802.11n*" -or $radioType -like "*802.11g*") {
            Write-Host "  ℹ️  You might be on 2.4 GHz network" -ForegroundColor Cyan
        }
    }
    
    if ($wifiInfo -match "Channel\s+:\s+(\d+)") {
        $channel = [int]$matches[1]
        Write-Host "  Channel: $channel" -ForegroundColor Yellow
        
        if ($channel -le 14) {
            Write-Host "  ✓ Confirmed: 2.4 GHz network (channels 1-14)" -ForegroundColor Green
        } else {
            Write-Host "  ✓ Confirmed: 5 GHz network (channels 36+)" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "  ⚠️  Could not detect WiFi info (might be using Ethernet)" -ForegroundColor Yellow
}

Write-Host ""

# 2. Verificar si hay conexión Ethernet
Write-Host "[2/4] Checking Ethernet connection..." -ForegroundColor Green

$ethernetAdapters = Get-NetAdapter | Where-Object { 
    $_.Status -eq "Up" -and 
    $_.PhysicalMediaType -like "*802.3*" 
}

if ($ethernetAdapters) {
    Write-Host "  ✓ Ethernet connection detected" -ForegroundColor Green
    Write-Host "  This is the BEST option for camera streaming!" -ForegroundColor Cyan
} else {
    Write-Host "  No Ethernet connection" -ForegroundColor Gray
}

Write-Host ""

# 3. Listar todas las redes WiFi disponibles
Write-Host "[3/4] Scanning available WiFi networks..." -ForegroundColor Green

try {
    $networks = netsh wlan show networks mode=bssid
    
    # Buscar redes con nombres similares (mismo SSID con sufijos)
    $ssidPattern = @{}
    
    if ($networks -match "SSID \d+ : (.+)") {
        $allSSIDs = [regex]::Matches($networks, "SSID \d+ : (.+)") | ForEach-Object { $_.Groups[1].Value.Trim() }
        
        Write-Host ""
        Write-Host "  Available networks:" -ForegroundColor Yellow
        $allSSIDs | Select-Object -Unique | ForEach-Object {
            Write-Host "    • $_" -ForegroundColor Cyan
            
            # Detectar si hay variantes 2.4G/5G
            if ($_ -match "(.+?)[-_]?(2\.?4G?|5G?)$") {
                $baseName = $matches[1]
                if (-not $ssidPattern.ContainsKey($baseName)) {
                    $ssidPattern[$baseName] = @()
                }
                $ssidPattern[$baseName] += $_
            }
        }
        
        # Reportar redes con múltiples bandas
        if ($ssidPattern.Count -gt 0) {
            Write-Host ""
            Write-Host "  Networks with multiple bands detected:" -ForegroundColor Yellow
            foreach ($base in $ssidPattern.Keys) {
                if ($ssidPattern[$base].Count -gt 1) {
                    Write-Host "    • $base has:" -ForegroundColor Cyan
                    $ssidPattern[$base] | ForEach-Object {
                        Write-Host "      - $_" -ForegroundColor Gray
                    }
                }
            }
        }
    }
} catch {
    Write-Host "  Could not scan networks" -ForegroundColor Red
}

Write-Host ""

# 4. Probar conectividad con cámaras conocidas
Write-Host "[4/4] Testing camera connectivity..." -ForegroundColor Green

$cameras = @(
    @{IP="192.168.1.76"; Name="Camera 76"},
    @{IP="192.168.1.81"; Name="Camera 81"}
)

foreach ($cam in $cameras) {
    Write-Host "  Testing $($cam.Name) ($($cam.IP))..." -NoNewline
    $ping = Test-Connection -ComputerName $cam.IP -Count 1 -Quiet
    
    if ($ping) {
        Write-Host " ✓ REACHABLE" -ForegroundColor Green
    } else {
        Write-Host " ✗ NOT REACHABLE" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Recommendations:" -ForegroundColor Yellow
Write-Host ""

# Generar recomendaciones basadas en los resultados
$recommendations = @()

if (-not $ethernetAdapters) {
    $recommendations += "1. BEST: Connect your PC via Ethernet cable"
}

if ($wifiInfo -match "Channel\s+:\s+(\d+)" -and [int]$matches[1] -gt 14) {
    $recommendations += "2. Switch to 2.4 GHz WiFi network (cameras are likely on 2.4 GHz)"
    $recommendations += "   Look for network names ending in '-2.4G' or similar"
}

$recommendations += "3. Check router settings:"
$recommendations += "   • Disable 'AP Isolation' or 'Client Isolation'"
$recommendations += "   • Enable 'Band Steering' or 'Smart Connect'"
$recommendations += "   • Access router at: http://192.168.1.1"

$recommendations += "4. If cameras still unreachable:"
$recommendations += "   • Verify cameras are powered on"
$recommendations += "   • Check if cameras are connected to WiFi"
$recommendations += "   • Try resetting cameras to factory defaults"

foreach ($rec in $recommendations) {
    Write-Host "  $rec" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
