using System.Collections.Generic;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for the status bar component
/// </summary>
public class StatusBarViewModel : ViewModelBase
{
    private readonly StatusService _statusService;
    private string _statusMessage = string.Empty;
    private bool _isStatusError = false;
    private bool _isBusy = false;
    private double _progressValue = 0;
    private bool _isProgressIndeterminate = false;

    public StatusBarViewModel(StatusService statusService)
    {
        _statusService = statusService;
        
        // Subscribe to status changes
        _statusService.StatusChanged += OnStatusChanged;
        _statusService.ProgressChanged += OnProgressChanged;
        
        // Initialize with current values
        _statusMessage = _statusService.StatusMessage;
        _isStatusError = _statusService.IsStatusError;
        _isBusy = _statusService.IsBusy;
        _progressValue = _statusService.ProgressValue;
        _isProgressIndeterminate = _statusService.IsProgressIndeterminate;
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => this.RaiseAndSetIfChanged(ref _isStatusError, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (this.RaiseAndSetIfChanged(ref _isBusy, value))
            {
                this.RaisePropertyChanged(nameof(IsProgressVisible));
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (!EqualityComparer<double>.Default.Equals(_progressValue, value))
            {
                this.RaiseAndSetIfChanged(ref _progressValue, value);
                this.RaisePropertyChanged(nameof(IsProgressVisible));
            }
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set
        {
            if (this.RaiseAndSetIfChanged(ref _isProgressIndeterminate, value))
            {
                this.RaisePropertyChanged(nameof(IsProgressVisible));
            }
        }
    }

    public bool IsProgressVisible => IsBusy && (IsProgressIndeterminate || ProgressValue > 0);

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        StatusMessage = e.Message;
        IsStatusError = e.IsError;
    }

    private void OnProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        IsBusy = e.IsBusy;
        ProgressValue = e.ProgressValue;
        IsProgressIndeterminate = e.IsIndeterminate;
    }
}
