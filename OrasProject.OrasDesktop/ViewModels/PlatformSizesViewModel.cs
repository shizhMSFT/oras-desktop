using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// ViewModel for displaying platform-specific image sizes
/// </summary>
public class PlatformSizesViewModel : ViewModelBase
{
    private readonly ArtifactService _artifactService;
    private readonly ILogger<PlatformSizesViewModel> _logger;
    
    private ObservableCollection<PlatformImageSize> _platformImageSizes = new();

    public PlatformSizesViewModel(
        ArtifactService artifactService,
        ILogger<PlatformSizesViewModel> logger)
    {
        _artifactService = artifactService;
        _logger = logger;

        // Subscribe to artifact size updates
        _artifactService.ArtifactSizeUpdated += OnArtifactSizeUpdated;
    }

    /// <summary>
    /// Collection of platform-specific image sizes
    /// </summary>
    public ObservableCollection<PlatformImageSize> PlatformImageSizes
    {
        get => _platformImageSizes;
        private set => this.RaiseAndSetIfChanged(ref _platformImageSizes, value);
    }

    /// <summary>
    /// Whether platform sizes are available to display
    /// </summary>
    public bool HasPlatformSizes => _artifactService.HasPlatformSizes;

    /// <summary>
    /// Summary of artifact size (total or multi-platform)
    /// </summary>
    public string ArtifactSizeSummary => _artifactService.ArtifactSizeSummary;

    /// <summary>
    /// Updates platform sizes from the artifact service
    /// </summary>
    public void UpdatePlatformSizes()
    {
        PlatformImageSizes.Clear();

        var sizes = _artifactService.PlatformImageSizes;
        if (sizes == null) return;

        foreach (var size in sizes)
        {
            PlatformImageSizes.Add(size);
        }

        this.RaisePropertyChanged(nameof(HasPlatformSizes));
        this.RaisePropertyChanged(nameof(ArtifactSizeSummary));
    }

    /// <summary>
    /// Clears all platform size data
    /// </summary>
    public void Clear()
    {
        PlatformImageSizes.Clear();
        this.RaisePropertyChanged(nameof(HasPlatformSizes));
        this.RaisePropertyChanged(nameof(ArtifactSizeSummary));
    }

    /// <summary>
    /// Handles artifact size updates from the service
    /// </summary>
    private void OnArtifactSizeUpdated(object? sender, EventArgs e)
    {
        UpdatePlatformSizes();
    }
}
