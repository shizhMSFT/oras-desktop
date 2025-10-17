using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// View model for repository context menu operations.
/// </summary>
public class RepositoryContextMenuViewModel : ViewModelBase
{
    private string _repositoryName = string.Empty;
    private string _repositoryPath = string.Empty;
    private string _registryUrl = string.Empty;
    private bool _isActualRepository = true;
    private Func<Task<TopLevel?>>? _getTopLevel;

    public string RepositoryName
    {
        get => _repositoryName;
        set => this.RaiseAndSetIfChanged(ref _repositoryName, value);
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set => this.RaiseAndSetIfChanged(ref _repositoryPath, value);
    }

    public string RegistryUrl
    {
        get => _registryUrl;
        set => this.RaiseAndSetIfChanged(ref _registryUrl, value);
    }

    /// <summary>
    /// Gets or sets whether this is an actual repository (leaf node) or just a parent grouping node.
    /// </summary>
    public bool IsActualRepository
    {
        get => _isActualRepository;
        set => this.RaiseAndSetIfChanged(ref _isActualRepository, value);
    }

    public ICommand CopyRepositoryNameCommand { get; }
    public ICommand CopyFullyQualifiedNameCommand { get; }

    public RepositoryContextMenuViewModel()
    {
        CopyRepositoryNameCommand = ReactiveCommand.CreateFromTask(CopyRepositoryName);
        CopyFullyQualifiedNameCommand = ReactiveCommand.CreateFromTask(
            CopyFullyQualifiedName,
            this.WhenAnyValue(x => x.IsActualRepository)
        );
    }

    public void SetTopLevelProvider(Func<Task<TopLevel?>> provider)
    {
        _getTopLevel = provider;
    }

    private async Task CopyRepositoryName()
    {
        if (!string.IsNullOrEmpty(RepositoryPath) && _getTopLevel != null)
        {
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(RepositoryPath);
            }
        }
    }

    private async Task CopyFullyQualifiedName()
    {
        if (!string.IsNullOrEmpty(RepositoryPath) && IsActualRepository && _getTopLevel != null)
        {
            // Format: registry.example.com/repository
            var registryPrefix = string.IsNullOrEmpty(RegistryUrl) ? "" : $"{RegistryUrl}/";
            var fullyQualifiedName = $"{registryPrefix}{RepositoryPath}";
            
            var topLevel = await _getTopLevel();
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(fullyQualifiedName);
            }
        }
    }
}
