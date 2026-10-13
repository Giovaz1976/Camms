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

## Desacoplar un panel de video a su propia ventana ("pop out")

Cada celda del `VideoGrid` (creada en `SetupGridLayout`) tiene un botón "↗" que llama a
`PopOutVideo(cameraView, cellGrid, videoView, popOutPlaceholder)`: mueve el `VideoView` de esa
celda a una `Window` nueva e independiente (no modal, sin `Owner` — ver más abajo por qué). El
`MediaPlayer` nunca se toca — sigue siendo el mismo objeto esté donde esté montado el `VideoView`,
así que la reproducción no se interrumpe al mover el control.

- **Volver a anclar = cerrar la ventana flotante**: no hay un botón "dock back" aparte; el
  `Closing` de la ventana flotante reinserta el `VideoView` en su `cellGrid` original (capturado
  por closure) y oculta el placeholder "↗ Shown in a separate window" que mientras tanto ocupa su
  lugar en la celda.
- **`_poppedOutWindows` (`Dictionary<CameraView, Window>`)**: registro de qué cámara está
  despegada en qué ventana. `ClosePoppedOutWindows()` cierra todas y se llama al inicio de
  `SetupGridLayout` (cambio de layout) y al inicio de `CleanupResources()` (cierre de la app —
  `_isDisposing` ya en `true` evita que el `Closing` de cada ventana intente re-anclar nada).
  Verificado: cerrar la app con una cámara despegada no cuelga el proceso ni lanza excepciones.
- **Bug real encontrado (ya corregido) — el botón dejaba de responder justo al empezar a verse
  video real**: reportado por el usuario y reproducido con sus propias palabras — "debo dar clic
  antes de que aparezca el video, de lo contrario, si el video ya está desplegado, el botón no
  funciona [...] incluso aunque cambie el tamaño de la ventana". Mis pruebas previas solo habían
  usado IPs falsas/inalcanzables (nunca llegaba a reproducirse un frame real), por eso nunca until
  then lo reproduje. **Causa raíz confirmada**: `VideoView` pinta con una ventana nativa propia
  (`HwndHost`, ver sección siguiente); mientras el panel está vacío/"conectando", WPF puede
  recortar esa ventana nativa para que los controles de WPF superpuestos (como el botón "↗" o
  `audioButton`, ambos con `ZIndex` más alto) sigan recibiendo el mouse input con normalidad — pero
  en cuanto hay **video real reproduciéndose**, la ventana nativa parece recuperar la captura del
  mouse de toda su superficie a nivel de Windows, por debajo del Z-order lógico de WPF, y ya no
  suelta esa captura ni con un resize posterior. Intentos previos que NO sirvieron para esto
  (no reintentar): cambiar `Click` por `PreviewMouseLeftButtonDown` en el botón — el problema no es
  qué evento se escucha, es que el clic directamente no llega al `Button` en absoluto mientras
  esté superpuesto al video activo.
  - **Fix real: dejar de superponer los botones al video.** `cellGrid` pasó de ser un área única a
    un `Grid` de 2 filas — fila 0 (`Auto`) es una franja (`buttonStrip`, `StackPanel` horizontal,
    fondo `#1A252F`) con `popOutButton` y `audioButton`, FUERA del área de video; fila 1 (`*`) es
    el `VideoView` + `idleOverlay` + `dragOverlay` + `popOutPlaceholder`, igual que antes. Al no
    haber overlap entre los botones y el `VideoView`, no hay ventana nativa de por medio que pueda
    robarse el click — por eso `popOutButton` volvió a usar `Click` normal (ya no hace falta
    `PreviewMouseLeftButtonDown`).
  - **Riesgo no resuelto, mismo origen**: `dragOverlay` (usado para iniciar el arrastre de un panel
    con video activo entre celdas, ver sección de drag-and-drop) **sí** sigue superpuesto al video
    en la fila 1 — no se puede sacar de ahí porque la interacción requiere tocar el video mismo
    para iniciar el drag. Si se reporta que arrastrar un panel con video real activo deja de
    funcionar, es la misma causa de raíz que esto; no tiene una solución tan simple como reubicar
    un botón.
  - **Lección sobre diagnóstico en esta app**: `CameraViewer.csproj` tiene `OutputType=WinExe`, o
    sea que **no hay consola adjunta** corriendo normalmente — `Console.WriteLine` no se ve en uso
    real (solo bajo un depurador adjunto). Para diagnósticos visibles al usuario sin pedirle que
    corra la app de un modo especial, usar `TxtStatus.Text` (la barra de estado de la ventana
    principal, siempre visible) en vez de `Console.WriteLine`. `PopOutVideo` ya sigue este patrón.
