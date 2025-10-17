using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// View model for tag context menu operations.
/// </summary>
public class TagContextMenuViewModel : ViewModelBase
{
    private string _tagName = string.Empty;
    private string _repository = string.Empty;
    private string _registryUrl = string.Empty;
    private Func<Task<TopLevel?>>? _getTopLevel;

    public string TagName
    {
        get => _tagName;
        set => this.RaiseAndSetIfChanged(ref _tagName, value);
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

    public ICommand CopyTagCommand { get; }
    public ICommand CopyFullyQualifiedReferenceCommand { get; }

    public TagContextMenuViewModel()
    {
        CopyTagCommand = ReactiveCommand.CreateFromTask(CopyTag);
        CopyFullyQualifiedReferenceCommand = ReactiveCommand.CreateFromTask(CopyFullyQualifiedReference);
    }

    public void SetTopLevelProvider(Func<Task<TopLevel?>> provider)
    {
        _getTopLevel = provider;
    }

    private async Task CopyTag()
    {
        if (!string.IsNullOrEmpty(TagName) && _getTopLevel != null)
        {
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(TagName);
            }
        }
    }

    private async Task CopyFullyQualifiedReference()
    {
        if (!string.IsNullOrEmpty(TagName) && !string.IsNullOrEmpty(Repository) && _getTopLevel != null)
        {
            // Format: registry.example.com/repository:tag
            var registryPrefix = string.IsNullOrEmpty(RegistryUrl) ? "" : $"{RegistryUrl}/";
            var fullyQualifiedRef = $"{registryPrefix}{Repository}:{TagName}";
            
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(fullyQualifiedRef);
            }
        }
    }
}
