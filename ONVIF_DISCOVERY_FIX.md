# ONVIF Discovery - Corrección Aplicada

## Problema Identificado

La cámara **192.168.1.81** no era detectada por el escaneo ONVIF de CameraViewer, pero **sí era detectada por la app O-KAM**.

### Diagnóstico
- ✅ Código base correcto
- ✅ Firewall configurado
- ✅ Red accesible
- ❌ **Problema**: El código no se unía correctamente al grupo multicast

## Cambios Aplicados

### Archivo: `CameraViewer/Services/OnvifDiscovery.cs`

#### 1. **Unirse al Grupo Multicast**
```csharp
// ANTES: Solo enviaba, no escuchaba correctamente
using var client = new UdpClient();
client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

// AHORA: Se une al grupo multicast para recibir respuestas
var multicastAddress = IPAddress.Parse(MULTICAST_ADDRESS);
client.JoinMulticastGroup(multicastAddress);
client.MulticastLoopback = true;
```

**Por qué es importante**: Sin unirse al grupo multicast, el socket UDP no recibe las respuestas de las cámaras.

#### 2. **Envío Múltiple para Mayor Confiabilidad**
```csharp
// Enviar 2 veces con pequeño delay
await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
await Task.Delay(100, cancellationToken);
await client.SendAsync(probeBytes, probeBytes.Length, multicastEndpoint);
```

**Por qué es importante**: Los paquetes UDP pueden perderse. Enviar múltiples veces aumenta la probabilidad de detección.

#### 3. **Timeout Extendido**
```csharp
// ANTES: 3 segundos
private const int DISCOVERY_TIMEOUT = 3000;

// AHORA: 5 segundos
var discoveryTimeout = 5000;
```

**Por qué es importante**: Algunas cámaras tardan más en responder, especialmente si están ocupadas o en redes congestionadas.

#### 4. **Logging Mejorado**
```csharp
System.Diagnostics.Debug.WriteLine($"[ONVIF] Sending discovery probe...");
System.Diagnostics.Debug.WriteLine($"[ONVIF] Received response from {result.RemoteEndPoint.Address}");
System.Diagnostics.Debug.WriteLine($"[ONVIF] Camera discovered: {camera.Name} at {camera.IpAddress}");
System.Diagnostics.Debug.WriteLine($"[ONVIF] Discovery complete. Found {cameras.Count} camera(s)");
```

**Por qué es importante**: Permite diagnosticar problemas viendo los logs en la ventana de Output de Visual Studio.

#### 5. **Limpieza Apropiada**
```csharp
// Salir del grupo multicast al terminar
client.DropMulticastGroup(multicastAddress);
```

**Por qué es importante**: Libera recursos de red correctamente.

## Cómo Probar

### 1. Ejecutar CameraViewer
```bash
cd F:\Apps\Camms\CameraViewer
dotnet run
```

### 2. Click en "🔍 Scan Cameras"

### 3. Verificar Output Window (Visual Studio)
Si ejecutas desde Visual Studio, verás logs como:
```
[ONVIF] Sending discovery probe to 239.255.255.250:3702
[ONVIF] Listening for responses (5000ms)...
[ONVIF] Received response from 192.168.1.81
[ONVIF] Camera discovered: ONVIF Camera (192.168.1.81) at 192.168.1.81
[ONVIF] Discovery complete. Found 1 camera(s)
```

## Resultado Esperado

La cámara **192.168.1.81** ahora debería ser detectada automáticamente por el escaneo ONVIF.

## Si Aún No Funciona

### Verificar Logs
1. Ejecuta desde Visual Studio en modo Debug
2. Abre View → Output
3. Selecciona "Debug" en el dropdown
4. Busca mensajes `[ONVIF]`

### Posibles Problemas Adicionales

#### 1. Firewall Bloqueando Multicast
```powershell
# Crear regla específica para multicast
New-NetFirewallRule -DisplayName "ONVIF Multicast" -Direction Inbound -Protocol UDP -LocalPort 3702 -Action Allow
New-NetFirewallRule -DisplayName "ONVIF Multicast Out" -Direction Outbound -Protocol UDP -RemotePort 3702 -Action Allow
```

#### 2. ONVIF Deshabilitado en la Cámara
- Accede a http://192.168.1.81
- Busca configuración ONVIF
- Habilita "ONVIF Discovery" o "WS-Discovery"

#### 3. Multicast Bloqueado en el Router/Switch
- Verifica configuración del router
- Habilita IGMP Snooping si está disponible
- Asegúrate de que multicast no esté bloqueado

## Comparación con O-KAM

**O-KAM funciona** porque probablemente:
1. Se une correctamente al grupo multicast ✓ (ahora CameraViewer también)
2. Envía múltiples probes ✓ (ahora CameraViewer también)
3. Espera suficiente tiempo ✓ (ahora CameraViewer también)

## Alternativa: Agregar Manualmente

Si el escaneo automático aún no funciona, siempre puedes agregar la cámara manualmente:

1. Click "➕ Add Camera"
2. IP: `192.168.1.81`
3. Nombre: `Camera 1`
4. Credenciales: `admin` / (vacía o `admin`)

## Resumen de Cambios

| Aspecto | Antes | Ahora |
|---------|-------|-------|
| Multicast Join | ❌ No | ✅ Sí |
| Multicast Loopback | ❌ No | ✅ Sí |
| Envíos | 1 vez | 2 veces |
| Timeout | 3 segundos | 5 segundos |
| Logging | Mínimo | Detallado |
| Cleanup | Parcial | Completo |

## Estado

✅ **Corrección aplicada y compilada exitosamente**

La próxima vez que ejecutes CameraViewer, el escaneo ONVIF debería funcionar correctamente.