- **Sin `Owner` a propósito**: una ventana `Window` con `Owner = this` se **oculta** (no solo se
  minimiza, `IsWindowVisible` da `false`) en cuanto se minimiza su dueña — confirmado
  empíricamente. El usuario pidió lo contrario (que el video del pop-out siga visible aunque la
  ventana principal esté minimizada), así que el pop-out se crea sin `Owner`. El cierre al salir de
  la app sigue funcionando igual porque `CleanupResources()` cierra cada ventana flotante por
  código (`ClosePoppedOutWindows()`), no depende de la relación `Owner` de Win32.
- **Limitación cosmética conocida, no resuelta**: el fix de fondo oscuro (ver sección siguiente)
  cubre cada celda del `VideoGrid` pero **no** la ventana flotante del pop-out — ésta solo
  contiene el `VideoView` desnudo, sin el `idleOverlay`. Si la cámara despegada todavía está
  "conectando" (p. ej. IP inválida, nunca llegó un frame real), la ventana flotante puede mostrar
  el fondo claro nativo de Windows hasta que llegue un frame real o se cierre. No se consideró
  grave como para replicar el overlay ahí también — si se retoma, ver la sección siguiente para
  el patrón a seguir (cubrir con un `Border` oscuro, no pelear con la ventana nativa).

## VideoView en blanco mientras no hay stream (fix aplicado: overlay, no P/Invoke)

`LibVLCSharp.WPF.VideoView` no renderiza con el compositor de WPF: internamente crea una ventana
nativa de verdad (confirmado con un mini-proyecto de reflexión apuntando al paquete
`LibVLCSharp.WPF` 3.8.5: campo privado `_videoHwndHost`, tipo `VideoHwndHost : HwndHost`) y le
entrega el handle a libvlc para que pinte ahí directamente ("airspace"). Mientras no hay ningún
stream reproduciéndose esa ventana nativa muestra el fondo claro por defecto de Windows en vez de
quedarse oscura — y punto importante: **ningún `Background` fijado en nuestro XAML/code-behind
tiene efecto sobre ella**, porque pinta por su cuenta, fuera del árbol visual de WPF.

- **Primer intento (abandonado, no reintentar)**: cambiar el brush de fondo de la *clase* de
  ventana nativa vía P/Invoke (`SetClassLongPtr`/`GCLP_HBRBACKGROUND`), accediendo a
  `_videoHwndHost` por reflexión. Funcionaba para el caso simple (ventana recién creada, resize
  suave) pero **no sobrevivía a un resize "agresivo"** — múltiples resizes seguidos en rápida
  sucesión, como arrastrar el `GridSplitter` del panel de cámaras (ver sección de abajo) — ni a
  reintentos con `Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ...)`, ni a reaplicarlo en
  cada `SizeChanged`. Se retiró todo ese código (ya no existe `DarkenVideoViewNativeBackground`
  ni los `DllImport` de `user32`/`gdi32` en `MainWindow.xaml.cs`).
