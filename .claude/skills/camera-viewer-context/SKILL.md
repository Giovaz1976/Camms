---
name: camera-viewer-context
description: Contexto arquitectónico y de convenciones del proyecto CameraViewer (visor WPF de cámaras IP locales V380/ONVIF). Usar al trabajar en este repo — implementar features, refactorizar, escribir tests o revisar diseño — para no tener que re-derivar la estructura del proyecto desde cero.
---

# CameraViewer — Contexto del proyecto

Aplicación de escritorio **WPF (.NET 8.0, C#)** para visualizar cámaras IP (principalmente
**V380** y protocolo **ONVIF**) directamente en la red local, sin pasar por la nube del
fabricante. Es un proyecto de **ingeniería inversa de protocolo** (ver `README.md`): los
paquetes V380 reales se capturan con Wireshark y se van reemplazando placeholders en
`V380Discovery.cs` / `V380Connection.cs`.

## Estructura de la solución

```
CameraViewer.sln
├── CameraViewer/                  # App WPF principal
│   ├── App.xaml.cs                # Composition root: DI container (Microsoft.Extensions.DependencyInjection)
│   ├── MainWindow.xaml(.cs)       # Única ventana; code-behind con lógica de UI (ver deuda técnica)
│   ├── Configuration/             # POCOs de configuración (AppSettings, CameraSettings, StreamingSettings, OnvifDiscoverySettings)
│   ├── Interfaces/                # Abstracciones por área: Configuration, Discovery, Logging, Network, Parsing
│   ├── Implementation/            # Implementaciones concretas de esas interfaces (misma subdivisión por carpeta)
│   ├── Models/                    # CameraInfo, CameraView, StreamQuality (POCOs, sin lógica)
│   ├── Services/                  # OnvifDiscovery, OnvifPtzService, V380Discovery, V380Connection
│   ├── ViewModels/                # MainViewModel (CommunityToolkit.Mvvm, ObservableObject) + ViewModelBase
│   ├── Styles/ButtonStyles.xaml   # Tema "Steam-like" (ver STEAM_THEME_GUIDE.md)
│   └── appsettings.json           # Config externa (secciones: Logging, OnvifDiscovery, Camera, Streaming, PTZ, UI, Network)
└── CameraViewer.Tests/            # xUnit/MSTest — Implementation/{Configuration,Logging,Parsing} + ViewModels/MainViewModelTests
```

## Decisiones de arquitectura clave

- **DI manual en `App.xaml.cs`** (`ConfigureServices`): construye `ServiceCollection`, carga
  `appsettings.json` con `Microsoft.Extensions.Configuration`, bindea `AppSettings` y registra
  cada sub-sección (`OnvifDiscoverySettings`, `CameraSettings`, `StreamingSettings`, etc.) como
  singleton individual para inyección granular. `MainWindow` y `MainViewModel` se resuelven desde
  el contenedor, no se instancian con `new`.
- **MVVM**: `ViewModelBase : ObservableObject` (CommunityToolkit.Mvvm). `MainViewModel` recibe
  `OnvifDiscovery` y `V380Discovery` por constructor y se suscribe a sus eventos
  `CameraDiscovered`. Ver `MVVM_PATTERN_IMPLEMENTATION.md`.
- **Interfaces + Implementation separados por carpeta espejo** (patrón del repo): cada capability
  tiene `Interfaces/<Area>/I<Nombre>.cs` y `Implementation/<Area>/<Nombre>.cs`. Al añadir una
  nueva pieza de infraestructura (red, parsing, logging, discovery, configuración), seguir ese
  mismo patrón en vez de meter la implementación directa en `Services/`.
- **Logging**: `ILogger` con dos implementaciones, `DebugLogger` (usa `Debug.WriteLine`, prefijo
  configurable, p.ej. `"[ONVIF]"`) y `NullLogger` (no-op, útil en tests).
- **Discovery ONVIF** ya refactorizado en piezas SOLID: `IOnvifMulticastDiscovery`,
  `IOnvifPortScanner`, `IOnvifMessageBuilder`/`OnvifSoapMessageBuilder`,
  `IOnvifResponseParser`/`OnvifResponseParser`. La clase legacy `Services/OnvifDiscovery.cs` se
  mantiene como wrapper de compatibilidad hacia atrás que compone `IOnvifMulticastDiscovery` +
  `IOnvifPortScanner` — no añadir lógica nueva ahí, va en las interfaces/implementaciones.
- **V380 (propietario, no documentado)**: `Services/V380Discovery.cs` y `V380Connection.cs` SÍ
  siguen con lógica placeholder/hardcoded pendiente de ingeniería inversa — no tratarlos como
  "terminados"; cualquier cambio debe basarse en capturas reales (`.pcap` en la raíz, ver
  `PROTOCOL_NOTES.txt` y `CAPTURE_GUIDE.md`).
