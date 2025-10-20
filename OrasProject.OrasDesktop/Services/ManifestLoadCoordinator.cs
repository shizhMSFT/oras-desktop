using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Coordinates the loading of repositories and tags when a manifest is loaded from a reference.
/// This is particularly important for registries like DockerHub that don't support repository listing.
/// When a manifest is loaded via reference (e.g., docker.io/library/nginx:latest):
/// 1. Checks if repositories are loaded; if not, attempts to load them (may fail for DockerHub)
/// 2. Ensures the repository from the reference is available
/// 3. Loads tags for the repository
/// 4. Selects the matching tag if it's a tag reference (not a digest)
/// </summary>
public class ManifestLoadCoordinator
{
    private readonly ConnectionService _connectionService;
    private readonly RepositoryService _repositoryService;
    private readonly TagService _tagService;
    private readonly ArtifactService _artifactService;
    private readonly ManifestService _manifestService;
    private readonly StatusService _statusService;
    private readonly ILogger<ManifestLoadCoordinator> _logger;
    private bool _isCoordinating = false;

    public ManifestLoadCoordinator(
        ConnectionService connectionService,
        RepositoryService repositoryService,
        TagService tagService,
        ArtifactService artifactService,
        ManifestService manifestService,
        StatusService statusService,
        ILogger<ManifestLoadCoordinator> logger)
    {
        _connectionService = connectionService;
        _repositoryService = repositoryService;
        _tagService = tagService;
        _artifactService = artifactService;
        _manifestService = manifestService;
        _statusService = statusService;
        _logger = logger;

        // Subscribe to manifest load completion
        _manifestService.LoadCompleted += OnManifestLoadCompleted;
    }

    /// <summary>
    /// Event raised when tags have been loaded and a matching tag should be selected in the UI
    /// </summary>
    public event EventHandler<TagSelectionRequestedEventArgs>? TagSelectionRequested;

    /// <summary>
    /// Event raised when a repository should be selected in the UI
    /// </summary>
    public event EventHandler<RepositorySelectionRequestedEventArgs>? RepositorySelectionRequested;
    
    /// <summary>
    /// Event raised when a manifest is loaded by digest and tags should be synced based on digest
    /// </summary>
    public event EventHandler<DigestSelectionRequestedEventArgs>? DigestSelectionRequested;

    private async void OnManifestLoadCompleted(object? sender, ManifestLoadedEventArgs e)
    {
        // Only coordinate for ReferenceBox loads (user typed a reference)
        // History and TagSelection should not trigger this coordination
        if (e.Source != LoadSource.ReferenceBox)
        {
            return;
        }

        await CoordinateLoadAsync(e.Reference);
    }

