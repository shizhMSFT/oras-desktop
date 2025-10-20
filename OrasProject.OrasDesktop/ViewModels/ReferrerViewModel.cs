using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for managing referrer tree display and operations
/// </summary>
public class ReferrerViewModel : ViewModelBase
{
    private readonly ArtifactService _artifactService;
    private readonly IRegistryService _registryService;
    private readonly StatusService _statusService;
    private readonly ILogger<ReferrerViewModel> _logger;
    
    private ObservableCollection<ReferrerNode> _referrers = new();
    private ReferrerNode? _selectedReferrerNode;
    private bool _isLoading;
    private ReferrerNodeContextMenuViewModel _contextMenu = new();

    public ReferrerViewModel(
        ArtifactService artifactService,
        IRegistryService registryService,
        StatusService statusService,
        ILogger<ReferrerViewModel> logger)
    {
        _artifactService = artifactService;
        _registryService = registryService;
        _statusService = statusService;
        _logger = logger;
    }

    /// <summary>
    /// Collection of referrer nodes
    /// </summary>
    public ObservableCollection<ReferrerNode> Referrers
    {
        get => _referrers;
        private set => this.RaiseAndSetIfChanged(ref _referrers, value);
    }

    /// <summary>
    /// Currently selected referrer node
    /// </summary>
    public ReferrerNode? SelectedReferrerNode
    {
        get => _selectedReferrerNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedReferrerNode, value);
            if (value != null)
            {
                UpdateContextMenu();
            }
        }
    }

    /// <summary>
    /// Whether referrers are currently being loaded
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// Context menu for referrer node operations
    /// </summary>
    public ReferrerNodeContextMenuViewModel ContextMenu
    {
        get => _contextMenu;
        set => this.RaiseAndSetIfChanged(ref _contextMenu, value);
    }

    /// <summary>
    /// Loads referrers for the current manifest
    /// </summary>
    public async Task LoadReferrersAsync(string repositoryPath, string digest, string reference, string fullReference, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        _statusService.SetProgress(0, isIndeterminate: false);

        try
        {
            var progress = CreateProgressHandler(reference, fullReference);
            var nodes = await _registryService.GetReferrersRecursiveAsync(
                repositoryPath, 
                digest, 
                progress, 
                cancellationToken);

            await UpdateReferrersCollection(nodes);
            ReportLoadComplete(nodes, reference, fullReference);
        }
        catch (RegistryOperationException regEx)
        {
            HandleLoadError(regEx.Message);
        }
        catch (Exception ex)
        {
            HandleLoadError($"Error loading referrers: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears all referrers
    /// </summary>
    public void Clear()
    {
        Referrers.Clear();
        SelectedReferrerNode = null;
    }

    /// <summary>
    /// Updates the context menu for the selected referrer node
    /// </summary>
    public void UpdateContextMenu()
    {
        if (SelectedReferrerNode == null) return;

        var registryUrl = _artifactService.CurrentRegistry?.Url ?? string.Empty;
        var repository = GetRepositoryPath();

        ContextMenu.RegistryUrl = registryUrl;
        ContextMenu.Repository = repository;
        ContextMenu.Node = SelectedReferrerNode;
    }

    /// <summary>
    /// Creates a progress handler for referrer loading
    /// </summary>
    private Progress<int> CreateProgressHandler(string reference, string fullReference)
    {
        return new Progress<int>(count =>
        {
            _statusService.SetStatus($"Loading referrers ({count}) for {reference} ({fullReference})...");
            
            if (count == 1)
            {
                _statusService.SetProgress(0);
            }
            else
            {
                _statusService.SetProgress(Math.Min(count, 100));
            }
        });
    }

    /// <summary>
    /// Updates the referrers collection on the UI thread
    /// </summary>
    private async Task UpdateReferrersCollection(System.Collections.Generic.IReadOnlyList<ReferrerNode> nodes)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Referrers.Clear();
            foreach (var node in nodes)
            {
                Referrers.Add(node);
            }
        });
    }

    /// <summary>
    /// Reports successful load completion with statistics
    /// </summary>
    private void ReportLoadComplete(System.Collections.Generic.IReadOnlyList<ReferrerNode> nodes, string reference, string fullReference)
    {
        int total = CountReferrers(nodes);
        string referrerWord = total == 1 ? "referrer" : "referrers";
        _statusService.SetStatus($"Loaded {total} {referrerWord} for {reference} ({fullReference})");
        _statusService.SetProgress(100);
    }

    /// <summary>
    /// Counts total referrers in the node tree
    /// </summary>
    private int CountReferrers(System.Collections.Generic.IReadOnlyList<ReferrerNode> nodes)
    {
        int total = 0;
        
        void CountNode(ReferrerNode node)
        {
            if (node.Info != null) total++;
            foreach (var child in node.Children)
            {
                CountNode(child);
            }
        }
        
        foreach (var root in nodes)
        {
            CountNode(root);
        }
        
        return total;
    }

    /// <summary>
    /// Handles referrer load errors
    /// </summary>
    private void HandleLoadError(string message)
    {
        _statusService.SetStatus(message, isError: true);
        Referrers.Clear();
    }

    /// <summary>
    /// Gets the current repository path without registry URL
    /// </summary>
    private string GetRepositoryPath()
    {
        var repository = _artifactService.CurrentRepository;
        var registry = _artifactService.CurrentRegistry;
        
        if (repository == null || registry == null)
        {
            return string.Empty;
        }

        return repository.FullPath.Replace($"{registry.Url}/", string.Empty);
    }
}