- **Fix actual, mucho más simple y robusto**: en vez de pelear con la ventana nativa, **taparla**
  con un `Border` de WPF normal (`CameraView.IdleOverlay`, creado en `SetupGridLayout` con
  `ZIndex=2`, entre el `VideoView` en `ZIndex 0` y el resto de botones/overlays en `ZIndex 1/5/10`)
  — pintado por el compositor de WPF como cualquier otro control, así que no le afecta nada de lo
  de arriba. `StartCamera` lo colapsa (`Visibility.Collapsed`) justo después de
  `MediaPlayer.Play(media)` (hay dos puntos donde se llama `Play` — el flujo normal y
  `StartAllRecordings` — los dos ocultan el overlay); `StopCamera` lo vuelve a mostrar en su
  `finally`. Verificado con el `GridSplitter` (resize continuo arrastrando) y con una cámara
  "conectando" (IP inválida, nunca llega un frame real): en ambos casos el panel se queda oscuro.
- **Por qué no se intentó crear los `VideoView` de forma perezosa** (solo al asignar una cámara a
  esa celda) como alternativa: habría evitado el bug de raíz desde otro ángulo (sin ventana nativa
  creada, no hay nada que pintar de blanco) pero implica repensar todo el código que ya asume que
  `_cameraViews[i]` siempre tiene `MediaPlayer`/`AudioButton`/`IdleOverlay` listos desde que se
  crea el grid (selección por lista, drag-and-drop entre paneles, toggle de audio, pop-out) —
  cambio mucho más invasivo que agregar un `Border` tapando la celda.

## Panel de lista de cámaras redimensionable

La columna de `CameraListPanel` (`ColCameraList` en `MainWindow.xaml`, `Grid.Column="0"` del
Main Content Grid) tiene `MinWidth="150"`/`MaxWidth="500"` y se redimensiona arrastrando un
`GridSplitter` (`CameraListSplitter`, `Grid.Column="1"`, `ResizeBehavior="PreviousAndNext"`) entre
ella y el área de video (`Grid.Column="2"`, antes era la columna 1 — se corrió al agregar la del
splitter). El área de video tiene su propio `MinWidth="300"` para que el splitter no la pueda
achicar hasta desaparecer.

- **Interacción con `BtnToggleCameraList` (ocultar/mostrar lista)**: el toggle no usa el ancho fijo
  250 a secas — `_lastCameraListWidth` guarda `ColCameraList.ActualWidth` justo antes de ocultar
  (sea cual sea el ancho que haya quedado tras arrastrar el splitter) y lo restaura tal cual al
  volver a mostrar. Al ocultar, además de `Visibility.Collapsed` en `CameraListPanel` y en
  `CameraListSplitter`, hay que poner `ColCameraList.MinWidth = 0` temporalmente — si no, el
  `MinWidth="150"` de la `ColumnDefinition` evita que el ancho llegue a 0 aunque se le asigne
  `GridLength(0)`. Se restaura a 150 al volver a mostrar.

## Diagnóstico de PTZ ("el control de PTZ no funciona")

`Services/OnvifPtzService.cs` prueba, para cada comando, una lista de 8-9 URLs candidatas
(combinaciones de puerto/path comunes entre fabricantes — V380 usa `:8899`, otras `:10080`,
`:80`, etc.) porque no hay forma de saber de antemano cuál usa una cámara dada ("ingeniería
inversa", igual que V380 — ver sección inicial del SKILL). El botón **"🔧 Test PTZ"** (dentro del
panel PTZ, visible solo con una cámara seleccionada) ya hace un auto-diagnóstico — úsalo primero
antes de reportar "no funciona" sin más detalle.

