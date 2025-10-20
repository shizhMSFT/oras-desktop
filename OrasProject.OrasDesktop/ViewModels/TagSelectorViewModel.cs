using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for the tag selector component.
/// Handles tag filtering, selection, and loading requests.
/// </summary>
public class TagSelectorViewModel : ViewModelBase
{
    private readonly TagService _tagService;
    private string _filterText = string.Empty;
    private Tag? _selectedTag;
    private ObservableCollection<Tag> _tags = new();
    private List<Tag> _allTags = new();
    private TagContextMenuViewModel _contextMenu = new();
    private System.Threading.Timer? _tagSelectionTimer;
    private const int SelectionDebounceMilliseconds = 500; // Wait 500ms before loading
    
    public TagSelectorViewModel(TagService tagService)
    {
        _tagService = tagService;
        
        // Subscribe to tag service events
        _tagService.TagsLoaded += OnTagsLoaded;
        _tagService.LoadFailed += OnTagLoadFailed;
        
        RefreshTagsCommand = ReactiveCommand.Create(RequestRefresh);
        CopyTagCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedTag != null && ContextMenu != null)
            {
                ContextMenu.CopyTagCommand.Execute(Unit.Default);
            }
        });
        
        // Update filtered tags when filter text changes
        this.WhenAnyValue(x => x.FilterText)
            .Subscribe(_ => ApplyTagFilter());
    }
    
    /// <summary>
    /// The filter text for searching tags
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }
    
    /// <summary>
    /// The currently selected tag
    /// </summary>
    public Tag? SelectedTag
    {
        get => _selectedTag;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTag, value);
            
            if (value != null)
            {
                // Update context menu
                ContextMenu.TagName = value.Name;
                ContextMenu.Repository = value.Repository?.FullPath.Replace($"{value.Repository?.Registry?.Url ?? string.Empty}/", string.Empty) ?? string.Empty;
                ContextMenu.RegistryUrl = value.Repository?.Registry?.Url ?? string.Empty;
                
                // Schedule debounced tag load
                ScheduleTagLoad(value);
            }
        }
    }
    
    /// <summary>
    /// Collection of available tags
    /// </summary>
    public ObservableCollection<Tag> Tags
    {
        get => _tags;
        set => this.RaiseAndSetIfChanged(ref _tags, value);
    }
    
    /// <summary>
    /// Context menu view model for tag operations
    /// </summary>
    public TagContextMenuViewModel ContextMenu
    {
        get => _contextMenu;
        set => this.RaiseAndSetIfChanged(ref _contextMenu, value);
    }
    
    public ReactiveCommand<Unit, Unit> RefreshTagsCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyTagCommand { get; }
    
    /// <summary>
    /// Event raised when the user wants to refresh tags
    /// </summary>
    public event EventHandler? RefreshRequested;
    
    /// <summary>
    /// Event raised when a tag should be loaded (user selected and pressed Enter)
    /// </summary>
    public event EventHandler<Tag>? TagLoadRequested;
    
    /// <summary>
    /// Selects a tag and requests its manifest to be loaded
    /// </summary>
    public void SelectAndLoadTag(Tag tag)
    {
        System.Diagnostics.Debug.WriteLine($"[TagSelectorViewModel] SelectAndLoadTag called for tag: {tag.Name}");
        SelectedTag = tag;
        LoadSelectedTag();
    }
    
    /// <summary>
    /// Requests the currently selected tag's manifest to be loaded
    /// </summary>
    public void LoadSelectedTag()
    {
        if (SelectedTag != null)
        {
            // Cancel any pending timer and load immediately
            _tagSelectionTimer?.Dispose();
            
            System.Diagnostics.Debug.WriteLine($"[TagSelectorViewModel] LoadSelectedTag firing TagLoadRequested event for tag: {SelectedTag.Name}, FullReference: {SelectedTag.FullReference}");
            TagLoadRequested?.Invoke(this, SelectedTag);
        }
    }
    
    /// <summary>
    /// Schedules a debounced tag load after selection change
    /// </summary>
    private void ScheduleTagLoad(Tag tag)
    {
        // Cancel any pending timer
        _tagSelectionTimer?.Dispose();
        
        // Create new timer that will fire after the debounce period
        _tagSelectionTimer = new System.Threading.Timer(
            _ =>
            {
                // Marshal back to UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[TagSelectorViewModel] Debounced timer firing TagLoadRequested event for tag: {tag.Name}");
                    TagLoadRequested?.Invoke(this, tag);
                });
            },
            null,
            SelectionDebounceMilliseconds,
            System.Threading.Timeout.Infinite
        );
    }
    
    /// <summary>
    /// Updates the selected tag without triggering load events (for programmatic updates)
    /// </summary>
    public void UpdateSelectedTagSilently(Tag? tag)
    {
        // Directly update the backing field to avoid triggering property change notifications
        // Only raise if the value actually changed
        if (_selectedTag != tag)
        {
            _selectedTag = tag;
            this.RaisePropertyChanged(nameof(SelectedTag));
        }
        
        if (tag?.Repository != null)
        {
            // Update context menu
            ContextMenu.TagName = tag.Name;
            ContextMenu.Repository = tag.Repository.FullPath.Replace($"{tag.Repository.Registry?.Url ?? string.Empty}/", string.Empty);
            ContextMenu.RegistryUrl = tag.Repository.Registry?.Url ?? string.Empty;
        }
    }
    
    /// <summary>
    /// Finds and selects a tag that matches the given digest, if one exists.
    /// Clears selection if no matching tag is found.
    /// This is used when a manifest is loaded by digest to sync the tag list.
    /// </summary>
    /// <param name="digest">The manifest digest (e.g., sha256:abc123...)</param>
    /// <returns>True if a matching tag was found and selected, false otherwise</returns>
    public bool TrySelectTagByDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            UpdateSelectedTagSilently(null);
            return false;
        }
        
        // Find a tag that points to this digest
        var matchingTag = Tags.FirstOrDefault(t => 
            !string.IsNullOrEmpty(t.Digest) && 
            string.Equals(t.Digest, digest, StringComparison.OrdinalIgnoreCase));
        
        if (matchingTag != null)
        {
            // Found a tag that points to this digest - select it
            UpdateSelectedTagSilently(matchingTag);
            return true;
        }
        
        // No matching tag found - clear selection since this digest has no tag
        UpdateSelectedTagSilently(null);
        return false;
    }
    
    private void RequestRefresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnTagsLoaded(object? sender, TagsLoadedEventArgs e)
    {
        // Store the currently selected tag name if any
        string? selectedTagName = SelectedTag?.Name;
        
        _allTags = e.Tags.ToList();
        ApplyTagFilter(selectedTagName);
    }
    
    private void OnTagLoadFailed(object? sender, TagLoadFailedEventArgs e)
    {
        // Clear tags on failure
        _allTags.Clear();
        Tags.Clear();
    }
    
    private void ApplyTagFilter(string? preferredSelection = null)
    {
        var selectionName = preferredSelection ?? SelectedTag?.Name;
        
        IEnumerable<Tag> source = _allTags;
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var trimmed = FilterText.Trim();
            source = source.Where(t => WildcardFilter.Matches(t.Name, trimmed));
        }
        
        var filtered = source.ToList();
        
        Tags.Clear();
        foreach (var tag in filtered)
        {
            Tags.Add(tag);
        }
        
        // Restore selection if the tag still exists in the filtered list
        if (!string.IsNullOrEmpty(selectionName))
        {
            var tagToSelect = Tags.FirstOrDefault(t => string.Equals(t.Name, selectionName, StringComparison.Ordinal));
            if (tagToSelect != null && SelectedTag != tagToSelect)
            {
                UpdateSelectedTagSilently(tagToSelect);
            }
        }
    }
}
