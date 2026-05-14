# Guía de Configuración de Cámaras - CameraViewer

## Estado Actual

**Cámara Detectada**: 192.168.1.81 (Puerto RTSP 554 abierto)

## Problema: ONVIF Discovery No Funciona

A pesar de las mejoras aplicadas al código, la cámara **no responde a ONVIF Discovery multicast**. Esto es común en:

- Cámaras V380
- Cámaras con ONVIF deshabilitado
- Redes con multicast bloqueado
- Configuraciones de fábrica restrictivas

## ✅ Solución: Agregar Cámara Manualmente

### Método 1: Usando CameraViewer (Recomendado)

1. **Ejecuta CameraViewer**
   ```bash
   cd F:\Apps\Camms\CameraViewer
   dotnet run
   ```

2. **Click en "➕ Add Camera"**

3. **Ingresa los datos**:
   - **IP Address**: `192.168.1.81`
   - **Name**: `Camera 1` (o el nombre que prefieras)
   - **Port**: `554` (RTSP estándar)

4. **Click "Add"**

5. **Configura credenciales** cuando te lo pida:
   
   Prueba en este orden:
   
   | Usuario | Contraseña | Notas |
   |---------|-----------|-------|
   | `admin` | (vacía) | Configuración de fábrica común |
   | `admin` | `admin` | Muy común en cámaras IP |
   | `admin` | `888888` | Común en cámaras chinas |
   | `admin` | `12345` | Otra opción común |
   | `root` | (vacía) | Algunas cámaras Linux |

6. **Selecciona la cámara** de la lista y debería comenzar a transmitir

### Método 2: Configurar RTSP URL Personalizada

Si conoces la URL RTSP exacta de tu cámara:

**Formatos comunes de URL RTSP**:

```
# V380 / XMEye
rtsp://admin:@192.168.1.81/live/ch00_0

# Hikvision
rtsp://admin:password@192.168.1.81:554/Streaming/Channels/101

# Dahua
rtsp://admin:password@192.168.1.81:554/cam/realmonitor?channel=1&subtype=0

# ONVIF Genérico
rtsp://admin:password@192.168.1.81:554/stream1
```

Para V380 específicamente:
```
# Canal 0, Stream principal (HD)
rtsp://admin:@192.168.1.81/live/ch00_0

# Canal 0, Stream secundario (SD)
rtsp://admin:@192.168.1.81/live/ch00_1
```

## Verificar Configuración de la Cámara

### Acceder a la Interfaz Web

1. Abre un navegador
2. Ve a: `http://192.168.1.81`
3. Ingresa credenciales (prueba las mismas de arriba)

### Configuración ONVIF (si está disponible)

Si logras acceder a la interfaz web:

1. Busca sección **Network** o **Red**
2. Busca **ONVIF** o **WS-Discovery**
3. **Habilita ONVIF** si está deshabilitado
4. **Habilita Discovery** o **WS-Discovery**
5. Guarda cambios y reinicia la cámara
6. Intenta escanear nuevamente en CameraViewer

## Usar App O-KAM para Obtener Información

Ya que O-KAM puede detectar la cámara:

1. **Abre O-KAM**
2. **Detecta la cámara**
3. **Verifica la información**:
   - Modelo de cámara
   - Versión de firmware
   - URL RTSP (si la muestra)
   - Puerto ONVIF (si lo muestra)

Esta información te ayudará a configurar CameraViewer correctamente.

## Troubleshooting

### La cámara no transmite después de agregarla

**Posibles causas**:

1. **Credenciales incorrectas**
   - Prueba todas las combinaciones de la tabla
   - Verifica mayúsculas/minúsculas

2. **Puerto RTSP incorrecto**
   - Prueba puerto 8554 si 554 no funciona
   - Algunas cámaras usan puertos no estándar

3. **URL RTSP incorrecta**
   - Verifica el formato para tu marca de cámara
   - Consulta el manual de la cámara

4. **Límite de conexiones**
   - Cierra O-KAM u otras apps que usen la cámara
   - Algunas cámaras solo permiten 1-2 conexiones simultáneas

### Ver logs de depuración

Si ejecutas desde Visual Studio:

1. **View → Output**
2. **Selecciona "Debug"** en el dropdown
3. Busca mensajes de error relacionados con RTSP o VLC

### Comandos útiles

**Verificar conectividad**:
```powershell
# Ping
ping 192.168.1.81

# Puerto RTSP
Test-NetConnection -ComputerName 192.168.1.81 -Port 554

# Puerto HTTP
Test-NetConnection -ComputerName 192.168.1.81 -Port 80
```

**Probar RTSP con VLC**:
```
1. Abre VLC Media Player
2. Media → Open Network Stream
3. URL: rtsp://admin:@192.168.1.81/live/ch00_0
4. Play
```

Si funciona en VLC, debería funcionar en CameraViewer.

## Mejoras Aplicadas al Código

Para referencia futura, se aplicaron las siguientes mejoras a `OnvifDiscovery.cs`:

✅ Unirse al grupo multicast  
✅ Habilitar multicast loopback  
✅ Envío triple de probes  
✅ Broadcast adicional  
✅ Timeout de 5 segundos  
✅ Logging detallado  
✅ Limpieza correcta de recursos  

Estas mejoras aumentan la compatibilidad, pero algunas cámaras simplemente no soportan ONVIF Discovery.

## Resumen

| Método | Estado | Recomendación |
|--------|--------|---------------|
| ONVIF Auto-Discovery | ❌ No funciona | No usar |
| Agregar Manualmente | ✅ Disponible | **Usar este método** |
| O-KAM | ✅ Funciona | Usar para obtener info |
| Interfaz Web | ⚠️ Verificar | Configurar ONVIF si es posible |

## Próximos Pasos

1. ✅ **Agregar cámara manualmente** usando IP 192.168.1.81
2. ✅ **Probar credenciales** de la tabla
3. ⚠️ **Opcional**: Acceder a interfaz web para habilitar ONVIF
4. ⚠️ **Opcional**: Verificar URL RTSP exacta en O-KAM

La funcionalidad de agregar cámara manualmente está completamente operativa y es la forma recomendada de usar CameraViewer con cámaras que no soportan ONVIF Discovery.
