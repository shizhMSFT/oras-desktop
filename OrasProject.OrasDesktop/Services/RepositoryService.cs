using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service responsible for loading repositories from the registry.
/// Similar to ManifestService, this encapsulates repository loading logic and provides events for components to subscribe to.
/// Subscribes to ConnectionService to automatically load repositories when a connection is established.
/// </summary>
public class RepositoryService
{
    private readonly IRegistryService _registryService;
    private readonly ArtifactService _artifactService;
    private readonly StatusService _statusService;
    private readonly ILogger<RepositoryService> _logger;
    private Registry? _currentRegistry;

    public RepositoryService(
        IRegistryService registryService,
        ArtifactService artifactService,
        StatusService statusService,
        ConnectionService connectionService,
        ILogger<RepositoryService> logger)
    {
        _registryService = registryService;
        _artifactService = artifactService;
        _statusService = statusService;
        _logger = logger;

        // Subscribe to connection events to automatically load repositories
        connectionService.ConnectionEstablished += OnConnectionEstablished;
    }

    /// <summary>
    /// Event raised when repositories have been successfully loaded from the registry
    /// </summary>
    public event EventHandler<RepositoriesLoadedEventArgs>? RepositoriesLoaded;

    /// <summary>
    /// Event raised when a repository load operation fails
    /// </summary>
    public event EventHandler<RepositoryLoadFailedEventArgs>? LoadFailed;

    /// <summary>
    /// Gets the current registry that repositories are being loaded from
    /// </summary>
    public Registry? CurrentRegistry => _currentRegistry;

    /// <summary>
    /// Loads repositories from the connected registry and builds a hierarchical tree structure
    /// </summary>
    public async Task LoadRepositoriesAsync(Registry registry, CancellationToken cancellationToken = default)
    {
        _currentRegistry = registry;

        _statusService.SetBusy(true);
        _statusService.SetStatus($"Loading repositories from {registry.Url}...");

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Loading repositories from registry: {Registry}", registry.Url);
            }

            System.Diagnostics.Debug.WriteLine($"[RepositoryService.LoadRepositoriesAsync] About to call ListRepositoriesAsync");
            var repos = await _registryService.ListRepositoriesAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[RepositoryService.LoadRepositoriesAsync] ListRepositoriesAsync returned {repos.Count} repositories");
            
            var repositoryTree = await BuildRepositoryTreeAsync(registry, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully loaded {Count} repositories", repositoryTree.Count);
            }

