using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service responsible for loading tags from a repository.
/// Subscribes to RepositorySelector's RepositoryLoadRequested event to automatically load tags when a repository is selected.
/// </summary>

public class TagService
{
    private readonly IRegistryService _registryService;
    private readonly ArtifactService _artifactService;
    private readonly StatusService _statusService;
    private readonly ILogger<TagService> _logger;
    private Repository? _currentRepository;

    public TagService(
        IRegistryService registryService,
        ArtifactService artifactService,
        StatusService statusService,
        ILogger<TagService> logger)
    {
        _registryService = registryService;
        _artifactService = artifactService;
        _statusService = statusService;
        _logger = logger;
        
        // Subscribe to repository changes from ArtifactService
        _artifactService.RepositoryChanged += OnRepositoryChanged;
    }
    
    /// <summary>
    /// Handles repository change events from ArtifactService
    /// </summary>
    private void OnRepositoryChanged(object? sender, RepositoryChangedEventArgs e)
    {
        if (e.NewRepository != null && _artifactService.CurrentRegistry != null)
        {
            _ = LoadTagsAsync(e.NewRepository, _artifactService.CurrentRegistry);
        }
    }

    /// <summary>
    /// Event raised when tags have been successfully loaded from a repository
    /// </summary>
    public event EventHandler<TagsLoadedEventArgs>? TagsLoaded;

    /// <summary>
    /// Event raised when a tag load operation fails
    /// </summary>
    public event EventHandler<TagLoadFailedEventArgs>? LoadFailed;

    /// <summary>
    /// Gets the current repository that tags are being loaded from
    /// </summary>
    public Repository? CurrentRepository => _currentRepository;

    /// <summary>
    /// Event handler for repository selection events.
    /// Requires registry to be passed explicitly since the event only provides repository.
    /// </summary>
    public void OnRepositorySelected(Repository repository, Registry registry)
    {
        _ = LoadTagsAsync(repository, registry);
    }

    /// <summary>
    /// Refreshes tags for the current repository
    /// </summary>
    public async Task RefreshTagsAsync(CancellationToken cancellationToken = default)
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;

        if (registry == null)
        {
            _statusService.SetStatus("Not connected to a registry", isError: true);
            return;
        }

        if (repository == null)
        {
            _statusService.SetStatus("No repository selected", isError: true);
            return;
        }

        _statusService.SetBusy(true);
        _statusService.SetStatus("Refreshing tags...");

        try
        {
            await LoadTagsAsync(repository, registry, cancellationToken);
            _statusService.SetStatus("Tags refreshed"); // Show success message for explicit refresh
        }
        catch (Exception ex)
        {
            _statusService.SetStatus($"Failed to refresh tags: {ex.Message}", isError: true);
        }
        finally
        {
            _statusService.SetBusy(false);
        }
    }

    /// <summary>
    /// Loads tags for the specified repository
    /// </summary>
    public async Task LoadTagsAsync(Repository repository, Registry registry, CancellationToken cancellationToken = default)
    {
        if (repository == null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        _currentRepository = repository;

        _statusService.SetBusy(true);
        _statusService.SetStatus($"Loading tags for {repository.Name}...");

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Loading tags for repository: {Repository}", repository.Name);
            }

            var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);
            var tagNames = await _registryService.ListTagsAsync(repositoryPath, cancellationToken);

            var tags = new List<Tag>();
            foreach (var name in tagNames)
            {
                tags.Add(new Tag
                {
                    Name = name,
                    Repository = repository,
                    CreatedAt = DateTimeOffset.Now,
                });
            }

            // Do not sort tags; OCI spec guarantees lexical order

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully loaded {Count} tags for repository: {Repository}", tags.Count, repository.Name);
            }

            _statusService.SetStatus($"Loaded {tags.Count} tags for {repository.FullPath}"); // Show count with full registry/repository path
            TagsLoaded?.Invoke(this, new TagsLoadedEventArgs(tags, repository));
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to load tags for repository: {Repository}", repository.Name);
            }

            _statusService.SetStatus($"Failed to load tags: {ex.Message}", isError: true);
            LoadFailed?.Invoke(this, new TagLoadFailedEventArgs(ex, repository));
        }
        finally
        {
            _statusService.SetBusy(false);
        }
    }
}

/// <summary>
/// Event arguments for the TagsLoaded event
/// </summary>
public class TagsLoadedEventArgs : EventArgs
{
    public TagsLoadedEventArgs(IReadOnlyList<Tag> tags, Repository repository)
    {
        Tags = tags;
        Repository = repository;
    }

    public IReadOnlyList<Tag> Tags { get; }
    public Repository Repository { get; }
}

/// <summary>
/// Event arguments for the TagLoadFailed event
/// </summary>
public class TagLoadFailedEventArgs : EventArgs
{
    public TagLoadFailedEventArgs(Exception exception, Repository repository)
    {
        Exception = exception;
        Repository = repository;
    }

    public Exception Exception { get; }
    public Repository Repository { get; }
}
