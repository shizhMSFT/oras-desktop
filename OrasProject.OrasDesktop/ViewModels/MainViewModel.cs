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

        // Properties
        private TextBlock? _manifestViewer;

        private string _selectedTagReference = string.Empty;
        private string _manifestContent = string.Empty;
        private ObservableCollection<ReferrerNode> _referrers = new();
        private bool _referrersLoading;
        private DigestContextMenuViewModel _digestContextMenu = new();
        private ReferrerNodeContextMenuViewModel _referrerNodeContextMenu = new();
        private string _currentRepositoryPath = string.Empty; // Repository path without registry URL
        private int _selectedTabIndex = 0; // 0 = Manifest tab, 1 = Referrers tab
        private ReferenceHistoryViewModel _referenceHistory; // Initialized in constructor with manifestService
        private RepositorySelectorViewModel _repositorySelector; // Repository selector component (injected via DI)
        private TagSelectorViewModel _tagSelector; // Tag selector component (initialized in constructor)
        private ConnectionViewModel _connection = new(); // Connection control component
        private StatusBarViewModel _statusBar; // Status bar component (subscribes to StatusService)

        // Commands
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceWithTagCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceWithDigestCommand { get; }
        public ReactiveCommand<Unit, Unit> ArtifactActionsCommand { get; }
        public ReactiveCommand<bool, Unit> ForceLoginCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadManifestByReferenceCommand { get; }
        public ReactiveCommand<PlatformImageSize, Unit> ViewPlatformManifestCommand { get; }

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

            // Context menus no longer need TopLevel provider - they use Application.Current for clipboard access

            // Initialize commands
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectToRegistryAsync);
            ForceLoginCommand = ReactiveCommand.CreateFromTask<bool>(forceLogin => ConnectToRegistryAsync(Connection.RegistryUrl, forceLogin));
            
            // DeleteManifestCommand - enabled when a manifest is loaded
            // Create an observable that updates when manifest changes
            var canDeleteObservable = Observable.Create<bool>(observer =>
            {
                // Initial value
                var initialValue = _artifactService.CanDeleteManifest();
                _logger.LogInformation("CanDelete observable: Initial value = {InitialValue}", initialValue);
                observer.OnNext(initialValue);
                
                // Subscribe to manifest changes
                EventHandler<ManifestChangedEventArgs> manifestChangedHandler = (s, e) =>
                {
                    var canDelete = _artifactService.CanDeleteManifest();
                    _logger.LogInformation("CanDelete observable: ManifestChanged, new value = {CanDelete}", canDelete);
                    observer.OnNext(canDelete);
                };
                
                EventHandler<RepositoryChangedEventArgs> repositoryChangedHandler = (s, e) =>
                {
                    var canDelete = _artifactService.CanDeleteManifest();
                    _logger.LogInformation("CanDelete observable: RepositoryChanged, new value = {CanDelete}", canDelete);
                    observer.OnNext(canDelete);
                };
                
                _artifactService.ManifestChanged += manifestChangedHandler;
                _artifactService.RepositoryChanged += repositoryChangedHandler;
                
                // Cleanup
                return System.Reactive.Disposables.Disposable.Create(() =>
                {
                    _artifactService.ManifestChanged -= manifestChangedHandler;
                    _artifactService.RepositoryChanged -= repositoryChangedHandler;
                });
            });
            
            // Create an observable for copy tag command - only enabled when tag is available
            var canCopyTagObservable = Observable.Create<bool>(observer =>
            {
                // Helper function to check if tag is available
                bool CanCopyTag() => _artifactService.CanDeleteManifest() && _artifactService.CurrentTag != null;
                
                // Initial value
                observer.OnNext(CanCopyTag());
                
                // Subscribe to tag, manifest, and repository changes
                EventHandler<TagChangedEventArgs> tagChangedHandler = (s, e) =>
                {
                    observer.OnNext(CanCopyTag());
                };
                
                EventHandler<ManifestChangedEventArgs> manifestChangedHandler = (s, e) =>
                {
                    observer.OnNext(CanCopyTag());
                };
                
                EventHandler<RepositoryChangedEventArgs> repositoryChangedHandler = (s, e) =>
                {
                    observer.OnNext(CanCopyTag());
                };
                
                _artifactService.TagChanged += tagChangedHandler;
                _artifactService.ManifestChanged += manifestChangedHandler;
                _artifactService.RepositoryChanged += repositoryChangedHandler;
                
                // Cleanup
                return System.Reactive.Disposables.Disposable.Create(() =>
                {
                    _artifactService.TagChanged -= tagChangedHandler;
                    _artifactService.ManifestChanged -= manifestChangedHandler;
                    _artifactService.RepositoryChanged -= repositoryChangedHandler;
                });
            });
            
            DeleteManifestCommand = ReactiveCommand.CreateFromTask(DeleteManifestAsync, canDeleteObservable);
            
            // ArtifactActionsCommand - just a dummy command for the button, the actual work is done by menu item commands
            ArtifactActionsCommand = ReactiveCommand.Create(() => { }, canDeleteObservable);
            
            CopyReferenceCommand = ReactiveCommand.CreateFromTask(CopyReferenceToClipboardAsync);
            CopyReferenceWithTagCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithTagAsync, canCopyTagObservable);
            CopyReferenceWithDigestCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithDigestAsync, canDeleteObservable);
            
            LoadManifestByReferenceCommand = ReactiveCommand.Create(() =>
            {
                var reference = ReferenceHistory.CurrentReference?.Trim();
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    _manifestService!.RequestLoad(reference!, Services.LoadSource.ReferenceBox, forceReload: true);
                }
            });
            ViewPlatformManifestCommand = ReactiveCommand.CreateFromTask<PlatformImageSize>(ViewPlatformManifestAsync);

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
            
            // Wire up DigestContextMenu event for "Get Manifest"
            DigestContextMenu.ManifestRequested += OnDigestManifestRequested;
            
            // Wire up ReferrerNodeContextMenu event for "Get Manifest" from referrer tree
            ReferrerNodeContextMenu.ManifestRequested += OnDigestManifestRequested;
            
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
       public TextBlock? ManifestViewer
        {
            get => _manifestViewer;
            set => this.RaiseAndSetIfChanged(ref _manifestViewer, value);
        }
        public string SelectedTagReference
        {
            get => _selectedTagReference;
            set => this.RaiseAndSetIfChanged(ref _selectedTagReference, value);
        }

        public string ManifestContent
        {
            get => _manifestContent;
            set => this.RaiseAndSetIfChanged(ref _manifestContent, value);
        }

        public ObservableCollection<ReferrerNode> Referrers
        {
            get => _referrers;
            set => this.RaiseAndSetIfChanged(ref _referrers, value);
        }

        public bool ReferrersLoading
        {
            get => _referrersLoading;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _referrersLoading, value))
                {
                    // When referrer loading starts, set determinate progress mode
                    if (value)
                    {
                        _statusService.SetProgress(0, isIndeterminate: false);
                    }
                    // When referrer loading ends, reset progress
                    else
                    {
                        _statusService.ResetProgress();
                    }
                }
            }
        }

        public string ArtifactSizeSummary => _artifactService.ArtifactSizeSummary;

        public ObservableCollection<PlatformImageSize> PlatformImageSizes => _artifactService.PlatformImageSizes;

        public bool HasPlatformSizes => _artifactService.HasPlatformSizes;

        public Manifest? CurrentManifest => _artifactService.CurrentManifest;

        public DigestContextMenuViewModel DigestContextMenu
        {
            get => _digestContextMenu;
            set => this.RaiseAndSetIfChanged(ref _digestContextMenu, value);
        }

        public ReferrerNodeContextMenuViewModel ReferrerNodeContextMenu
        {
            get => _referrerNodeContextMenu;
            set => this.RaiseAndSetIfChanged(ref _referrerNodeContextMenu, value);
        }

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

        private ReferrerNode? _selectedReferrerNode;
        public ReferrerNode? SelectedReferrerNode
        {
            get => _selectedReferrerNode;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedReferrerNode, value);
                UpdateReferrerNodeContextMenu();
            }
        }

        // Allow operations as soon as a tag is selected; internal commands manage busy state themselves.
        public bool CanModifySelectedTag => _artifactService.CanDeleteManifest();

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
        }

        // Helper method to get the main window
        private Window? GetMainWindow()
        {
            if (
                Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop
            )
            {
                return desktop.MainWindow;
            }
            return null;
        }

        // Event handlers for component events
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

        private async Task LoadReferrersAsync(string repositoryPath, string digest, string reference)
        {
            ReferrersLoading = true;
            _statusService.SetProgress(0, isIndeterminate: false);
            try
            {
                // Use a progress handler to update both the status message and progress bar
                var progress = new Progress<int>(count =>
                {
                    _statusService.SetStatus($"Loading referrers ({count}) for {reference}...");
                    // Set an indeterminate progress at first to show activity
                    if (count == 1)
                    {
                        _statusService.SetProgress(0);
                    }
                    else
                    {
                        // Once we start getting counts, update the progress
                        // We don't know the total in advance, so we'll use a fixed scale up to 100
                        // and clamp it between 0-100
                        _statusService.SetProgress(Math.Min(count, 100));
                    }
                });
                var nodes = await _registryService.GetReferrersRecursiveAsync(repositoryPath, digest, progress, default);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Referrers.Clear();
                    foreach (var n in nodes)
                        Referrers.Add(n);
                });
                // Count total descriptor referrers (exclude group and annotation key/value leaves)
                int total = 0;
                void CountNode(ReferrerNode n)
                {
                    if (n.Info != null) total++;
                    foreach (var c in n.Children) CountNode(c);
                }
                foreach (var root in nodes) CountNode(root);
                string referrerWord = total == 1 ? "referrer" : "referrers";
                _statusService.SetStatus($"Loaded {total} {referrerWord} for {reference}");
                // Set progress to 100% when complete
                _statusService.SetProgress(100);
            }
            catch (Services.RegistryOperationException regEx)
            {
                _statusService.SetStatus(regEx.Message, isError: true);
                Referrers.Clear();
            }
            catch (Exception ex)
            {
                _statusService.SetStatus($"Error loading referrers: {ex.Message}", isError: true);
                Referrers.Clear();
            }
            finally
            {
                ReferrersLoading = false;
                
                // If no referrers were loaded and user is on Referrers tab, switch back to Manifest tab
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Referrers.Count == 0 && SelectedTabIndex == 1)
                    {
                        SelectedTabIndex = 0; // Switch to Manifest tab
                    }
                });
            }
        }

        private async Task DeleteManifestAsync()
        {
            if (!_artifactService.CanDeleteManifest())
            {
                _statusService.SetStatus("No manifest to delete", isError: true);
                return;
            }

            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                _statusService.SetStatus("Failed to get main window", isError: true);
                return;
            }

            if (_artifactService.CurrentRegistry == null || _artifactService.CurrentRepository == null || _artifactService.CurrentManifest == null)
            {
                _statusService.SetStatus("No registry, repository, or manifest loaded", isError: true);
                return;
            }

            // Calculate the full reference for the manifest (handle both tag and digest references)
            string manifestRepoPath = _artifactService.CurrentRepository.FullPath.Replace(
                $"{_artifactService.CurrentRegistry.Url}/",
                string.Empty
            );
            
            string fullReference;
            if (_artifactService.CurrentTag != null)
            {
                // Tag reference
                fullReference = $"{_artifactService.CurrentRegistry.Url}/{manifestRepoPath}:{_artifactService.CurrentTag.Name}";
            }
            else
            {
                // Digest reference
                fullReference = $"{_artifactService.CurrentRegistry.Url}/{manifestRepoPath}@{_artifactService.CurrentManifest.Digest}";
            }

            // Show delete confirmation dialog
            var dialog = new DeleteManifestDialog(fullReference);
            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (!result)
            {
                return;
            }

            _statusService.SetBusy(true);
            var referenceDesc = _artifactService.CurrentTag != null 
                ? _artifactService.CurrentTag.Name 
                : _artifactService.CurrentManifest.Digest.Substring(0, 12) + "...";
            _statusService.SetStatus($"Deleting manifest for {referenceDesc}...");

            try
            {
                var (success, message) = await _artifactService.DeleteManifestAsync();

                if (success)
                {
                    // Only refresh tags if we have a tag selected
                    if (_artifactService.CurrentTag != null)
                    {
                        await RefreshTagsAsync();
                    }
                    _statusService.SetStatus(message);
                }
                else
                {
                    _statusService.SetStatus(message, isError: true);
                }
            }
            finally
            {
                _statusService.SetBusy(false);
                _statusService.ResetProgress();
            }
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

        private async Task CopyReferenceWithTagAsync()
        {
            if (_artifactService.CurrentRegistry == null || _artifactService.CurrentRepository == null)
            {
                _statusService.SetStatus("No manifest loaded", isError: true);
                return;
            }

            try
            {
                string manifestRepoPath = _artifactService.CurrentRepository.FullPath.Replace(
                    $"{_artifactService.CurrentRegistry.Url}/",
                    string.Empty
                );

                string reference;
                if (_artifactService.CurrentTag != null)
                {
                    // Use the actual tag
                    reference = $"{_artifactService.CurrentRegistry.Url}/{manifestRepoPath}:{_artifactService.CurrentTag.Name}";
                }
                else
                {
                    _statusService.SetStatus("No tag available for this manifest", isError: true);
                    return;
                }

                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel != null)
                {
                    await topLevel.Clipboard!.SetTextAsync(reference);
                    _statusService.SetStatus("Reference with tag copied to clipboard");
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

        private async Task CopyReferenceWithDigestAsync()
        {
            if (_artifactService.CurrentRegistry == null || _artifactService.CurrentRepository == null || _artifactService.CurrentManifest == null)
            {
                _statusService.SetStatus("No manifest loaded", isError: true);
                return;
            }

            try
            {
                string manifestRepoPath = _artifactService.CurrentRepository.FullPath.Replace(
                    $"{_artifactService.CurrentRegistry.Url}/",
                    string.Empty
                );

                string reference = $"{_artifactService.CurrentRegistry.Url}/{manifestRepoPath}@{_artifactService.CurrentManifest.Digest}";

                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel != null)
                {
                    await topLevel.Clipboard!.SetTextAsync(reference);
                    _statusService.SetStatus("Reference with digest copied to clipboard");
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

                ManifestContent = newManifest.RawContent;
                _currentRepositoryPath = repository; // Store for referrer context menu

                // Update digest context menu
                DigestContextMenu.Digest = newManifest.Digest;
                DigestContextMenu.Repository = repository;
                DigestContextMenu.RegistryUrl = _artifactService.CurrentRegistry?.Url ?? string.Empty;

                // Create a highlighted and selectable text block
                ManifestViewer = _jsonHighlightService.HighlightJson(newManifest.RawContent);

                // Load referrers
                _statusService.SetProgress(0);
                // Progress indeterminate handled by SetProgress
                _ = LoadReferrersAsync(repository, newManifest.Digest, originalReference);

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
                ManifestContent = string.Empty;
                ManifestViewer = null;
            }
            finally
            {
                _statusService.SetBusy(false);
                if (!ReferrersLoading)
                {
                    _statusService.ResetProgress();
                }
                else
                {
                    // Progress reset handled by StatusService
                }
            }
        }

        /// <summary>
        /// Loads and displays the manifest for a specific platform
        /// </summary>
        private async Task ViewPlatformManifestAsync(PlatformImageSize platformSize)
        {
            if (string.IsNullOrEmpty(platformSize.Digest) || _artifactService.CurrentRepository == null)
            {
                _statusService.SetStatus("Platform digest not available", isError: true);
                return;
            }

            _statusService.SetBusy(true);
            _statusService.SetStatus($"Loading manifest for platform {platformSize.Platform}...");

            try
            {
                var repoPath = _artifactService.CurrentRepository.FullPath.Replace(
                    $"{_artifactService.CurrentRegistry?.Url}/",
                    string.Empty
                );

                // Get the manifest for this specific platform
                var manifest = await _registryService.GetManifestByDigestAsync(
                    repoPath,
                    platformSize.Digest,
                    default
                );

                // Create and set the manifest in ArtifactService (will calculate size automatically)
                var newManifest = new Manifest
                {
                    RawContent = manifest.Json,
                    Digest = manifest.Digest,
                    Tag = _artifactService.CurrentTag,
                    MediaType = manifest.MediaType,
                };

                await _artifactService.SetManifestAsync(newManifest, repoPath);

                ManifestContent = newManifest.RawContent;
                _currentRepositoryPath = repoPath; // Store for referrer context menu

                // Update digest context menu
                DigestContextMenu.Digest = newManifest.Digest;
                DigestContextMenu.Repository = repoPath;
                DigestContextMenu.RegistryUrl = _artifactService.CurrentRegistry?.Url ?? string.Empty;

                // Create a highlighted and selectable text block
                ManifestViewer = _jsonHighlightService.HighlightJson(newManifest.RawContent);

                // Update the selected tag reference to use the digest of the platform-specific manifest
                string platformReference = SelectedTagReference;
                if (_artifactService.CurrentTag != null && _artifactService.CurrentRegistry != null)
                {
                    string repository = _artifactService.CurrentTag.Repository!.FullPath.Replace($"{_artifactService.CurrentRegistry.Url}/", string.Empty);
                    platformReference = $"{_artifactService.CurrentRegistry.Url}/{repository}@{platformSize.Digest}";
                    SelectedTagReference = platformReference;
                }

                // Kick off referrers load (fire and forget, separate status message)
                _statusService.SetProgress(0);
                // Progress indeterminate handled by SetProgress
                _ = LoadReferrersAsync(repoPath, newManifest.Digest, platformReference);

                _statusService.SetStatus($"Loaded manifest for platform {platformSize.Platform}");
            }
            catch (Exception ex)
            {
                _statusService.SetStatus($"Error loading platform manifest: {ex.Message}", isError: true);
            }
            finally
            {
                _statusService.SetBusy(false);
                if (!ReferrersLoading)
                {
                    _statusService.ResetProgress();
                }
                else
                {
                    // Progress reset handled by StatusService
                }
            }
        }
    }
}

