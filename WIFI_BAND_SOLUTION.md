# 📡 Solución: Problema de Bandas WiFi 2.4 GHz vs 5 GHz

## 🔍 Problema Identificado

**Configuración actual**:
- 🖥️ **PC**: Conectado a `INFINITUM6A0F_5` (5 GHz)
- 📹 **Cámara .76**: En red 5 GHz ✅ (accesible)
- 📹 **Cámara .81**: En red 2.4 GHz ❌ (NO accesible)

**Causa**: Aislamiento entre bandas WiFi 2.4 GHz y 5 GHz

---

## ✅ Soluciones Disponibles

### Opción 1: Cambiar PC a 2.4 GHz (Más Rápido) ⭐

**Ventajas**:
- ✅ Solución inmediata
- ✅ Acceso a ambas cámaras
- ✅ No requiere configurar router

**Desventajas**:
- ❌ Internet más lento
- ❌ Hay que cambiar manualmente

**Cómo hacerlo**:
```powershell
# Ejecutar script
.\connect_to_24ghz.ps1

# O manualmente:
netsh wlan connect name="INFINITUM6A0F_2.4"
```

**Para volver a 5 GHz**:
```powershell
.\connect_to_5ghz.ps1
```

---

### Opción 2: Configurar Router (Mejor Solución) ⭐⭐⭐

**Desactivar aislamiento entre bandas**:

1. **Acceder al router**:
   ```
   http://192.168.1.1
   Usuario: admin (o TELMEX)
   Contraseña: (en etiqueta del router)
   ```

2. **Buscar configuración WiFi**:
   - Red Inalámbrica → Configuración Avanzada
   - Wireless → Advanced Settings

3. **Desactivar**:
   - ❌ AP Isolation
   - ❌ Client Isolation
   - ❌ Wireless Isolation

4. **Activar** (si está disponible):
   - ✅ Band Steering
   - ✅ Smart Connect
   - ✅ Roaming entre bandas

5. **Guardar y reiniciar router**

**Resultado**: PC en 5 GHz puede comunicarse con cámaras en 2.4 GHz

---

### Opción 3: Mover Cámara .81 a 5 GHz ⭐⭐

**Pasos**:

1. **Conectar PC a 2.4 GHz temporalmente**:
   ```powershell
   .\connect_to_24ghz.ps1
   ```

2. **Acceder a cámara .81**:
   - Buscar IP en router o app
   - Acceder vía web o app móvil

3. **Cambiar configuración WiFi**:
   - Network → WiFi Settings
   - Seleccionar: `INFINITUM6A0F_5`
   - Ingresar contraseña
   - Guardar y reiniciar

4. **Volver PC a 5 GHz**:
   ```powershell
   .\connect_to_5ghz.ps1
   ```

**Resultado**: Ambas cámaras en 5 GHz, mejor rendimiento

---

### Opción 4: Usar Cable Ethernet (Óptimo) ⭐⭐⭐⭐

**Conectar PC por cable al router**:

**Ventajas**:
- ✅ Acceso a todas las redes WiFi
- ✅ Mejor rendimiento para streaming
- ✅ Sin latencia
- ✅ Conexión más estable

**Desventajas**:
- ❌ Requiere cable físico

---

## 🛠️ Scripts Disponibles

### Para Cambiar de Red

```powershell
# Conectar a 2.4 GHz (para acceder a cámara .81)
.\connect_to_24ghz.ps1

# Volver a 5 GHz (internet más rápido)
.\connect_to_5ghz.ps1

# Ver redes disponibles
.\switch_wifi_network.ps1

# Verificar banda actual
.\check_wifi_band.ps1
```

---

## 📋 Guía Paso a Paso Recomendada

### Para Uso Diario con CameraViewer

**Opción A: Trabajar en 2.4 GHz**
```powershell
# Al iniciar el día
.\connect_to_24ghz.ps1

# Usar CameraViewer con ambas cámaras
dotnet run --project CameraViewer

# Al terminar (si necesitas internet rápido)
.\connect_to_5ghz.ps1
```

**Opción B: Configurar Router (Una sola vez)**
1. Acceder a router
2. Desactivar aislamiento de bandas
3. Guardar
4. Listo - siempre funciona

---

## 🔧 Comandos Útiles

### Información de Red Actual
```powershell
# Ver red WiFi actual
netsh wlan show interfaces

# Ver todas las redes disponibles
netsh wlan show networks

# Ver canal (determina banda)
netsh wlan show interfaces | Select-String "Channel"
```

### Conectar Manualmente
```powershell
# Desconectar
netsh wlan disconnect

# Conectar a 2.4 GHz
netsh wlan connect name="INFINITUM6A0F_2.4"

# Conectar a 5 GHz
netsh wlan connect name="INFINITUM6A0F_5"
```

### Probar Cámaras
```powershell
# Ping a cámaras
Test-Connection 192.168.1.76 -Count 1
Test-Connection 192.168.1.81 -Count 1

# Script completo
.\test_camera_quick.ps1 -IP "192.168.1.76"
.\test_camera_quick.ps1 -IP "192.168.1.81"
```

---

## 📊 Comparación de Opciones

| Opción | Dificultad | Tiempo | Permanente | Velocidad Internet |
|--------|------------|--------|------------|-------------------|
| Cambiar a 2.4 GHz | ⭐ Fácil | 1 min | ❌ No | 🐌 Lenta |
| Configurar Router | ⭐⭐ Media | 10 min | ✅ Sí | 🚀 Rápida |
| Mover cámara a 5G | ⭐⭐ Media | 15 min | ✅ Sí | 🚀 Rápida |
| Cable Ethernet | ⭐ Fácil | 2 min | ✅ Sí | 🚀 Rápida |

---

## 🎯 Recomendación Final

**Para uso inmediato**: Ejecuta `.\connect_to_24ghz.ps1`

**Para solución permanente**: Configura el router para desactivar aislamiento entre bandas

**Para mejor rendimiento**: Usa cable Ethernet en el PC

---

## ❓ Preguntas Frecuentes

**P: ¿Por qué la cámara .76 funciona y la .81 no?**  
R: La .76 está en 5 GHz (misma red que tu PC), la .81 está en 2.4 GHz (red aislada)

**P: ¿Perderé velocidad en 2.4 GHz?**  
R: Sí, 2.4 GHz es más lenta (hasta 300 Mbps vs 1300 Mbps en 5 GHz), pero suficiente para streaming de cámaras

**P: ¿Puedo tener ambas cámaras accesibles sin cambiar de red?**  
R: Sí, configurando el router para permitir comunicación entre bandas, o usando cable Ethernet

**P: ¿Qué pasa si no tengo acceso al router?**  
R: Usa los scripts para cambiar entre redes según necesites, o conecta por cable

---

## 📝 Notas

- Los scripts guardan la configuración de red
- Puedes crear accesos directos para cambiar rápidamente
- El router Infinitum permite configurar comunicación entre bandas
- Cable Ethernet es siempre la mejor opción para streaming de video

---

**Última actualización**: 19 de Junio, 2026
