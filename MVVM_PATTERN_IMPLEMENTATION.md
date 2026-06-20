# 🎯 MVVM Pattern Implementation

## Resumen

Se ha implementado el patrón **MVVM (Model-View-ViewModel)** en CameraViewer para separar la lógica de negocio de la interfaz de usuario.

**Estado**: ✅ Implementación base completada y compilando

---

## 📦 Paquetes Agregados

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
```

**CommunityToolkit.Mvvm** (anteriormente MVVM Toolkit) proporciona:
- `ObservableObject` - Implementación de `INotifyPropertyChanged`
- `RelayCommand` - Implementación de `ICommand`
- `ObservableProperty` - Source generators para propiedades
- `RelayCommand` attribute - Source generators para comandos

---

## 🏗️ Estructura MVVM Creada

```
CameraViewer/
├── Models/              (Existing)
│   └── CameraInfo.cs
├── ViewModels/          ✅ NEW
│   ├── ViewModelBase.cs
│   └── MainViewModel.cs
├── Views/               (MainWindow.xaml)
└── Services/            (Existing)
    ├── OnvifDiscovery.cs
    └── V380Discovery.cs
```

---

## 📝 Clases Creadas

### 1. ViewModelBase

**Ubicación**: `ViewModels/ViewModelBase.cs`

**Propósito**: Clase base para todos los ViewModels

```csharp
public abstract class ViewModelBase : ObservableObject
{
    // Hereda de ObservableObject:
    // - INotifyPropertyChanged
    // - SetProperty<T>()
    // - OnPropertyChanged()
}
```

**Beneficios**:
- ✅ Implementación automática de `INotifyPropertyChanged`
- ✅ Método `SetProperty` para actualizar propiedades
- ✅ Notificaciones automáticas a la UI

---

### 2. MainViewModel

**Ubicación**: `ViewModels/MainViewModel.cs`

**Propósito**: ViewModel principal para la ventana principal

**Responsabilidades**:
- Gestionar el descubrimiento de cámaras
- Mantener la lista de cámaras descubiertas
- Manejar el estado de escaneo
- Proporcionar comandos para la UI

**Propiedades Observables**:

```csharp
// Colección de cámaras descubiertas
public ObservableCollection<CameraInfo> DiscoveredCameras { get; set; }

// Estado de escaneo
public bool IsScanning { get; set; }

// Texto del botón de escaneo
public string ScanButtonText { get; set; }

// Mensaje de estado
public string StatusMessage { get; set; }
```

**Comandos**:

```csharp
// Comando para escanear cámaras
public ICommand ScanCamerasCommand { get; }
```

**Inyección de Dependencias**:

```csharp
public MainViewModel(
    OnvifDiscovery onvifDiscovery,
    V380Discovery v380Discovery)
{
    // Servicios inyectados
}
```

---

## 🔄 Flujo MVVM

### Separación de Responsabilidades

**Antes (Code-Behind)**:
```
MainWindow.xaml.cs
├── UI Logic (event handlers)
├── Business Logic (camera discovery)
├── Data Management (camera list)
└── State Management (scanning state)
```

**Después (MVVM)**:
```
View (MainWindow.xaml)
└── Data Binding → ViewModel (MainViewModel)
                    ├── Properties (data)
                    ├── Commands (actions)
                    └── Services (business logic)
                        └── Model (CameraInfo)
```

---

## 📊 Patrón MVVM Explicado

### Model (Modelo)

**Qué es**: Datos y lógica de negocio

**En CameraViewer**:
- `CameraInfo` - Información de la cámara
- `OnvifDiscovery` - Servicio de descubrimiento
- `V380Discovery` - Servicio de descubrimiento V380

**Responsabilidad**: Representar datos y reglas de negocio

---

### View (Vista)

**Qué es**: Interfaz de usuario (XAML)

**En CameraViewer**:
- `MainWindow.xaml` - UI principal
- Controles WPF (Buttons, ListBox, etc.)

**Responsabilidad**: Mostrar datos y capturar input del usuario

**Ejemplo de Binding**:
```xml
<ListBox ItemsSource="{Binding DiscoveredCameras}" />
<Button Content="{Binding ScanButtonText}" 
        Command="{Binding ScanCamerasCommand}" />