- **PTZ**: `Services/OnvifPtzService.cs` (405 líneas) es el archivo de servicio más grande;
  candidato a dividirse si se le añade más responsabilidad.

## Configuración (`appsettings.json`)

Secciones y su POCO correspondiente en `Configuration/`:
`Logging`, `OnvifDiscovery` (multicast, puertos alternativos, rangos de escaneo de IP),
`Camera` (credenciales/timeouts por defecto), `Streaming` (buffer/codec/hardware decoding vía
LibVLCSharp), `PTZ` (velocidades), `UI`, `Network`. Ver `APPSETTINGS_CONFIGURATION.md` para el
detalle de cada campo. Nunca hardcodear estos valores en código nuevo — inyectar el `*Settings`
correspondiente.

## Deuda técnica conocida (documentada por el propio repo, no inferida)

`SOLID_ANALYSIS.md` da una autoevaluación con nota general 6/10:
- **SRP parcial**: `MainWindow.xaml.cs` todavía mezcla lógica de UI con descubrimiento de red
  (manipulación de IPs, actualización de botones, llamadas a discovery en el mismo handler).
- **ISP/DIP débiles** (3/10 y 4/10): quedan puntos donde se depende de clases concretas en lugar
  de interfaces (p. ej. `MainViewModel` depende de `OnvifDiscovery`/`V380Discovery` concretas, no
  de una interfaz `ICameraDiscovery` aunque esta ya existe en `Interfaces/Discovery/`).
- Si se pide "aplicar SOLID" o "mejorar arquitectura", **leer primero `SOLID_ANALYSIS.md`**: ya
  trae el diagnóstico y las clases recomendadas; no repetir el análisis desde cero.

## Stack y paquetes relevantes

- `.NET 8.0-windows`, WPF (`UseWPF=true`), `Nullable=enable`.
- `CommunityToolkit.Mvvm` 8.4.2 (MVVM / ObservableObject / comandos).
- `LibVLCSharp` + `LibVLCSharp.WPF` + `VideoLAN.LibVLC.Windows` para decodificación/render de video
  (no FFmpeg.AutoGen pese a lo que dice `README.md` — el README está desactualizado en ese punto).
- `Microsoft.Extensions.Configuration.{Binder,Json}` + `Microsoft.Extensions.DependencyInjection`.
- `System.Drawing.Common` para procesamiento de imagen/snapshot.

## Tests

`CameraViewer.Tests` cubre hoy: `Implementation/Configuration` (OnvifDiscoveryConfiguration),
`Implementation/Logging` (DebugLogger, NullLogger), `Implementation/Parsing`
(OnvifResponseParser, OnvifSoapMessageBuilder) y `ViewModels/MainViewModelTests`. **No hay tests**
de `Services/V380*.cs`, `OnvifPtzService`, ni de los discovery de `Implementation/Discovery/`
(`OnvifMulticastDiscovery`, `OnvifPortScanner`) — tenerlo en cuenta si se pide "aumentar
cobertura".

## Documentación auxiliar en la raíz (no duplicar, referenciar)

El repo acumula muchos `.md` de bitácora de features/fixes puntuales (p. ej.
`AUDIO_IMPLEMENTATION.md`, `ONVIF_DISCOVERY_FIX.md`, `THEME_IMPLEMENTATION.md`,
`LATENCY_TUNING.md`, `WIFI_BAND_SOLUTION.md`, `CAMERA_76_SOLUTION.md`, etc.) y varios scripts
`.ps1`/`.py` sueltos para diagnóstico de red/cámaras (escaneo, verificación de credenciales,
pruebas RTSP/ONVIF). Son historial de troubleshooting específico de hardware, no arquitectura
general — si una tarea toca un tema que ya tiene su propio `.md`, leerlo primero en vez de
reinvestigar (p. ej. problemas de descubrimiento → `ONVIF_DISCOVERY_FIX.md` /
`ONVIF_TROUBLESHOOTING.md`; audio → `AUDIO_FEATURE_SUMMARY.md` / `AUDIO_IMPLEMENTATION.md`; UI/tema
→ `STEAM_THEME_GUIDE.md`, `THEME_FIX.md`, `BUTTON_REDESIGN_*.md`).

## Build / Run

```bash
dotnet restore
dotnet build
dotnet run --project CameraViewer
```

Requiere Windows (WPF) + .NET 8.0 SDK. `appsettings.json` se copia al output dir
(`CopyToOutputDirectory=PreserveNewest`).
