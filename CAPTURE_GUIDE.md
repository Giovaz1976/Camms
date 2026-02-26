# 📡 Guía de Captura de Protocolo V380

Esta guía te ayudará a capturar el protocolo real de V380 para implementarlo en la aplicación.

## 🎯 Objetivo

Capturar los paquetes de red que la app V380 Pro envía/recibe para replicar el protocolo en nuestra aplicación.

## 🛠️ Herramientas Necesarias

1. **Wireshark** - Captura de paquetes de red
2. **Smartphone** - Con app V380 Pro instalada
3. **Cámara V380** - Conectada a tu red WiFi
4. **PC** - En la misma red que la cámara

## 📋 Pasos Detallados

### 1. Preparación

```bash
# Instalar Wireshark
# Windows: https://www.wireshark.org/download.html
# Descargar e instalar la versión estable
```

**Configurar tu red:**
- Asegúrate de que tu PC, smartphone y cámara estén en la misma red WiFi
- Anota la IP de tu cámara (puedes verla en el router o en la app V380)

### 2. Configurar Wireshark

1. Abre Wireshark como Administrador
2. Selecciona tu interfaz de red (WiFi o Ethernet)
3. Aplica este filtro de captura para reducir ruido:
   ```
   host [IP_DE_TU_CAMARA]
   ```
   Ejemplo: `host 192.168.1.100`

### 3. Capturar Discovery (Descubrimiento)

**Objetivo**: Capturar cómo la app encuentra cámaras en la red.

1. **Cierra completamente** la app V380 Pro en tu móvil
2. **Inicia captura** en Wireshark
3. **Abre** la app V380 Pro
4. **Espera** a que aparezcan las cámaras (no te conectes aún)
5. **Detén** la captura

**Qué buscar:**
- Paquetes **UDP** al puerto **32108** o similar
- Paquetes de **broadcast** (255.255.255.255)
- Respuestas de la cámara

**Exportar:**
```
File > Export Specified Packets > Save as "discovery.pcap"
```

### 4. Capturar Login (Autenticación)

**Objetivo**: Capturar el proceso de login.

1. **Inicia captura** en Wireshark
2. En la app V380, **toca** en una cámara para conectarte
3. **Espera** a que se conecte y muestre el video
4. **Detén** la captura

**Qué buscar:**
- Primeros paquetes **TCP** después de establecer conexión
- Paquetes con credenciales (username/password)
- Respuesta de éxito/fallo

**Exportar:**
```
File > Export Specified Packets > Save as "login.pcap"
```

### 5. Capturar Stream (Video)

**Objetivo**: Capturar cómo se solicita y recibe el video.

1. **Inicia captura** en Wireshark
2. **Desconecta** de la cámara en la app
3. **Reconecta** a la cámara
4. **Deja correr** unos 10-15 segundos de video
5. **Detén** la captura

**Qué buscar:**
- Comando para **iniciar stream**
- Paquetes de **datos de video** (grandes, continuos)
- Posibles **heartbeat** packets (cada X segundos)

**Exportar:**
```
File > Export Specified Packets > Save as "stream.pcap"
```

## 🔍 Análisis de Paquetes

### Analizar en Wireshark

1. **Abre** el archivo .pcap
2. **Click derecho** en un paquete > Follow > TCP Stream (o UDP Stream)
3. **Cambia** a vista "Hex Dump" para ver bytes

### Identificar Estructura

Busca patrones comunes:

```
Ejemplo de paquete típico:
44 48 00 01 | 02 00 00 00 | 20 00 | [datos...] | A3 B2

Posible interpretación:
44 48       = Magic bytes "DH" (Dahua, fabricante común)
00 01       = Versión del protocolo
02          = Command ID (ej: 0x01=login, 0x02=stream, etc.)
00 00 00    = Flags o reservado
20 00       = Longitud del payload (32 bytes)
[datos...]  = Payload (credenciales, comandos, etc.)
A3 B2       = Checksum
```

### Documentar Hallazgos

Crea un archivo `PROTOCOL_NOTES.txt`:

```
=== DISCOVERY PACKET ===
Puerto: 32108 UDP
Broadcast: 255.255.255.255
Hex: 44 48 00 01 01 00 00 00 00 00 XX XX
Descripción: Paquete de búsqueda de cámaras

=== DISCOVERY RESPONSE ===
Hex: 44 48 00 01 81 00 00 00 [device_id] [ip] [mac] [...]
Descripción: Respuesta de cámara con su info

=== LOGIN PACKET ===
Puerto: 32108 TCP
Hex: 44 48 00 01 02 00 00 00 [username_32bytes] [password_32bytes] XX XX
Descripción: Autenticación con usuario/contraseña

=== START STREAM ===
Hex: 44 48 00 01 03 00 00 00 00 XX XX
Descripción: Solicitud para iniciar stream de video
```

## 🔧 Implementar en el Código

Una vez documentado el protocolo:

### 1. Actualizar Discovery

```csharp
// En V380Discovery.cs
private byte[] BuildDiscoveryPacket()
{
    // Reemplaza con los bytes reales capturados
    return new byte[] { 
        0x44, 0x48, 0x00, 0x01,  // Magic + version
        0x01, 0x00, 0x00, 0x00,  // Command: Discovery
        // ... resto del paquete real
    };
}
```

### 2. Actualizar Login

```csharp
// En V380Connection.cs
private byte[] BuildLoginPacket(string username, string password)
{
    // Usa la estructura exacta capturada
    List<byte> packet = new List<byte>();
    packet.AddRange(new byte[] { /* magic bytes reales */ });
    // ... implementar según captura
    return packet.ToArray();
}
```

### 3. Actualizar Stream

```csharp
private byte[] BuildStartStreamCommand()
{
    // Comando exacto capturado
    return new byte[] { /* bytes reales del comando */ };
}
```

## 🧪 Probar

1. Compila la aplicación
2. Ejecuta y presiona "Scan Cameras"
3. Verifica en Wireshark que se envíen los paquetes correctos
4. Compara con las capturas originales

## 💡 Tips

- **Compara múltiples capturas** para identificar qué bytes cambian y cuáles son fijos
- **Los checksums** suelen ser los últimos 2-4 bytes
- **Device IDs** suelen ser únicos por cámara
- **Timestamps** pueden estar en formato Unix epoch
- **Strings** (username/password) suelen ser null-padded a longitud fija

## ⚠️ Problemas Comunes

### No veo paquetes de mi cámara
- Verifica que estés en la misma red
- Usa filtro: `ip.addr == [IP_CAMARA]`
- Asegúrate de capturar en la interfaz correcta

### Paquetes encriptados
- Algunas cámaras usan TLS/SSL
- Busca el handshake inicial (suele estar sin encriptar)
- Considera usar un proxy SSL

### Demasiados paquetes
- Aplica filtros más específicos
- Captura solo durante acciones específicas
- Usa "Display Filters" en Wireshark

## 📚 Recursos Adicionales

- [Wireshark User Guide](https://www.wireshark.org/docs/wsug_html_chunked/)
- [TCP/IP Protocol Analysis](https://en.wikipedia.org/wiki/Transmission_Control_Protocol)
- [Reverse Engineering Network Protocols](https://resources.infosecinstitute.com/topic/reverse-engineering-protocols/)

---

**¿Necesitas ayuda?** Comparte tus capturas .pcap y te ayudo a analizarlas.
