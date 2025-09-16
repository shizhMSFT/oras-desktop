# GitHub Copilot Context for ORAS Desktop Application

This document provides essential context to GitHub Copilot for generating code suggestions aligned with the ORAS Desktop application architecture and patterns.

## ORAS (OCI Registry As Storage)

ORAS is a tool for working with OCI artifacts in registries like Docker Hub and GitHub Container Registry.

### ORAS .NET SDK

The [OrasProject.Oras](https://www.nuget.org/packages/OrasProject.Oras/0.3.0) NuGet package provides .NET bindings with key features:
- Push/pull OCI artifacts
- Repository/tag management
- Manifest operations
- Cross-registry artifact copying

### Core Capabilities & UI Implementation

| Capability | API Concept | UI Implementation |
|------------|-------------|-------------------|
| Authentication | Client config with credentials | Login dialog → client instance |
| Repository Listing | `ListRepositoriesAsync` | TreeView root nodes |
| Tag Listing | `ListTagsAsync(repo)` | ListBox when repo selected |
| Manifest Retrieval | `GetManifestAsync(repo, ref)` | JSON display with clickable digests |
| Blob/Layer Access | `GetBlobAsync(repo, digest)` | JSON display or metadata |
| Artifact Copy | High-level copy API | Copy button with target selection |
| Manifest Deletion | `DeleteAsync(repo, digest)` | Delete button for selected item |
| Referrers API | Attach/list referrers | Future supply chain visualization |

### Service Layer Design

```csharp
public interface IRegistryService {
  Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken ct);
  Task<IReadOnlyList<string>> ListTagsAsync(string repository, CancellationToken ct);
  Task<ManifestResult> GetManifestByTagAsync(string repository, string tag, CancellationToken ct);
  Task<ManifestResult> GetManifestByDigestAsync(string repository, string digest, CancellationToken ct);
  Task DeleteManifestAsync(string repository, string digest, CancellationToken ct);
  Task CopyAsync(CopyRequest request, IProgress<CopyProgress>? progress, CancellationToken ct);
}

public record ManifestResult(string Digest, string MediaType, string Json, IReadOnlyList<string> ReferencedDigests);
public record CopyRequest(string SourceRepository, string Reference, string TargetRepository, string? TargetTag);
public record CopyProgress(string Stage, long? Completed, long? Total);
```

### Technical Implementation Notes

- **JSON Processing**: Parse with `System.Text.Json` plus regex for digest detection (`sha256:[a-f0-9]{64}`)
- **UI Threading**: Async network calls with proper dispatcher marshalling
- **Error Handling**: Mapped responses (auth failures → login prompt, 404 → "Not found" message)
- **Security**: In-memory credential storage only, no persistence in MVP
- **Performance**: Stream large manifests, truncate display >2MB
- **Code Cleanliness**: Removed unused `using` directives and legacy code that was replaced by the ORAS .NET SDK

## Avalonia UI Framework

Cross-platform UI framework for .NET applications on Windows, macOS, and Linux with XAML-based approach.

### Key UI Components

1. **TreeView**: For repository hierarchy
   ```xml
   <TreeView Items="{Binding Items}">
     <TreeView.ItemTemplate>
       <TreeDataTemplate ItemsSource="{Binding SubItems}">
         <TextBlock Text="{Binding Name}"/>
       </TreeDataTemplate>
     </TreeView.ItemTemplate>
   </TreeView>
   ```

2. **ListBox**: For tag collections
3. **TextBlock with Inlines**: For highlighted text
4. **Buttons**: For user actions
5. **JSON Highlighting**: Using `JsonHighlightService` with clickable digest links

### MVVM Architecture

- **Models**: Data and business logic
- **ViewModels**: Data and commands exposed to views
- **Views**: UI structure and appearance
- **ReactiveUI**: Reactive programming model integration

## OCI Registry Concepts

### Registry Structure
- Registry → Repository → Tag → Manifest → Layers/Config

### Authentication Methods
- Anonymous (default)
- Basic auth (username/password)
- Bearer tokens

### Manifest Format
```json
{
  "schemaVersion": 2,
  "mediaType": "application/vnd.oci.image.manifest.v1+json",
  "config": {
    "mediaType": "application/vnd.oci.image.config.v1+json",
    "size": 7023,
    "digest": "sha256:b5b2b2c507a0944348e0303114d8d93aaaa081732b86451d9bce1f432a537bc7"
  },
  "layers": [
    {
      "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
      "size": 32654,
      "digest": "sha256:9834876dcfb05cb167a5c24953eba58c4ac89b1adf57f28f2f9d09af107ee8f0"
    }
  ]
}
```

## Implementation Best Practices

1. **Asynchronous Operations**: All network calls off UI thread
2. **Error Handling**: Comprehensive error mapping with user-friendly messages
3. **UI Responsiveness**: Background processing for long operations
4. **JSON Visualization**: Syntax highlighting with clickable digest references