# Pruebas de Protocolos Estándar para V380

## El Problema

V380 Pro usa TLS/SSL para encriptar la comunicación, lo que hace imposible 
capturar el protocolo directamente con Wireshark.

## Soluciones Alternativas

### 1. RTSP (Protocolo de Streaming)

Muchas cámaras V380 tienen un servidor RTSP oculto. Prueba estas URLs:

```
rtsp://admin:@192.168.1.81:554/stream
rtsp://admin:@192.168.1.81:554/live
rtsp://admin:@192.168.1.81:554/ch0
rtsp://admin:@192.168.1.81:554/h264
rtsp://admin:@192.168.1.81:8554/stream
rtsp://192.168.1.81:554/stream
```

**Probar con VLC:**
1. Abre VLC Media Player
2. Media > Open Network Stream
3. Pega una de las URLs de arriba
4. Click Play

Si funciona, verás el video de la cámara.

### 2. ONVIF (Protocolo de Cámaras IP)

Algunas cámaras V380 soportan ONVIF:

**Probar con ONVIF Device Manager:**
1. Descarga: https://sourceforge.net/projects/onvifdm/
2. Instala y abre
3. Click "Refresh" para buscar cámaras
4. Si aparece tu cámara, puedes usarla con ONVIF

### 3. Puerto HTTP

Algunas cámaras tienen interfaz web:

```
http://192.168.1.81
http://192.168.1.81:80
http://192.168.1.81:8080
http://192.168.1.81:8081
```

Abre en navegador y ve si hay interfaz web.

### 4. Otros Puertos Comunes

```
34567 - Puerto Dahua
37777 - Puerto XMeye
8000  - Puerto alternativo
```

## Recomendación

**Prueba primero RTSP con VLC:**

1. Abre VLC
2. Media > Open Network Stream
3. URL: `rtsp://admin:@192.168.1.81:554/stream`
4. Play

Si ves video, podemos modificar nuestra app para usar RTSP en lugar del 
protocolo propietario V380.

## Si RTSP Funciona

Actualizaremos la app para usar FFmpeg y decodificar el stream RTSP directamente,
lo cual es mucho más simple que hacer ingeniería inversa del protocolo encriptado.
