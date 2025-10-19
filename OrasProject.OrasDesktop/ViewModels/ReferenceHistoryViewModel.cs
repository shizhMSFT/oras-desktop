using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for managing reference history similar to a browser address bar.
/// Maintains a history of successfully loaded references with navigation support.
/// Automatically subscribes to ManifestService events to update history.
/// </summary>
public class ReferenceHistoryViewModel : ViewModelBase
{
    private const int MaxHistoryItems = 7;
    
    private string _currentReference = string.Empty;
    private int _currentHistoryIndex = -1;
    private int _selectedHistoryIndex = -1;
    private bool _isDropDownOpen = false; // Start closed
    private bool _isNavigating = false; // Track if we're in navigation mode
    private bool _isJustBrowsing = false; // Track if we're just browsing with arrow keys (don't load yet)
    private readonly ILogger<ReferenceHistoryViewModel>? _logger;
    
    public ReferenceHistoryViewModel(ManifestService? ManifestService = null, ILogger<ReferenceHistoryViewModel>? logger = null)
    {
        _logger = logger;
        
        HistoryItems = new ObservableCollection<string>();
        
        // Add dummy items for testing
        HistoryItems.Add("ghcr.io/oras-project/oras:v1.0.0");
        HistoryItems.Add("docker.io/library/nginx:latest");
        HistoryItems.Add("mcr.microsoft.com/dotnet/runtime:8.0");
        HistoryItems.Add("registry.k8s.io/pause:3.9");
        
        RemoveHistoryItemCommand = ReactiveCommand.Create<string>(RemoveHistoryItem);
        RemoveSelectedItemCommand = ReactiveCommand.Create(RemoveSelectedItem);
        NavigateBackCommand = ReactiveCommand.Create(NavigateBack, this.WhenAnyValue(x => x.CanNavigateBack));
        NavigateForwardCommand = ReactiveCommand.Create(NavigateForward, this.WhenAnyValue(x => x.CanNavigateForward));
        NavigateUpCommand = ReactiveCommand.Create(NavigateUp);
        NavigateDownCommand = ReactiveCommand.Create(NavigateDown);
        ClearHistoryCommand = ReactiveCommand.Create(ClearHistory);
        ToggleDropDownCommand = ReactiveCommand.Create(ToggleDropDown);
        OpenDropDownCommand = ReactiveCommand.Create(OpenDropDown);
        CloseDropDownCommand = ReactiveCommand.Create(CloseDropDown);
        FocusAndOpenCommand = ReactiveCommand.Create(FocusAndOpen);
        
        // Subscribe to ManifestService events if provided
        System.Diagnostics.Debug.WriteLine($"[ReferenceHistoryViewModel] Constructor: ManifestService is {(ManifestService == null ? "NULL" : "NOT NULL")}");
        
        if (ManifestService != null)
        {
            ManifestService.LoadRequested += OnManifestLoadRequested;
            ManifestService.LoadCompleted += OnManifestLoadCompleted;
            
            System.Diagnostics.Debug.WriteLine("[ReferenceHistoryViewModel] Successfully subscribed to ManifestService.LoadRequested and LoadCompleted events");
            
            if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("ReferenceHistoryViewModel subscribed to ManifestService events.");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ReferenceHistoryViewModel] WARNING: ManifestService is NULL - events will NOT be subscribed!");
        }
    }
    
    /// <summary>
    /// Handles manifest load requested events to immediately update the reference text.
    /// Provides optimistic UI update for immediate feedback.
    /// </summary>
    private void OnManifestLoadRequested(object? sender, ManifestLoadRequestedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[ReferenceHistoryViewModel] OnManifestLoadRequested received for reference: {e.Reference}, source: {e.Source}");
        System.Diagnostics.Debug.WriteLine($"[ReferenceHistoryViewModel] Setting CurrentReference to: {e.Reference}");
        
        // Update the current reference immediately when load is requested (optimistic update)
        CurrentReference = e.Reference;
        
        System.Diagnostics.Debug.WriteLine($"[ReferenceHistoryViewModel] CurrentReference is now: {CurrentReference}");
        
        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Reference text updated to {Reference} from {Source} (load requested).", e.Reference, e.Source);
        }
    }
    
    /// <summary>
    /// Handles manifest load completion events to automatically update history.
    /// Called whenever a manifest is successfully loaded from any source.
    /// </summary>
    private void OnManifestLoadCompleted(object? sender, ManifestLoadedEventArgs e)
    {
        // Add to history for all sources (History source will just move existing item to top)
        AddToHistory(e.Reference);
        
        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("History updated with reference {Reference} from {Source}.", e.Reference, e.Source);
        }
    }

    /// <summary>
    /// The current reference text in the textbox
    /// </summary>
    public string CurrentReference
    {
        get => _currentReference;
        set => this.RaiseAndSetIfChanged(ref _currentReference, value);
    }

    /// <summary>
    /// Observable collection of historical references (most recent first)
    /// </summary>
    public ObservableCollection<string> HistoryItems { get; }

    /// <summary>
    /// Whether the dropdown is currently open
    /// </summary>
    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        set => this.RaiseAndSetIfChanged(ref _isDropDownOpen, value);
    }

    /// <summary>
    /// Whether we're currently navigating (to prevent TextChanged from resetting state)
    /// </summary>
    public bool IsNavigating => _isNavigating;

    /// <summary>
    /// Whether we're just browsing with arrow keys (don't trigger load yet, wait for Enter)
    /// </summary>
    public bool IsJustBrowsing => _isJustBrowsing;

    /// <summary>
    /// Currently selected index in the history dropdown for visual highlighting
    /// </summary>
    public int SelectedHistoryIndex
    {
        get => _selectedHistoryIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedHistoryIndex, value);
    }

    /// <summary>
    /// Whether back navigation is available
    /// </summary>
    public bool CanNavigateBack => _currentHistoryIndex < HistoryItems.Count - 1;

    /// <summary>
    /// Whether forward navigation is available
    /// </summary>
    public bool CanNavigateForward => _currentHistoryIndex > 0;

    public ReactiveCommand<string, Unit> RemoveHistoryItemCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveSelectedItemCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateUpCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateDownCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleDropDownCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDropDownCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseDropDownCommand { get; }
    public ReactiveCommand<Unit, Unit> FocusAndOpenCommand { get; }
    
    /// <summary>
    /// Event raised when Enter is pressed to load the current reference
    /// </summary>
    public event EventHandler? LoadRequested;

    /// <summary>
    /// Adds a reference to history if it was successfully loaded.
    /// If the reference already exists, moves it to the top.
    /// Private - history is automatically managed via ManifestService event subscription.
    /// </summary>
    private void AddToHistory(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return;

        reference = reference.Trim();

        // Remove if already exists
        var existing = HistoryItems.FirstOrDefault(h => h.Equals(reference, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            HistoryItems.Remove(existing);
        }

        // Add to the top
        HistoryItems.Insert(0, reference);

        // Enforce max items
        while (HistoryItems.Count > MaxHistoryItems)
        {
            HistoryItems.RemoveAt(HistoryItems.Count - 1);
        }

        // Reset navigation index to current (top)
        _currentHistoryIndex = 0;
        this.RaisePropertyChanged(nameof(CanNavigateBack));
        this.RaisePropertyChanged(nameof(CanNavigateForward));
    }

    /// <summary>
    /// Removes a specific item from history
    /// </summary>
    private void RemoveHistoryItem(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return;

        var item = HistoryItems.FirstOrDefault(h => h.Equals(reference, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            var removedIndex = HistoryItems.IndexOf(item);
            HistoryItems.Remove(item);

            // Adjust current index if needed
            if (_currentHistoryIndex >= removedIndex && _currentHistoryIndex > 0)
            {
                _currentHistoryIndex--;
            }
            else if (_currentHistoryIndex >= HistoryItems.Count)
            {
                _currentHistoryIndex = HistoryItems.Count - 1;
            }

            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }
    }

    /// <summary>
    /// Removes the currently selected item from history (triggered by Delete key)
    /// </summary>
    private void RemoveSelectedItem()
    {
        if (IsDropDownOpen && SelectedHistoryIndex >= 0 && SelectedHistoryIndex < HistoryItems.Count)
        {
            var reference = HistoryItems[SelectedHistoryIndex];
            RemoveHistoryItem(reference);
            
            // Close dropdown if empty
            if (HistoryItems.Count == 0)
            {
                IsDropDownOpen = false;
            }
        }
    }

    /// <summary>
    /// Navigate to previous reference (Ctrl+Left)
    /// </summary>
    private void NavigateBack()
    {
        if (CanNavigateBack)
        {
            _currentHistoryIndex++;
            CurrentReference = HistoryItems[_currentHistoryIndex];
            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }
    }

    /// <summary>
    /// Navigate to next reference (Ctrl+Right)
    /// </summary>
    private void NavigateForward()
    {
        if (CanNavigateForward)
        {
            _currentHistoryIndex--;
            CurrentReference = HistoryItems[_currentHistoryIndex];
            this.RaisePropertyChanged(nameof(CanNavigateBack));
            this.RaisePropertyChanged(nameof(CanNavigateForward));
        }
    }

    /// <summary>
    /// Navigate up in dropdown list (move towards earlier items, decreasing index)
    /// Updates the text in the reference box to show the highlighted item (like browser address bar)
    /// </summary>
    private void NavigateUp()
    {
        if (HistoryItems.Count == 0)
            return;

        // Open dropdown if closed
        if (!IsDropDownOpen)
        {
            IsDropDownOpen = true;
        }

        // Mark that we're navigating and just browsing (don't load yet)
        _isNavigating = true;
        _isJustBrowsing = true;
        
        // If not in navigation mode yet, start from the first item
        if (SelectedHistoryIndex < 0)
        {
            SelectedHistoryIndex = 0;
        }
        // Move up (towards top of list, decreasing index)
        else if (SelectedHistoryIndex > 0)
        {
            SelectedHistoryIndex--;
        }
        // Cycle to last item when at the top
        else if (SelectedHistoryIndex == 0)
        {
            SelectedHistoryIndex = HistoryItems.Count - 1;
        }

        // Update the text to show the highlighted item (like browser address bar)
        if (SelectedHistoryIndex >= 0 && SelectedHistoryIndex < HistoryItems.Count)
        {
            CurrentReference = HistoryItems[SelectedHistoryIndex];
        }

        // Clear flags after a short delay
        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => 
        {
            _isNavigating = false;
            _isJustBrowsing = false;
        });
    }

    /// <summary>
    /// Navigate down in dropdown list
    /// Updates the text in the reference box to show the highlighted item (like browser address bar)
    /// </summary>
    private void NavigateDown()
    {
        if (HistoryItems.Count == 0)
            return;

        // Open dropdown if closed
        if (!IsDropDownOpen)
        {
            IsDropDownOpen = true;
        }

        // Mark that we're navigating and just browsing (don't load yet)
        _isNavigating = true;
        _isJustBrowsing = true;

        // If not in navigation mode, start from the first item
        if (SelectedHistoryIndex < 0)
        {
            SelectedHistoryIndex = 0;
        }
        // Move down to next item (towards bottom of list)
        else if (SelectedHistoryIndex < HistoryItems.Count - 1)
        {
            SelectedHistoryIndex++;
        }
        // Cycle back to first item when at the bottom
        else if (SelectedHistoryIndex == HistoryItems.Count - 1)
        {
            SelectedHistoryIndex = 0;
        }

        // Update the text to show the highlighted item (like browser address bar)
        if (SelectedHistoryIndex >= 0 && SelectedHistoryIndex < HistoryItems.Count)
        {
            CurrentReference = HistoryItems[SelectedHistoryIndex];
        }

        // Clear flags after a short delay
        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => 
        {
            _isNavigating = false;
            _isJustBrowsing = false;
        });
    }

    /// <summary>
    /// Clears all history
    /// </summary>
    private void ClearHistory()
    {
        HistoryItems.Clear();
        _currentHistoryIndex = -1;
        this.RaisePropertyChanged(nameof(CanNavigateBack));
        this.RaisePropertyChanged(nameof(CanNavigateForward));
    }

    /// <summary>
    /// Resets navigation state when user manually edits the reference
    /// </summary>
    public void ResetNavigationState()
    {
        _currentHistoryIndex = -1;
        this.RaisePropertyChanged(nameof(CanNavigateBack));
        this.RaisePropertyChanged(nameof(CanNavigateForward));
    }

    /// <summary>
    /// Toggles the dropdown visibility
    /// </summary>
    private void ToggleDropDown()
    {
        IsDropDownOpen = !IsDropDownOpen;
    }

    /// <summary>
    /// Opens the dropdown if there is history
    /// </summary>
    private void OpenDropDown()
    {
        if (HistoryItems.Count > 0)
        {
            IsDropDownOpen = true;
        }
    }

    /// <summary>
    /// Closes the dropdown
    /// </summary>
    private void CloseDropDown()
    {
        IsDropDownOpen = false;
    }

    /// <summary>
    /// Focuses the textbox and opens dropdown (F4 behavior)
    /// </summary>
    private void FocusAndOpen()
    {
        // This will be handled in the code-behind
        IsDropDownOpen = true;
    }

    /// <summary>
    /// Triggers loading of the current reference (Enter key behavior)
    /// </summary>
    public void RequestLoad()
    {
        LoadRequested?.Invoke(this, EventArgs.Empty);
    }
}
