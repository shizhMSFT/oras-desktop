using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.ViewModels;
using ReactiveUI;
using System.Reactive.Linq;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service that manages the current artifact context and manifest operations.
/// Acts as a central state coordinator that notifies components when the artifact context changes.
/// Handles manifest loading, size calculation, and artifact operations.
/// </summary>
public class ArtifactService
{
    private readonly ILogger<ArtifactService> _logger;
    private readonly IRegistryService _registryService;
    private readonly StatusService _statusService;
    private Registry? _currentRegistry;
    private Repository? _currentRepository;
    private Tag? _currentTag;
    private string _currentReference = string.Empty;
    private bool _isDigest = false;
    private Manifest? _currentManifest;
    private string _artifactSizeSummary = string.Empty;
    private ObservableCollection<PlatformImageSize> _platformImageSizes = new();
    private bool _hasPlatformSizes = false;

    public ArtifactService(
        ILogger<ArtifactService> _logger,
        IRegistryService registryService,
        StatusService statusService)
    {
        this._logger = _logger;
        _registryService = registryService;
        _statusService = statusService;
    }
    
    /// <summary>
    /// Initializes connections to component ViewModels.
    /// Call this after all ViewModels are created.
    /// </summary>
    public void Initialize(RepositorySelectorViewModel repositorySelector, TagSelectorViewModel tagSelector)
    {
        // Subscribe to repository selection events
        repositorySelector.RepositorySelected += OnRepositorySelected;
        
        // Subscribe to tag selection changes via ReactiveUI
        tagSelector.WhenAnyValue(x => x.SelectedTag)
            .Subscribe(tag => OnTagSelected(null, tag));
        
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("ArtifactService initialized and subscribed to RepositorySelector and TagSelector events.");
        }
    }
    
    /// <summary>
    /// Handles repository selection and triggers tag loading
    /// </summary>
    private void OnRepositorySelected(object? sender, Repository repository)
    {
        if (repository == null || _currentRegistry == null)
            return;

        // Update current repository context
        SetRepository(repository);
        
        // Notify subscribers (TagService will handle tag loading)
        _statusService.SetBusy(true);
        _statusService.SetStatus($"Loading tags for {repository.Name}...");
        
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Repository selected: {Repository}. Raising RepositoryChanged event.", repository.Name);
        }
    }
    
    /// <summary>
    /// Handles tag selection
    /// </summary>
    private void OnTagSelected(object? sender, Tag? tag)
    {
        // Update current tag context
        SetTag(tag);
        
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Tag selected: {Tag}.", tag?.Name ?? "<none>");
        }
    }

    /// <summary>
    /// Gets the current registry
    /// </summary>
    public Registry? CurrentRegistry => _currentRegistry;

    /// <summary>
    /// Gets the current repository
    /// </summary>
    public Repository? CurrentRepository => _currentRepository;

    /// <summary>
    /// Gets the current tag
    /// </summary>
    public Tag? CurrentTag => _currentTag;

    /// <summary>
    /// Gets the current reference (tag or digest)
    /// </summary>
    public string CurrentReference => _currentReference;

    /// <summary>
    /// Gets whether the current reference is a digest
    /// </summary>
    public bool IsDigest => _isDigest;

    /// <summary>
    /// Gets the current manifest
    /// </summary>
    public Manifest? CurrentManifest => _currentManifest;

    /// <summary>
    /// Gets the artifact size summary
    /// </summary>
    public string ArtifactSizeSummary => _artifactSizeSummary;

    /// <summary>
    /// Gets the platform-specific image sizes (for multi-platform manifests)
    /// </summary>
    public ObservableCollection<PlatformImageSize> PlatformImageSizes => _platformImageSizes;

    /// <summary>
    /// Gets whether the artifact has platform-specific sizes
    /// </summary>
    public bool HasPlatformSizes => _hasPlatformSizes;

    /// <summary>
    /// Event raised when the registry changes
    /// </summary>
    public event EventHandler<RegistryChangedEventArgs>? RegistryChanged;

    /// <summary>
    /// Event raised when the repository changes
    /// </summary>
    public event EventHandler<RepositoryChangedEventArgs>? RepositoryChanged;

    /// <summary>
    /// Event raised when the tag changes
    /// </summary>
    public event EventHandler<TagChangedEventArgs>? TagChanged;

    /// <summary>
    /// Event raised when the reference (tag or digest) changes
    /// </summary>
    public event EventHandler<ReferenceChangedEventArgs>? ReferenceChanged;

    /// <summary>
    /// Event raised when the complete artifact context changes (all three: registry, repository, reference)
    /// </summary>
    public event EventHandler<ArtifactContextChangedEventArgs>? ArtifactContextChanged;

    /// <summary>
    /// Event raised when the manifest changes
    /// </summary>
    public event EventHandler<ManifestChangedEventArgs>? ManifestChanged;

    /// <summary>
    /// Event raised when artifact size information is updated
    /// </summary>
    public event EventHandler? ArtifactSizeUpdated;

    /// <summary>
    /// Sets the current registry
    /// </summary>
    public void SetRegistry(Registry? registry)
    {
        if (_currentRegistry == registry)
        {
            return;
        }

        var oldRegistry = _currentRegistry;
        _currentRegistry = registry;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Registry changed from {OldRegistry} to {NewRegistry}",
                oldRegistry?.Url ?? "<none>",
                registry?.Url ?? "<none>");
        }

        RegistryChanged?.Invoke(this, new RegistryChangedEventArgs(oldRegistry, registry));

        // When registry changes, clear repository and reference
        if (oldRegistry != registry)
        {
            SetRepository(null);
        }
    }

    /// <summary>
    /// Sets the current repository
    /// </summary>
    public void SetRepository(Repository? repository)
    {
        if (_currentRepository == repository)
        {
            return;
        }

        var oldRepository = _currentRepository;
        _currentRepository = repository;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Repository changed from {OldRepository} to {NewRepository}",
                oldRepository?.Name ?? "<none>",
                repository?.Name ?? "<none>");
        }

        RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(oldRepository, repository));

        // When repository changes, clear tag and reference
        if (oldRepository != repository)
        {
            SetTag(null);
            SetReference(string.Empty, false);
        }
    }

    /// <summary>
    /// Sets the current tag
    /// </summary>
    public void SetTag(Tag? tag)
    {
        if (_currentTag == tag)
        {
            return;
        }

        var oldTag = _currentTag;
        _currentTag = tag;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Tag changed from {OldTag} to {NewTag}",
                oldTag?.Name ?? "<none>",
                tag?.Name ?? "<none>");
        }

        TagChanged?.Invoke(this, new TagChangedEventArgs(oldTag, tag));

        // When tag changes, update reference
        if (tag != null)
        {
            SetReference(tag.Name, false);
        }
        else if (oldTag != null)
        {
            SetReference(string.Empty, false);
        }
    }

    /// <summary>
    /// Sets the current reference (tag or digest)
    /// </summary>
    public void SetReference(string reference, bool isDigest)
    {
        if (_currentReference == reference && _isDigest == isDigest)
        {
            return;
        }

        var oldReference = _currentReference;
        var wasDigest = _isDigest;
        _currentReference = reference ?? string.Empty;
        _isDigest = isDigest;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Reference changed from {OldReference} ({OldType}) to {NewReference} ({NewType})",
                string.IsNullOrEmpty(oldReference) ? "<none>" : oldReference,
                wasDigest ? "digest" : "tag",
                string.IsNullOrEmpty(reference) ? "<none>" : reference,
                isDigest ? "digest" : "tag");
        }

        ReferenceChanged?.Invoke(this, new ReferenceChangedEventArgs(oldReference, reference ?? string.Empty, wasDigest, isDigest));
    }

    /// <summary>
    /// Sets the complete artifact context (registry, repository, reference) atomically
    /// </summary>
    public void SetArtifactContext(Registry? registry, Repository? repository, string reference, bool isDigest)
    {
        var registryChanged = _currentRegistry != registry;
        var repositoryChanged = _currentRepository != repository;
        var referenceChanged = _currentReference != reference || _isDigest != isDigest;

        if (!registryChanged && !repositoryChanged && !referenceChanged)
        {
            return;
        }

        var oldRegistry = _currentRegistry;
        var oldRepository = _currentRepository;
        var oldReference = _currentReference;
        var wasDigest = _isDigest;

        _currentRegistry = registry;
        _currentRepository = repository;
        _currentReference = reference ?? string.Empty;
        _isDigest = isDigest;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Artifact context changed: Registry={Registry}, Repository={Repository}, Reference={Reference} ({Type})",
                registry?.Url ?? "<none>",
                repository?.Name ?? "<none>",
                string.IsNullOrEmpty(reference) ? "<none>" : reference,
                isDigest ? "digest" : "tag");
        }

        // Fire individual events
        if (registryChanged)
        {
            RegistryChanged?.Invoke(this, new RegistryChangedEventArgs(oldRegistry, registry));
        }

        if (repositoryChanged)
        {
            RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(oldRepository, repository));
        }

        if (referenceChanged)
        {
            ReferenceChanged?.Invoke(this, new ReferenceChangedEventArgs(oldReference, reference ?? string.Empty, wasDigest, isDigest));
        }

        // Fire combined event
        ArtifactContextChanged?.Invoke(this, new ArtifactContextChangedEventArgs(
            oldRegistry, registry,
            oldRepository, repository,
            oldReference, reference ?? string.Empty,
            wasDigest, isDigest
        ));
    }

    /// <summary>
    /// Clears the current artifact context
    /// </summary>
    public void Clear()
    {
        SetArtifactContext(null, null, string.Empty, false);
    }

    /// <summary>
    /// Clears authentication credentials from the current registry, setting it to anonymous mode.
    /// Useful when connecting to a new registry or resetting authentication.
    /// </summary>
    public void ClearAuth()
    {
        if (_currentRegistry == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Attempted to clear auth but no registry is set.");
            }
            return;
        }

        _currentRegistry.AuthenticationType = AuthenticationType.None;
        _currentRegistry.Username = string.Empty;
        _currentRegistry.Password = string.Empty;
        _currentRegistry.Token = string.Empty;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Cleared authentication for registry {Registry}.", _currentRegistry.Url);
        }
    }

    /// <summary>
    /// Sets the current manifest and calculates artifact size
    /// </summary>
    public async Task SetManifestAsync(Manifest? manifest, string repositoryPath, CancellationToken cancellationToken = default)
    {
        var oldManifest = _currentManifest;
        _currentManifest = manifest;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Manifest changed: Digest={Digest}",
                manifest?.Digest ?? "<none>");
        }

        ManifestChanged?.Invoke(this, new ManifestChangedEventArgs(oldManifest, manifest));

        // Calculate artifact size if manifest is not null
        if (manifest != null && !string.IsNullOrEmpty(repositoryPath))
        {
            await CalculateArtifactSizeAsync(repositoryPath, manifest.RawContent, manifest.MediaType, cancellationToken);
        }
        else
        {
            // Clear size information
            _artifactSizeSummary = string.Empty;
            _platformImageSizes.Clear();
            _hasPlatformSizes = false;
            ArtifactSizeUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Calculates and updates artifact size information
    /// </summary>
    private async Task CalculateArtifactSizeAsync(string repositoryPath, string manifestJson, string mediaType, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear previous platform sizes
            _platformImageSizes.Clear();

            // Create a ManifestResult to pass to the calculator
            var manifestResult = new ManifestResult(
                string.Empty, // digest - not needed for size calculation
                mediaType,
                manifestJson,
                Array.Empty<string>() // referenced digests - not needed for size calculation
            );

            var result = await ArtifactSizeCalculator.AnalyzeManifestSizeAsync(
                _registryService,
                repositoryPath,
                manifestResult,
                cancellationToken
            );

            _artifactSizeSummary = result.summary;
            _hasPlatformSizes = result.hasPlatformSizes;

            foreach (var platform in result.platformSizes)
            {
                _platformImageSizes.Add(platform);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Artifact size calculated: {Summary}, HasPlatformSizes={HasPlatformSizes}",
                    _artifactSizeSummary, _hasPlatformSizes);
            }

            ArtifactSizeUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to calculate artifact size");
            }

            _artifactSizeSummary = "Error calculating size";
            _hasPlatformSizes = false;
            ArtifactSizeUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Checks if a manifest can be deleted (requires a manifest and repository to be loaded)
    /// </summary>
    public bool CanDeleteManifest()
    {
        return _currentManifest != null && _currentRepository != null && _currentRegistry != null;
    }

    /// <summary>
    /// Deletes the current manifest
    /// </summary>
    public async Task<(bool success, string message)> DeleteManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_currentManifest == null)
        {
            return (false, "No manifest loaded");
        }

        if (_currentRepository == null)
        {
            return (false, "No repository selected");
        }

        if (_currentRegistry == null)
        {
            return (false, "No registry connected");
        }

        try
        {
            var repoPath = _currentRepository.FullPath.Replace(
                $"{_currentRegistry.Url}/",
                string.Empty
            );

            await _registryService.DeleteManifestAsync(repoPath, _currentManifest.Digest, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted manifest: {Digest}", _currentManifest.Digest);
            }

            // Clear the current manifest after deletion
            await SetManifestAsync(null, string.Empty, cancellationToken);

            // Create a friendly message
            var referenceInfo = _currentTag != null ? $"tag {_currentTag.Name}" : $"digest {_currentManifest.Digest.Substring(0, 12)}...";
            return (true, $"Deleted manifest for {referenceInfo}");
        }
        catch (RegistryOperationException regEx)
        {
            return (false, regEx.Message);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to delete manifest");
            }
            return (false, $"Error deleting manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the current manifest
    /// </summary>
    public async Task ClearManifestAsync()
    {
        await SetManifestAsync(null, string.Empty);
    }
}

