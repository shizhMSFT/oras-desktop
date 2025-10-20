using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using OrasProject.OrasDesktop.Views;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// Event args for when a reference should be updated without triggering a load
/// </summary>
public class ReferenceUpdatedEventArgs : EventArgs
{
    public ReferenceUpdatedEventArgs(string reference, bool shouldFocus = true)
    {
        Reference = reference;
        ShouldFocus = shouldFocus;
    }

    public string Reference { get; }
    public bool ShouldFocus { get; }
}

/// <summary>
/// ViewModel for the artifact display component.
/// Coordinates manifest JSON, referrers, and platform sizes display.
/// </summary>
public class ArtifactViewModel : ViewModelBase
{
    private readonly ArtifactService _artifactService;
    private readonly ManifestService _manifestService;
    private readonly StatusService _statusService;
    private readonly IRegistryService _registryService;
    private readonly ILogger<ArtifactViewModel> _logger;
    
    private int _selectedTabIndex = 0; // 0=Manifest, 1=Referrers

    public ArtifactViewModel(
        ArtifactService artifactService,
        ManifestService manifestService,
        StatusService statusService,
        IRegistryService registryService,
        JsonHighlightService jsonHighlightService,
        ILogger<ArtifactViewModel> logger,
        ILoggerFactory loggerFactory)
    {
        _artifactService = artifactService;
        _manifestService = manifestService;
        _statusService = statusService;
        _registryService = registryService;
        _logger = logger;

        // Create child ViewModels
        JsonViewer = new JsonViewerViewModel(
            jsonHighlightService,
            manifestService,
            loggerFactory.CreateLogger<JsonViewerViewModel>());

        Referrer = new ReferrerViewModel(
            artifactService,
            registryService,
            statusService,
            loggerFactory.CreateLogger<ReferrerViewModel>());

        PlatformSizes = new PlatformSizesViewModel(
            artifactService,
            loggerFactory.CreateLogger<PlatformSizesViewModel>());

        // Create observables for commands
        var canDeleteObservable = CreateCanDeleteObservable();
        var canCopyTagObservable = CreateCanCopyTagObservable();

        // Initialize commands
        DeleteManifestCommand = ReactiveCommand.CreateFromTask(DeleteManifestAsync, canDeleteObservable);
        CopyReferenceWithTagCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithTagAsync, canCopyTagObservable);
        CopyReferenceWithDigestCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithDigestAsync, canDeleteObservable);
        ArtifactActionsCommand = ReactiveCommand.Create(() => { }, canDeleteObservable);
        ViewPlatformManifestCommand = ReactiveCommand.CreateFromTask<PlatformImageSize>(ViewPlatformManifestAsync);

        // Wire up events
        _artifactService.ManifestChanged += OnManifestChanged;
        Referrer.ContextMenu.ManifestRequested += OnReferrerManifestRequested;
    }

    // Child ViewModels
    public JsonViewerViewModel JsonViewer { get; }
    public ReferrerViewModel Referrer { get; }
    public PlatformSizesViewModel PlatformSizes { get; }

    // State
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public bool CanModifyArtifact => _artifactService.CanDeleteManifest();

    // Commands
    public ReactiveCommand<Unit, Unit> DeleteManifestCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyReferenceWithTagCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyReferenceWithDigestCommand { get; }
    public ReactiveCommand<Unit, Unit> ArtifactActionsCommand { get; }
    public ReactiveCommand<PlatformImageSize, Unit> ViewPlatformManifestCommand { get; }

    // Events
    /// <summary>
    /// Event raised when a reference should be updated in the reference box without triggering a load
    /// </summary>
    public event EventHandler<ReferenceUpdatedEventArgs>? ReferenceUpdateRequested;

    /// <summary>
    /// Handles manifest changes to update display
    /// </summary>
    private void OnManifestChanged(object? sender, ManifestChangedEventArgs e)
    {
        if (e.NewManifest != null)
        {
            DisplayManifest(e.NewManifest);
            _ = LoadReferrersAsync();
        }
        else
        {
            ClearDisplay();
        }

        this.RaisePropertyChanged(nameof(CanModifyArtifact));
    }

    /// <summary>
    /// Handles manifest requests from referrer context menu
    /// </summary>
    private void OnReferrerManifestRequested(object? sender, string reference)
    {
        _manifestService.TryRequestLoad(
            reference,
            LoadSource.ReferenceBox,
            forceReload: false);
    }

    /// <summary>
    /// Displays a manifest with all its details
    /// </summary>
    private void DisplayManifest(Manifest manifest)
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;

        if (registry == null || repository == null) return;

        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);

        JsonViewer.DisplayManifest(
            manifest.RawContent,
            manifest.Digest,
            registry.Url,
            repositoryPath);

        PlatformSizes.UpdatePlatformSizes();
    }

    /// <summary>
    /// Clears all artifact displays
    /// </summary>
    private void ClearDisplay()
    {
        JsonViewer.Clear();
        Referrer.Clear();
        PlatformSizes.Clear();
    }

    /// <summary>
    /// Loads referrers for the current manifest
    /// </summary>
    private async Task LoadReferrersAsync()
    {
        var manifest = _artifactService.CurrentManifest;
        var repository = _artifactService.CurrentRepository;
        var registry = _artifactService.CurrentRegistry;

        if (manifest == null || repository == null || registry == null)
        {
            Referrer.Clear();
            return;
        }

        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);
        var reference = _artifactService.CurrentTag?.Name ?? manifest.Digest;

        var fullReference = _artifactService.CurrentTag != null
            ? $"{registry.Url}/{repositoryPath}:{_artifactService.CurrentTag.Name}"
            : $"{registry.Url}/{repositoryPath}@{manifest.Digest}";

        await Referrer.LoadReferrersAsync(repositoryPath, manifest.Digest, reference, fullReference);
        
        // If no referrers and on Referrers tab, switch to Manifest tab
        if (Referrer.Referrers.Count == 0 && SelectedTabIndex == 1)
        {
            SelectedTabIndex = 0;
        }
    }

    /// <summary>
    /// Deletes the current manifest
    /// </summary>
    private async Task DeleteManifestAsync()
    {
        if (!_artifactService.CanDeleteManifest())
        {
            _statusService.SetStatus("No manifest to delete", isError: true);
            return;
        }

        var mainWindow = GetMainWindow();
        if (mainWindow == null)
        {
            _statusService.SetStatus("Failed to get main window", isError: true);
            return;
        }

        var fullReference = BuildFullReference();
        if (fullReference == null)
        {
            _statusService.SetStatus("Cannot build reference", isError: true);
            return;
        }

        if (!await ConfirmDelete(mainWindow, fullReference))
        {
            return;
        }

        await ExecuteDelete();
    }

    /// <summary>
    /// Copies the tag-based reference to clipboard
    /// </summary>
    private async Task CopyReferenceWithTagAsync()
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;
        var tag = _artifactService.CurrentTag;

        if (registry == null || repository == null)
        {
            _statusService.SetStatus("No manifest loaded", isError: true);
            return;
        }

        if (tag == null)
        {
            _statusService.SetStatus("No tag available for this manifest", isError: true);
            return;
        }

        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);
        var reference = $"{registry.Url}/{repositoryPath}:{tag.Name}";

        await CopyToClipboard(reference, "Reference with tag copied to clipboard");
    }

    /// <summary>
    /// Copies the digest-based reference to clipboard
    /// </summary>
    private async Task CopyReferenceWithDigestAsync()
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;
        var manifest = _artifactService.CurrentManifest;

        if (registry == null || repository == null || manifest == null)
        {
            _statusService.SetStatus("No manifest loaded", isError: true);
            return;
        }

        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);
        var reference = $"{registry.Url}/{repositoryPath}@{manifest.Digest}";

        // Update the reference box without triggering a load
        ReferenceUpdateRequested?.Invoke(this, new ReferenceUpdatedEventArgs(reference));

        await CopyToClipboard(reference, "Reference with digest copied to clipboard");
    }

    /// <summary>
    /// Views a specific platform manifest
    /// </summary>
    private Task ViewPlatformManifestAsync(PlatformImageSize platformSize)
    {
        if (string.IsNullOrEmpty(platformSize.Digest))
        {
            _statusService.SetStatus("Platform digest is missing", isError: true);
            return Task.CompletedTask;
        }

        _manifestService.TryRequestLoad(
            platformSize.Digest,
            LoadSource.ReferenceBox,
            forceReload: false);
            
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates observable for delete/copy digest commands
    /// </summary>
    private IObservable<bool> CreateCanDeleteObservable()
    {
        return Observable.Create<bool>(observer =>
        {
            observer.OnNext(_artifactService.CanDeleteManifest());

            EventHandler<ManifestChangedEventArgs> manifestHandler = (s, e) =>
                observer.OnNext(_artifactService.CanDeleteManifest());

            EventHandler<RepositoryChangedEventArgs> repositoryHandler = (s, e) =>
                observer.OnNext(_artifactService.CanDeleteManifest());

            _artifactService.ManifestChanged += manifestHandler;
            _artifactService.RepositoryChanged += repositoryHandler;

            return System.Reactive.Disposables.Disposable.Create(() =>
            {
                _artifactService.ManifestChanged -= manifestHandler;
                _artifactService.RepositoryChanged -= repositoryHandler;
            });
        });
    }

    /// <summary>
    /// Creates observable for copy tag command
    /// </summary>
    private IObservable<bool> CreateCanCopyTagObservable()
    {
        return Observable.Create<bool>(observer =>
        {
            bool CanCopyTag() => _artifactService.CanDeleteManifest() && _artifactService.CurrentTag != null;

            observer.OnNext(CanCopyTag());

            EventHandler<TagChangedEventArgs> tagHandler = (s, e) => observer.OnNext(CanCopyTag());
            EventHandler<ManifestChangedEventArgs> manifestHandler = (s, e) => observer.OnNext(CanCopyTag());
            EventHandler<RepositoryChangedEventArgs> repositoryHandler = (s, e) => observer.OnNext(CanCopyTag());

            _artifactService.TagChanged += tagHandler;
            _artifactService.ManifestChanged += manifestHandler;
            _artifactService.RepositoryChanged += repositoryHandler;

            return System.Reactive.Disposables.Disposable.Create(() =>
            {
                _artifactService.TagChanged -= tagHandler;
                _artifactService.ManifestChanged -= manifestHandler;
                _artifactService.RepositoryChanged -= repositoryHandler;
            });
        });
    }

    /// <summary>
    /// Builds the full reference for the current manifest
    /// </summary>
    private string? BuildFullReference()
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;
        var manifest = _artifactService.CurrentManifest;

        if (registry == null || repository == null || manifest == null)
        {
            return null;
        }

        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);

        if (_artifactService.CurrentTag != null)
        {
            return $"{registry.Url}/{repositoryPath}:{_artifactService.CurrentTag.Name}";
        }

        return $"{registry.Url}/{repositoryPath}@{manifest.Digest}";
    }

    /// <summary>
    /// Shows delete confirmation dialog
    /// </summary>
    private async Task<bool> ConfirmDelete(Window mainWindow, string fullReference)
    {
        var dialog = new DeleteManifestDialog(fullReference);
        return await dialog.ShowDialog<bool>(mainWindow);
    }

    /// <summary>
    /// Executes the delete operation
    /// </summary>
    private async Task ExecuteDelete()
    {
        _statusService.SetBusy(true);
        
        var referenceDesc = _artifactService.CurrentTag != null
            ? _artifactService.CurrentTag.Name
            : _artifactService.CurrentManifest!.Digest.Substring(0, 12) + "...";
            
        _statusService.SetStatus($"Deleting manifest for {referenceDesc}...");

        try
        {
            var (success, message) = await _artifactService.DeleteManifestAsync();
            _statusService.SetStatus(message, isError: !success);
        }
        finally
        {
            _statusService.SetBusy(false);
            _statusService.ResetProgress();
        }
    }

    /// <summary>
    /// Copies text to clipboard
    /// </summary>
    private async Task CopyToClipboard(string text, string successMessage)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(GetMainWindow());
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
                _statusService.SetStatus(successMessage);
            }
            else
            {
                _statusService.SetStatus("Failed to access clipboard", isError: true);
            }
        }
        catch (Exception ex)
        {
            _statusService.SetStatus($"Error copying reference: {ex.Message}", isError: true);
        }
    }

    /// <summary>
    /// Gets the main window
    /// </summary>
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
