# ONVIF Discovery Troubleshooting Guide

## Estado Actual

**El código de escaneo ONVIF está funcionando correctamente** - No ha habido cambios que afecten la funcionalidad.

El problema es que **no hay cámaras ONVIF respondiendo** en la red actual.

## Diagnóstico Realizado

### Test de Multicast ONVIF
```
Resultado: 0 respuestas
Conclusión: No hay cámaras ONVIF activas o accesibles en la red
```

## Posibles Causas

### 1. **Cámaras No Conectadas**
- ✓ Verificar que las cámaras estén encendidas
- ✓ Verificar conexión de red (cable Ethernet conectado)
- ✓ Verificar que las cámaras tengan IP asignada

### 2. **Subnet Diferente**
Las cámaras pueden estar en una subnet diferente a la PC:
- PC: `192.168.1.x`
- Cámaras: `192.168.0.x` o `10.0.0.x`

**Solución**: Verificar configuración de red de las cámaras

### 3. **ONVIF Deshabilitado**
Algunas cámaras tienen ONVIF deshabilitado por defecto:
- Acceder a la interfaz web de la cámara
- Buscar configuración ONVIF
- Habilitar ONVIF Discovery

### 4. **Firewall Bloqueando Multicast**
Windows Firewall puede bloquear tráfico multicast:

**Verificar reglas de firewall**:
```powershell
Get-NetFirewallRule | Where-Object { $_.DisplayName -like "*CameraViewer*" }
```

**Crear regla si es necesario**:
```powershell
New-NetFirewallRule -DisplayName "CameraViewer ONVIF" -Direction Inbound -Protocol UDP -LocalPort 3702 -Action Allow
```

### 5. **Switch de Red Bloqueando Multicast**
Algunos switches bloquean tráfico multicast por defecto:
- Verificar configuración del switch
- Habilitar IGMP Snooping si está disponible

### 6. **Cámaras V380 (No ONVIF)**
Las cámaras V380 pueden no soportar ONVIF estándar:
- Usar el botón "Add Camera" para agregar manualmente
- Ingresar IP de la cámara directamente

## Soluciones Alternativas

### Opción 1: Agregar Cámara Manualmente
1. Click en "➕ Add Camera"
2. Ingresar IP de la cámara (ej: `192.168.1.100`)
3. Ingresar nombre (opcional)
4. Click "Add"

### Opción 2: Escanear Rango de IPs
Crear script para escanear IPs comunes:

```powershell
# Escanear subnet local
$subnet = "192.168.1"
100..110 | ForEach-Object {
    $ip = "$subnet.$_"
    if (Test-Connection -ComputerName $ip -Count 1 -Quiet) {
        Write-Host "Device found: $ip" -ForegroundColor Green
    }
}
```

### Opción 3: Verificar IP de Cámara
Usar herramientas del fabricante:
- **Hikvision**: SADP Tool
- **Dahua**: ConfigTool
- **V380**: V380 Pro app

## Verificación del Código

El código de `OnvifDiscovery.cs` está **funcionando correctamente**:

```csharp
// Línea 25-83: Método DiscoverCamerasAsync
// - Envía probe multicast a 239.255.255.250:3702 ✓
// - Escucha respuestas por 3 segundos ✓
// - Parsea ProbeMatch responses ✓
// - Maneja cancelación correctamente ✓
```

## Pruebas Recomendadas

### 1. Verificar Conectividad Básica
```powershell
# Ping a cámara conocida
ping 192.168.1.100

# Verificar puerto RTSP
Test-NetConnection -ComputerName 192.168.1.100 -Port 554
```

### 2. Verificar ONVIF Manualmente
```powershell
# Ejecutar test rápido
.\quick_onvif_test.ps1
```

### 3. Verificar Interfaces de Red
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" }
```

## Logs de Depuración

Para habilitar logs detallados en la aplicación:

1. Abrir Output window en Visual Studio
2. Ejecutar aplicación en modo Debug
3. Click "Scan Cameras"
4. Revisar mensajes en consola:
   - "ONVIF Discovery error: ..." indica problema
   - "Scan complete. Found 0 camera(s)" es normal si no hay cámaras

## Conclusión

**El código está funcionando correctamente**. El problema es de configuración de red o hardware:

1. ✓ Código ONVIF intacto (sin cambios recientes)
2. ✗ No hay cámaras ONVIF respondiendo en la red
3. ✓ Función "Add Camera" manual disponible como alternativa

## Próximos Pasos

1. Verificar que cámaras estén encendidas y conectadas
2. Verificar configuración de red de las cámaras
3. Habilitar ONVIF en configuración de cámaras
4. Usar "Add Camera" manual si ONVIF no está disponible
5. Verificar firewall de Windows
