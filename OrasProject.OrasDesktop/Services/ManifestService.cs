using System;
using System.Threading.Tasks;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Centralized manifest service that prevents circular dependencies.
/// Both tag selection and reference box can request loads independently,
/// and components subscribe to load events to update their state.
/// </summary>
public class ManifestService
{
    private string _lastLoadedReference = string.Empty;
    private LoadSource _currentLoadSource = LoadSource.ReferenceBox;
    private bool _currentReferenceIsDigest = false;
    
    /// <summary>
    /// Event raised when a manifest load is requested
    /// </summary>
    public event EventHandler<ManifestLoadRequestedEventArgs>? LoadRequested;
    
    /// <summary>
    /// Event raised when a manifest has been successfully loaded
    /// </summary>
    public event EventHandler<ManifestLoadedEventArgs>? LoadCompleted;
    
    /// <summary>
    /// Gets the current load source (the source of the active or most recent load)
    /// </summary>
    public LoadSource CurrentLoadSource => _currentLoadSource;
    
    /// <summary>
    /// Requests a manifest load via the LoadRequested event.
    /// Includes circuit breaker to prevent duplicate loads of the same reference.
    /// </summary>
    /// <param name="reference">The full reference to load (e.g., registry/repo:tag)</param>
    /// <param name="source">The source of the request (Tag, ReferenceBox, History)</param>
    /// <param name="forceReload">If true, bypasses the circuit breaker</param>
    /// <returns>True if load was requested, false if blocked by circuit breaker</returns>
    public bool TryRequestLoad(string reference, LoadSource source, bool forceReload = false)
    {
        System.Diagnostics.Debug.WriteLine($"[ManifestLoader] TryRequestLoad called with reference: {reference}, source: {source}, forceReload: {forceReload}");
        
        if (string.IsNullOrWhiteSpace(reference))
        {
            System.Diagnostics.Debug.WriteLine($"[ManifestLoader] TryRequestLoad rejected - reference is null or whitespace");
            return false;
        }
        
        // Circuit breaker: prevent reload of same reference unless forced
        if (!forceReload && _lastLoadedReference == reference)
        {
            System.Diagnostics.Debug.WriteLine($"[ManifestLoader] TryRequestLoad circuit-broken - same reference was just loaded: {reference}");
            return false;
        }
        
        // Store the source and check if this is a digest reference
        _currentLoadSource = source;
        _currentReferenceIsDigest = reference.Contains("@sha256:") || reference.Contains("@sha512:");
        
        System.Diagnostics.Debug.WriteLine($"[ManifestLoader] Firing LoadRequested event for reference: {reference}, source: {source}");
        // Raise the load requested event
        LoadRequested?.Invoke(this, new ManifestLoadRequestedEventArgs(reference, source));
        
        return true;
    }
    
    /// <summary>
    /// Notifies that a manifest has been successfully loaded
    /// </summary>
    public void NotifyLoadCompleted(string reference, LoadSource source)
    {
        _lastLoadedReference = reference;
        _currentLoadSource = source;
        _currentReferenceIsDigest = reference.Contains("@sha256:") || reference.Contains("@sha512:");
        LoadCompleted?.Invoke(this, new ManifestLoadedEventArgs(reference, source));
    }
    
    /// <summary>
    /// Gets whether tag auto-selection should be suppressed.
    /// This is true when:
    /// 1. Loading from history (to prevent jumping back to previously selected tag)
    /// 2. Loading a digest reference (digests don't have corresponding tags to select)
    /// </summary>
    public bool ShouldSuppressTagAutoSelection => 
        _currentLoadSource == LoadSource.History || _currentReferenceIsDigest;
    
    /// <summary>
    /// Clears the circuit breaker to allow reloading the same reference
    /// </summary>
    public void ClearCircuitBreaker()
    {
        _lastLoadedReference = string.Empty;
    }
}

/// <summary>
/// Source of the manifest load request
/// </summary>
public enum LoadSource
{
    TagSelection,
    ReferenceBox,
    History
}

public class ManifestLoadRequestedEventArgs : EventArgs
{
    public string Reference { get; }
    public LoadSource Source { get; }
    
    public ManifestLoadRequestedEventArgs(string reference, LoadSource source)
    {
        Reference = reference;
        Source = source;
    }
}

public class ManifestLoadedEventArgs : EventArgs
{
    public string Reference { get; }
    public LoadSource Source { get; }
    
    public ManifestLoadedEventArgs(string reference, LoadSource source)
    {
        Reference = reference;
        Source = source;
    }
}