// Partial class extension for helper methods
namespace OrasProject.OrasDesktop.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>
        /// Attempts to navigate the UI (repository and tag selection) when a reference is selected from history.
        /// Only navigates if the registry matches the current registry.
        /// </summary>
        private async Task TryNavigateToReferenceAsync(string reference, bool selectTag = true)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Trying to navigate to {Reference} after load from {Source}. SelectTag: {SelectTag}", reference, _manifestService.CurrentLoadSource, selectTag);
            }
            if (string.IsNullOrWhiteSpace(reference) || _artifactService.CurrentRegistry == null)
                return;

            try
            {
                // Parse the reference to extract registry, repository, and tag/digest
                var parts = reference.Split('/', 2);
                if (parts.Length < 2)
                    return; // Invalid format

                var registryHost = parts[0];
                var remainder = parts[1];

                // Check if the registry matches the current one
                if (!_artifactService.CurrentRegistry.Url.Contains(registryHost, StringComparison.OrdinalIgnoreCase))
                    return; // Different registry, don't navigate

                // Check if this is a digest reference
                bool isDigest = remainder.Contains("@sha256:") || remainder.Contains("@sha512:");

                // Skip navigation for digest references - they don't have a corresponding tag to select
                // and navigating to the repository might incorrectly select a previously selected tag
                if (isDigest)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Skipping UI navigation for digest reference {Reference}.", reference);
                    }
                    return;
                }

                // Split repository and tag reference
                string repository;
                string tagOrDigest;
                var tagParts = remainder.Split(':', 2);
                repository = tagParts[0];
                tagOrDigest = tagParts.Length > 1 ? tagParts[1] : "latest";

                // Try to find and select the repository in the tree
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // Clear tag filter if needed
                    if (!string.IsNullOrEmpty(TagSelector.FilterText))
                    {
                        TagSelector.FilterText = string.Empty;
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("Cleared tag filter to ensure navigation to {Tag}.", tagOrDigest);
                        }
                    }
                    
                    // Use the RepositorySelector's navigation method to handle filter clearing,
                    // repository finding, ancestor expansion, and selection
                    bool repositoryFound = await RepositorySelector.NavigateToRepositoryAsync(repository);
                    
                    if (repositoryFound)
                    {
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("Successfully navigated to repository {Repository}.", repository);
                        }
                        
                        // Wait for tags to load after repository selection
                        await Task.Delay(100);
                        
                        // Try to select the matching tag (only if selectTag is true and not a digest)
                        if (selectTag && !isDigest)
                        {
                            var matchingTag = TagSelector.Tags.FirstOrDefault(t => 
                                string.Equals(t.Name, tagOrDigest, StringComparison.OrdinalIgnoreCase));
                            
                            if (matchingTag != null)
                            {
                                if (_logger.IsEnabled(LogLevel.Information))
                                {
                                    _logger.LogInformation("Navigating to tag {Tag} in repository {Repository}.", matchingTag.Name, repository);
                                }
                                
                                // Sync with TagSelector (silently - no load trigger)
                                // ArtifactService subscribes to TagSelector.SelectedTag changes
                                TagSelector.UpdateSelectedTagSilently(matchingTag);
                            }
                            else if (_logger.IsEnabled(LogLevel.Information))
                            {
                                _logger.LogInformation("Tag {Tag} was not found in repository {Repository}.", tagOrDigest, repository);
                            }
                        }
                        else if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("Skipping tag selection as requested (selectTag={SelectTag}, isDigest={IsDigest}).", selectTag, isDigest);
                        }
                    }
                    else if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Repository {Repository} was not found in the current registry tree.", repository);
                    }
                });
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(ex, "Failed to navigate to reference {Reference}.", reference);
                }
                // Ignore navigation errors - user can still manually load
            }
        }

        /// <summary>
        /// Finds a repository by its path (e.g., "library/nginx" or "dotnet/runtime")
        /// </summary>
        private void UpdateReferrerNodeContextMenu()
        {
            if (SelectedReferrerNode != null)
            {
                // Set RegistryUrl and Repository FIRST, before Node
                // because setting Node triggers UpdateContextMenus which creates the DigestContextMenu
                ReferrerNodeContextMenu.RegistryUrl = _artifactService.CurrentRegistry?.Url ?? string.Empty;
                ReferrerNodeContextMenu.Repository = _currentRepositoryPath;
                ReferrerNodeContextMenu.Node = SelectedReferrerNode;
            }
        }

        /// <summary>
        /// Handles manifest load requests from manifestService
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
                        _ = TryNavigateToReferenceAsync(e.Reference, selectTag: false);
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
                        _ = TryNavigateToReferenceAsync(e.Reference);
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

            _manifestService.RequestLoad(reference, Services.LoadSource.History, forceReload: true);
        }
        
        private void OnDigestManifestRequested(object? sender, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                _statusService.SetStatus("Invalid reference", isError: true);
                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Digest context menu requested manifest load for {Reference}.", reference);
            }

            // Update the reference box to show the digest reference
            ReferenceHistory.CurrentReference = reference;
            
            // Request the load
            _manifestService.RequestLoad(reference, Services.LoadSource.ReferenceBox, forceReload: true);
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
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Calling manifestService.RequestLoad with reference: {tag.FullReference}, source: TagSelection");
            _manifestService.RequestLoad(tag.FullReference, Services.LoadSource.TagSelection);
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






