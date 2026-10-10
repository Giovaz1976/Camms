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

## Tema visual y diálogos modales (code-behind)

- **Tema oscuro es el default**: en `MainWindow.xaml.cs`, `_isDarkTheme = true` por defecto y
  `Window_Loaded` llama `ApplyTheme(_isDarkTheme)` al iniciar (antes `ApplyTheme` solo se
  disparaba desde el clic del botón, así que el oscuro nunca se aplicaba solo). El botón
  `BtnTheme` arranca como `☀️ Normal` (clic → tema normal/claro, colores Steam originales
  `#2C3E50`/`#34495E`) y alterna a `🌙 Oscuro` (`#171A21`/`#2A475E`/`#66C0F4`, Steam dark).
- **Patrón de bug recurrente en diálogos**: los diálogos modales (`BtnAddCamera_Click`,
  `ConfigurePtzCredentials`, `MenuConfigureCredentials_Click`, `ShowRtspCredentialsDialogAsync`,
  `ShowCredentialsDialogAsync`) se construyen 100% en C# (`new Window { ... }`, sin `.xaml`
  propio) con `ResizeMode.NoResize` y sin `ScrollViewer`. Si antes se les fijaba un `Height` a
  mano y el contenido real (títulos, separadores, hints) lo superaba, el panel de botones
  (Save/Cancel/OK) quedaba renderizado fuera del área visible y la ventana no se podía agrandar
  — pasó en "Camera Credentials" (RTSP+ONVIF). **Ya corregido en los 5 diálogos**: usan
  `SizeToContent = SizeToContent.Height` (solo `Width` fijo) en vez de `Height` adivinado. Si se
  agrega un diálogo nuevo en code-behind, usar `SizeToContent.Height` desde el inicio.
- **Estilos de inputs globales** (`Styles/ButtonStyles.xaml`, mergeados vía `App.xaml` →
  `Application.Resources`): además del `Style TargetType="Button"` ya existente, hay
  `Style TargetType="TextBox"` y `Style TargetType="PasswordBox"` sin `x:Key` — se aplican por
  defecto a todo el árbol, **incluidos los controles creados dinámicamente en code-behind** —
  con el mismo tema oscuro: fondo `#34495E`, texto blanco, borde `#1A252F` → `#66C0F4` al
  enfocar. Antes de esto, los `TextBox`/`PasswordBox` de los diálogos salían con el estilo
  blanco por defecto de Windows, inconsistente con el resto de la app.
  **Bug ya corregido (no reintroducir)**: estos dos estilos traían un `ControlTemplate` propio
  (`Border` con `CornerRadius="3"` conteniendo un `ScrollViewer x:Name="PART_ContentHost"`) para
  lograr esquinas redondeadas. Ese template impedía que el texto se **viera** — el valor sí
  llegaba a `Text` (confirmado leyendo `ValuePattern.Current.Value` vía UI Automation) pero no se
  pintaba ningún glifo en pantalla, en cualquier `TextBox`/`PasswordBox` de la app (no solo en
  "Add Camera Manually"; reproducido también en los campos de usuario/password de
  "Camera Credentials"). Diagnosticado reemplazando el `Style` por uno vacío
  (`new Style(typeof(TextBox))`) en un control de prueba: con el template nativo de WPF el texto
  sí se veía. Fix aplicado: se quitó el `Setter Property="Template"` de ambos estilos y se dejan
  solo los `Setter` de color sobre el `ControlTemplate` nativo (que si respeta
  `Background`/`Foreground`/`BorderBrush`), moviendo los triggers de `IsFocused`/`IsEnabled` de
  `ControlTemplate.Triggers` a `Style.Triggers`. Costo: se perdió el `CornerRadius` (esquinas ya
  no redondeadas en inputs). Si se quiere recuperar el redondeado con un `ControlTemplate`
  propio, **verificar primero en pantalla real** (no basta con que compile) que el texto
  efectivamente se renderiza antes de darlo por bueno — este bug pasó inadvertido porque
  compilaba limpio y UI Automation podía leer/escribir el valor sin problema.
- **Botones Save/Cancel en diálogos**: preferir los estilos ya definidos
  `{StaticResource SuccessButton}` (acción afirmativa: Save/Add/Connect) y
  `{StaticResource DangerButton}` (Cancel), asignados en code-behind con
  `Style = (Style)Application.Current.Resources["SuccessButton"]`, en vez de fijar
  `Background`/`Foreground`/`BorderThickness` a mano. Ya migrados así:
  `MenuConfigureCredentials_Click` y `BtnAddCamera_Click`. **Pendientes** (todavía con colores
  sueltos en vez de los estilos compartidos): los popups Skip/Save/Connect de
  `ShowRtspCredentialsDialogAsync` y `ShowCredentialsDialogAsync` (se disparan durante el scan
  de cámaras, no desde un botón/menú directo) y el diálogo `ConfigurePtzCredentials`.

