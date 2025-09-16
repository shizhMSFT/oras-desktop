﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DynamicData;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using OrasProject.OrasDesktop.Views;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IOrasRegistryService _orasService;
        private readonly JsonHighlightService _jsonHighlightService;

        // Properties
        private string _registryUrl = "mcr.microsoft.com";
        private ObservableCollection<Repository> _repositories =
            new ObservableCollection<Repository>();
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

        // Commands
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshTagsCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyManifestCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyReferenceCommand { get; }

        public MainViewModel()
        {
            _orasService = new OrasRegistryService();
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
                await _orasService.InitializeAsync(
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
                var repos = await _orasService.ListRepositoriesAsync(default);
                var connected = repos.Count >= 0; // if call returns without exception treat as connected
                if (!connected)
                {
                    StatusMessage = "Failed to connect to registry";
                    return;
                }

                // Get repositories
                var repositories = await BuildRepositoryTreeAsync();

                // Sort repositories by name
                var sortedRepositories = repositories.OrderBy(r => r.Name).ToList();

                Repositories.Clear();
                foreach (var repo in sortedRepositories)
                {
                    Repositories.Add(repo);
                }

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
                await _orasService.InitializeAsync(
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
                var tagNames = await _orasService.ListTagsAsync(
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

                // Sort tags by name
                var sortedTags = tags.OrderBy(t => t.Name).ToList();

                Tags.Clear();
                foreach (var tag in sortedTags)
                {
                    Tags.Add(tag);
                }

                // Digest resolution removed for performance; resolved only when required for delete.

                StatusMessage = $"Loaded {sortedTags.Count} tags for {repository.Name}";
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
                var manifest = await _orasService.GetManifestByTagAsync(
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

                StatusMessage = $"Loaded manifest for {tag.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading manifest: {ex.Message}";
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
                var manifest = await _orasService.GetManifestByDigestAsync(
                    repoPath,
                    digest,
                    default
                );
                ManifestContent = manifest.Json;

                StatusMessage = $"Loaded content for {digest}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading content: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
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

                var manifest = await _orasService.GetManifestByTagAsync(
                    repoPath,
                    SelectedTag.Name,
                    default
                );
                await _orasService.DeleteManifestAsync(repoPath, manifest.Digest, default);

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
                await _orasService.CopyAsync(
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
            var list = await _orasService.ListRepositoriesAsync(default);
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
            return root.OrderBy(r => r.Name).ToList();
        }
    }
}
