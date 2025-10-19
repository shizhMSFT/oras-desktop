using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// Reusable context menu view model for digest operations.
/// Can be used for manifest digests, referrer digests, and any other digest displays.
/// </summary>
public class DigestContextMenuViewModel : ViewModelBase
{
    private string _digest = string.Empty;
    private string? _repository;
    private string? _registryUrl;

    public DigestContextMenuViewModel()
    {
        CopyDigestCommand = ReactiveCommand.CreateFromTask(CopyDigest);
        CopyFullyQualifiedReferenceCommand = ReactiveCommand.CreateFromTask(CopyFullyQualifiedReference);
        GetManifestCommand = ReactiveCommand.Create(GetManifest, this.WhenAnyValue(
            x => x.RegistryUrl,
            x => x.Repository,
            (url, repo) => !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(repo)));
    }

    public DigestContextMenuViewModel(string digest, string? registryUrl = null, string? repository = null) : this()
    {
        Digest = digest;
        RegistryUrl = registryUrl;
        Repository = repository;
    }

    public string Digest
    {
        get => _digest;
        set => this.RaiseAndSetIfChanged(ref _digest, value);
    }

    public string? Repository
    {
        get => _repository;
        set => this.RaiseAndSetIfChanged(ref _repository, value);
    }

    public string? RegistryUrl
    {
        get => _registryUrl;
        set => this.RaiseAndSetIfChanged(ref _registryUrl, value);
    }

    public ReactiveCommand<Unit, Unit> CopyDigestCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyFullyQualifiedReferenceCommand { get; }
    public ReactiveCommand<Unit, Unit> GetManifestCommand { get; }
    
    /// <summary>
    /// Event raised when the user requests to load the manifest for this digest
    /// </summary>
    public event EventHandler<string>? ManifestRequested;

    private async Task CopyDigest()
    {
        await CopyToClipboardAsync(Digest);
    }

    private async Task CopyFullyQualifiedReference()
    {
        if (string.IsNullOrEmpty(RegistryUrl) || string.IsNullOrEmpty(Repository))
            return;

        // Format: registry.example.com/repository@digest
        var registryPrefix = RegistryUrl.EndsWith('/') ? RegistryUrl : $"{RegistryUrl}/";
        var fullyQualifiedRef = $"{registryPrefix}{Repository}@{Digest}";

        await CopyToClipboardAsync(fullyQualifiedRef);
    }
    
    private void GetManifest()
    {
        if (string.IsNullOrEmpty(RegistryUrl) || string.IsNullOrEmpty(Repository))
            return;

        // Format: registry.example.com/repository@digest
        var fullyQualifiedRef = $"{RegistryUrl}/{Repository}@{Digest}";
        
        // Raise event for MainViewModel to handle via ManifestLoader
        ManifestRequested?.Invoke(this, fullyQualifiedRef);
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.Clipboard != null)
                {
                    await mainWindow.Clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception)
        {
            // Silently fail clipboard operations
        }
    }
}
