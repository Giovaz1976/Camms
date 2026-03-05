# 🔐 Auto-Detección de Credenciales ONVIF

## 📋 Funcionalidad Implementada

La aplicación ahora incluye un sistema inteligente de detección y validación de credenciales durante el escaneo ONVIF.

---

## 🎯 Flujo de Escaneo

### **Paso 1: Descubrimiento**
```
Usuario hace click en "🔍 Scan Cameras"
    ↓
Escaneo ONVIF multicast (WS-Discovery)
    ↓
Cámara ONVIF encontrada (IP detectada)
```

### **Paso 2: Autenticación Automática**
```
Intenta credenciales por defecto:
    1. admin / (vacío)
    2. admin / admin
    3. admin / 888888
    4. admin / 12345
    
¿Alguna funciona?
    ├─ SÍ → Agregar cámara automáticamente ✅
    └─ NO → Solicitar credenciales manualmente
```

### **Paso 3: Solicitud Manual (si es necesario)**
```
Mostrar diálogo:
    ┌─────────────────────────────────────┐
    │ Credentials Required - 192.168.1.76 │
    ├─────────────────────────────────────┤
    │ Camera found at 192.168.1.76        │
    │ Default credentials failed.         │
    │ Please enter credentials:           │
    │                                     │
    │ Username: [admin            ]       │
    │ Password: [●●●●●●●●        ]       │
    │                                     │
    │              [Skip] [Connect]       │
    └─────────────────────────────────────┘

Usuario ingresa credenciales
    ↓
Validar con ONVIF GetProfiles
    ├─ Válidas → Agregar cámara ✅
    └─ Inválidas → Mostrar error ❌
```

---

## 🔑 Credenciales por Defecto

La aplicación prueba automáticamente estas combinaciones:

| # | Usuario | Contraseña | Uso Común |
|---|---------|------------|-----------|
| 1 | `admin` | *(vacío)* | V380, algunas Hikvision |
| 2 | `admin` | `admin` | Muchas marcas genéricas |
| 3 | `admin` | `888888` | Dahua, algunas chinas |
| 4 | `admin` | `12345` | Algunas genéricas |

---

## 📊 Comportamiento por Escenario

### **Escenario 1: Cámara V380**
```
1. Descubre cámara en 192.168.1.81
2. Prueba admin/(vacío) → ✅ ÉXITO
3. Agrega automáticamente a la lista
4. Usuario selecciona → Video funciona
```

### **Escenario 2: Cámara ONVIF con Contraseña Estándar**
```
1. Descubre cámara en 192.168.1.100
2. Prueba admin/(vacío) → ❌ Falla
3. Prueba admin/admin → ✅ ÉXITO
4. Agrega automáticamente a la lista
5. Usuario selecciona → Video funciona
```

### **Escenario 3: Cámara ONVIF con Contraseña Personalizada**
```
1. Descubre cámara en 192.168.1.76
2. Prueba todas las credenciales por defecto → ❌ Todas fallan
3. Muestra diálogo "Credentials Required"
4. Usuario ingresa admin/Wegata76
5. Valida con ONVIF → ✅ ÉXITO
6. Agrega a la lista
7. Usuario selecciona → Video funciona
```

### **Escenario 4: Usuario Cancela**
```
1. Descubre cámara en 192.168.1.76
2. Prueba todas las credenciales por defecto → ❌ Todas fallan
3. Muestra diálogo "Credentials Required"
4. Usuario hace click en "Skip"
5. Cámara NO se agrega a la lista
6. Continúa escaneando otras cámaras
```

---

## 🎮 Validación de Credenciales

### **Método de Validación**
La aplicación valida credenciales usando el comando ONVIF `GetProfiles`:

```csharp
var profileToken = await _ptzService.GetProfileTokenAsync(
    camera.IpAddress, 
    username, 
    password
);

if (!string.IsNullOrEmpty(profileToken))
{
    // ✅ Credenciales válidas
    // Agregar cámara
}
else
{
    // ❌ Credenciales inválidas
    // Solicitar otras credenciales
}
```

### **Puertos Probados**
Para cada conjunto de credenciales, prueba estos puertos:
- **10080** (ONVIF estándar alternativo)
- **8899** (V380)
- **80** (HTTP estándar)
- **8080** (HTTP alternativo)
- **8889** (Algunas cámaras)

---

## 🚀 Detección Automática de URL RTSP

Una vez autenticada, la aplicación:

1. **Obtiene ProfileToken** desde ONVIF
2. **Llama a GetStreamUri** para obtener URL RTSP real
3. **Guarda la URL** en `camera.CustomRtspUrl`
4. **Usa la URL correcta** al reproducir video