## Drag & drop de cámaras entre paneles de video

`MainWindow.xaml.cs` soporta arrastrar cámaras hacia/entre los paneles de `VideoGrid`, además
del flujo original de selección múltiple en `LstCameras` (`LstCameras_SelectionChanged`):

- **Origen 1 — lista de cámaras**: `LstCameras_PreviewMouseLeftButtonDown`/`_PreviewMouseMove`
  detectan el arrastre (umbral `SystemParameters.MinimumHorizontalDragDistance`), resuelven el
  `ListBoxItem` bajo el cursor con `FindAncestor<T>` y arrancan `DragDrop.DoDragDrop` con el
  `CameraInfo` como payload. No requiere tener la cámara seleccionada — es un camino alterno al
  checkbox.
- **Origen 2 — un panel con video activo**: cada celda de `VideoGrid` (creada dinámicamente en
  `SetupGridLayout`) lleva un **overlay `Border` transparente** (`ZIndex 5`, entre el `VideoView`
  en `ZIndex 0` y el botón de audio en `ZIndex 10`) que es el verdadero origen del drag
  (`VideoPanel_PreviewMouseLeftButtonDown`/`_PreviewMouseMove`). Se usa un overlay en vez de
  enganchar los eventos directamente al `VideoView` porque el control de LibVLCSharp.WPF puede no
  propagar de forma confiable los eventos de mouse de WPF — si se agrega más interacción sobre el
  video, mantener este patrón de overlay en vez de intentar engancharse al `VideoView`.
- **Destino — cualquier panel**: el `Border` exterior de la celda es `AllowDrop=true` con
  `Drop`/`DragEnter`/`DragLeave` (`VideoPanel_*`); `Tag` guarda el índice de esa celda en
  `_cameraViews` (asignado en `SetupGridLayout` como `panelIndex = _cameraViews.Count - 1`, mismo
  orden en que se crean las celdas).
- **La reasignación es lógica, no física**: nunca se mueve el `VideoView`/`MediaPlayer` entre
  celdas del grid (riesgo de reparenting con el control de LibVLCSharp). En su lugar,
  `AssignCameraToPanel` hace `StopCamera` (detiene/dispone el stream, resetea `Camera` a un
  `CameraInfo` vacío) seguido de `StartCamera` con la cámara nueva en esa misma celda — es decir,
  se reinicia la conexión RTSP de la celda, no se transporta el control visual. Al soltar un panel
  sobre otro panel ocupado se hace swap (dos `AssignCameraToPanel`); sobre un panel vacío, se
  mueve.
- **Limitación conocida, no resuelta**: `LstCameras_SelectionChanged` sigue reconstruyendo *todo*
  el layout por orden de selección cada vez que cambia la selección múltiple en la lista. Si el
  usuario reacomoda paneles por drag-and-drop y luego toca el checkbox de alguna cámara, ese
  reacomodo manual se pierde. No se intentó resolver esto (requeriría distinguir "modo manual" vs
  "modo por selección"); tenerlo en cuenta si se pide hacer convivir ambos flujos.

## VideoView en blanco al redimensionar (fix aplicado)

`LibVLCSharp.WPF.VideoView` no renderiza con el compositor de WPF: internamente crea una ventana
nativa de verdad (campo privado `_videoHwndHost`, tipo `VideoHwndHost : HwndHost`) y le entrega el
handle a libvlc para que pinte el video ahí directamente ("airspace" — confirmado con un
mini-proyecto de reflexión apuntando al paquete `LibVLCSharp.WPF` 3.8.5). Ningún `Background` que
se fije en nuestro XAML/code-behind (el `Border` del grid de video, el `cellGrid`, el `VideoView`
mismo) tiene efecto sobre lo que realmente se ve, porque esa ventana nativa pinta por su cuenta.

- **Síntoma que esto causaba**: mientras una celda del `VideoGrid` no tiene ningún stream
  reproduciéndose, la ventana nativa muestra el brush de fondo *por defecto de Windows* (gris
  claro) en vez de quedarse transparente/oscura — y lo repinta así cada vez que Windows le manda
  `WM_ERASEBKGND`, lo cual pasa en **cualquier resize** (se confirmó que ocurre igual
  redimensionando la ventana a mano, sin usar el botón de ocultar/mostrar lista — no era un bug
  de ese botón sino preexistente; con `SetupGridLayout` no se notaba porque esa función destruye y
  recrea los `Border` de cada celda desde cero en vez de solo re-medir el existente).
