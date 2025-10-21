using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OrasProject.OrasDesktop.Logging;

/// <summary>
/// Logger provider that writes log entries to a temporary file and debug output when enabled.
/// </summary>
public sealed class TempFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly Func<bool> _isEnabled;

    public TempFileLoggerProvider(string filePath, Func<bool> isEnabled)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
    }

    public ILogger CreateLogger(string categoryName) => new TempFileLogger(_filePath, categoryName, _isEnabled);

    public void Dispose()
    {
        // Nothing to dispose.
    }

    private sealed class TempFileLogger : ILogger
    {
        private static readonly object SyncRoot = new();
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly Func<bool> _isEnabled;

        public TempFileLogger(string filePath, string categoryName, Func<bool> isEnabled)
        {
            _filePath = filePath;
            _categoryName = categoryName;
            _isEnabled = isEnabled;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (!_isEnabled())
            {
                return false;
            }

            var minLevel = DesktopLoggingOptions.DebugLoggingEnabled ? LogLevel.Debug : LogLevel.Information;
            return logLevel >= minLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }

            var line = $"[OrasDesktop] {DateTime.UtcNow:O} {logLevel} {_categoryName}: {message}";

            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                    if (exception != null)
                    {
                        File.AppendAllText(_filePath, exception + Environment.NewLine);
                    }
                }
            }
            catch (Exception ioEx)
            {
                Debug.WriteLine($"[OrasDesktop] Failed to write log: {ioEx.Message}");
            }

            Debug.WriteLine(line);
            if (exception != null)
            {
                Debug.WriteLine(exception);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose()
            {
            }
        }
    }
}
