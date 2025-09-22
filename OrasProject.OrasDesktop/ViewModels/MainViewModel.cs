using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using OrasProject.OrasDesktop.Views;
using ReactiveUI;
using System.Text.Json;

namespace OrasProject.OrasDesktop.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IRegistryService _registryService;
        private readonly JsonHighlightService _jsonHighlightService;

        // Properties
        private string _registryUrl = "mcr.microsoft.com";
        private ObservableCollection<Repository> _repositories =
            new ObservableCollection<Repository>();
        private ObservableCollection<Repository> _filteredRepositories = new();
        private Repository? _selectedRepository;
        private ObservableCollection<Tag> _tags = new ObservableCollection<Tag>();
        private Tag? _selectedTag;
        private TextBlock? _manifestViewer;
        private Manifest? _currentManifest;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private Registry _currentRegistry = new Registry();
        private ObservableCollection<string> _authTypes = new ObservableCollection<string>
        {
            "None",
            "Basic",
            "Token",
        };
        private string _selectedAuthType = "None";

        private string _selectedTagReference = string.Empty;
        private string _manifestContent = string.Empty;
        private ObservableCollection<ReferrerNode> _referrers = new();
        private bool _referrersLoading;
        private double _progressValue;
        private bool _isProgressIndeterminate;
        private string _repositoryFilterText = string.Empty;
        private string _artifactSizeSummary = string.Empty;
        private ObservableCollection<PlatformImageSize> _platformImageSizes = new();
        private bool _hasPlatformSizes = false;

        // Commands
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshTagsCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceCommand { get; }

        public MainViewModel()
        {
            _registryService = new RegistryService();
            _jsonHighlightService = new JsonHighlightService();

            // Initialize commands
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectToRegistryAsync);
            LoginCommand = ReactiveCommand.CreateFromTask(LoginToRegistryAsync);
            RefreshTagsCommand = ReactiveCommand.CreateFromTask(RefreshTagsAsync);
            DeleteManifestCommand = ReactiveCommand.CreateFromTask(DeleteManifestAsync);
            CopyManifestCommand = ReactiveCommand.CreateFromTask(CopyManifestAsync);
            CopyReferenceCommand = ReactiveCommand.CreateFromTask(CopyReferenceToClipboardAsync);

            // Setup property change handlers
            this.WhenAnyValue(x => x.SelectedRepository)
                .WhereNotNull()
                .Subscribe(async repo => await LoadTagsAsync(repo));

            this.WhenAnyValue(x => x.SelectedTag)
                .WhereNotNull()
                .Subscribe(async tag =>
                {
                    SelectedTagReference = tag.FullReference;
                    Referrers.Clear();
                    ReferrersLoading = false;
                    // Reset progress bar to default state
                    ProgressValue = 0;
                    // Clear size information
                    ArtifactSizeSummary = string.Empty;
                    PlatformImageSizes.Clear();
                    HasPlatformSizes = false;
                    await LoadManifestAsync(tag);
                });

            this.WhenAnyValue(x => x.SelectedAuthType)
                .Subscribe(authType =>
                {
                    _currentRegistry.AuthenticationType = authType switch
                    {
                        "Basic" => AuthenticationType.Basic,
                        "Token" => AuthenticationType.Token,
                        _ => AuthenticationType.None,
                    };
                });
        }

        // Property accessors
        public string RegistryUrl
        {
            get => _registryUrl;
            set => this.RaiseAndSetIfChanged(ref _registryUrl, value);
        }

        public ObservableCollection<Repository> Repositories
        {
            get => _repositories;
            set => this.RaiseAndSetIfChanged(ref _repositories, value);
        }

        public ObservableCollection<Repository> FilteredRepositories
        {
            get => _filteredRepositories;
            set => this.RaiseAndSetIfChanged(ref _filteredRepositories, value);
        }

        public Repository? SelectedRepository
        {
            get => _selectedRepository;
            set => this.RaiseAndSetIfChanged(ref _selectedRepository, value);
        }

        public ObservableCollection<Tag> Tags
        {
            get => _tags;
            set => this.RaiseAndSetIfChanged(ref _tags, value);
        }

        public Tag? SelectedTag
        {
            get => _selectedTag;
            set
            {
                if (!EqualityComparer<Tag?>.Default.Equals(_selectedTag, value))
                {
                    this.RaiseAndSetIfChanged(ref _selectedTag, value);
                    this.RaisePropertyChanged(nameof(CanModifySelectedTag));
                }
            }
        }

        public TextBlock? ManifestViewer
        {
            get => _manifestViewer;
            set => this.RaiseAndSetIfChanged(ref _manifestViewer, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _isBusy, value))
                {
                    this.RaisePropertyChanged(nameof(CanModifySelectedTag));
                    
                    // If we're setting busy to true, make the progress bar indeterminate
                    // unless we're specifically loading referrers
                    if (value && !ReferrersLoading)
                    {
                        IsProgressIndeterminate = true;
                    }
                    else if (!value)
                    {
                        // If we're no longer busy, hide the progress indicator
                        // unless referrers are still loading
                        IsProgressIndeterminate = ReferrersLoading;
                    }
                }
            }
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
            set => this.RaiseAndSetIfChanged(ref _referrersLoading, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => this.RaiseAndSetIfChanged(ref _progressValue, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => this.RaiseAndSetIfChanged(ref _isProgressIndeterminate, value);
        }

        public string RepositoryFilterText
        {
            get => _repositoryFilterText;
            set
            {
                if (!string.Equals(_repositoryFilterText, value, StringComparison.Ordinal))
                {
                    this.RaiseAndSetIfChanged(ref _repositoryFilterText, value);
                    ApplyRepositoryFilter();
                }
            }
        }

        public string ArtifactSizeSummary
        {
            get => _artifactSizeSummary;
            set => this.RaiseAndSetIfChanged(ref _artifactSizeSummary, value);
        }

        public ObservableCollection<PlatformImageSize> PlatformImageSizes
        {
            get => _platformImageSizes;
            set => this.RaiseAndSetIfChanged(ref _platformImageSizes, value);
        }

        public bool HasPlatformSizes
        {
            get => _hasPlatformSizes;
            set => this.RaiseAndSetIfChanged(ref _hasPlatformSizes, value);
        }

        public ObservableCollection<string> AuthTypes
        {
            get => _authTypes;
            set => this.RaiseAndSetIfChanged(ref _authTypes, value);
        }

        public string SelectedAuthType
        {
            get => _selectedAuthType;
            set => this.RaiseAndSetIfChanged(ref _selectedAuthType, value);
        }

        // Allow operations as soon as a tag is selected; internal commands manage busy state themselves.
        public bool CanModifySelectedTag => _selectedTag != null;

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

        // Command implementations
        private async Task ConnectToRegistryAsync()
        {
            IsBusy = true;
            StatusMessage = "Connecting to registry...";

            try
            {
                _currentRegistry.Url = RegistryUrl;

                // Initialize new oras service (anonymous for now) in parallel with legacy connect until migration completes
                await _registryService.InitializeAsync(
                    new RegistryConnection(
                        _currentRegistry.Url,
                        _currentRegistry.IsSecure,
                        _currentRegistry.AuthenticationType switch
                        {
                            AuthenticationType.Basic => AuthType.Basic,
                            AuthenticationType.Token => AuthType.Bearer,
                            _ => AuthType.Anonymous,
                        },
                        _currentRegistry.Username,
                        _currentRegistry.Password,
                        _currentRegistry.Token
                    ),
                    default
                );

                // Ping /v2/ manually to validate connectivity (basic anonymous check)
                var repos = await _registryService.ListRepositoriesAsync(default);
                var connected = repos.Count >= 0; // if call returns without exception treat as connected
                if (!connected)
                {
                    StatusMessage = "Failed to connect to registry";
                    return;
                }

                // Get repositories (already sorted at all levels)
                var repositories = await BuildRepositoryTreeAsync();

                Repositories.Clear();
                foreach (var repo in repositories)
                {
                    Repositories.Add(repo);
                }

                ApplyRepositoryFilter();

                StatusMessage = "Connected to registry";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error connecting to registry: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoginToRegistryAsync()
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusMessage = "Failed to get main window";
                return;
            }

            try
            {
                var result = await LoginDialog.ShowDialog(mainWindow, RegistryUrl);
                if (!result.Result)
                {
                    return;
                }

                IsBusy = true;
                StatusMessage = "Authenticating with registry...";

                // Update registry with authentication information
                _currentRegistry.AuthenticationType = result.AuthType;
                _currentRegistry.Username = result.Username;
                _currentRegistry.Password = result.Password;
                _currentRegistry.Token = result.Token;

                // Reinitialize with supplied credentials
                await _registryService.InitializeAsync(
                    new RegistryConnection(
                        _currentRegistry.Url,
                        _currentRegistry.IsSecure,
                        _currentRegistry.AuthenticationType switch
                        {
                            AuthenticationType.Basic => AuthType.Basic,
                            AuthenticationType.Token => AuthType.Bearer,
                            _ => AuthType.Anonymous,
                        },
                        _currentRegistry.Username,
                        _currentRegistry.Password,
                        _currentRegistry.Token
                    ),
                    default
                );

                StatusMessage = "Authenticated with registry";

                // Update the authentication type in the UI
                SelectedAuthType = result.AuthType switch
                {
                    AuthenticationType.Basic => "Basic",
                    AuthenticationType.Token => "Token",
                    _ => "None",
                };
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error authenticating with registry: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadTagsAsync(Repository repository)
        {
            if (repository == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"Loading tags for {repository.Name}...";

            try
            {
                var tagNames = await _registryService.ListTagsAsync(
                    repository.FullPath.Replace($"{_currentRegistry.Url}/", string.Empty),
                    default
                );
                var tags = new List<Tag>();
                foreach (var name in tagNames)
                {
                    tags.Add(
                        new Tag
                        {
                            Name = name,
                            Repository = repository,
                            CreatedAt = DateTimeOffset.Now,
                        }
                    );
                }

                // Sort tags by name using IComparable implementation
                tags.Sort();

                Tags.Clear();
                foreach (var tag in tags)
                {
                    Tags.Add(tag);
                }

                // Digest resolution removed for performance; resolved only when required for delete.

                StatusMessage = $"Loaded {tags.Count} tags for {repository.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading tags: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshTagsAsync()
        {
            if (SelectedRepository == null)
            {
                StatusMessage = "No repository selected";
                return;
            }

            await LoadTagsAsync(SelectedRepository);
        }

        private async Task LoadManifestAsync(Tag tag)
        {
            if (tag == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"Loading manifest for {tag.Name}...";

            try
            {
                var repoPath = tag.Repository!.FullPath.Replace(
                    $"{_currentRegistry.Url}/",
                    string.Empty
                );
                var manifest = await _registryService.GetManifestByTagAsync(
                    repoPath,
                    tag.Name,
                    default
                );
                _currentManifest = new Manifest
                {
                    RawContent = manifest.Json,
                    Digest = manifest.Digest,
                    Tag = tag,
                    MediaType = manifest.MediaType,
                };
                ManifestContent = _currentManifest.RawContent;
                
                // Create a highlighted and selectable text block
                ManifestViewer = _jsonHighlightService.HighlightJson(
                    _currentManifest.RawContent,
                    async (digest) => await LoadContentByDigestAsync(digest)
                );

                // Calculate artifact size information
                await CalculateArtifactSizeAsync(repoPath, manifest);

                // Kick off referrers load (fire and forget, separate status message)
                ProgressValue = 0;
                IsProgressIndeterminate = false;
                _ = LoadReferrersAsync(repoPath, _currentManifest.Digest);

                StatusMessage = $"Loaded manifest for {tag.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading manifest: {ex.Message}";
                ManifestContent = string.Empty;
                ManifestViewer = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadContentByDigestAsync(string digest)
        {
            if (SelectedRepository == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"Loading content for {digest}...";

            try
            {
                var repoPath = SelectedRepository.FullPath.Replace(
                    $"{_currentRegistry.Url}/",
                    string.Empty
                );
                var manifest = await _registryService.GetManifestByDigestAsync(
                    repoPath,
                    digest,
                    default
                );
                ManifestContent = manifest.Json;
                
                // Create a highlighted and selectable text block
                ManifestViewer = _jsonHighlightService.HighlightJson(
                    manifest.Json,
                    async (digestValue) => await LoadContentByDigestAsync(digestValue)
                );

                // Calculate artifact size
                await CalculateArtifactSizeAsync(repoPath, manifest);

                // Reset progress state before loading referrers
                ProgressValue = 0;
                IsProgressIndeterminate = false;
                _ = LoadReferrersAsync(repoPath, manifest.Digest);

                StatusMessage = $"Loaded content for {digest}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading content: {ex.Message}";
                ManifestContent = string.Empty;
                ManifestViewer = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Calculates and updates artifact size information for the current manifest
        /// </summary>
        private async Task CalculateArtifactSizeAsync(string repositoryPath, ManifestResult manifest)
        {
            try
            {
                // Reset previous data
                PlatformImageSizes.Clear();
                HasPlatformSizes = false;
                
                // Calculate new size information
                var (summary, platformSizes, hasPlatforms) = await ArtifactSizeCalculator.AnalyzeManifestSizeAsync(
                    _registryService, 
                    repositoryPath,
                    manifest, 
                    default);
                    
                // Update UI
                ArtifactSizeSummary = summary;
                
                if (hasPlatforms && platformSizes.Count > 0)
                {
                    // Sort platforms alphabetically
                    foreach (var platform in platformSizes.OrderBy(p => p.Platform))
                    {
                        PlatformImageSizes.Add(platform);
                    }
                    HasPlatformSizes = true;
                }
            }
            catch (Exception ex)
            {
                // In case of errors, just show basic information
                ArtifactSizeSummary = $"Size calculation error: {ex.Message}";
                HasPlatformSizes = false;
            }
        }

        private async Task LoadReferrersAsync(string repositoryPath, string digest)
        {
            ReferrersLoading = true;
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            try
            {
                // Use a progress handler to update both the status message and progress bar
                var progress = new Progress<int>(count =>
                {
                    StatusMessage = $"Loading referrers ({count})...";
                    // Set an indeterminate progress at first to show activity
                    if (count == 1)
                    {
                        ProgressValue = 0;
                    }
                    else 
                    {
                        // Once we start getting counts, update the progress
                        // We don't know the total in advance, so we'll use a fixed scale up to 100
                        // and clamp it between 0-100
                        ProgressValue = Math.Min(count, 100);
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
                StatusMessage = $"Loaded referrers ({total})";
                // Set progress to 100% when complete
                ProgressValue = 100;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading referrers: {ex.Message}";
                Referrers.Clear();
            }
            finally
            {
                ReferrersLoading = false;
                // Reset progress bar state if no other operation is busy
                if (!IsBusy)
                {
                    IsProgressIndeterminate = false;
                    ProgressValue = 0;
                }
            }
        }

        private async Task DeleteManifestAsync()
        {
            if (SelectedTag == null)
            {
                StatusMessage = "No tag selected";
                return;
            }

            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusMessage = "Failed to get main window";
                return;
            }

            // Confirm deletion
            var messageBox = new Window
            {
                Title = "Confirm Deletion",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SizeToContent = SizeToContent.Height,
                Content = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,Auto"),
                    Margin = new Avalonia.Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text =
                                $"Are you sure you want to delete the manifest for {SelectedTag.Name}?",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            [Grid.RowProperty] = 0,
                        },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 10,
                            [Grid.RowProperty] = 1,
                            Children =
                            {
                                new Button
                                {
                                    Content = "Cancel",
                                    Width = 80,
                                    [Grid.ColumnProperty] = 0,
                                    Tag = false,
                                },
                                new Button
                                {
                                    Content = "Delete",
                                    Width = 80,
                                    [Grid.ColumnProperty] = 1,
                                    Tag = true,
                                },
                            },
                        },
                    },
                },
            };

            bool confirmResult = false;
            foreach (var button in ((StackPanel)((Grid)messageBox.Content!).Children[1]).Children)
            {
                if (button is Button btn)
                {
                    btn.Click += (s, e) =>
                    {
                        confirmResult = (bool)btn.Tag!;
                        messageBox.Close();
                    };
                }
            }

            await messageBox.ShowDialog(mainWindow);

            if (!confirmResult)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"Deleting manifest for {SelectedTag.Name}...";

            try
            {
                var repoPath = SelectedTag.Repository!.FullPath.Replace(
                    $"{_currentRegistry.Url}/",
                    string.Empty
                );

                var manifest = await _registryService.GetManifestByTagAsync(
                    repoPath,
                    SelectedTag.Name,
                    default
                );
                await _registryService.DeleteManifestAsync(repoPath, manifest.Digest, default);

                // Refresh tags
                await RefreshTagsAsync();

                StatusMessage = $"Deleted manifest for {SelectedTag.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting manifest: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CopyManifestAsync()
        {
            if (SelectedTag == null || SelectedRepository == null)
            {
                StatusMessage = "No tag or repository selected";
                return;
            }

            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusMessage = "Failed to get main window";
                return;
            }

            var result = await CopyManifestDialog.ShowDialog(mainWindow, SelectedTag.Name);
            if (!result.Result)
            {
                return;
            }

            IsBusy = true;
            StatusMessage =
                $"Copying manifest for {SelectedTag.Name} to {result.DestinationTag}...";

            try
            {
                var repoPath = SelectedRepository.FullPath.Replace(
                    $"{_currentRegistry.Url}/",
                    string.Empty
                );
                var progress = new Progress<CopyProgress>(p =>
                {
                    StatusMessage =
                        p.Total <= 0 ? p.Stage : $"{p.Stage} {(p.Completed ?? 0)}/{p.Total}";
                });
                await _registryService.CopyAsync(
                    new CopyRequest(repoPath, SelectedTag.Name, repoPath, result.DestinationTag),
                    progress,
                    default
                );

                // Refresh tags
                await RefreshTagsAsync();

                StatusMessage =
                    $"Copied manifest for {SelectedTag.Name} to {result.DestinationTag}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error copying manifest: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CopyReferenceToClipboardAsync()
        {
            if (SelectedTag == null)
            {
                StatusMessage = "No tag selected";
                return;
            }

            try
            {
                // Get reference without the "Reference: " prefix
                string reference = SelectedTag.FullReference;

                // Copy to clipboard
                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel != null)
                {
                    await topLevel.Clipboard!.SetTextAsync(reference);
                    StatusMessage = "Reference copied to clipboard";
                }
                else
                {
                    StatusMessage = "Failed to access clipboard";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error copying reference: {ex.Message}";
            }
        }
    }
}

// Partial class extension for helper methods
namespace OrasProject.OrasDesktop.ViewModels
{
    public partial class MainViewModel
    {
        private async Task<List<Repository>> BuildRepositoryTreeAsync()
        {
            var list = await _registryService.ListRepositoriesAsync(default);
            var root = new List<Repository>();
            var dict = new Dictionary<string, Repository>(StringComparer.OrdinalIgnoreCase);
            foreach (var full in list)
            {
                var segments = full.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string path = string.Empty;
                Repository? parent = null;
                for (int i = 0; i < segments.Length; i++)
                {
                    path = path.Length == 0 ? segments[i] : path + "/" + segments[i];
                    if (!dict.TryGetValue(path, out var repo))
                    {
                        repo = new Repository
                        {
                            Name = segments[i],
                            FullPath = $"{_currentRegistry.Url}/{path}",
                            Parent = parent,
                            Registry = _currentRegistry,
                            IsLeaf = i == segments.Length - 1,
                        };
                        dict[path] = repo;
                        if (parent != null)
                            parent.Children.Add(repo);
                        else
                            root.Add(repo);
                    }
                    parent = repo;
                }
            }
            
            // Sort all repositories recursively
            SortRepositoriesRecursively(root);
            return root;
        }
        
        private void SortRepositoriesRecursively(List<Repository> repositories)
        {
            // Sort the current level using IComparable implementation
            repositories.Sort();
            
            // Recursively sort all children
            foreach (var repo in repositories)
            {
                if (repo.Children.Count > 0)
                {
                    SortRepositoriesRecursively(repo.Children);
                }
            }
        }

        private void ApplyRepositoryFilter()
        {
            var filter = RepositoryFilterText;
            FilteredRepositories.Clear();

            // No filter -> shallow copy of original top-level collection
            if (string.IsNullOrWhiteSpace(filter))
            {
                foreach (var r in Repositories)
                    FilteredRepositories.Add(r);
                return;
            }

            var trimmed = filter.Trim();
            foreach (var root in Repositories)
            {
                var pruned = PruneRepository(root, trimmed);
                if (pruned != null)
                    FilteredRepositories.Add(pruned);
            }
        }

        private Repository? PruneRepository(Repository source, string filter)
        {
            bool selfMatch = source.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            var prunedChildren = new List<Repository>();
            foreach (var child in source.Children)
            {
                var prunedChild = PruneRepository(child, filter);
                if (prunedChild != null)
                {
                    prunedChild.Parent = null; // will set below after attaching
                    prunedChildren.Add(prunedChild);
                }
            }

            if (!selfMatch && prunedChildren.Count == 0)
                return null; // neither this node nor descendants match

            // create clone (prunedChildren already filtered)
            var clone = new Repository
            {
                Name = source.Name,
                FullPath = source.FullPath,
                Registry = source.Registry,
                IsLeaf = source.IsLeaf,
            };
            foreach (var c in prunedChildren)
            {
                c.Parent = clone;
                clone.Children.Add(c);
            }
            return clone;
        }
    }
}