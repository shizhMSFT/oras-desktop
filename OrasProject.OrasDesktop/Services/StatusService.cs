using System;
using Microsoft.Extensions.Logging;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service for managing application status messages and progress tracking.
/// Provides centralized status messaging and progress indication that can be displayed in the UI.
/// </summary>
public class StatusService
{
    private readonly ILogger<StatusService> _logger;
    private string _statusMessage = string.Empty;
    private bool _isStatusError = false;
    private bool _isBusy = false;
    private double _progressValue = 0;
    private bool _isProgressIndeterminate = false;

    public StatusService(ILogger<StatusService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the current status message
    /// </summary>
    public string StatusMessage => _statusMessage;

    /// <summary>
    /// Gets whether the current status is an error
    /// </summary>
    public bool IsStatusError => _isStatusError;

    /// <summary>
    /// Gets whether the application is busy
    /// </summary>
    public bool IsBusy => _isBusy;

    /// <summary>
    /// Gets the current progress value (0-100)
    /// </summary>
    public double ProgressValue => _progressValue;

    /// <summary>
    /// Gets whether progress is indeterminate
    /// </summary>
    public bool IsProgressIndeterminate => _isProgressIndeterminate;

    /// <summary>
    /// Event raised when the status message changes
    /// </summary>
    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Event raised when progress tracking changes
    /// </summary>
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;

    /// <summary>
    /// Sets the status message
    /// </summary>
    public void SetStatus(string message, bool isError = false)
    {
        _statusMessage = message;
        _isStatusError = isError;

        if (isError && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Status error: {Message}", message);
        }
        else if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Status: {Message}", message);
        }

        StatusChanged?.Invoke(this, new StatusChangedEventArgs(message, isError));
    }

    /// <summary>
    /// Clears the status message
    /// </summary>
    public void ClearStatus()
    {
        SetStatus(string.Empty, false);
    }

    /// <summary>
    /// Sets the busy state
    /// </summary>
    public void SetBusy(bool isBusy, bool isIndeterminate = true)
    {
        _isBusy = isBusy;
        _isProgressIndeterminate = isIndeterminate;

        if (isBusy && isIndeterminate)
        {
            _progressValue = 0;
        }

        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_isBusy, _progressValue, _isProgressIndeterminate));
    }

    /// <summary>
    /// Sets the progress value (0-100)
    /// </summary>
    public void SetProgress(double value, bool isIndeterminate = false)
    {
        _progressValue = Math.Clamp(value, 0, 100);
        _isProgressIndeterminate = isIndeterminate;

        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_isBusy, _progressValue, _isProgressIndeterminate));
    }

    /// <summary>
    /// Resets progress indicators
    /// </summary>
    public void ResetProgress()
    {
        _isBusy = false;
        _progressValue = 0;
        _isProgressIndeterminate = false;

        ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(_isBusy, _progressValue, _isProgressIndeterminate));
    }
}

/// <summary>
/// Event arguments for status change events
/// </summary>
public class StatusChangedEventArgs : EventArgs
{
    public string Message { get; }
    public bool IsError { get; }

    public StatusChangedEventArgs(string message, bool isError)
    {
        Message = message;
        IsError = isError;
    }
}

/// <summary>
/// Event arguments for progress change events
/// </summary>
public class ProgressChangedEventArgs : EventArgs
{
    public bool IsBusy { get; }
    public double ProgressValue { get; }
    public bool IsIndeterminate { get; }

    public ProgressChangedEventArgs(bool isBusy, double progressValue, bool isIndeterminate)
    {
        IsBusy = isBusy;
        ProgressValue = progressValue;
        IsIndeterminate = isIndeterminate;
    }
}
