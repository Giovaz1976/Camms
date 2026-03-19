# 🎨 Steam Dark Theme - Guía de Usuario

## 🌙 Tema Oscuro Inspirado en Steam

La aplicación ahora incluye un tema oscuro inspirado en la plataforma de juegos **Steam**, con colores característicos y un diseño moderno.

---

## 🎮 Paleta de Colores Steam

### **Tema Oscuro (Steam)**

| Elemento | Color | Hex | Uso |
|----------|-------|-----|-----|
| **Fondo Principal** | Azul Oscuro Profundo | `#1B2838` | Panel de lista de cámaras |
| **Panel Superior** | Negro Azulado | `#171A21` | Barra de herramientas |
| **Acento** | Azul Grisáceo | `#2A475E` | Botones de layout inactivos |
| **Azul Steam** | Azul Claro | `#66C0F4` | Botón de tema, enlaces |
| **Texto Principal** | Gris Claro | `#C7D5E0` | Nombres de cámaras |
| **Texto Secundario** | Gris Medio | `#8F98A0` | IPs, Device IDs |
| **Bordes** | Gris Oscuro | `#3D4450` | Separadores |
| **Hover** | Azul Medio | `#417A9B` | Estados hover |

### **Tema Claro (Por Defecto)**

| Elemento | Color | Hex | Uso |
|----------|-------|-----|-----|
| **Fondo Principal** | Gris Claro | `#ECF0F1` | Panel de lista de cámaras |
| **Panel Superior** | Azul Oscuro | `#2C3E50` | Barra de herramientas |
| **Acento** | Gris Azulado | `#34495E` | Botones de layout |
| **Azul** | Azul Brillante | `#3498DB` | Botones principales |
| **Texto Principal** | Azul Oscuro | `#2C3E50` | Nombres de cámaras |
| **Texto Secundario** | Gris | `#7F8C8D` | IPs, Device IDs |
| **Bordes** | Gris Medio | `#BDC3C7` | Separadores |
| **Fondo Blanco** | Blanco | `#FFFFFF` | Lista de cámaras |

---

## 🔘 Botón de Cambio de Tema

### **Ubicación:**
El botón se encuentra en la **barra superior**, a la izquierda de los botones de layout:

```
[🔍 Scan] [➕ Add] [⚙️ HD] Status...    [🌙 Dark] Layout: [1][2][4][9] [🎮 PTZ] [✕ Exit]
```

### **Estados del Botón:**

| Tema Actual | Botón Muestra | Al Hacer Click |
|-------------|---------------|----------------|
| **Claro** | `🌙 Dark` | Cambia a tema oscuro |
| **Oscuro** | `☀️ Light` | Cambia a tema claro |

### **Color del Botón:**
- Siempre usa el color **Steam Blue** (`#66C0F4`)
- Texto blanco para máximo contraste

---

## 🎨 Comparación Visual

### **Tema Claro (Por Defecto)**

```
┌────────────────────────────────────────────────────────────────┐
│ [Barra Superior - Azul Oscuro #2C3E50]                        │
│ 🔍 Scan  ➕ Add  ⚙️ HD  Ready    🌙 Dark  Layout: 1 2 4 9  ✕  │
├──────────────┬─────────────────────────────────────────────────┤
│ [Lista Gris] │ [Video Negro]                                   │
│ #ECF0F1      │ #252525                                         │
│              │                                                 │
│ Cameras:     │                                                 │
│ ┌──────────┐ │                                                 │
│ │ Camera 1 │ │  Select cameras to start viewing               │
│ │ 192.168… │ │                                                 │
│ └──────────┘ │                                                 │
│              │                                                 │
└──────────────┴─────────────────────────────────────────────────┘
```

### **Tema Oscuro Steam**

```
┌────────────────────────────────────────────────────────────────┐
│ [Barra Superior - Negro Azulado #171A21]                      │
│ 🔍 Scan  ➕ Add  ⚙️ HD  Ready    ☀️ Light  Layout: 1 2 4 9  ✕ │
├──────────────┬─────────────────────────────────────────────────┤
│ [Lista Azul] │ [Video Negro]                                   │
│ #1B2838      │ #252525                                         │
│              │                                                 │
│ Cameras:     │                                                 │
│ ┌──────────┐ │                                                 │
│ │ Camera 1 │ │  Select cameras to start viewing               │
│ │ 192.168… │ │  (texto gris claro #C7D5E0)                    │
│ └──────────┘ │                                                 │
│ (texto claro)│                                                 │
└──────────────┴─────────────────────────────────────────────────┘
```

---

## 🚀 Cómo Usar

### **Activar Tema Oscuro:**
1. Click en el botón **"🌙 Dark"** en la barra superior
2. La interfaz cambia inmediatamente al tema oscuro Steam
3. El botón ahora muestra **"☀️ Light"**

### **Volver al Tema Claro:**
1. Click en el botón **"☀️ Light"**
2. La interfaz vuelve al tema claro original
3. El botón ahora muestra **"🌙 Dark"**

### **Cambio Instantáneo:**
- ✅ No requiere reiniciar la aplicación
- ✅ Cambio inmediato sin parpadeos
- ✅ Todas las cámaras siguen reproduciendo
- ✅ Configuración se mantiene durante la sesión

---

## 🎯 Elementos que Cambian de Color

