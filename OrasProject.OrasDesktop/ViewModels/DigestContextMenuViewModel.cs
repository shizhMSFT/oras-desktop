using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// View model for digest context menu operations.
/// </summary>
public class DigestContextMenuViewModel : ViewModelBase
{
    private string _digest = string.Empty;
    private string _repository = string.Empty;
    private string _registryUrl = string.Empty;
    private Func<Task<TopLevel?>>? _getTopLevel;

    public string Digest
    {
        get => _digest;
        set => this.RaiseAndSetIfChanged(ref _digest, value);
    }

    public string Repository
    {
        get => _repository;
        set => this.RaiseAndSetIfChanged(ref _repository, value);
    }

    public string RegistryUrl
    {
        get => _registryUrl;
        set => this.RaiseAndSetIfChanged(ref _registryUrl, value);
    }

    public ICommand CopyDigestCommand { get; }
    public ICommand CopyFullyQualifiedReferenceCommand { get; }

    public DigestContextMenuViewModel()
    {
        CopyDigestCommand = ReactiveCommand.CreateFromTask(CopyDigest);
        CopyFullyQualifiedReferenceCommand = ReactiveCommand.CreateFromTask(CopyFullyQualifiedReference);
    }

    public void SetTopLevelProvider(Func<Task<TopLevel?>> provider)
    {
        _getTopLevel = provider;
    }

    private async Task CopyDigest()
    {
        if (!string.IsNullOrEmpty(Digest) && _getTopLevel != null)
        {
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(Digest);
            }
        }
    }

    private async Task CopyFullyQualifiedReference()
    {
        if (!string.IsNullOrEmpty(Digest) && !string.IsNullOrEmpty(Repository) && _getTopLevel != null)
        {
            // Format: registry.example.com/repository@digest
            var registryPrefix = string.IsNullOrEmpty(RegistryUrl) ? "" : $"{RegistryUrl}/";
            var fullyQualifiedRef = $"{registryPrefix}{Repository}@{Digest}";
            
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(fullyQualifiedRef);
            }
        }
    }
}
