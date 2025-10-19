using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for the repository selector component that handles repository filtering and selection.
/// </summary>
public class RepositorySelectorViewModel : ViewModelBase
{
    private readonly RepositoryService _RepositoryService;
    private readonly ArtifactService _artifactService;
    private readonly ILogger<RepositorySelectorViewModel> _logger;
    private string _filterText = string.Empty;
    private Repository? _selectedRepository;
    private ObservableCollection<Repository> _repositories = new();
    private ObservableCollection<Repository> _filteredRepositories = new();
    private RepositoryContextMenuViewModel _contextMenu = new();

    public RepositorySelectorViewModel(
        RepositoryService RepositoryService, 
        ArtifactService artifactService,
        ILogger<RepositorySelectorViewModel> logger)
    {
        _RepositoryService = RepositoryService;
        _artifactService = artifactService;
        _logger = logger;
        RefreshCommand = ReactiveCommand.Create(OnRefreshRequested);

        // Subscribe to repository loader events
        _RepositoryService.RepositoriesLoaded += OnRepositoriesLoaded;
        _RepositoryService.LoadFailed += OnRepositoryLoadFailed;

        // Update filtered repositories when filter text changes
        this.WhenAnyValue(x => x.FilterText)
            .Subscribe(_ => UpdateFilteredRepositories());

        // Load repository when selection changes
        this.WhenAnyValue(x => x.SelectedRepository)
            .Subscribe(repo =>
            {
                if (repo != null)
                {
                    LoadSelectedRepository();
                }
            });
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>
    /// Event raised when user requests to load repositories (e.g., after connecting to registry)
    /// </summary>
    public event EventHandler? RefreshRequested;

    /// <summary>
    /// Event raised when user requests to load a repository (Enter key or explicit selection)
    /// </summary>
    public event EventHandler<Repository>? RepositoryLoadRequested;

    /// <summary>
    /// Event raised when a repository is selected (via click or Enter key)
    /// Used by TagLoader to automatically load tags for the selected repository
    /// </summary>
    public event EventHandler<Repository>? RepositorySelected;

    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public Repository? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedRepository, value);
            
