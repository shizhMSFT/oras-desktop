using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

    private async Task CopyRepositoryName()
    {
        if (!string.IsNullOrEmpty(RepositoryPath))
        {
            await CopyToClipboardAsync(RepositoryPath);
        }
    }

    private async Task CopyFullyQualifiedName()
    {
        if (!string.IsNullOrEmpty(RepositoryPath) && IsActualRepository)
        {
            // Format: registry.example.com/repository
            var registryPrefix = string.IsNullOrEmpty(RegistryUrl) ? "" : $"{RegistryUrl}/";
            var fullyQualifiedName = $"{registryPrefix}{RepositoryPath}";
            
            await CopyToClipboardAsync(fullyQualifiedName);
        }
    }

    private static async Task CopyToClipboardAsync(string text)
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
}