    private async Task CoordinateLoadAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (_isCoordinating)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Coordination already in progress, ignoring request for {Reference}", reference);
            }
            return;
        }

        try
        {
            _isCoordinating = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Starting load coordination for reference: {Reference}", reference);
            }

            // Parse the reference: registry/repository:tag or registry/repository@digest
            var parsed = ParseReference(reference);
            if (parsed == null)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Failed to parse reference: {Reference}", reference);
                }
                return;
            }

            // Check if we're connected to the right registry
            var currentRegistry = _artifactService.CurrentRegistry;
            if (currentRegistry == null || !string.Equals(currentRegistry.Url, parsed.RegistryUrl, StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Cannot coordinate - different registry. Current: {Current}, Required: {Required}",
                        currentRegistry?.Url ?? "<none>", parsed.RegistryUrl);
                }
                return;
            }

            // Check if we need to load the repository first
            // For registries like DockerHub, repository listing might fail, so we'll create a minimal Repository object
            Repository? repository = await EnsureRepositoryAsync(parsed.Repository, currentRegistry, cancellationToken);
            if (repository == null)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Could not ensure repository {Repository}", parsed.Repository);
                }
                return;
            }

            // Update ArtifactService with the repository context
            _artifactService.SetRepository(repository);

            // Request repository selection in the UI (if repositories were loaded)
            RepositorySelectionRequested?.Invoke(this, new RepositorySelectionRequestedEventArgs(repository));

            // Load tags for the repository
            await _tagService.LoadTagsAsync(repository, currentRegistry, cancellationToken);

            // If this is a tag reference (not a digest), request tag selection in the UI
            if (!parsed.IsDigest)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Requesting tag selection for tag: {Tag}", parsed.TagOrDigest);
                }
                TagSelectionRequested?.Invoke(this, new TagSelectionRequestedEventArgs(parsed.TagOrDigest));
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Reference is a digest, will sync tags after manifest loads");
                }
                // For digest references, we need to wait for the manifest to load
                // Then we'll try to find a matching tag based on the loaded manifest's digest
                // The actual digest from the manifest will be available via CurrentManifest
                // We'll request digest-based tag syncing after tags are loaded
                if (_artifactService.CurrentManifest != null)
                {
                    DigestSelectionRequested?.Invoke(this, new DigestSelectionRequestedEventArgs(_artifactService.CurrentManifest.Digest));
                }
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error during load coordination for reference: {Reference}", reference);
            }
        }
        finally
        {
            _isCoordinating = false;
        }
    }

    /// <summary>
    /// Ensures that the repository exists, either by finding it in loaded repositories
    /// or by creating a minimal Repository object if repository listing is not supported
    /// </summary>
    private async Task<Repository?> EnsureRepositoryAsync(string repositoryPath, Registry registry, CancellationToken cancellationToken)
    {
        // Check if we have loaded repositories
        var currentRepo = _artifactService.CurrentRepository;
        
        // Check if the current repository matches
        if (currentRepo != null)
        {
            var currentRepoPath = currentRepo.FullPath.Replace($"{registry.Url}/", string.Empty);
            if (string.Equals(currentRepoPath, repositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Repository {Repository} already loaded", repositoryPath);
                }
                return currentRepo;
            }
        }

        // Try to load repositories from the registry (this might fail for DockerHub)
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Attempting to load repositories from registry to find {Repository}", repositoryPath);
            }

            // This will fire RepositoriesLoaded event if successful
            await _repositoryService.LoadRepositoriesAsync(registry, cancellationToken);

            // Try to find the repository in the loaded list
            // Note: This requires access to the repository tree, which we'll handle via event subscription
            // For now, we'll return null and rely on the RepositoriesLoaded event handler to continue
        }
        catch (RegistryOperationException ex) when (ex.Message.Contains("not supported") || ex.Message.Contains("404"))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Repository listing not supported - creating minimal repository object for {Repository}", repositoryPath);
            }

            // Create a minimal repository object for registries that don't support listing
            return CreateMinimalRepository(repositoryPath, registry);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to load repositories - creating minimal repository object for {Repository}", repositoryPath);
            }

            // Fallback: create a minimal repository object
            return CreateMinimalRepository(repositoryPath, registry);
        }

        // If we successfully loaded repositories, return null and rely on the event handler to continue
        // This is a bit awkward but avoids circular dependencies
        return null;
    }

    /// <summary>
    /// Creates a minimal Repository object when repository listing is not supported
    /// </summary>
    private Repository CreateMinimalRepository(string repositoryPath, Registry registry)
    {
        var segments = repositoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var name = segments.Length > 0 ? segments[segments.Length - 1] : repositoryPath;
        
        return new Repository
        {
            Name = name,
            FullPath = $"{registry.Url}/{repositoryPath}",
            Registry = registry,
            IsLeaf = true // Assume it's a leaf since we're creating it from a reference
        };
    }

    /// <summary>
    /// Parses a reference string into its components
    /// </summary>
    private ReferenceComponents? ParseReference(string reference)
    {
        // Expected formats:
        // registry/repo:tag
        // registry/repo@sha256:digest
        // registry/namespace/repo:tag

        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        // Split by @ first (for digest references)
        var digestSplit = reference.Split('@', 2);
        if (digestSplit.Length == 2)
        {
            // Digest reference: registry/repo@digest
            var registryAndRepo = digestSplit[0];
            var digest = digestSplit[1];

            var firstSlash = registryAndRepo.IndexOf('/');
            if (firstSlash < 0)
            {
                return null;
            }

            return new ReferenceComponents
            {
                RegistryUrl = registryAndRepo.Substring(0, firstSlash),
                Repository = registryAndRepo.Substring(firstSlash + 1),
                TagOrDigest = digest,
                IsDigest = true
            };
        }

        // Split by : (for tag references)
        // Need to be careful with port numbers in registry URLs
        var colonIndex = reference.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var registryAndRepo = reference.Substring(0, colonIndex);
            var tag = reference.Substring(colonIndex + 1);

            // Check if this colon is part of a port number (e.g., localhost:5000/repo)
            // If there's no slash after the last colon, it's likely a port number
            var slashAfterColon = reference.IndexOf('/', colonIndex);
            if (slashAfterColon < 0)
            {
                // No slash after colon - might be just registry:port without repo/tag
                // Try to find the first slash to separate registry from repo
                var firstSlash = registryAndRepo.IndexOf('/');
                if (firstSlash < 0)
                {
                    // No repository path, invalid reference
                    return null;
                }

                return new ReferenceComponents
                {
                    RegistryUrl = registryAndRepo.Substring(0, firstSlash),
                    Repository = registryAndRepo.Substring(firstSlash + 1),
                    TagOrDigest = tag,
                    IsDigest = false
                };
            }
            else
            {
                // Slash after colon means colon is part of registry:port
                // Find last colon before the slash
                var lastColonBeforeSlash = registryAndRepo.LastIndexOf(':', registryAndRepo.IndexOf('/'));
                if (lastColonBeforeSlash > 0)
                {
                    // Registry has a port
                    var firstSlash = registryAndRepo.IndexOf('/');
                    return new ReferenceComponents
                    {
                        RegistryUrl = registryAndRepo.Substring(0, firstSlash),
                        Repository = registryAndRepo.Substring(firstSlash + 1),
                        TagOrDigest = tag,
                        IsDigest = false
                    };
                }
                else
                {
                    // No port in registry
                    var firstSlash = registryAndRepo.IndexOf('/');
                    if (firstSlash < 0)
                    {
                        return null;
                    }

                    return new ReferenceComponents
                    {
                        RegistryUrl = registryAndRepo.Substring(0, firstSlash),
                        Repository = registryAndRepo.Substring(firstSlash + 1),
                        TagOrDigest = tag,
                        IsDigest = false
                    };
                }
            }
        }

        return null;
    }

    private class ReferenceComponents
    {
        public string RegistryUrl { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string TagOrDigest { get; set; } = string.Empty;
        public bool IsDigest { get; set; }
    }
}

/// <summary>
/// Event arguments for requesting tag selection in the UI
/// </summary>
public class TagSelectionRequestedEventArgs : EventArgs
{
    public string TagName { get; }

    public TagSelectionRequestedEventArgs(string tagName)
    {
        TagName = tagName;
    }
}

/// <summary>
/// Event arguments for requesting digest-based tag selection in the UI
/// </summary>
public class DigestSelectionRequestedEventArgs : EventArgs
{
    public string Digest { get; }

    public DigestSelectionRequestedEventArgs(string digest)
    {
        Digest = digest;
    }
}

/// <summary>
/// Event arguments for requesting repository selection in the UI
/// </summary>
public class RepositorySelectionRequestedEventArgs : EventArgs
{
    public Repository Repository { get; }

    public RepositorySelectionRequestedEventArgs(Repository repository)
    {
        Repository = repository;
    }
}