            // Update context menu when selection changes
            if (value != null)
            {
                ContextMenu.RepositoryName = value.Name;
                
                var repoPath = value.FullPath.Replace($"{_artifactService.CurrentRegistry?.Url}/", string.Empty);
                ContextMenu.RepositoryPath = repoPath;
                ContextMenu.RegistryUrl = _artifactService.CurrentRegistry?.Url ?? string.Empty;
                ContextMenu.IsActualRepository = value.IsLeaf;
            }
        }
    }

    public ObservableCollection<Repository> Repositories
    {
        get => _repositories;
    }

    public ObservableCollection<Repository> FilteredRepositories
    {
        get => _filteredRepositories;
        private set => this.RaiseAndSetIfChanged(ref _filteredRepositories, value);
    }

    /// <summary>
    /// Selects and loads a repository (called when Enter is pressed or explicit selection)
    /// </summary>
    public void SelectAndLoadRepository(Repository repository)
    {
        SelectedRepository = repository;
        LoadSelectedRepository();
    }

    /// <summary>
    /// Loads the currently selected repository
    /// </summary>
    public void LoadSelectedRepository()
    {
        if (SelectedRepository != null)
        {
            RepositoryLoadRequested?.Invoke(this, SelectedRepository);
            RepositorySelected?.Invoke(this, SelectedRepository);
        }
    }

    private void OnRepositoriesLoaded(object? sender, RepositoriesLoadedEventArgs e)
    {
        // Update the repositories collection
        _repositories.Clear();
        foreach (var repo in e.Repositories)
        {
            _repositories.Add(repo);
        }
        this.RaisePropertyChanged(nameof(Repositories));
        
        // Update filtered view
        UpdateFilteredRepositories();
    }

    private void OnRepositoryLoadFailed(object? sender, RepositoryLoadFailedEventArgs e)
    {
        // Clear repositories on failure
        _repositories.Clear();
        this.RaisePropertyChanged(nameof(Repositories));
        UpdateFilteredRepositories();
    }

    private void OnRefreshRequested()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Navigates to and selects a repository by its path, clearing filters and expanding ancestors.
    /// This ensures the repository is visible and properly selected in the TreeView.
    /// </summary>
    /// <param name="repositoryPath">The path of the repository (e.g., "dotnet/runtime")</param>
    /// <returns>True if the repository was found and selected, false otherwise</returns>
    public async Task<bool> NavigateToRepositoryAsync(string repositoryPath)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("NavigateToRepositoryAsync called for: {RepositoryPath}", repositoryPath);
            _logger.LogInformation("FilterText before clearing: '{FilterText}'", FilterText);
            _logger.LogInformation("FilteredRepositories count: {Count}", FilteredRepositories.Count);
        }
        
        // Clear the filter to ensure all repositories are visible
        if (!string.IsNullOrEmpty(FilterText))
        {
            FilterText = string.Empty;
            // Wait for the FilteredRepositories to update after clearing the filter
            await Task.Delay(50);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Filter cleared. FilteredRepositories count after delay: {Count}", FilteredRepositories.Count);
            }
        }
        
        // Search in FilteredRepositories since that's what the TreeView is bound to
        // After clearing the filter, FilteredRepositories should contain the original instances
        var repo = _RepositoryService.FindRepositoryByPath(repositoryPath, FilteredRepositories);
        if (repo != null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Repository found: {FullPath}, IsExpanded: {IsExpanded}", repo.FullPath, repo.IsExpanded);
            }
            
            // Expand ancestors first to make the repository visible
            ExpandRepositoryAncestors(repo);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Ancestors expanded");
            }
            
            // Wait for the TreeView to render the expanded nodes
            await Task.Delay(50);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Waited for TreeView expansion rendering");
            }
            
            // Set the selection using the property setter so the TreeView binding updates properly
            SelectedRepository = repo;
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("SelectedRepository set. Current selection: {Selection}", SelectedRepository?.FullPath);
            }
            
            return true;
        }
        
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Repository not found: {RepositoryPath}", repositoryPath);
        }
        return false;
    }

    /// <summary>
    /// Finds and selects a repository by its path, expanding ancestors in the tree.
    /// </summary>
    public async Task FindAndSelectRepositoryAsync(string repositoryPath)
    {
        // Use the new encapsulated navigation method
        await NavigateToRepositoryAsync(repositoryPath);
    }

    /// <summary>
    /// Expands all ancestor repositories in the tree to make the specified repository visible.
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

    private void UpdateFilteredRepositories()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            FilteredRepositories = new ObservableCollection<Repository>(Repositories);
        }
        else
        {
            var filter = FilterText.Replace("*", ".*");
            var filtered = new ObservableCollection<Repository>();

            foreach (var repo in Repositories)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(repo.Name, filter, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    filtered.Add(repo);
                }
                else if (repo.Children.Count > 0)
                {
                    // Check if any children match
                    var matchingChildren = new ObservableCollection<Repository>();
                    foreach (var child in repo.Children)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(child.Name, filter,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            matchingChildren.Add(child);
                        }
                    }

                    if (matchingChildren.Count > 0)
                    {
                        // Create a copy of the parent with only matching children
                        var parentCopy = new Repository
                        {
                            Name = repo.Name,
                            FullPath = repo.FullPath,
                            Registry = repo.Registry,
                            IsLeaf = false,
                            IsExpanded = true
                        };
                        foreach (var child in matchingChildren)
                        {
                            parentCopy.Children.Add(child);
                        }
                        filtered.Add(parentCopy);
                    }
                }
            }

            FilteredRepositories = filtered;
        }
    }

    /// <summary>
    /// Gets the context menu for the selected repository
    /// </summary>
    public RepositoryContextMenuViewModel ContextMenu
    {
        get => _contextMenu;
        set => this.RaiseAndSetIfChanged(ref _contextMenu, value);
    }
}
