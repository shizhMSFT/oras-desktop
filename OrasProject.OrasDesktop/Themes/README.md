# JSON Syntax Highlighting Theme System

This document describes the theme system for JSON syntax highlighting in the ORAS Desktop application.

## Overview

The JSON syntax highlighting system now supports both light and dark themes that automatically adapt to the application's current theme variant.

## Architecture

### Theme Service (`IThemeService`)

The theme service is responsible for providing theme-aware colors based on the current application theme.

**Location**: `OrasProject.OrasDesktop/Themes/ThemeService.cs`

**Interface**:
```csharp
public interface IThemeService
{
    JsonSyntaxColors GetJsonSyntaxColors();
    ThemeVariant GetCurrentTheme();
}
```

### JSON Syntax Colors (`JsonSyntaxColors`)

Defines the color scheme for JSON syntax highlighting with separate palettes for light and dark themes.

**Location**: `OrasProject.OrasDesktop/Themes/JsonSyntaxColors.cs`

**Properties**:
- `PropertyBrush` - Color for JSON property names
- `StringBrush` - Color for JSON string values
- `NumberBrush` - Color for JSON number values
- `BooleanBrush` - Color for JSON boolean and null values
- `DefaultBrush` - Default color for normal text

**Color Schemes**:

#### Light Theme
- **Properties**: Blue (#0000FF)
- **Strings**: Dark Green (#008000)
- **Numbers**: Dark Orange (#FF8C00)
- **Booleans/Null**: Dark Magenta (#8B008B)
- **Default**: Black (#000000)

#### Dark Theme
- **Properties**: Light Blue (#569CD6)
- **Strings**: Light Orange (#CE9178)
- **Numbers**: Light Green (#B5CEA8)
- **Booleans/Null**: Light Purple (#C586C0)
- **Default**: Light Gray (#D4D4D4)

## Dependency Injection

The application uses a simple service locator pattern for dependency injection.

**Location**: `OrasProject.OrasDesktop/ServiceLocator.cs`

### Registered Services

1. **IThemeService** - Singleton providing theme information
2. **JsonHighlightService** - Singleton for JSON highlighting (depends on IThemeService)
3. **IRegistryService** - Singleton for registry operations

### Initialization

The DI container is initialized in `App.Initialize()`:

```csharp
public override void Initialize()
{
    AvaloniaXamlLoader.Load(this);
    ServiceLocator.Initialize();
}
```

### Usage

Services are now injected through constructor dependency injection. For ViewModels and other services, declare dependencies in the constructor:

```csharp
public MainViewModel(IRegistryService registryService, JsonHighlightService jsonHighlightService)
{
    _registryService = registryService;
    _jsonHighlightService = jsonHighlightService;
}
```

Services can also be retrieved directly from the service provider if needed:

```csharp
var serviceProvider = ServiceLocator.Current;
var jsonHighlightService = serviceProvider.GetRequiredService<JsonHighlightService>();
```

## JSON Highlight Service

**Location**: `OrasProject.OrasDesktop/Services/JsonHighlightService.cs`

The `JsonHighlightService` now takes `IThemeService` as a constructor dependency:

```csharp
public JsonHighlightService(IThemeService themeService)
{
    _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
}
```

When highlighting JSON, it retrieves the current theme colors:

```csharp
public TextBlock HighlightJson(string json)
{
    var colors = _themeService.GetJsonSyntaxColors();
    // ... apply colors to JSON elements
}
```

## Customizing Colors

To customize the color scheme:

1. Modify the static `Light` or `Dark` properties in `JsonSyntaxColors.cs`
2. Update the color values using standard hex color codes
3. Rebuild the application

Example:
```csharp
public static JsonSyntaxColors Dark => new()
{
    PropertyBrush = new SolidColorBrush(Color.Parse("#YOUR_COLOR")),
    // ... other colors
};
```

## Theme Switching

The application theme is controlled by the `RequestedThemeVariant` property in `App.axaml`:

```xml
<Application RequestedThemeVariant="Default">
```

Options:
- `Default` - Follows system theme
- `Light` - Always use light theme
- `Dark` - Always use dark theme

The JSON syntax highlighting will automatically adapt when the theme changes.

## Testing

To test different themes:

1. **System Theme**: Set `RequestedThemeVariant="Default"` and change your system theme
2. **Light Theme**: Set `RequestedThemeVariant="Light"`
3. **Dark Theme**: Set `RequestedThemeVariant="Dark"`

The JSON viewer will update its colors accordingly when you load or refresh manifest content.