/// <summary>
/// Event arguments for manifest change events
/// </summary>
public class ManifestChangedEventArgs : EventArgs
{
    public Manifest? OldManifest { get; }
    public Manifest? NewManifest { get; }

    public ManifestChangedEventArgs(Manifest? oldManifest, Manifest? newManifest)
    {
        OldManifest = oldManifest;
        NewManifest = newManifest;
    }
}

/// <summary>
/// Event arguments for registry change events
/// </summary>
public class RegistryChangedEventArgs : EventArgs
{
    public Registry? OldRegistry { get; }
    public Registry? NewRegistry { get; }

    public RegistryChangedEventArgs(Registry? oldRegistry, Registry? newRegistry)
    {
        OldRegistry = oldRegistry;
        NewRegistry = newRegistry;
    }
}

/// <summary>
/// Event arguments for repository change events
/// </summary>
public class RepositoryChangedEventArgs : EventArgs
{
    public Repository? OldRepository { get; }
    public Repository? NewRepository { get; }

    public RepositoryChangedEventArgs(Repository? oldRepository, Repository? newRepository)
    {
        OldRepository = oldRepository;
        NewRepository = newRepository;
    }
}

/// <summary>
/// Event arguments for tag change events
/// </summary>
public class TagChangedEventArgs : EventArgs
{
    public Tag? OldTag { get; }
    public Tag? NewTag { get; }