- **Fix (`DarkenVideoViewNativeBackground`, cerca de `SetupGridLayout` en
  `MainWindow.xaml.cs`)**: como no hay API pública para fijar ese color, se cambia el brush de
  fondo de la **clase** de ventana nativa vía `SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, brush)` en
  cuanto el `VideoView` dispara su evento `Loaded` (momento en el que `_videoHwndHost.Handle` ya es
  válido). Se accede a `_videoHwndHost` por reflexión porque no es un campo público — por eso todo
  el método está en un único `try/catch`: si una futura versión de LibVLCSharp cambia ese nombre de
  campo interno, el fix simplemente no hace nada (no rompe la app, solo deja de aplicar el
  oscurecido). Se llama una vez por cada `VideoView` creado, justo después de
  `videoView.MediaPlayer = mediaPlayer;`.
- **Por qué no se intentó crear los `VideoView` de forma perezosa** (solo al asignar una cámara a
  esa celda) como alternativa: habría evitado el bug de raíz (sin ventana nativa, no hay nada que
  pintar de blanco) pero implica repensar todo el código que ya asume que `_cameraViews[i]` siempre
  tiene `MediaPlayer`/`AudioButton` listos desde que se crea el grid (selección por lista,
  drag-and-drop entre paneles, toggle de audio) — cambio mucho más invasivo para lo que era un bug
  cosmético puntual.

## Cierre de la aplicación (liberar conexiones a las cámaras)

`MainWindow.xaml.cs` centraliza todo el apagado en **`CleanupResources()`**, invocado tanto desde
`Window_Closing` como desde `OnClosed` (ésta última como red de seguridad; es idempotente vía el
flag `_isDisposing`). Antes había dos métodos separados y duplicados con lógica contradictoria —
`Window_Closing` llegaba a disponer `_libVLC` explícitamente mientras `OnClosed` evitaba
disponerlo a propósito ("para evitar un AccessViolationException") — lo que en realidad invertía
el orden correcto de LibVLCSharp (hay que detener/disponer **todos** los `MediaPlayer` antes de
disponer el `LibVLC` del que dependen, nunca al revés).

- **Orden correcto ya aplicado en `CleanupResources()`**: cancelar scan en curso → detener
  grabaciones *sin reiniciar el stream* (`StopAllRecordings(restartStream: false)`, ver abajo) →
  `MediaPlayer.Stop()` de cada panel (dispara el TEARDOWN RTSP hacia la cámara) → pausa de 200ms →
  `Dispose()` de cada `Media`/`MediaPlayer` → sólo entonces `_libVLC.Dispose()` → por último
  `_discovery` (V380), `_onvifDiscovery` y `_ptzService` (cierran sus sockets UDP/HttpClient).
- **`StopAllRecordings(bool restartStream = true)`**: el uso normal (toggle del botón Record)
  reinicia el stream sin las opciones de grabación para seguir viendo en vivo — eso requiere
  volver a llamar `StartCamera`. Al cerrar la ventana eso es contraproducente (abriría conexiones
  nuevas mientras se intenta cerrar todo), por eso el cierre pasa `restartStream: false`.
- **Bug encontrado y corregido — proceso zombie al cerrar con una conexión en curso**: si se
  cierra la ventana mientras `StartCamera` sigue esperando una respuesta ONVIF (`await
  _ptzService.GetProfileTokenAsync(...)` / `GetRtspUrlAsync(...)`), esa continuación reanuda en el
  Dispatcher *después* de `CleanupResources()`, con `_libVLC` ya en `null` — y caía en
  `MessageBox.Show("LibVLC no está inicializado", ...)`, un diálogo modal sin ventana dueña visible
  que dejaba el proceso colgado indefinidamente (visible en `tasklist`, sin nada en pantalla;
  reproducido y confirmado con `EnumWindows` encontrando un hwnd `visible=True title='Error'`
  huérfano). Fix: `StartCamera` ahora chequea `_isDisposing` al entrar y de nuevo justo después de
  los `await`, retornando en silencio si la app ya está cerrando — sin tocar `MediaPlayer`/mostrar
  diálogos. Al reproducir este escenario (agregar cámara con IP inalcanzable, seleccionarla, y
  cerrar la ventana de inmediato mientras el ONVIF sigue en vuelo) es la forma más fácil de
  verificar una regresión aquí.
- **Ruido cosmético conocido, no corregido**: `OnvifPtzService.GetProfileTokenAsync` prueba varios
  endpoints/puertos secuencialmente con su propio try/catch interno; si la ventana se cierra a
  mitad de esa secuencia, los intentos restantes loguean `Cannot access a disposed object` (su
  `HttpClient` ya fue disposed por `CleanupResources()`) antes de terminar. No crashea ni cuelga el
  cierre, solo ensucia la consola. Si se quiere eliminar del todo, habría que enhebrar un
  `CancellationToken` a través de `StartCamera` → `GetProfileTokenAsync`/`GetRtspUrlAsync`.

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
