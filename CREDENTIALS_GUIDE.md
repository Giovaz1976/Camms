# 🔐 Guía de Configuración de Credenciales por Cámara

## 📋 Resumen

La aplicación V380 Viewer ahora soporta **credenciales individuales** para cada cámara, permitiendo configurar diferentes usuarios y contraseñas para:

- **RTSP Streaming** (visualización de video)
- **ONVIF/PTZ** (control de movimiento)

---

## 🎯 Características

### ✅ Credenciales Separadas
Cada cámara puede tener:
- **Usuario/Contraseña RTSP** - Para streaming de video
- **Usuario/Contraseña ONVIF** - Para control PTZ

### ✅ Configuración Fácil
- Click derecho en cualquier cámara de la lista
- Seleccionar "⚙️ Configure Credentials"
- Editar credenciales en el diálogo

### ✅ Reinicio Automático
- Al cambiar credenciales RTSP, el stream se reinicia automáticamente
- No necesitas detener/iniciar manualmente

---

## 📖 Cómo Usar

### 1️⃣ Configurar Credenciales

1. **Click derecho** en una cámara de la lista
2. Selecciona **"⚙️ Configure Credentials"**
3. Configura las credenciales:

   **📹 RTSP Streaming Credentials**
   - Username: `admin` (por defecto)
   - Password: (vacío o tu contraseña)

   **🎮 ONVIF/PTZ Credentials**
   - Username: `admin` (por defecto)
   - Password: (vacío o tu contraseña)

4. Click **"Save"**

### 2️⃣ Credenciales Comunes

Las contraseñas más comunes para cámaras IP:

| Usuario | Contraseña | Uso Común |
|---------|-----------|-----------|
| `admin` | *(vacío)* | V380, muchas cámaras económicas |
| `admin` | `admin` | Cámaras genéricas |
| `admin` | `888888` | Algunas cámaras chinas |
| `admin` | `12345` | Configuración por defecto |

### 3️⃣ Eliminar Cámara

1. **Click derecho** en una cámara
2. Selecciona **"🗑️ Remove Camera"**
3. Confirma la eliminación

---

## 🔧 Detalles Técnicos

### Modelo de Datos

```csharp
public class CameraInfo
{
    // Credenciales para ONVIF/PTZ
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "";
    
    // Credenciales para RTSP (streaming)
    public string RtspUsername { get; set; } = "admin";
    public string RtspPassword { get; set; } = "";
}
```

### URL RTSP Generada

```
rtsp://{RtspUsername}:{RtspPassword}@{IpAddress}/live/ch00_{channel}
```

Ejemplos:
- `rtsp://admin:@192.168.1.81/live/ch00_0` (sin contraseña)
- `rtsp://admin:888888@192.168.1.81/live/ch00_0` (con contraseña)

---

## 🚀 Puertos ONVIF Soportados

La aplicación ahora escanea automáticamente:

| Puerto | Descripción |
|--------|-------------|
| **8899** | V380 cámaras |
| **10080** | Hikvision, Dahua, otras marcas ✨ NUEVO |
| **80** | Puerto HTTP estándar |
| **8080** | Puerto HTTP alternativo |

---

## ⚠️ Notas Importantes

1. **Seguridad**: Las contraseñas se almacenan en texto plano en memoria. No uses contraseñas críticas.

2. **PTZ en V380**: Las cámaras V380 **NO soportan PTZ vía ONVIF**. Usa la app V380 Pro para control PTZ.

3. **Reinicio de Stream**: Al cambiar credenciales RTSP, el stream se reinicia automáticamente si la cámara está activa.

4. **Credenciales por Defecto**: 
   - RTSP: `admin` / *(vacío)*
   - ONVIF: `admin` / *(vacío)*

---

## 🐛 Solución de Problemas

### ❌ Stream no funciona después de cambiar credenciales

**Solución:**
1. Verifica que las credenciales sean correctas
2. Prueba con contraseña vacía primero
3. Reinicia la aplicación

### ❌ PTZ no funciona

**Solución:**
1. Verifica que la cámara soporte ONVIF PTZ
2. Configura las credenciales ONVIF correctamente
3. Usa el botón "🔧 Test PTZ" para verificar

### ❌ No puedo eliminar una cámara

**Solución:**
1. Asegúrate de seleccionar la cámara primero
2. Click derecho → "🗑️ Remove Camera"
3. Confirma la eliminación

---

## 📝 Changelog

### v1.1.0 (Actual)
- ✅ Credenciales individuales por cámara (RTSP + ONVIF)
- ✅ Puerto 10080 agregado al escaneo ONVIF
- ✅ Menú contextual para configuración
- ✅ Función de eliminar cámaras
- ✅ Reinicio automático de stream al cambiar credenciales

### v1.0.0
- Streaming RTSP básico
- Descubrimiento ONVIF
- Control PTZ (limitado)

---

## 💡 Tips

1. **Diferentes Contraseñas**: Si tienes múltiples cámaras con diferentes contraseñas, configúralas individualmente.

2. **Prueba Primero**: Antes de configurar credenciales, prueba con las credenciales por defecto (`admin` / vacío).

3. **Guarda la Configuración**: La aplicación recuerda las credenciales mientras esté abierta. Para persistencia, se necesitaría implementar guardado en archivo.

---

## 📧 Soporte

Para reportar problemas o sugerencias, consulta los logs en la consola de la aplicación.