### **Ejemplo: Cámara en 192.168.1.76**
```
ONVIF GetStreamUri devuelve:
    rtsp://192.168.1.76:10554/tcp/av0_0

Aplicación agrega credenciales:
    rtsp://admin:Wegata76@192.168.1.76:10554/tcp/av0_0

VLC reproduce video exitosamente ✅
```

---

## 💾 Persistencia

Las credenciales y URLs se guardan en memoria durante la sesión:

```csharp
camera.Username = "admin";           // ONVIF
camera.Password = "Wegata76";        // ONVIF
camera.RtspUsername = "admin";       // RTSP
camera.RtspPassword = "Wegata76";    // RTSP
camera.CustomRtspUrl = "rtsp://..."; // URL desde ONVIF
camera.UseCustomRtspUrl = true;      // Usar URL personalizada
```

**Nota**: Las credenciales NO se persisten entre sesiones de la aplicación. Para implementar persistencia, se necesitaría un archivo de configuración o base de datos.

---

## 🎯 Ventajas del Sistema

✅ **Automático** - Prueba credenciales comunes primero  
✅ **Interactivo** - Solicita credenciales solo si es necesario  
✅ **Validado** - Verifica que las credenciales funcionen antes de agregar  
✅ **Flexible** - Permite saltar cámaras que no se pueden autenticar  
✅ **Inteligente** - Detecta URL RTSP correcta desde ONVIF  
✅ **Compatible** - Funciona con V380 y cámaras ONVIF estándar  

---

## 📝 Logs de Ejemplo

### **Cámara con Credenciales por Defecto (V380)**
```
Trying credentials admin/(empty) for 192.168.1.81...
✓ Found ProfileToken: PROFILE_000 (from http://192.168.1.81:8899/onvif/device_service)
✓ Authenticated with admin/(empty)
✓ RTSP URL obtained: rtsp://admin:@192.168.1.81/live/ch00_0
✓ Camera Camera (192.168.1.81) added to list
```

### **Cámara con Credenciales Personalizadas**
```
Trying credentials admin/(empty) for 192.168.1.76...
GetProfileToken failed for http://192.168.1.76:8899/onvif/device_service: Connection refused
GetProfileToken failed for http://192.168.1.76:10080/onvif/device_service: Unauthorized

Trying credentials admin/admin for 192.168.1.76...
GetProfileToken failed for http://192.168.1.76:10080/onvif/device_service: Unauthorized

Trying credentials admin/888888 for 192.168.1.76...
GetProfileToken failed for http://192.168.1.76:10080/onvif/device_service: Unauthorized

Trying credentials admin/12345 for 192.168.1.76...
GetProfileToken failed for http://192.168.1.76:10080/onvif/device_service: Unauthorized

⚠ Default credentials failed for 192.168.1.76, requesting manual input...

[Usuario ingresa admin/Wegata76 en el diálogo]

✓ Found ProfileToken: PROFILE_000 (from http://192.168.1.76:10080/onvif/device_service)
✓ Authenticated with manual credentials for 192.168.1.76
✓ RTSP URL obtained: rtsp://admin:Wegata76@192.168.1.76:10554/tcp/av0_0
✓ Camera Camera (192.168.1.76) added to list
```

---

## 🔧 Configuración Manual Posterior

Si necesitas cambiar las credenciales después:

1. **Click derecho** en la cámara
2. **"⚙️ Configure Credentials"**
3. Modifica credenciales
4. **"Save"**
5. Stream se reinicia automáticamente con nuevas credenciales

---

## ⚠️ Notas Importantes

- **Timeout**: Cada prueba de credenciales tiene timeout de 5 segundos
- **Múltiples cámaras**: Si se descubren varias cámaras, el diálogo aparece para cada una que requiera credenciales
- **Cancelación**: Puedes hacer "Skip" en cualquier cámara y continuar con las demás
- **Duplicados**: No agrega cámaras duplicadas (verifica por IP)

---

## 🎉 Resultado Final

**Experiencia de Usuario Mejorada:**

1. Click en "🔍 Scan Cameras"
2. Esperar unos segundos
3. Cámaras con credenciales estándar → **Agregadas automáticamente** ✅
4. Cámaras con credenciales personalizadas → **Diálogo aparece** 🔐
5. Ingresar credenciales → **Validadas y agregadas** ✅
6. Seleccionar cámara → **Video funciona con URL correcta** 🎥

**¡Todo automático y validado!**
