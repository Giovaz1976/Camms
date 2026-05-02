# Camera Viewer - Local Network

Aplicación de escritorio para Windows que permite visualizar cámaras  sin pasar por la nube, mediante conexión directa en red local.

## 🎯 Características

- ✅ Descubrimiento automático de cámaras en red local
- ✅ Conexión directa P2P sin servidores cloud
- ✅ Visualización de stream de video en tiempo real
- ✅ Interfaz moderna WPF

## 🛠️ Tecnologías

- **Framework**: .NET 8.0 + WPF
- **Lenguaje**: C#
- **Video**: FFmpeg.AutoGen para decodificación


## 📋 Requisitos

- Windows 10/11
- .NET 8.0 Runtime
- Visual Studio 2022 (para desarrollo)
- Cámaras V380 en la misma red local

## 🚀 Compilación

```bash
dotnet restore
dotnet build
dotnet run --project CameraViewer
```

## 📡 Ingeniería Inversa

### Paso 1: Capturar Tráfico Real

Para que la aplicación funcione, necesitas capturar los paquetes reales del protocolo 

1. **Instalar Wireshark**
   - Descarga: https://www.wireshark.org/

2. **Capturar tráfico de la app oficial**
   - Abre Wireshark
   - Selecciona tu interfaz de red (WiFi/Ethernet)
   - Inicia captura
   - Abre la app de la cam en tu móvil
   - Conecta a una cámara
   - Detén la captura en Wireshark

3. **Filtrar paquetes relevantes**
   ```
   ip.addr == [IP_DE_TU_CAMARA]
   ```

4. **Identificar paquetes clave**
   - **Discovery**: Paquetes UDP broadcast al puerto 32108
   - **Login**: Primer paquete TCP después de conectar
   - **Start Stream**: Comando para iniciar video
   - **Video Data**: Stream continuo de datos

### Paso 2: Analizar Estructura de Paquetes

Busca patrones en los paquetes capturados:

```
Ejemplo de estructura típica:
[Magic Bytes: 2-4 bytes] [Command ID: 1 byte] [Length: 2-4 bytes] [Payload] [Checksum]
```

### Paso 3: Actualizar el Código

Una vez capturados los paquetes reales, actualiza:

1. **V380Discovery.cs**
   - `BuildDiscoveryPacket()`: Reemplaza con el paquete UDP real
   - `ParseDiscoveryResponse()`: Parsea la respuesta real de la cámara

2. **V380Connection.cs**
   - `BuildLoginPacket()`: Estructura real de login
   - `BuildStartStreamCommand()`: Comando real para iniciar stream
   - `CalculateChecksum()`: Algoritmo de checksum correcto

## 🔍 Herramientas Útiles

### Wireshark
```bash
# Filtros útiles
udp.port == 32108          # Discovery packets
tcp.port == 32108          # Connection packets
ip.addr == 192.168.1.X     # Tu cámara específica
```

### HxD (Editor Hexadecimal)
- Para analizar archivos de captura
- Comparar paquetes byte por byte

### Fiddler / Charles Proxy
- Si la app móvil usa HTTP/HTTPS
- Interceptar API calls

## 📝 Notas Importantes

⚠️ **ADVERTENCIA**: Esta aplicación usa ingeniería inversa del protocolo. Los valores actuales en el código son **placeholders** y necesitan ser reemplazados con los valores reales capturados de tu cámara.

### Próximos Pasos

1. ✅ Estructura base del proyecto creada
2. ⏳ **ACCIÓN REQUERIDA**: Capturar paquetes reales con Wireshark
3. ⏳ Actualizar `BuildDiscoveryPacket()` con datos reales
4. ⏳ Implementar parser de respuestas
5. ⏳ Implementar decodificación de video (H.264/H.265)
6. ⏳ Optimizar rendimiento de streaming

## 🤝 Contribución

Este es un proyecto de ingeniería inversa educativo. El protocolo V380 es propietario y puede cambiar en futuras versiones del firmware.

## ⚖️ Legal

Este software es solo para uso educativo y personal. Asegúrate de tener permiso para acceder a las cámaras que monitoreas.
