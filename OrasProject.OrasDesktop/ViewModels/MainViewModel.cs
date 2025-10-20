using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using OrasProject.OrasDesktop.Views;
using ReactiveUI;
using System.Text.Json;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Exceptions;

namespace OrasProject.OrasDesktop.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
    private readonly IRegistryService _registryService;
    private readonly JsonHighlightService _jsonHighlightService;
    private readonly Services.ConnectionService _connectionService;
    private readonly Services.ManifestService _manifestService;
    private readonly Services.RepositoryService _repositoryService;
    private readonly Services.TagService _tagService;
    private readonly Services.ArtifactService _artifactService;
    private readonly Services.StatusService _statusService;
    private readonly Services.ManifestLoadCoordinator _manifestLoadCoordinator;
    private readonly ILogger<MainViewModel> _logger;

        // Events
        public event EventHandler? RegistryConnected;

        // Component ViewModels
        private string _currentRepositoryPath = string.Empty; // Repository path without registry URL
        private ReferenceHistoryViewModel _referenceHistory; // Initialized in constructor with manifestService
        private RepositorySelectorViewModel _repositorySelector; // Repository selector component (injected via DI)
        private TagSelectorViewModel _tagSelector; // Tag selector component (initialized in constructor)
        private ConnectionViewModel _connection = new(); // Connection control component
        private StatusBarViewModel _statusBar; // Status bar component (subscribes to StatusService)
        private ArtifactViewModel _artifact; // Artifact display component (initialized in constructor)

        // Commands
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceCommand { get; }
        public ReactiveCommand<bool, Unit> ForceLoginCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadManifestByReferenceCommand { get; }

        public MainViewModel(
            IRegistryService registryService, 
            JsonHighlightService jsonHighlightService,
            ConnectionService connectionService,
            ManifestService manifestService,
            RepositoryService repositoryService,
            TagService tagService,
            ArtifactService artifactService,
            StatusService statusService,
            ManifestLoadCoordinator manifestLoadCoordinator,
            ILogger<MainViewModel> logger,
            ILoggerFactory loggerFactory)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] ===== CONSTRUCTOR CALLED =====");
            
            _registryService = registryService;
            _jsonHighlightService = jsonHighlightService;
            _connectionService = connectionService;
            _manifestService = manifestService;
            _repositoryService = repositoryService;
            _tagService = tagService;
            _artifactService = artifactService;
            _statusService = statusService;
            _manifestLoadCoordinator = manifestLoadCoordinator;
            _logger = logger;

            // Initialize component ViewModels
            _repositorySelector = new RepositorySelectorViewModel(
                _repositoryService, 
                _artifactService, 
                loggerFactory.CreateLogger<RepositorySelectorViewModel>());
            _tagSelector = new TagSelectorViewModel(_tagService);

            // Initialize ArtifactService to coordinate repository/tag selection and tag loading
            _artifactService.Initialize(_repositorySelector, _tagSelector);

            // Initialize ReferenceHistory with manifestService so it can subscribe to events
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Creating ReferenceHistoryViewModel with manifestService: {_manifestService != null}");
            _referenceHistory = new ReferenceHistoryViewModel(_manifestService, null);
            System.Diagnostics.Debug.WriteLine("[MainViewModel] ReferenceHistoryViewModel created");

            // Initialize StatusBar ViewModel (subscribes to StatusService)
            _statusBar = new StatusBarViewModel(_statusService);

            // Initialize Artifact ViewModel (artifact display component)
            _artifact = new ArtifactViewModel(
                _artifactService,
                _manifestService!, // Null-forgiving: initialized from constructor parameter
                _statusService,
                _registryService,
                _jsonHighlightService,
                loggerFactory.CreateLogger<ArtifactViewModel>(),
                loggerFactory);

            // Wire up Artifact events
            _artifact.ReferenceUpdateRequested += OnArtifactReferenceUpdateRequested;

            // Wire up navigation event from ManifestLoadCoordinator
            _manifestLoadCoordinator.NavigationRequested += OnNavigationRequested;

            // Initialize commands
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectToRegistryAsync);
            ForceLoginCommand = ReactiveCommand.CreateFromTask<bool>(forceLogin => ConnectToRegistryAsync(Connection.RegistryUrl, forceLogin));
            CopyReferenceCommand = ReactiveCommand.CreateFromTask(CopyReferenceToClipboardAsync);
            
            LoadManifestByReferenceCommand = ReactiveCommand.Create(() =>
            {
                var reference = ReferenceHistory.CurrentReference?.Trim();
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    _manifestService!.TryRequestLoad(reference!, Services.LoadSource.ReferenceBox, forceReload: true);
                }
            });

            // Setup property change handlers
            // Note: SelectedRepository and SelectedTag change handling is now done via component ViewModels firing events
            // RepositorySelector.RepositoryLoadRequested and TagSelector.TagLoadRequested

            // Sync ReferenceHistory.CurrentReference with SelectedTagReference
            // Use DistinctUntilChanged to prevent circular updates
            // DO NOT trigger loads here - loads should only happen on explicit user action (Enter key, button click, history selection)
            this.WhenAnyValue(x => x.ReferenceHistory.CurrentReference)
                .DistinctUntilChanged()
                .Subscribe(reference =>
                {
                    if (SelectedTagReference != reference)
                    {
                        SelectedTagReference = reference ?? string.Empty;
                    }
                });

            this.WhenAnyValue(x => x.SelectedTagReference)
                .DistinctUntilChanged()
                .Subscribe(reference =>
                {
                    if (ReferenceHistory.CurrentReference != reference)
                    {
                        ReferenceHistory.CurrentReference = reference ?? string.Empty;
                    }
                });


            // Wire up ConnectionService events
            _connectionService.ConnectionEstablished += OnConnectionEstablished;
            _connectionService.ConnectionFailed += OnConnectionFailed;

            // Wire up manifestService events (readonly field assigned from constructor parameter)
            _manifestService!.LoadRequested += OnManifestLoadRequested;
            _manifestService.LoadCompleted += OnManifestLoadCompleted;

            ReferenceHistory.LoadRequested += OnReferenceHistoryLoadRequested;
            
            
            
            // Wire up Connection events
            Connection.ConnectionRequested += OnConnectionRequested;
            
            // Wire up RepositorySelector events
            RepositorySelector.RepositoryLoadRequested += OnRepositoryLoadRequested;
            RepositorySelector.RefreshRequested += OnRepositoryRefreshRequested;
            
            // Wire up TagSelector events
            TagSelector.TagLoadRequested += OnTagLoadRequested;
            TagSelector.RefreshRequested += OnTagRefreshRequested;
            
            // Wire up ManifestLoadCoordinator events
            _manifestLoadCoordinator.RepositorySelectionRequested += OnCoordinatorRepositorySelectionRequested;
            _manifestLoadCoordinator.TagSelectionRequested += OnCoordinatorTagSelectionRequested;
            _manifestLoadCoordinator.DigestSelectionRequested += OnCoordinatorDigestSelectionRequested;
            
            // Wire up ArtifactService events to raise property changed notifications
            _artifactService.ManifestChanged += (s, e) =>
            {
                try
                {
                    this.RaisePropertyChanged(nameof(CurrentManifest));
                    this.RaisePropertyChanged(nameof(CanModifySelectedTag));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MainViewModel: Error in ManifestChanged handler");
                }
            };
            _artifactService.RepositoryChanged += (s, e) =>
            {
                try
                {
                    this.RaisePropertyChanged(nameof(CanModifySelectedTag));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MainViewModel: Error in RepositoryChanged handler");
                }
            };
            _artifactService.ArtifactSizeUpdated += (s, e) =>
            {
                this.RaisePropertyChanged(nameof(ArtifactSizeSummary));
                this.RaisePropertyChanged(nameof(PlatformImageSizes));
                this.RaisePropertyChanged(nameof(HasPlatformSizes));
            };
        }
        // Public properties

        public ArtifactViewModel Artifact
        {
            get => _artifact;
            set => this.RaiseAndSetIfChanged(ref _artifact, value);
        }

        // Reference tracking (not artifact-specific, shared by reference history)
        private string _selectedTagReference = string.Empty;
        public string SelectedTagReference
        {
            get => _selectedTagReference;
            set => this.RaiseAndSetIfChanged(ref _selectedTagReference, value);
        }

        public string ArtifactSizeSummary => _artifactService.ArtifactSizeSummary;

        public ObservableCollection<PlatformImageSize> PlatformImageSizes => _artifactService.PlatformImageSizes;

        public bool HasPlatformSizes => _artifactService.HasPlatformSizes;

        public Manifest? CurrentManifest => _artifactService.CurrentManifest;

        public ReferenceHistoryViewModel ReferenceHistory
        {
            get => _referenceHistory;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (ReferenceEquals(_referenceHistory, value))
                {
                    return;
                }

                var previous = _referenceHistory;
                if (previous != null)
                {
                    previous.LoadRequested -= OnReferenceHistoryLoadRequested;
                }

                _referenceHistory = value;
                this.RaisePropertyChanged(nameof(ReferenceHistory));

                _referenceHistory.LoadRequested += OnReferenceHistoryLoadRequested;
            }
        }

        public ConnectionViewModel Connection
        {
            get => _connection;
            set => this.RaiseAndSetIfChanged(ref _connection, value);
        }

        public RepositorySelectorViewModel RepositorySelector
        {
            get => _repositorySelector;
            set => this.RaiseAndSetIfChanged(ref _repositorySelector, value);
        }

        public TagSelectorViewModel TagSelector
        {
            get => _tagSelector;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (ReferenceEquals(_tagSelector, value))
                {
                    return;
                }

                var previous = _tagSelector;
                if (previous != null)
                {
                    previous.TagLoadRequested -= OnTagLoadRequested;
                    previous.RefreshRequested -= OnTagRefreshRequested;
                }

                _tagSelector = value;
                this.RaisePropertyChanged(nameof(TagSelector));

                _tagSelector.TagLoadRequested += OnTagLoadRequested;
                _tagSelector.RefreshRequested += OnTagRefreshRequested;
            }
        }

        public StatusBarViewModel StatusBar
        {
            get => _statusBar;
            set => this.RaiseAndSetIfChanged(ref _statusBar, value);
        }

        // Allow operations as soon as a tag is selected; internal commands manage busy state themselves.
        public bool CanModifySelectedTag => _artifactService.CanDeleteManifest();

        // Helper method to get the main window
        private Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        // Event handlers for component events

        private async void OnNavigationRequested(object? sender, Services.NavigationRequestedEventArgs e)
        {
            // Navigate to the requested repository and optionally tag
            // The ManifestLoadCoordinator has already validated the repository belongs to current registry
            await RepositorySelector.NavigateToRepositoryAsync(e.Repository);
            
            if (e.SelectTag && !string.IsNullOrEmpty(e.TagOrDigest))
            {
                // Try to find and select the tag
                var tag = TagSelector.Tags.FirstOrDefault(t => t.Name == e.TagOrDigest);
                if (tag != null)
                {
                    TagSelector.SelectAndLoadTag(tag);
                }
            }
        }

        private void OnConnectionRequested(object? sender, ConnectionRequestedEventArgs e)
        {
            // Trigger the connection with the registry URL from the connection control
            // ConnectionService will handle authentication type
            _ = ConnectToRegistryAsync(e.RegistryUrl, forceLogin: false);
        }

        // Command implementations
        private async Task ConnectToRegistryAsync()
        {
            // Use the URL from the connection control
            await ConnectToRegistryAsync(Connection.RegistryUrl, false);
        }

        private async Task ConnectToRegistryAsync(string registryUrl, bool forceLogin)
        {
            _statusService.SetBusy(true);
            _statusService.SetStatus("Connecting to registry...");

            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null)
                {
                    _statusService.SetStatus("Failed to get main window", isError: true);
                    return;
                }

                // Use ConnectionService to handle the full connection flow
                // We provide a callback for requesting credentials via the login dialog
                var success = await _connectionService.ConnectWithFlowAsync(
                    registryUrl,
                    forceLogin,
                    async (url) =>
                    {
                        var dialogResult = await LoginDialog.ShowDialog(mainWindow, url);
                        return new Services.LoginDialogResult(
                            dialogResult.Result,
                            dialogResult.AuthType,
                            dialogResult.Username,
                            dialogResult.Password,
                            dialogResult.Token
                        );
                    },
                    default
                );

                if (!success)
                {
                    // Connection was cancelled or failed - error messages already handled by ConnectionService events
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Connection to {Registry} was not successful", registryUrl);
                    }
                }
                // Success case - ConnectionEstablished event will be fired by ConnectionService
            }
            catch (Exception ex)
            {
                _statusService.SetStatus($"Error connecting to registry: {ex.Message}", isError: true);
            }
            finally
            {
                _statusService.SetBusy(false);
                _statusService.ResetProgress();
            }
        }

        /// <summary>
        /// Event handler for successful connection to registry
        /// Note: RepositoryService automatically loads repositories via its ConnectionEstablished subscription
        /// </summary>
        private void OnConnectionEstablished(object? sender, ConnectionEstablishedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Connection established to registry: {Registry} (Authenticated: {IsAuthenticated})", 
                    e.Registry.Url, e.IsAuthenticated);
            }

            // Update artifact service with the connected registry
            _artifactService.SetRegistry(e.Registry);

            // Clear status - RepositoryService will set its own status while loading
            _statusService.ClearStatus();
            
            RegistryConnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event handler for failed connection to registry
        /// </summary>
        private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(e.Exception, "Connection failed to registry: {Registry}", e.RegistryUrl);
            }

            _statusService.SetStatus($"Connection failed: {e.Exception.Message}", isError: true);
        }

        // LoginToRegistryAsync method removed as functionality merged into ConnectToRegistryAsync

        // Note: LoadTagsAsync method removed - tag loading now handled by tagService service

        private async Task RefreshTagsAsync()
        {
            if (_artifactService.CurrentRepository == null)
            {
                _statusService.SetStatus("No repository selected", isError: true);
                return;
            }

            if (_artifactService.CurrentRegistry == null)
            {
                _statusService.SetStatus("Not connected to a registry", isError: true);
                return;
            }

            _tagService.OnRepositorySelected(_artifactService.CurrentRepository, _artifactService.CurrentRegistry);
            await Task.CompletedTask; // Keep async signature for command binding
        }

        private async Task CopyReferenceToClipboardAsync()
        {
            if (_artifactService.CurrentTag == null)
            {
                _statusService.SetStatus("No tag selected", isError: true);
                return;
            }

            try
            {
                // Get reference without the "Reference: " prefix
                string reference = _artifactService.CurrentTag.FullReference;

                // Copy to clipboard
                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel != null)
                {
                    await topLevel.Clipboard!.SetTextAsync(reference);
                    _statusService.SetStatus("Reference copied to clipboard");
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

        private async Task LoadManifestByReferenceAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedTagReference))
            {
                _statusService.SetStatus("Reference is empty", isError: true);
                return;
            }

            _statusService.SetBusy(true);
            string originalReference = SelectedTagReference.Trim();
            _statusService.SetStatus($"Processing reference {originalReference}...");

            try
            {
                // Validate and parse the reference using ORAS library
                Reference? parsedRef = null;
                try
                {
                    if (!Reference.TryParse(originalReference, out parsedRef))
                    {
                        _statusService.SetStatus("Invalid reference format. Expected: registry/repository:tag or registry/repository@digest", isError: true);
                        return;
                    }
                }
                catch (FormatException ex)
                {
                    // Catch InvalidResponseException and other format exceptions from ORAS library
                    _statusService.SetStatus($"Error parsing reference: {ex.Message}", isError: true);
                    return;
                }
                catch (Exception ex)
                {
                    _statusService.SetStatus($"Unexpected error parsing reference: {ex.Message}", isError: true);
                    return;
                }
                
                if (parsedRef == null || 
                    string.IsNullOrEmpty(parsedRef.Registry) || 
                    string.IsNullOrEmpty(parsedRef.Repository))
                {
                    _statusService.SetStatus("Invalid reference format. Expected: registry/repository:tag or registry/repository@digest", isError: true);
                    return;
                }

                string registry = parsedRef.Registry;
                string repository = parsedRef.Repository;
                
                // ContentReference can be either a tag or digest
                // Check if it's a digest by seeing if it starts with a hash algorithm (e.g., "sha256:")
                string? contentRef = parsedRef.ContentReference;
                bool isDigestReference = !string.IsNullOrEmpty(contentRef) && contentRef.Contains(':') && 
                                        (contentRef.StartsWith("sha256:") || contentRef.StartsWith("sha512:"));
                string tagOrDigest = contentRef ?? "latest";

                // Check if we need to connect to a different registry
                bool needToChangeRegistry = !string.Equals(registry, _artifactService.CurrentRegistry?.Url, StringComparison.OrdinalIgnoreCase);

                if (needToChangeRegistry)
                {
                    // Update the connection control's registry URL
                    Connection.RegistryUrl = registry;

                    // Connect to the new registry using ConnectionService
                    _statusService.SetStatus($"Connecting to registry {registry}...");

                    var mainWindow = GetMainWindow();
                    if (mainWindow == null)
                    {
                        _statusService.SetStatus("Failed to get main window", isError: true);
                        return;
                    }

                    var success = await _connectionService.ConnectWithFlowAsync(
                        registry,
                        forceLogin: false,
                        async (url) =>
                        {
                            var dialogResult = await LoginDialog.ShowDialog(mainWindow, url);
                            return new Services.LoginDialogResult(
                                dialogResult.Result,
                                dialogResult.AuthType,
                                dialogResult.Username,
                                dialogResult.Password,
                                dialogResult.Token
                            );
                        },
                        default,
                        // Test connection by resolving the specific tag (cheaper than listing all repos)
                        repository: repository,
                        tag: isDigestReference ? null : tagOrDigest
                    );

                    if (!success)
                    {
                        _statusService.SetStatus("Connection cancelled or failed", isError: true);
                        return;
                    }

                    // ConnectionEstablished event will update ArtifactService and load repositories
                }

                // Ensure repositories are loaded before trying to navigate
                // If no repositories are loaded yet, load them first
                if (RepositorySelector.Repositories.Count == 0 && _artifactService.CurrentRegistry != null)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("No repositories loaded yet, loading repositories for {Registry}...", _artifactService.CurrentRegistry.Url);
                    }
                    
                    _statusService.SetStatus($"Loading repositories...");
                    
                    try
                    {
                        await _repositoryService.RefreshRepositoriesAsync(default);
                        
                        // Wait a bit for the UI to update
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        if (_logger.IsEnabled(LogLevel.Warning))
                        {
                            _logger.LogWarning(ex, "Failed to load repositories while processing reference {Reference}.", originalReference);
                        }
                        // Continue anyway - user might still be able to load the manifest
                    }
                }

                // Now fetch tags for the repository if possible (TagService will handle actual loading)
                try
                {
                    // Try to find and select the repository in the tree which will trigger tag loading via TagService
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await RepositorySelector.FindAndSelectRepositoryAsync(repository);
                    });
                }
                catch (Exception ex)
                {
                    _statusService.SetStatus($"Error selecting repository {repository}: {ex.Message}", isError: true);
                }

                // Now load the manifest
                _statusService.SetStatus($"Loading manifest for {originalReference}...");

                ManifestResult manifest;

                if (isDigestReference)
                {
                    manifest = await _registryService.GetManifestByDigestAsync(
                        repository,
                        tagOrDigest,
                        default
                    );
                }
                else
                {
                    manifest = await _registryService.GetManifestByTagAsync(
                        repository,
                        tagOrDigest,
                        default
                    );
                }

                // Create a new manifest object and set it in ArtifactService
                // This will also calculate artifact size automatically
                var newManifest = new Manifest
                {
                    RawContent = manifest.Json,
                    Digest = manifest.Digest,
                    // Tag will be null since we're not selecting from a repository/tag list
                    MediaType = manifest.MediaType,
                };

                await _artifactService.SetManifestAsync(newManifest, repository);
                _currentRepositoryPath = repository; // Store for context

                // IMPORTANT: Preserve the original pasted reference instead of resetting it
                SelectedTagReference = originalReference;

                // Update ArtifactService with the current reference (digest or tag)
                // This keeps the artifact context in sync with what's displayed
                _artifactService.SetReference(originalReference, isDigestReference);

                // NOTE: We intentionally do NOT select the matching tag in the UI here
                // because doing so triggers the SelectedTag property changed event which
                // schedules a debounced load of that tag's manifest, overwriting the manifest
                // we just loaded from the reference. The user's intended manifest is already loaded.
                // await TrySelectMatchingTag(repository, tagOrDigest, isDigestReference);

                // Notify manifestService that load completed with the current source
                // Note: History is automatically updated by ReferenceHistoryViewModel subscribing to LoadCompleted event
                _manifestService.NotifyLoadCompleted(originalReference, _manifestService.CurrentLoadSource);

                _statusService.SetStatus($"Loaded manifest for {originalReference}");
            }
            catch (Exception ex)
            {
                _statusService.SetStatus($"Error processing reference: {ex.Message}", isError: true);
            }
            finally
            {
                _statusService.SetBusy(false);
                _statusService.ResetProgress();
            }
        }

        /// <summary>
        /// Handles manifest load request events from various sources
        /// </summary>
        private void OnManifestLoadRequested(object? sender, Services.ManifestLoadRequestedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnManifestLoadRequested received for reference: {e.Reference}, source: {e.Source}");
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Manifest load requested from {Source} for {Reference}.", e.Source, e.Reference);
            }
            
            // Close the history dropdown whenever a load kicks off so the popup collapses reliably
            if (ReferenceHistory.IsDropDownOpen)
            {
                ReferenceHistory.IsDropDownOpen = false;
            }

            // Note: History is updated automatically by ReferenceHistoryViewModel subscribing to manifestService.LoadCompleted

            // Update the reference box to show what we're loading
            if (SelectedTagReference != e.Reference)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Setting SelectedTagReference to: {e.Reference}");
                SelectedTagReference = e.Reference;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("SelectedTagReference set to {Reference} during request handling.", e.Reference);
                }
            }
            
            // Actually perform the manifest load
            _ = LoadManifestByReferenceAsync();
        }

        /// <summary>
        /// Handles manifest load completion events from manifestService
        /// </summary>
        private void OnManifestLoadCompleted(object? sender, Services.ManifestLoadedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Manifest load completed from {Source} for {Reference}.", e.Source, e.Reference);
            }
            
            // Check if this is a digest reference
            bool isDigest = e.Reference.Contains("@sha256:") || e.Reference.Contains("@sha512:");
            
            // Note: History is automatically updated by ReferenceHistoryViewModel subscribing to this LoadCompleted event
            
            // Update UI state based on which component requested the load
            switch (e.Source)
            {
                case Services.LoadSource.History:
                    // When history loads, update the reference box and navigate to the repository
                    // but don't auto-select the tag to avoid jumping back to previously selected tag
                    SelectedTagReference = e.Reference;
                    
                    // Navigate to repository only (selectTag=false)
                    if (!isDigest)
                    {
                        _ = _manifestLoadCoordinator.TryNavigateToReferenceAsync(e.Reference, selectTag: false);
                    }
                    
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("History load applied reference {Reference} without auto-selecting tag (current tag: {Tag}).", e.Reference, _artifactService.CurrentTag?.Name ?? "<none>");
                    }
                    break;
                    
                case Services.LoadSource.ReferenceBox:
                    // When reference box loads, update reference box
                    SelectedTagReference = e.Reference;
                    
                    // Only try to navigate for non-digest references
                    if (!isDigest)
                    {
                        _ = _manifestLoadCoordinator.TryNavigateToReferenceAsync(e.Reference);
                    }
                    else if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("ReferenceBox load with digest reference {Reference} - skipping navigation.", e.Reference);
                    }
                    break;
                    
                case Services.LoadSource.TagSelection:
                    // When tag loads, update reference box
                    SelectedTagReference = e.Reference;
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Tag selection load finalized reference {Reference} (current tag: {Tag}).", e.Reference, _artifactService.CurrentTag?.Name ?? "<none>");
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles reference update requests from ArtifactViewModel (Ctrl+Shift+T/D shortcuts)
        /// Updates the reference box without triggering a manifest load
        /// </summary>
        private void OnArtifactReferenceUpdateRequested(object? sender, ReferenceUpdatedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Reference))
            {
                ReferenceHistory.CurrentReference = e.Reference;
                
                if (e.ShouldFocus)
                {
                    ReferenceHistory.RequestFocus();
                }
                
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Reference box updated to {Reference} (no load triggered, focus: {ShouldFocus}).", e.Reference, e.ShouldFocus);
                }
            }
        }

        private void OnReferenceHistoryLoadRequested(object? sender, EventArgs e)
        {
            var reference = ReferenceHistory.CurrentReference?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
            {
                _statusService.SetStatus("Reference is empty", isError: true);
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("History dropdown requested manifest load for {Reference}.", reference);
            }

            _manifestService.TryRequestLoad(reference, Services.LoadSource.History, forceReload: true);
        }
        
        private void OnRepositoryLoadRequested(object? sender, Repository repository)
        {
            if (repository == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnRepositoryLoadRequested received for repository: {repository.Name}");
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Repository selector requested load for repository {Repository}.", repository.Name);
            }

            // Tag loading is now handled by tagService subscribing to RepositorySelector.RepositorySelected
            // This event can be used for other repository-specific actions if needed
        }

        // Note: OnRepositorySelectedForTagLoading removed - now handled by ArtifactService.OnRepositorySelected
        
        private async void OnRepositoryRefreshRequested(object? sender, EventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Repository selector requested refresh.");
            }

            // Delegate to repositoryService which handles status, error handling, and triggers events
            await _repositoryService.RefreshRepositoriesAsync(default);
        }
        
        private void OnTagLoadRequested(object? sender, Tag tag)
        {
            if (tag == null)
                return;

            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnTagLoadRequested received for tag: {tag.Name}, FullReference: {tag.FullReference}");
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Tag selector requested load for tag {Tag}.", tag.Name);
            }

            // Request load via manifestService (this is a user-initiated action)
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Calling manifestService.TryRequestLoad with reference: {tag.FullReference}, source: TagSelection");
            _manifestService.TryRequestLoad(tag.FullReference, Services.LoadSource.TagSelection);
        }
        
        private void OnTagRefreshRequested(object? sender, EventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Tag selector requested refresh.");
            }

            // Refresh tags using TagService's refresh method (which manages busy state)
            _ = _tagService.RefreshTagsAsync();
        }
        
        /// <summary>
        /// Handles repository selection request from ManifestLoadCoordinator
        /// </summary>
        private async void OnCoordinatorRepositorySelectionRequested(object? sender, RepositorySelectionRequestedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("ManifestLoadCoordinator requested repository selection: {Repository}", e.Repository.Name);
            }

            // Navigate to and select the repository in the RepositorySelector
            var repositoryPath = e.Repository.FullPath.Replace($"{_artifactService.CurrentRegistry?.Url}/", string.Empty);
            await RepositorySelector.NavigateToRepositoryAsync(repositoryPath);
        }
        
        /// <summary>
        /// Handles tag selection request from ManifestLoadCoordinator
        /// </summary>
        private void OnCoordinatorTagSelectionRequested(object? sender, TagSelectionRequestedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("ManifestLoadCoordinator requested tag selection: {Tag}", e.TagName);
            }

            // Find and select the tag in the TagSelector
            var tag = TagSelector.Tags.FirstOrDefault(t => string.Equals(t.Name, e.TagName, StringComparison.OrdinalIgnoreCase));
            if (tag != null)
            {
                // Suppress auto-selection during coordinator navigation
                var suppressFlag = _manifestService.ShouldSuppressTagAutoSelection;
                
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Found matching tag, selecting: {Tag} (suppress={Suppress})", tag.Name, suppressFlag);
                }
                
                TagSelector.SelectedTag = tag;
            }
            else if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Tag {Tag} not found in loaded tags", e.TagName);
            }
        }

        /// <summary>
        /// Handles digest-based tag selection request from ManifestLoadCoordinator
        /// This is called when a manifest is loaded by digest to sync the tag list
        /// </summary>
        private void OnCoordinatorDigestSelectionRequested(object? sender, DigestSelectionRequestedEventArgs e)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("ManifestLoadCoordinator requested digest-based tag sync: {Digest}", e.Digest);
            }

            // Try to find and select a tag that matches this digest
            bool found = TagSelector.TrySelectTagByDigest(e.Digest);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                if (found)
                {
                    _logger.LogInformation("Found tag matching digest {Digest}: {Tag}", 
                        e.Digest.Substring(0, Math.Min(20, e.Digest.Length)), 
                        TagSelector.SelectedTag?.Name ?? "<none>");
                }
                else
                {
                    _logger.LogInformation("No tag found matching digest {Digest}, selection cleared", 
                        e.Digest.Substring(0, Math.Min(20, e.Digest.Length)));
                }
            }
        }
    }
}