- **Dos bugs reales encontrados y corregidos en esta sesión** (no hipótesis — reproducidos y
  verificados con un servidor HTTP local simulando una cámara ONVIF, ver más abajo):
  1. **El diálogo de `BtnTestPtz_Click` mostraba `\n` literal en vez de saltos de línea** (usaba
     `\\n` dentro de un string normal de C#, no verbatim — produce backslash+n literal, no un
     salto de línea real). Corregido a `\n` simple.
  2. **Cada comando PTZ —incluido mantener presionada una flecha de movimiento— volvía a probar
     las ~9 URLs candidatas desde cero**, sin recordar cuál funcionó la vez anterior. Con el
     `HttpClient.Timeout` original de 5s, esto significaba hasta ~45s solo para encontrar el
     endpoint correcto, **en cada pulsación**. Reproducido con una IP inalcanzable de prueba: el
     botón "Test PTZ" completo (que hace ProfileToken + 2 movimientos + 2 stops) tardó **~165
     segundos** en reportar el fallo — tiempo más que suficiente para que cualquier usuario
     concluya "no funciona" y abandone antes de que aparezca el resultado.
  - **Fix**: `OnvifPtzService` ahora guarda, por IP de cámara, qué endpoint respondió bien la
    última vez (`_knownPtzEndpoints` para comandos de movimiento, `_knownProfileEndpoints` para
    `GetProfileToken` — por separado porque algunas cámaras los sirven en puertos distintos) y lo
    prueba primero en la siguiente llamada. Si ese endpoint cacheado deja de responder (cámara
    reiniciada, etc.), simplemente cae al resto de la lista como antes y el caché se actualiza
    solo. También se bajó `HttpClient.Timeout` de 5s a 2s (suficiente para LAN; 5s solo ayudaba a
    conexiones lentas que no son el caso típico acá) y se quitó una URL duplicada en la lista de
    `SendPtzCommandAsync` (`:8889/onvif/ptz_service` aparecía dos veces).
  - **Verificado con servidor mock** (`HttpListener` en `127.0.0.1:8899` respondiendo 200 OK solo
    en `/onvif/ptz_service` y `/onvif/device_service`, apuntando una "cámara" de prueba ahí): el
    flujo completo (GetProfileToken → MoveAsync Right → StopAsync → MoveAsync Left → StopAsync)
    corre en ~2 segundos y el diálogo "PTZ Test - Success" muestra el detalle correctamente
    formateado. Esto confirma que el *código* del pipeline PTZ (SOAP, auth, parsing de respuesta,
    UI) funciona correctamente cuando la cámara efectivamente expone esos endpoints — si el
    problema del usuario persiste con su cámara real, es casi seguro un problema de **compatibilidad
    de protocolo con esa cámara puntual** (puerto/path distinto a los 8-9 adivinados, auth Digest
    en vez de Basic que es lo único que soporta el código actual, o la cámara realmente no expone
    PTZ por ONVIF), no un bug genérico de la app — para eso sirve el detalle que ahora muestra
    "Test PTZ": decir EXACTAMENTE qué pasó en cada endpoint (401, SOAP fault con el mensaje, o
    timeout) en vez de un genérico "no funciona".
- **`LastAttemptLog` (`OnvifPtzService`)**: lista de líneas resumen (no los dumps crudos de XML,
  esos solo van a `Console.WriteLine`) que se llena vía el helper privado `Log(...)`. El llamador
  debe invocar `ClearLog()` antes de una nueva secuencia de prueba. `BtnTestPtz_Click` ya lo hace y
  concatena `LastAttemptLog` dentro del `MessageBox` de resultado (éxito o fallo).
- **Lección repetida en este proyecto**: `CameraViewer.csproj` es `OutputType=WinExe` → **no hay
  consola adjunta en uso normal**. `Console.WriteLine` (usado profusamente en
  `OnvifPtzService`/`V380Discovery`/etc.) solo es visible bajo un depurador adjunto (Visual Studio
  F5, Output window). Para cualquier diagnóstico que el usuario deba poder ver sin eso, usar
  `TxtStatus.Text` o un `MessageBox` con el detalle armado explícitamente (como `LastAttemptLog`
  arriba) — ya pasó esto mismo con el botón de pop-out de video, ver esa sección.

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
