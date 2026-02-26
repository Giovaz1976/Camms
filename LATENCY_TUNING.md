# 🎯 Guía de Ajuste de Latencia

## Configuración Actual

Buffer de red: **50ms** (balance entre latencia y estabilidad)

## Opciones de Ajuste Manual

Si quieres experimentar con diferentes niveles de latencia, modifica estos valores en `MainWindow.xaml.cs`:

### Opción 1: Latencia Ultra-Baja (100-150ms)
```csharp
"--network-caching=50",
"--live-caching=50",
```
**Pros:** Latencia mínima  
**Contras:** Puede tener micro-cortes si la red es inestable

### Opción 2: Latencia Baja (200-250ms)
```csharp
"--network-caching=100",
"--live-caching=100",
```
**Pros:** Buen balance  
**Contras:** Latencia ligeramente mayor

### Opción 3: Estable (300-400ms)
```csharp
"--network-caching=200",
"--live-caching=200",
```
**Pros:** Muy estable, sin cortes  
**Contras:** Latencia más notoria

### Opción 4: Muy Estable (500ms+)
```csharp
"--network-caching=300",
"--live-caching=300",
```
**Pros:** Máxima estabilidad  
**Contras:** Delay notable

## Factores que Afectan la Latencia

1. **Red WiFi vs Ethernet**
   - WiFi: +20-50ms
   - Ethernet: Latencia mínima

2. **Calidad de la señal WiFi**
   - Señal débil = mayor latencia
   - Interferencias = micro-cortes

3. **Codec de la cámara**
   - H.264: Más compatible
   - H.265: Más comprimido pero mayor latencia de decodificación

4. **Hardware del PC**
   - GPU moderna: Decodificación acelerada
   - CPU antigua: Mayor latencia

## Recomendaciones

### Para Monitoreo en Tiempo Real
- Usa Ethernet si es posible
- Configura `network-caching=50`
- Acepta micro-cortes ocasionales

### Para Grabación/Visualización General
- WiFi está bien
- Configura `network-caching=200`
- Prioriza estabilidad

### Para Máxima Calidad
- Ethernet
- Configura `network-caching=100`
- Balance óptimo

## Solución de Problemas

### Video se congela frecuentemente
**Solución:** Aumenta el caching
```csharp
"--network-caching=200",
"--live-caching=200",
```

### Latencia muy alta
**Solución:** Reduce el caching
```csharp
"--network-caching=30",
"--live-caching=30",
```

### Video se ve entrecortado
**Solución:** Verifica la conexión de red y aumenta threads
```csharp
"--avcodec-threads=8",  // Más threads
```

## Configuración Óptima por Escenario

### Seguridad/Vigilancia (Prioridad: Tiempo Real)
```csharp
"--network-caching=50",
"--live-caching=50",
"--drop-late-frames",
"--skip-frames"
```

### Monitoreo General (Prioridad: Balance)
```csharp
"--network-caching=100",
"--live-caching=100",
"--avcodec-threads=4"
```

### Grabación/Archivo (Prioridad: Calidad)
```csharp
"--network-caching=300",
"--live-caching=300",
"--no-skip-frames"
```
