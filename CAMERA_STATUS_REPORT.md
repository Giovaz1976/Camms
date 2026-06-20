# 📹 Estado de Cámaras - Reporte

**Fecha**: 19 de Junio, 2026  
**Red**: 192.168.1.x  
**PC**: 192.168.1.77

---

## 🎥 Cámaras Detectadas

### ✅ Cámara 192.168.1.76 (ONLINE)

**Estado**: ✅ Activa y accesible

**Puertos Abiertos**:
- ✅ **Puerto 10080** - ONVIF (Web Interface)
- ❌ Puerto 554 - RTSP (Cerrado)
- ❌ Puerto 80 - HTTP (Cerrado)
- ❌ Puerto 8080 - HTTP Alt (Cerrado)

**Acceso Web**:
```
http://192.168.1.76:10080
```

**Credenciales** (de test anterior):
- Usuario: `admin`
- Contraseña: `Wegata76`

**URLs RTSP Sugeridas** (probar en orden):
```
1. rtsp://admin:Wegata76@192.168.1.76:554/live/ch00_0
2. rtsp://admin:Wegata76@192.168.1.76:554/stream1
3. rtsp://admin:Wegata76@192.168.1.76:554/onvif1
4. rtsp://admin:Wegata76@192.168.1.76:554/Streaming/Channels/101
```

**Nota**: Puerto RTSP 554 está cerrado. Posibles razones:
- RTSP deshabilitado en la cámara
- Firewall bloqueando el puerto
- Cámara usa puerto RTSP alternativo
- Necesita habilitarse desde la interfaz web

---

### ❌ Cámara 192.168.1.81 (OFFLINE)

**Estado**: ❌ No responde

**Posibles causas**:
- Cámara apagada
- Cable de red desconectado
- IP cambió
- En otra red/VLAN

**Acción requerida**:
1. Verificar alimentación de la cámara
2. Verificar cable de red
3. Revisar router para ver IP actual
4. Intentar resetear la cámara

---

## 🔧 Próximos Pasos

### Para Cámara .76

#### Opción 1: Acceder a Interfaz Web (Recomendado)

1. Abre navegador
2. Ve a: `http://192.168.1.76:10080`
3. Login: `admin` / `Wegata76`
4. Busca configuración de RTSP:
   - Network → RTSP
   - Stream → RTSP
   - Settings → Video Stream
5. Anota la URL RTSP exacta
6. Verifica que RTSP esté habilitado
7. Habilita puerto 554 si está deshabilitado

#### Opción 2: Probar URLs en VLC

1. Abre VLC Media Player
2. Media → Open Network Stream
3. Prueba cada URL de la lista
4. Si funciona, usa esa URL en CameraViewer

#### Opción 3: Usar Puerto Alternativo

Si RTSP usa puerto diferente a 554:
```
rtsp://admin:Wegata76@192.168.1.76:8554/stream1
rtsp://admin:Wegata76@192.168.1.76:10554/stream1
```

---

### Para Cámara .81

1. **Verificar físicamente**:
   - ¿Tiene luz el LED de red?
   - ¿Está enchufada?
   - ¿Cable de red conectado?

2. **Revisar router**:
   - Accede a tu router (ej: 192.168.1.1)
   - Busca lista de dispositivos conectados
   - Encuentra la cámara y su IP actual

3. **Resetear cámara** (si es necesario):
   - Busca botón de reset en la cámara
   - Mantén presionado 10-15 segundos
   - Espera que reinicie
   - Busca IP por defecto en el manual

---

## 📝 Configuración de CameraViewer

### Agregar Cámara .76 Manualmente

Una vez que tengas la URL RTSP correcta:

1. Ejecuta CameraViewer
2. Click en "➕ Add Camera"
3. Ingresa:
   - **Name**: Camera 76
   - **IP**: 192.168.1.76
   - **Port**: 554 (o el puerto RTSP correcto)
   - **Username**: admin
   - **Password**: Wegata76
   - **URL**: (la URL RTSP que funcione en VLC)

---

## 🐛 Solución a Errores VLC

Los errores que viste:
```
Failed to connect with rtsp://192.168.1.81:554/live/ch00_0
VLC could not connect to "192.168.1.81:554"
```

**Causa**: Cámara .81 está offline

**Solución**: Usar cámara .76 o reactivar cámara .81

---

## 🔍 Scripts Disponibles

Para diagnóstico futuro:

```powershell
# Test rápido de una cámara
.\test_camera_quick.ps1 -IP "192.168.1.76"

# Escanear red completa
.\scan_network_simple.ps1

# Test profundo de puertos
.\deep_scan_76.ps1

# Test de credenciales ONVIF
.\test_onvif_10080.ps1
```

---

## ✅ Resumen

| Cámara | Estado | Puerto RTSP | Puerto Web | Acción |
|--------|--------|-------------|------------|--------|
| 192.168.1.76 | ✅ Online | ❌ 554 cerrado | ✅ 10080 abierto | Acceder a web y habilitar RTSP |
| 192.168.1.81 | ❌ Offline | ❌ No accesible | ❌ No accesible | Verificar físicamente |

**Recomendación**: Enfócate en la cámara .76 primero. Accede a su interfaz web para configurar RTSP correctamente.