### **Panel de Lista de Cámaras:**
- **Fondo:** Gris claro → Azul oscuro Steam
- **Título "Discovered Cameras":** Azul oscuro → Gris claro
- **Nombres de cámaras:** Azul oscuro → Gris claro
- **IPs y Device IDs:** Gris → Gris medio
- **Fondo de lista:** Blanco → Negro azulado

### **Barra Superior:**
- **Fondo:** Azul oscuro → Negro azulado Steam
- **Botones de layout inactivos:** Gris azulado → Azul grisáceo Steam

### **Panel PTZ:**
- **Fondo:** Azul oscuro → Negro azulado Steam
- **Texto "Pan/Tilt":** Gris → Gris medio Steam

### **Bordes:**
- **Color:** Gris medio → Gris oscuro Steam

---

## 💡 Características del Tema Steam

### **Inspiración:**
El tema oscuro está inspirado en la plataforma de juegos **Steam** de Valve:
- Colores azules oscuros característicos
- Alto contraste para facilitar la lectura
- Diseño moderno y profesional
- Reducción de fatiga visual en ambientes oscuros

### **Ventajas del Tema Oscuro:**
- ✅ **Menos fatiga visual** en sesiones largas
- ✅ **Mejor contraste** para video en pantalla
- ✅ **Aspecto profesional** tipo gaming/streaming
- ✅ **Ahorro de energía** en pantallas OLED
- ✅ **Estética moderna** inspirada en Steam

### **Cuándo Usar Cada Tema:**

| Situación | Tema Recomendado |
|-----------|------------------|
| Uso nocturno / ambiente oscuro | 🌙 **Oscuro** |
| Uso diurno / mucha luz | ☀️ **Claro** |
| Sesiones largas de monitoreo | 🌙 **Oscuro** |
| Presentaciones / demos | ☀️ **Claro** |
| Streaming / gaming setup | 🌙 **Oscuro** |

---

## 🔧 Implementación Técnica

### **Recursos Dinámicos:**
La aplicación usa **DynamicResource** en XAML para permitir cambios en tiempo real:

```xaml
<Border Background="{DynamicResource ThemePanel}">
<TextBlock Foreground="{DynamicResource ThemeText}"/>
```

### **Paletas Definidas:**
```xaml
<!-- Steam Dark Theme -->
<SolidColorBrush x:Key="SteamDarkBackground" Color="#1B2838"/>
<SolidColorBrush x:Key="SteamDarkPanel" Color="#171A21"/>
<SolidColorBrush x:Key="SteamBlue" Color="#66C0F4"/>

<!-- Light Theme -->
<SolidColorBrush x:Key="LightBackground" Color="#ECF0F1"/>
<SolidColorBrush x:Key="LightPanel" Color="#2C3E50"/>
```

### **Cambio de Tema:**
```csharp
private void BtnTheme_Click(object sender, RoutedEventArgs e)
{
    _isDarkTheme = !_isDarkTheme;
    ApplyTheme(_isDarkTheme);
}

private void ApplyTheme(bool isDark)
{
    if (isDark)
    {
        UpdateResource("ThemeBackground", "SteamDarkBackground");
        UpdateResource("ThemePanel", "SteamDarkPanel");
        // ... más recursos
        BtnTheme.Content = "☀️ Light";
    }
    else
    {
        UpdateResource("ThemeBackground", "LightBackground");
        UpdateResource("ThemePanel", "LightPanel");
        // ... más recursos
        BtnTheme.Content = "🌙 Dark";
    }
}
```

---

## 📸 Capturas de Pantalla

### **Tema Claro - Vista General**
- Barra superior azul oscura
- Panel de cámaras gris claro
- Lista con fondo blanco
- Texto oscuro sobre fondo claro

### **Tema Oscuro Steam - Vista General**
- Barra superior negra azulada (#171A21)
- Panel de cámaras azul oscuro (#1B2838)
- Lista con fondo negro azulado
- Texto claro (#C7D5E0) sobre fondo oscuro

### **Comparación Lado a Lado**
```
CLARO                           OSCURO
┌─────────────┐                ┌─────────────┐
│ #2C3E50     │                │ #171A21     │
│ [Barra]     │                │ [Barra]     │
├─────────────┤                ├─────────────┤
│ #ECF0F1     │                │ #1B2838     │
│ [Lista]     │                │ [Lista]     │
│ ┌─────────┐ │                │ ┌─────────┐ │
│ │ #FFFFFF │ │                │ │ #171A21 │ │
│ │ Items   │ │                │ │ Items   │ │
│ └─────────┘ │                │ └─────────┘ │
└─────────────┘                └─────────────┘
```

---

## 🎉 Resumen

**Nuevo Botón:** `🌙 Dark` / `☀️ Light` en la barra superior

**Temas Disponibles:**
1. ☀️ **Tema Claro** (por defecto) - Colores brillantes, ideal para uso diurno
2. 🌙 **Tema Oscuro Steam** - Colores oscuros azulados, ideal para uso nocturno

**Colores Steam:**
- Azul oscuro profundo (#1B2838)
- Negro azulado (#171A21)
- Azul Steam (#66C0F4)
- Texto gris claro (#C7D5E0)

**Características:**
- ✅ Cambio instantáneo sin reiniciar
- ✅ Inspirado en Steam de Valve
- ✅ Reduce fatiga visual
- ✅ Aspecto profesional gaming

**¡Disfruta del nuevo tema oscuro Steam!** 🎮🌙
