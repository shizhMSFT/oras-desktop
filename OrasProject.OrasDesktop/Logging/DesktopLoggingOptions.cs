using System;
using System.IO;

namespace OrasProject.OrasDesktop.Logging;

/// <summary>
/// Central configuration for desktop diagnostics logging.
/// </summary>
public static class DesktopLoggingOptions
{
    private const string EnvVarName = "ORAS_DESKTOP_LOG";

    static DesktopLoggingOptions()
    {
        LogFilePath = Path.Combine(Path.GetTempPath(), "oras-desktop.log");

        var envValue = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            if (bool.TryParse(envValue, out var enabled))
            {
                IsEnabled = enabled;
            }
            else if (string.Equals(envValue, "1", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(envValue, "on", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether diagnostics logging is active.
    /// Defaults to false and can be toggled via the ORAS_DESKTOP_LOG environment variable.
    /// </summary>
    public static bool IsEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets whether Debug-level logging is enabled.
    /// When false, only Information level and above are logged.
    /// </summary>
    public static bool DebugLoggingEnabled { get; set; }

    /// <summary>
    /// Gets the path to the log file used for diagnostics logging.
    /// </summary>
    public static string LogFilePath { get; }
}