<TextBlock Text="{Binding StatusMessage}" />
```

---

### ViewModel (Modelo de Vista)

**Qué es**: Intermediario entre View y Model

**En CameraViewer**:
- `MainViewModel` - Lógica de presentación

**Responsabilidad**:
- Exponer datos del Model a la View
- Manejar comandos de la View
- Transformar datos para presentación
- Gestionar estado de la UI

**Características**:
- ✅ Implementa `INotifyPropertyChanged`
- ✅ Expone `ObservableCollection` para binding
- ✅ Proporciona `ICommand` para acciones
- ✅ No tiene referencia a la View

---

## 🎯 Beneficios del Patrón MVVM

### 1. Separación de Responsabilidades

**Antes**:
```csharp
// MainWindow.xaml.cs - TODO mezclado
private void BtnScan_Click(object sender, RoutedEventArgs e)
{
    // UI logic
    BtnScan.IsEnabled = false;
    
    // Business logic
    var cameras = await _discovery.DiscoverCamerasAsync();
    
    // Data management
    LstCameras.Items.Clear();
    foreach (var cam in cameras)
        LstCameras.Items.Add(cam);
}
```

**Después**:
```csharp
// MainViewModel.cs - Solo lógica de presentación
private async Task ScanCamerasAsync()
{
    IsScanning = true;  // UI se actualiza automáticamente
    DiscoveredCameras.Clear();
    
    var cameras = await _discovery.DiscoverCamerasAsync();
    // Cameras se agregan vía evento
}
```

---

### 2. Testabilidad

**Antes** (Code-Behind):
```csharp
// Imposible de testear sin crear la ventana
[Test]
public void TestScan()
{
    var window = new MainWindow();  // Requiere UI
    // No se puede testear fácilmente
}
```

**Después** (MVVM):
```csharp
// Fácil de testear sin UI
[Test]
public async Task ScanCamerasAsync_ShouldUpdateStatus()
{
    // Arrange
    var mockDiscovery = new Mock<OnvifDiscovery>();
    var viewModel = new MainViewModel(mockDiscovery.Object, ...);
    
    // Act
    await viewModel.ScanCamerasCommand.Execute(null);
    
    // Assert
    Assert.That(viewModel.StatusMessage, Does.Contain("Scanning"));
}
```

---

### 3. Data Binding Automático

**Antes**:
```csharp
// Actualización manual de UI
private void UpdateCameraList(List<CameraInfo> cameras)
{
    LstCameras.Items.Clear();
    foreach (var cam in cameras)
        LstCameras.Items.Add(cam);
}
```

**Después**:
```csharp
// Actualización automática vía binding
DiscoveredCameras.Add(camera);  // UI se actualiza sola
```

---

### 4. Reutilización de Lógica

El ViewModel puede usarse con diferentes Views:
- Desktop (WPF)
- Mobile (MAUI)
- Web (Blazor)

---

## 🔧 Uso del ViewModel

### Configuración en DI

```csharp
// App.xaml.cs
services.AddTransient<MainViewModel>();
services.AddTransient<MainWindow>();
```

### Inyección en View

```csharp
// MainWindow.xaml.cs
public MainWindow(MainViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;  // Conectar ViewModel a View
}
```

### Binding en XAML

```xml
<!-- MainWindow.xaml -->
<Window DataContext="{Binding MainViewModel}">
    <StackPanel>
        <!-- Binding a propiedades -->
        <TextBlock Text="{Binding StatusMessage}" />
        
        <!-- Binding a colecciones -->
        <ListBox ItemsSource="{Binding DiscoveredCameras}" />
        
        <!-- Binding a comandos -->
        <Button Content="{Binding ScanButtonText}"
                Command="{Binding ScanCamerasCommand}" />
    </StackPanel>
