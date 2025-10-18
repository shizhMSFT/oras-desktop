using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

    private async Task CopyTag()
    {
        if (!string.IsNullOrEmpty(TagName))
        {
            await CopyToClipboardAsync(TagName);
        }
    }

    private async Task CopyFullyQualifiedReference()
    {
        if (!string.IsNullOrEmpty(TagName) && !string.IsNullOrEmpty(Repository))
        {
            // Format: registry.example.com/repository:tag
            var registryPrefix = string.IsNullOrEmpty(RegistryUrl) ? "" : $"{RegistryUrl}/";
            var fullyQualifiedRef = $"{registryPrefix}{Repository}:{TagName}";
            
            await CopyToClipboardAsync(fullyQualifiedRef);
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
