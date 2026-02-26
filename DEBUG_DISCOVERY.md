# 🔍 DEBUG: Discovery No Encuentra Cámaras

## Diagnóstico del Problema

### Posibles Causas

1. **Paquete capturado es RESPUESTA, no SOLICITUD**
   - Los paquetes con payload (80, 144 bytes) son respuestas de la cámara
   - Necesitamos el paquete de SOLICITUD (probablemente más pequeño)

2. **Firewall bloqueando UDP**
   - Windows Firewall puede estar bloqueando el puerto 32108

3. **Broadcast no llega a la cámara**
   - Problema de red o configuración

## 🧪 Pruebas a Realizar

### Prueba 1: Capturar el Paquete de Solicitud Correcto

**En PCAPdroid:**

1. **Limpia la captura anterior**
2. **Cierra completamente V380 Pro** en el móvil
3. **Inicia nueva captura en PCAPdroid**
4. **Abre V380 Pro**
5. **Espera solo 2-3 segundos** (no te conectes a ninguna cámara)
6. **Detén la captura**

**En Wireshark (en PC):**

1. Abre el .pcap
2. Filtra: `udp.port == 32108`
3. **Busca el PRIMER paquete UDP**
4. **Debe ser el MÁS PEQUEÑO** (probablemente 12-20 bytes)
5. Click derecho > Follow > UDP Stream
6. Copia los bytes en formato Hex

**Qué buscar:**
```
Paquete de SOLICITUD (del móvil):
- Tamaño: 12-20 bytes (pequeño, sin payload)
- Dirección: Broadcast (255.255.255.255)
- Contenido: Solo header, sin datos

Paquete de RESPUESTA (de la cámara):
- Tamaño: 80-200 bytes (con payload)
- Dirección: IP específica
- Contenido: Header + datos de la cámara
```

### Prueba 2: Verificar Firewall

**Opción A: Desactivar temporalmente Windows Firewall**
```powershell
# Ejecutar como Administrador
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False

# Probar la app

# Reactivar después
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
```

**Opción B: Crear regla específica**
```powershell
# Ejecutar como Administrador
New-NetFirewallRule -DisplayName "V380 Discovery" -Direction Inbound -Protocol UDP -LocalPort 32108 -Action Allow
New-NetFirewallRule -DisplayName "V380 Discovery Out" -Direction Outbound -Protocol UDP -LocalPort 32108 -Action Allow
```

### Prueba 3: Verificar Conectividad de Red

**Verifica que la cámara esté accesible:**
```powershell
# Reemplaza con la IP de tu cámara
ping 192.168.1.XXX

# Verifica que estén en la misma red
ipconfig
```

**Asegúrate de que:**
- Tu PC y la cámara estén en la misma subred (ej: 192.168.1.x)
- No haya VPN activa que cambie el routing
- WiFi y Ethernet no estén mezclados

### Prueba 4: Captura en Tiempo Real con Wireshark

**Mientras ejecutas la app:**

1. Abre Wireshark en tu PC
2. Selecciona tu interfaz de red (WiFi o Ethernet)
3. Inicia captura
4. En la app, presiona "Scan Cameras"
5. Observa si aparece algún paquete UDP en el puerto 32108

**Qué verificar:**
- ¿Se envía el paquete desde tu PC?
- ¿Llega alguna respuesta?
- ¿Qué dirección IP tiene el paquete enviado?

## 🔧 Soluciones Alternativas

### Opción 1: Discovery Dirigido (No Broadcast)

Si conoces la IP de tu cámara, podemos enviar el paquete directamente:

```csharp
// En lugar de broadcast, enviar a IP específica
IPEndPoint cameraEndpoint = new IPEndPoint(IPAddress.Parse("192.168.1.XXX"), DISCOVERY_PORT);
await _udpClient.SendAsync(discoveryPacket, discoveryPacket.Length, cameraEndpoint);
```

### Opción 2: Probar con Diferentes Comandos

Tal vez el comando de discovery sea diferente. Prueba estas variantes:

**Variante 1: Sin payload length**
```
18 08 03 38 01 02 02 00
```

**Variante 2: Con comando diferente**
```
18 08 03 38 01 01 00 00 00 00 00 00 00 00 00 00
```

**Variante 3: Solo magic bytes**
```
18 08 03 38
```

## 📊 Información Necesaria

Para ayudarte mejor, necesito saber:

1. **¿Aparece algún error en el status bar de la app?**
2. **¿Cuál es la IP de tu PC?** (`ipconfig`)
3. **¿Cuál es la IP de tu cámara?**
4. **¿Están en la misma subred?**
5. **¿Tienes Wireshark instalado en tu PC?**

## 🎯 Próximo Paso Recomendado

**Lo más importante:** Captura el paquete de SOLICITUD correcto.

El paquete que compartiste (con payloads de 80-144 bytes) son **respuestas**.
Necesitamos el paquete **pequeño** que la app móvil envía PRIMERO.

**Cómo identificarlo en Wireshark:**
1. Ordena por tiempo (Time column)
2. Busca el PRIMER paquete UDP al puerto 32108
3. Debe ser el más pequeño (12-20 bytes)
4. Ese es el paquete de solicitud que necesitamos