</Window>
```

---

## 📚 Conceptos Clave

### INotifyPropertyChanged

**Qué es**: Interfaz que notifica a la UI cuando una propiedad cambia

**Implementación Manual**:
```csharp
private string _statusMessage;
public string StatusMessage
{
    get => _statusMessage;
    set
    {
        if (_statusMessage != value)
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));  // Notificar
        }
    }
}
```

**Con CommunityToolkit.Mvvm**:
```csharp
private string _statusMessage;
public string StatusMessage
{
    get => _statusMessage;
    set => SetProperty(ref _statusMessage, value);  // Automático
}
```

---

### ObservableCollection

**Qué es**: Colección que notifica cuando se agregan/eliminan elementos

**Uso**:
```csharp
public ObservableCollection<CameraInfo> DiscoveredCameras { get; set; }

// La UI se actualiza automáticamente
DiscoveredCameras.Add(newCamera);
DiscoveredCameras.Remove(oldCamera);
DiscoveredCameras.Clear();
```

---

### ICommand

**Qué es**: Interfaz para ejecutar acciones desde la UI

**Implementación con RelayCommand**:
```csharp
public ICommand ScanCamerasCommand => new RelayCommand(
    execute: async () => await ScanCamerasAsync(),
    canExecute: () => !IsScanning
);
```

**Binding en XAML**:
```xml
<Button Command="{Binding ScanCamerasCommand}" />
```

---

## 🚀 Próximos Pasos

### 1. Actualizar MainWindow.xaml

Agregar bindings a las propiedades del ViewModel:

```xml
<Window x:Class="CameraViewer.MainWindow"
        DataContext="{Binding MainViewModel}">
    <Grid>
        <TextBlock Text="{Binding StatusMessage}" />
        <ListBox ItemsSource="{Binding DiscoveredCameras}" />
        <Button Content="{Binding ScanButtonText}"
                Command="{Binding ScanCamerasCommand}" />
    </Grid>
</Window>
```

---

### 2. Actualizar MainWindow.xaml.cs

Inyectar ViewModel y establecer DataContext:

```csharp
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

---

### 3. Crear ViewModels Adicionales

- `CameraViewModel` - Para cada cámara individual
- `SettingsViewModel` - Para configuración
- `PtzControlViewModel` - Para control PTZ

---

### 4. Implementar Mensajería

Usar `WeakReferenceMessenger` para comunicación entre ViewModels:

```csharp
// Enviar mensaje
WeakReferenceMessenger.Default.Send(new CameraSelectedMessage(camera));

// Recibir mensaje
WeakReferenceMessenger.Default.Register<CameraSelectedMessage>(this, (r, m) =>
{
    // Manejar mensaje
});
```

---

## 📊 Comparación Antes/Después

| Aspecto | Code-Behind | MVVM |
|---------|-------------|------|
| **Separación** | ❌ Todo mezclado | ✅ Responsabilidades claras |
| **Testabilidad** | ❌ Difícil | ✅ Fácil |
| **Mantenibilidad** | ❌ Complejo | ✅ Simple |
| **Reutilización** | ❌ Baja | ✅ Alta |
| **Data Binding** | ❌ Manual | ✅ Automático |
| **Curva de aprendizaje** | ✅ Fácil | ⚠️ Media |

---

## ✅ Estado Actual

- ✅ CommunityToolkit.Mvvm instalado
- ✅ ViewModelBase creado
- ✅ MainViewModel implementado
- ✅ Registrado en DI container
- ✅ Compilación exitosa
- ⏳ Pendiente: Actualizar XAML con bindings
- ⏳ Pendiente: Actualizar MainWindow.xaml.cs

---

## 🎓 Recursos de Aprendizaje

**Documentación**:
- [CommunityToolkit.Mvvm Docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)

**Ejemplos**:
- Ver `MainViewModel.cs` para implementación completa
- Ver tests unitarios (próximamente) para ejemplos de testing

---

**Progreso SOLID + MVVM**: 90% completado
- ✅ SOLID Principles
- ✅ Dependency Injection
- ✅ Unit Tests
- ✅ MVVM Pattern (base)
- ⏳ MVVM Integration con UI (pendiente)
