using System;
using System.Reactive;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for the connection control that handles registry URL and connection state.
/// </summary>
public class ConnectionViewModel : ViewModelBase
{
    private string _registryUrl = "mcr.microsoft.com";
    private bool _isAnonymous = true;
    private bool _isConnecting;
    private string _connectButtonText = "Connect";

    public ConnectionViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(RequestConnect, this.WhenAnyValue(
            x => x.RegistryUrl,
            x => x.IsConnecting,
            (url, connecting) => !string.IsNullOrWhiteSpace(url) && !connecting));
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    /// <summary>
    /// Event raised when user requests to connect to a registry
    /// </summary>
    public event EventHandler<ConnectionRequestedEventArgs>? ConnectionRequested;

    public string RegistryUrl
    {
        get => _registryUrl;
        set => this.RaiseAndSetIfChanged(ref _registryUrl, value);
    }

    public bool IsAnonymous
    {
        get => _isAnonymous;
        set => this.RaiseAndSetIfChanged(ref _isAnonymous, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            this.RaiseAndSetIfChanged(ref _isConnecting, value);
            ConnectButtonText = value ? "Connecting..." : "Connect";
        }
    }

    public string ConnectButtonText
    {
        get => _connectButtonText;
        private set => this.RaiseAndSetIfChanged(ref _connectButtonText, value);
    }

    private void RequestConnect()
    {
        if (string.IsNullOrWhiteSpace(RegistryUrl))
            return;

        ConnectionRequested?.Invoke(this, new ConnectionRequestedEventArgs(RegistryUrl, IsAnonymous));
    }
}

/// <summary>
/// Event args for connection requests
/// </summary>
public class ConnectionRequestedEventArgs : EventArgs
{
    public ConnectionRequestedEventArgs(string registryUrl, bool isAnonymous)
    {
        RegistryUrl = registryUrl;
        IsAnonymous = isAnonymous;
    }

    public string RegistryUrl { get; }
    public bool IsAnonymous { get; }
}
