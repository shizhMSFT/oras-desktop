using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// Context menu view model for artifact type (media type) group nodes in the referrer tree.
/// Provides simple copy functionality for the artifact type string.
/// </summary>
public class ArtifactTypeContextMenuViewModel : ViewModelBase
{
    private readonly string _artifactType;

    public ArtifactTypeContextMenuViewModel(string artifactType)
    {
        _artifactType = artifactType;
        CopyArtifactTypeCommand = ReactiveCommand.CreateFromTask(CopyArtifactTypeAsync);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CopyArtifactTypeCommand { get; }

    private async Task CopyArtifactTypeAsync()
    {
        if (string.IsNullOrEmpty(_artifactType))
            return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow?.Clipboard != null)
            {
                await mainWindow.Clipboard.SetTextAsync(_artifactType);
            }
        }
    }
}
