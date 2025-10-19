using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using OrasProject.OrasDesktop.Services;
using ReactiveUI;

namespace OrasProject.OrasDesktop.ViewModels;

/// <summary>
/// Context menu view model for referrer tree nodes.
/// Dynamically provides appropriate context menu based on node type (digest or annotation).
/// </summary>
public class ReferrerNodeContextMenuViewModel : ViewModelBase
{
    private ReferrerNode? _node;
    private string? _registryUrl;
    private string? _repository;
    private DigestContextMenuViewModel? _digestContextMenu;
    private AnnotationContextMenuViewModel? _annotationContextMenu;
    private ArtifactTypeContextMenuViewModel? _artifactTypeContextMenu;

    public ReferrerNodeContextMenuViewModel()
    {
        CopyCommand = ReactiveCommand.CreateFromTask(ExecuteCopyAsync);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CopyCommand { get; }
    
    /// <summary>
    /// Event raised when a manifest is requested from a referrer node's context menu
    /// </summary>
    public event EventHandler<string>? ManifestRequested;

    public ReferrerNode? Node
    {
        get => _node;
        set
        {
            this.RaiseAndSetIfChanged(ref _node, value);
            UpdateContextMenus();
        }
    }

    public string? RegistryUrl
    {
        get => _registryUrl;
        set
        {
            this.RaiseAndSetIfChanged(ref _registryUrl, value);
            UpdateContextMenus();
        }
    }

    public string? Repository
    {
        get => _repository;
        set
        {
            this.RaiseAndSetIfChanged(ref _repository, value);
            UpdateContextMenus();
        }
    }

    public DigestContextMenuViewModel? DigestContextMenu
    {
        get => _digestContextMenu;
        private set => this.RaiseAndSetIfChanged(ref _digestContextMenu, value);
    }

    public AnnotationContextMenuViewModel? AnnotationContextMenu
    {
        get => _annotationContextMenu;
        private set => this.RaiseAndSetIfChanged(ref _annotationContextMenu, value);
    }

    public ArtifactTypeContextMenuViewModel? ArtifactTypeContextMenu
    {
        get => _artifactTypeContextMenu;
        private set => this.RaiseAndSetIfChanged(ref _artifactTypeContextMenu, value);
    }

    public bool IsDigestNode => Node != null && !Node.IsGroup && Node.Info != null && !string.IsNullOrEmpty(Node.Info.Digest);
    public bool IsAnnotationNode => Node != null && !Node.IsGroup && Node.Display.Contains(':') && (Node.Info == null || string.IsNullOrEmpty(Node.Info.Digest));
    public bool IsArtifactTypeNode => Node != null && Node.IsGroup && !string.IsNullOrEmpty(Node.Display);

    private void UpdateContextMenus()
    {
        if (Node == null)
        {
            DigestContextMenu = null;
            AnnotationContextMenu = null;
            ArtifactTypeContextMenu = null;
            return;
        }

        // Check if this is a digest node (referrer with Info)
        if (!Node.IsGroup && Node.Info != null && !string.IsNullOrEmpty(Node.Info.Digest))
        {
            var digestContextMenu = new DigestContextMenuViewModel(
                Node.Info.Digest,
                RegistryUrl,
                Repository
            );
            
            // Subscribe to ManifestRequested event and bubble it up
            digestContextMenu.ManifestRequested += (sender, reference) =>
            {
                ManifestRequested?.Invoke(this, reference);
            };
            
            DigestContextMenu = digestContextMenu;
            AnnotationContextMenu = null;
            ArtifactTypeContextMenu = null;
            this.RaisePropertyChanged(nameof(IsDigestNode));
            this.RaisePropertyChanged(nameof(IsAnnotationNode));
            this.RaisePropertyChanged(nameof(IsArtifactTypeNode));
            return;
        }

        // Check if this is an artifact type group node
        if (Node.IsGroup && !string.IsNullOrEmpty(Node.Display))
        {
            ArtifactTypeContextMenu = new ArtifactTypeContextMenuViewModel(Node.Display);
            DigestContextMenu = null;
            AnnotationContextMenu = null;
            this.RaisePropertyChanged(nameof(IsDigestNode));
            this.RaisePropertyChanged(nameof(IsAnnotationNode));
            this.RaisePropertyChanged(nameof(IsArtifactTypeNode));
            return;
        }

        // Check if this is an annotation node (contains key:value format)
        if (!Node.IsGroup && Node.Display.Contains(':'))
        {
            var parts = Node.Display.Split(':', 2);
            if (parts.Length == 2)
            {
                AnnotationContextMenu = new AnnotationContextMenuViewModel(
                    parts[0].Trim(),
                    parts[1].Trim()
                );
                DigestContextMenu = null;
                ArtifactTypeContextMenu = null;
                this.RaisePropertyChanged(nameof(IsDigestNode));
                this.RaisePropertyChanged(nameof(IsAnnotationNode));
                this.RaisePropertyChanged(nameof(IsArtifactTypeNode));
                return;
            }
        }

        // Not a supported node type
        DigestContextMenu = null;
        AnnotationContextMenu = null;
        ArtifactTypeContextMenu = null;
        this.RaisePropertyChanged(nameof(IsDigestNode));
        this.RaisePropertyChanged(nameof(IsAnnotationNode));
        this.RaisePropertyChanged(nameof(IsArtifactTypeNode));
    }

    private Task ExecuteCopyAsync()
    {
        // Execute the appropriate copy command based on node type
        if (IsArtifactTypeNode && ArtifactTypeContextMenu != null)
        {
            ArtifactTypeContextMenu.CopyArtifactTypeCommand.Execute().Subscribe(
                onNext: _ => { },
                onError: ex => { }
            );
        }
        else if (IsDigestNode && DigestContextMenu != null)
        {
            DigestContextMenu.CopyDigestCommand.Execute().Subscribe(
                onNext: _ => { },
                onError: ex => { }
            );
        }
        else if (IsAnnotationNode && AnnotationContextMenu != null)
        {
            AnnotationContextMenu.CopyBothCommand.Execute().Subscribe(
                onNext: _ => { },
                onError: ex => { }
            );
        }
        return Task.CompletedTask;
    }
}
