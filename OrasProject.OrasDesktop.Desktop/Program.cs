using System;
using System.Linq;

using Avalonia;
using Avalonia.ReactiveUI;
using OrasProject.OrasDesktop.Logging;

namespace OrasProject.OrasDesktop.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for debug flag to enable detailed logging
        // Logs will be written to: %TEMP%\oras-desktop.log (Windows) or /tmp/oras-desktop.log (Linux/macOS)
        // Enable file logging with: set ORAS_DESKTOP_LOG=true (Windows) or export ORAS_DESKTOP_LOG=true (Linux/macOS)
        if (args.Contains("--debug") || args.Contains("-d"))
        {
            DesktopLoggingOptions.IsEnabled = true;  // Enable file logging
            DesktopLoggingOptions.DebugLoggingEnabled = true;  // Enable Debug-level logs
        }
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