            System.Diagnostics.Debug.WriteLine($"[RepositoryService.LoadRepositoriesAsync] Firing RepositoriesLoaded event with {repositoryTree.Count} repositories");
            _statusService.SetStatus($"Loaded {repositoryTree.Count} repositories for {registry.Url}"); // Show count and registry
            RepositoriesLoaded?.Invoke(this, new RepositoriesLoadedEventArgs(repositoryTree, registry));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RepositoryService.LoadRepositoriesAsync] Exception: {ex.Message}");
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to load repositories from registry: {Registry}", registry.Url);
            }

            _statusService.SetStatus($"Failed to load repositories: {ex.Message}", isError: true);
            LoadFailed?.Invoke(this, new RepositoryLoadFailedEventArgs(ex, registry));
        }
        finally
        {
            _statusService.SetBusy(false);
        }
    }

    /// <summary>
    /// Refreshes the repository list from the current registry
    /// </summary>
    public async Task RefreshRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var registry = _artifactService.CurrentRegistry;
        if (registry == null)
        {
            _statusService.SetStatus("Not connected to a registry", isError: true);
            return;
        }

        _statusService.SetBusy(true);
        _statusService.SetStatus("Refreshing repositories...");

        try
        {
            await LoadRepositoriesAsync(registry, cancellationToken);
            _statusService.SetStatus("Repositories refreshed"); // Show success message for explicit refresh
        }
        catch (Exception ex)
        {
            _statusService.SetStatus($"Failed to refresh repositories: {ex.Message}", isError: true);
        }
        finally
        {
            _statusService.SetBusy(false);
        }
    }

    /// <summary>
    /// Builds a hierarchical tree structure of repositories from the flat list returned by the registry
    /// </summary>
    private async Task<IReadOnlyList<Repository>> BuildRepositoryTreeAsync(Registry registry, CancellationToken cancellationToken)
    {
        var repos = await _registryService.ListRepositoriesAsync(cancellationToken);
        var rootRepositories = new List<Repository>();
        var allRepositories = new Dictionary<string, Repository>();

        // Create repository objects for all paths
        foreach (var repoPath in repos)
        {
            var segments = repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;
            Repository? parent = null;

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var previousPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
                var fullPath = $"{registry.Url}/{currentPath}";

                if (!allRepositories.ContainsKey(fullPath))
                {
                    var repo = new Repository
                    {
                        Name = segment,
                        FullPath = fullPath,
                        Registry = registry,
                        IsLeaf = i == segments.Length - 1 // Last segment is a leaf (has tags)
                    };

                    allRepositories[fullPath] = repo;

                    if (parent == null)
                    {
                        rootRepositories.Add(repo);
                    }
                    else
                    {
                        repo.Parent = parent;
                        parent.Children.Add(repo);
                    }
                }

                parent = allRepositories[fullPath];
            }
        }

        // Sort repositories at each level
        SortRepositories(rootRepositories);
        return rootRepositories;
    }

    private void SortRepositories(List<Repository> repositories)
    {
        repositories.Sort();
        foreach (var repo in repositories)
        {
            if (repo.Children.Count > 0)
            {
                SortRepositories(repo.Children);
            }
        }
    }

    /// <summary>
    /// Finds a repository by its path (without registry URL prefix)
    /// </summary>
    public Repository? FindRepositoryByPath(string path, IEnumerable<Repository> repositories)
    {
        foreach (var repo in repositories)
        {
            var found = FindRepositoryByPathRecursive(repo, path);
            if (found != null)
                return found;
        }
        return null;
    }

    private Repository? FindRepositoryByPathRecursive(Repository parent, string targetPath)
    {
        var registry = _artifactService.CurrentRegistry;
        if (registry == null)
            return null;

        // Check if this repository matches
        string repoFullPath = parent.FullPath.Replace($"{registry.Url}/", string.Empty);
        if (string.Equals(repoFullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return parent;

        // Check children
        foreach (var child in parent.Children)
        {
            var found = FindRepositoryByPathRecursive(child, targetPath);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Finds a repository by its name (searches recursively)
    /// </summary>
    public Repository? FindRepositoryByName(Repository parent, string name)
    {
        // Check if this repository matches
        if (string.Equals(parent.Name, name, StringComparison.OrdinalIgnoreCase))
            return parent;

        // Check children
        foreach (var child in parent.Children)
        {
            var found = FindRepositoryByName(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Finds a repository by path, searching through the repository tree.
    /// If not found by exact path, tries to find by the last segment of the path.
    /// Expands ancestor repositories if found.
    /// </summary>
    public Repository? FindAndSelectRepository(string repositoryPath, IEnumerable<Repository> repositories)
    {
        // Try to find repository by path
        var found = FindRepositoryByPath(repositoryPath, repositories);

        // If not found by path, try to find by last segment of the path
        if (found == null)
        {
            var segments = repositoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                string lastSegment = segments[segments.Length - 1];
                
                // Search by name in all root repositories
                foreach (var repo in repositories)
                {
                    found = FindRepositoryByName(repo, lastSegment);
                    if (found != null)
                        break;
                }
            }
        }

        // If found, expand its ancestors
        if (found != null)
        {
            ExpandRepositoryAncestors(found);
        }

        return found;
    }

    /// <summary>
    /// Expands all ancestor repositories up to the root
    /// </summary>
    public void ExpandRepositoryAncestors(Repository repository)
    {
        var current = repository;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Event handler for when a connection is established - automatically loads repositories
    /// </summary>
    private async void OnConnectionEstablished(object? sender, ConnectionEstablishedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[RepositoryService] OnConnectionEstablished called for registry: {e.Registry.Url}");
        await LoadRepositoriesAsync(e.Registry, CancellationToken.None);
        System.Diagnostics.Debug.WriteLine($"[RepositoryService] LoadRepositoriesAsync completed");
    }
}

/// <summary>
/// Event args for when repositories are successfully loaded
/// </summary>
public class RepositoriesLoadedEventArgs : EventArgs
{
    public IReadOnlyList<Repository> Repositories { get; }
    public Registry Registry { get; }

    public RepositoriesLoadedEventArgs(IReadOnlyList<Repository> repositories, Registry registry)
    {
        Repositories = repositories;
        Registry = registry;
    }
}

/// <summary>
/// Event args for when repository loading fails
/// </summary>
public class RepositoryLoadFailedEventArgs : EventArgs
{
    public Exception Exception { get; }
    public Registry Registry { get; }

    public RepositoryLoadFailedEventArgs(Exception exception, Registry registry)
    {
        Exception = exception;
        Registry = registry;
    }
}
