using System;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for displaying manifest JSON with syntax highlighting and digest navigation
/// </summary>
public class JsonViewerViewModel : ViewModelBase
{
    private readonly JsonHighlightService _jsonHighlightService;
    private readonly ManifestService _manifestService;
    private readonly ILogger<JsonViewerViewModel> _logger;
    
    private ContentControl? _manifestViewer;
    private string _manifestContent = string.Empty;
    private string _currentDigest = string.Empty;
    private DigestContextMenuViewModel _digestContextMenu = new();

    public JsonViewerViewModel(
        JsonHighlightService jsonHighlightService,
        ManifestService manifestService,
        ILogger<JsonViewerViewModel> logger)
    {
        _jsonHighlightService = jsonHighlightService;
        _manifestService = manifestService;
        _logger = logger;
    }

    /// <summary>
    /// The ContentControl used to display the manifest JSON TextBlock
    /// </summary>
    public ContentControl? ManifestViewer
    {
        get => _manifestViewer;
        set => this.RaiseAndSetIfChanged(ref _manifestViewer, value);
    }

    /// <summary>
    /// The raw JSON content of the manifest
    /// </summary>
    public string ManifestContent
    {
        get => _manifestContent;
        private set => this.RaiseAndSetIfChanged(ref _manifestContent, value);
    }

    /// <summary>
    /// The current manifest digest being displayed
    /// </summary>
    public string CurrentDigest
    {
        get => _currentDigest;
        private set => this.RaiseAndSetIfChanged(ref _currentDigest, value);
    }

    /// <summary>
    /// Context menu for digest operations
    /// </summary>
    public DigestContextMenuViewModel DigestContextMenu
    {
        get => _digestContextMenu;
        set => this.RaiseAndSetIfChanged(ref _digestContextMenu, value);
    }

    /// <summary>
    /// Displays a manifest with syntax highlighting and clickable digests
    /// </summary>
    public void DisplayManifest(string manifestJson, string digest, string registryUrl, string repository)
    {
        ManifestContent = manifestJson;
        CurrentDigest = digest;

        // Update context menu
        UpdateDigestContextMenu(digest, registryUrl, repository);

        // Apply syntax highlighting if viewer is available
        if (ManifestViewer != null)
        {
            ApplySyntaxHighlighting(manifestJson, digest);
        }
    }

    /// <summary>
    /// Clears the manifest display
    /// </summary>
    public void Clear()
    {
        ManifestContent = string.Empty;
        CurrentDigest = string.Empty;
        
        if (ManifestViewer != null)
        {
            ManifestViewer.Content = null;
        }
    }

    /// <summary>
    /// Applies JSON syntax highlighting with clickable digest links
    /// </summary>
    private void ApplySyntaxHighlighting(string json, string digest)
    {
        if (ManifestViewer == null)
        {
            _logger.LogWarning("ManifestViewer is null, cannot apply syntax highlighting");
            return;
        }

        try
        {
            // HighlightJson creates and returns a new TextBlock with syntax highlighting
            var highlightedTextBlock = _jsonHighlightService.HighlightJson(json);
            ManifestViewer.Content = highlightedTextBlock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying JSON syntax highlighting");
        }
    }

    /// <summary>
    /// Handles clicks on digest references in the JSON
    /// </summary>
    private void HandleDigestClick(string digestReference)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Digest clicked in JSON viewer: {Digest}", digestReference);
        }

        // Request manifest load through ManifestService
        _manifestService.TryRequestLoad(
            digestReference, 
            LoadSource.ReferenceBox, 
            forceReload: false);
    }

    /// <summary>
    /// Updates the digest context menu with current manifest information
    /// </summary>
    private void UpdateDigestContextMenu(string digest, string registryUrl, string repository)
    {
        DigestContextMenu.Digest = digest;
        DigestContextMenu.RegistryUrl = registryUrl;
        DigestContextMenu.Repository = repository;
    }
}
