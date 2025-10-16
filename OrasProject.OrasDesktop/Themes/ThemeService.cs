using Avalonia;
using Avalonia.Styling;

namespace OrasProject.OrasDesktop.Themes
{
    /// <summary>
    /// Service that provides theme-aware colors based on the current application theme variant
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets the current JSON syntax colors based on the active theme
        /// </summary>
        JsonSyntaxColors GetJsonSyntaxColors();
        
        /// <summary>
        /// Gets the current theme variant (Light or Dark)
        /// </summary>
        ThemeVariant GetCurrentTheme();
    }
    
    /// <summary>
    /// Default implementation of IThemeService
    /// </summary>
    public class ThemeService : IThemeService
    {
        public JsonSyntaxColors GetJsonSyntaxColors()
        {
            var theme = GetCurrentTheme();
            return theme == ThemeVariant.Dark ? JsonSyntaxColors.Dark : JsonSyntaxColors.Light;
        }
        
        public ThemeVariant GetCurrentTheme()
        {
            if (Application.Current is { } app)
            {
                var requestedTheme = app.RequestedThemeVariant;
                
                // If theme is set to Default, check actual theme
                if (requestedTheme == ThemeVariant.Default)
                {
                    // Try to get the actual theme from platform settings
                    if (app.ActualThemeVariant == ThemeVariant.Dark)
                    {
                        return ThemeVariant.Dark;
                    }
                    return ThemeVariant.Light;
                }
                
                return requestedTheme == ThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
            
            // Default to light theme if application is not available
            return ThemeVariant.Light;
        }
    }
}
