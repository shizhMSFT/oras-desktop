using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service responsible for orchestrating navigation to a manifest reference.
/// When a manifest is loaded, this service ensures:
/// 1. The correct registry is connected
/// 2. Repositories are loaded
/// 3. The correct repository is selected
/// 4. Tags are loaded
/// 5. The correct tag is selected
/// Without causing infinite loops or retriggering the manifest load.
/// </summary>
public class ManifestNavigator
{
    private readonly ConnectionService _connectionService;
    private readonly RepositoryService _repositoryService;
    private readonly TagService _tagService;
    private readonly ManifestService _manifestService;
    private readonly ILogger<ManifestNavigator> _logger;
    private bool _isNavigating = false;
    private string _pendingTagSelection = string.Empty;
    private Func<string, Repository?>? _findRepositoryByPath;

    public ManifestNavigator(
        ConnectionService connectionService,
        RepositoryService repositoryService,
        TagService tagService,
        ManifestService manifestService,
        ILogger<ManifestNavigator> logger)
    {
        _connectionService = connectionService;
        _repositoryService = repositoryService;
        _tagService = tagService;
        _manifestService = manifestService;
        _logger = logger;

        // Subscribe to ManifestService.LoadCompleted to start navigation
        _manifestService.LoadCompleted += OnManifestLoadCompleted;

        // Subscribe to TagsLoaded to complete navigation after tags are loaded
        _tagService.TagsLoaded += OnTagsLoaded;
    }
    
    /// <summary>
    /// Sets the repository finder function that will be used to locate repositories by path.
    /// This should be called by MainViewModel during initialization.
    /// </summary>
    public void SetRepositoryFinder(Func<string, Repository?> findRepositoryByPath)
    {
        _findRepositoryByPath = findRepositoryByPath;
    }

    /// <summary>
    /// Event raised when navigation to a tag is ready (tags have been loaded)
    /// The MainViewModel should subscribe to this to update the selected tag
    /// </summary>
    public event EventHandler<TagNavigationReadyEventArgs>? TagNavigationReady;

    /// <summary>
    /// Event raised when a repository is found and tags should be loaded
    /// </summary>
    public event EventHandler<RepositoryNavigationReadyEventArgs>? RepositoryNavigationReady;

    private async void OnManifestLoadCompleted(object? sender, ManifestLoadedEventArgs e)
    {
        // Only navigate for ReferenceBox loads (user typed a reference)
        // History and TagSelection should not trigger navigation
        if (e.Source != LoadSource.ReferenceBox)
        {
            return;
        }

        await NavigateToReferenceAsync(e.Reference);
    }

    private async Task NavigateToReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (_isNavigating)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Navigation already in progress, ignoring request for {Reference}", reference);
            }
            return;
        }

        try
        {
            _isNavigating = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Starting navigation to reference: {Reference}", reference);
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

            // Check if we need to connect to a different registry
            var currentRegistry = _connectionService.CurrentRegistry;
            if (currentRegistry == null || !string.Equals(currentRegistry.Url, parsed.RegistryUrl, StringComparison.OrdinalIgnoreCase))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Registry mismatch - need to connect to {NewRegistry} (current: {CurrentRegistry})",
                        parsed.RegistryUrl, currentRegistry?.Url ?? "<none>");
                }

                // Store pending tag selection
                _pendingTagSelection = parsed.TagOrDigest;

                // Connect to the new registry (this will trigger RepositoryLoader automatically via ConnectionEstablished event)
                // Then when repositories are loaded, we need to select the repository (which will trigger TagLoader)
                // Then when tags are loaded, OnTagsLoaded will fire TagNavigationReady

                // Note: For now, we won't auto-connect - that should remain a user action
                // Just log that we can't navigate because registry is different
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Cannot navigate - different registry. User must connect to {Registry} first.", parsed.RegistryUrl);
                }
                return;
            }

            // Same registry - check if we need to load the repository
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Same registry - checking if repository {Repository} is loaded", parsed.Repository);
            }

            // Store pending tag selection
            _pendingTagSelection = parsed.TagOrDigest;

            // Find the repository in the loaded repositories
            var repository = FindRepository(parsed.Repository);
            if (repository == null)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Repository {Repository} not found in loaded repositories", parsed.Repository);
                }
                return;
            }

            // Repository found - trigger tag loading
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Repository found - loading tags for {Repository}", parsed.Repository);
            }

            // This will trigger TagLoader via RepositorySelected event (wired in MainViewModel)
            // Raise event that MainViewModel can subscribe to for selecting the repository
            RepositoryNavigationReady?.Invoke(this, new RepositoryNavigationReadyEventArgs(repository));
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void OnTagsLoaded(object? sender, TagsLoadedEventArgs e)
    {
        if (!_isNavigating || string.IsNullOrEmpty(_pendingTagSelection))
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Tags loaded during navigation - searching for tag {Tag}", _pendingTagSelection);
        }

        // Find the tag in the loaded tags
        var tag = e.Tags.FirstOrDefault(t => t.Name == _pendingTagSelection);
        if (tag != null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Tag found - raising TagNavigationReady for {Tag}", _pendingTagSelection);
            }

            TagNavigationReady?.Invoke(this, new TagNavigationReadyEventArgs(tag));
        }
        else if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Tag {Tag} not found in loaded tags", _pendingTagSelection);
        }

        _pendingTagSelection = string.Empty;
    }

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

            var lastSlash = registryAndRepo.IndexOf('/');
            if (lastSlash < 0)
            {
                return null;
            }

            return new ReferenceComponents
            {
                RegistryUrl = registryAndRepo.Substring(0, lastSlash),
                Repository = registryAndRepo.Substring(lastSlash + 1),
                TagOrDigest = digest,
                IsDigest = true
            };
        }

        // Split by : (for tag references)
        var tagSplit = reference.Split(':', 2);
        if (tagSplit.Length == 2)
        {
            // Tag reference: registry/repo:tag
            var registryAndRepo = tagSplit[0];
            var tag = tagSplit[1];

            var lastSlash = registryAndRepo.IndexOf('/');
            if (lastSlash < 0)
            {
                return null;
            }

            return new ReferenceComponents
            {
                RegistryUrl = registryAndRepo.Substring(0, lastSlash),
                Repository = registryAndRepo.Substring(lastSlash + 1),
                TagOrDigest = tag,
                IsDigest = false
            };
        }

        return null;
    }

    private Repository? FindRepository(string repositoryPath)
    {
        if (_findRepositoryByPath == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Repository finder not set - cannot find repository {Repository}", repositoryPath);
            }
            return null;
        }
        
        return _findRepositoryByPath(repositoryPath);
    }

    private class ReferenceComponents
    {
        public string RegistryUrl { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string TagOrDigest { get; set; } = string.Empty;
        public bool IsDigest { get; set; }
    }
}

public class RepositoryNavigationReadyEventArgs : EventArgs
{
    public Repository Repository { get; }

    public RepositoryNavigationReadyEventArgs(Repository repository)
    {
        Repository = repository;
    }
}

public class TagNavigationReadyEventArgs : EventArgs
{
    public Tag Tag { get; }

    public TagNavigationReadyEventArgs(Tag tag)
    {
        Tag = tag;
    }
}