    public TagChangedEventArgs(Tag? oldTag, Tag? newTag)
    {
        OldTag = oldTag;
        NewTag = newTag;
    }
}

/// <summary>
/// Event arguments for reference change events
/// </summary>
public class ReferenceChangedEventArgs : EventArgs
{
    public string OldReference { get; }
    public string NewReference { get; }
    public bool WasDigest { get; }
    public bool IsDigest { get; }

    public ReferenceChangedEventArgs(string oldReference, string newReference, bool wasDigest, bool isDigest)
    {
        OldReference = oldReference ?? string.Empty;
        NewReference = newReference ?? string.Empty;
        WasDigest = wasDigest;
        IsDigest = isDigest;
    }
}

/// <summary>
/// Event arguments for complete artifact context change events
/// </summary>
public class ArtifactContextChangedEventArgs : EventArgs
{
    public Registry? OldRegistry { get; }
    public Registry? NewRegistry { get; }
    public Repository? OldRepository { get; }
    public Repository? NewRepository { get; }
    public string OldReference { get; }
    public string NewReference { get; }
    public bool WasDigest { get; }
    public bool IsDigest { get; }

    public ArtifactContextChangedEventArgs(
        Registry? oldRegistry, Registry? newRegistry,
        Repository? oldRepository, Repository? newRepository,
        string oldReference, string newReference,
        bool wasDigest, bool isDigest)
    {
        OldRegistry = oldRegistry;
        NewRegistry = newRegistry;
        OldRepository = oldRepository;
        NewRepository = newRepository;
        OldReference = oldReference ?? string.Empty;
        NewReference = newReference ?? string.Empty;
        WasDigest = wasDigest;
        IsDigest = isDigest;
    }
}
