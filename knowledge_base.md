# Knowledge Base for ORAS Desktop Application

## ORAS (OCI Registry As Storage)

ORAS is a tool and library for working with OCI (Open Container Initiative) artifacts in OCI registries. It allows pushing and pulling artifacts to and from OCI registries like Docker Hub, GitHub Container Registry, and others.

### ORAS .NET SDK

The ORAS .NET SDK (`oras-dotnet`) provides .NET bindings for working with ORAS. It's available as a NuGet package: [OrasProject.Oras](https://www.nuget.org/packages/OrasProject.Oras/0.3.0).

Key features:
- Push and pull OCI artifacts
- List repositories and tags
- Manage manifests
- Copy artifacts between registries
### oras-dotnet (v0.3.0) Capability Summary & Intended Desktop Usage

The public APIs (summarized from README & examples) enable end‑to‑end artifact lifecycle:

| Capability | oras-dotnet Concept / Likely API | Intended UI Usage |
|------------|----------------------------------|-------------------|
| Auth | Client configuration with registry credentials (basic / anonymous). | Login dialog -> create a client instance per registry. |
| List Repositories | Registry catalog enumeration (e.g., `ListRepositoriesAsync` or similar). | Populate TreeView root nodes. |
| List Tags | Repository tag listing (e.g., `ListTagsAsync(repo)` with paging). | Populate tag ListBox when a repository selected. |
| Fetch Manifest | Get manifest descriptor + JSON (e.g., `GetManifestAsync(repo, reference)` returning bytes/stream + descriptor). | Display highlighted JSON; extract digests for click navigation. |
| Fetch Blob / Layer | `GetBlobAsync(repo, digest)` (stream). | When user clicks a digest referencing config/layer manifest, show its JSON (if JSON media type) else show metadata placeholder. |
| Copy Artifact | High-level copy API (example: copy artifact between repositories). | Copy button executes async copy to target registry/repo/tag. |
| Push Artifact/Image | High-level push APIs (image or generic artifact). | (Future) Potential upload feature; not in current MVP. |
| Delete Manifest | `DeleteAsync(repo, digest)` (common registry operation). | Delete button for selected manifest/tag (tag->resolve digest then delete). |
| Referrers | Attach or list referrers. | (Future) Could display supply chain provenance (not MVP). |

Assumptions (to verify when integrating actual package):
1. APIs are asynchronous and accept `CancellationToken`.
2. Listing supports pagination; we'll implement a simple eager fetch first, then refine if large.
3. Manifest retrieval returns raw JSON bytes; we will pretty-print and syntax highlight.
4. Media types follow OCI image & artifact spec; digest clickable detection is regex based: `sha256:[a-f0-9]{64}`.

Planned Wrapper Abstractions (Service Layer):
```
public interface IOrasRegistryService {
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

JSON Digest Extraction Strategy:
1. Parse JSON with `System.Text.Json` for fields named `digest`.
2. Additionally run regex for any stray digests only inside quoted strings to ensure highlight.

Syntax Highlighting Plan:
Use existing `JsonHighlightService` (if adaptable) or integrate a lightweight tokenization: parse JSON -> produce spans styled by token type (braces, property names, strings, numbers, punctuation). Digests rendered as Hyperlink (Button styled like text) to enable click.

Threading & Performance Notes:
* All network calls off UI thread (async/await). UI updates marshalled back via Avalonia dispatcher if needed.
* Large manifests (e.g., SBOM) handled streaming -> buffer to string; we monitor size and truncate display > 2 MB with prompt to open externally (future enhancement).

Error Handling Mapping:
| Error Case | User Feedback |
|------------|---------------|
| Auth failure | Show login dialog again with message "Authentication failed". |
| 404 repo/tag | InfoBar style message: "Not found". |
| Network timeout | Retry option (button) with exponential backoff (future). |
| Delete denied | Display registry response status. |

Security Considerations:
* Credentials only kept in-memory for session; no persistence (MVP).
* Avoid logging sensitive tokens.

Open Questions / To Validate During Implementation:
1. Exact method names & namespaces in `OrasProject.Oras` (will inspect after adding package).
2. Whether copy requires constructing descriptors manually or high-level copy exists (examples suggest high-level API). If absent, we will fallback to: pull manifest+blobs then push to target.
3. Delete semantics: confirm digest vs tag deletion (will resolve tag to digest first then delete digest).

Planned Next Step: Add package and inspect concrete types to finalize service implementation signatures.


## Avalonia UI

Avalonia UI is a cross-platform UI framework for .NET that allows building desktop applications that run on Windows, macOS, and Linux. It uses a XAML-based approach similar to WPF.

### Key Avalonia UI Components

1. **TreeView**: For displaying hierarchical data like repositories
   ```xml
   <TreeView Items="{Binding Items}">
     <TreeView.ItemTemplate>
       <TreeDataTemplate ItemsSource="{Binding SubItems}">
         <TextBlock Text="{Binding Name}"/>
       </TreeDataTemplate>
     </TreeView.ItemTemplate>
   </TreeView>
   ```

2. **ListBox**: For displaying a collection of items like tags
   ```xml
   <ListBox Items="{Binding Items}">
     <ListBox.ItemTemplate>
       <DataTemplate>
         <TextBlock Text="{Binding Name}"/>
       </DataTemplate>
     </ListBox.ItemTemplate>
   </ListBox>
   ```

3. **TextBlock with Inlines**: For displaying highlighted text
   ```xml
   <TextBlock>
     <Run Foreground="Blue">Highlighted</Run>
     <Run>Normal</Run>
   </TextBlock>
   ```

4. **Buttons**: For actions like delete and copy
   ```xml
   <Button Command="{Binding DeleteCommand}">Delete</Button>
   ```

5. **JSON Highlighting**: Using a specialized control or a custom solution with TextBlock and Run elements

### MVVM Pattern in Avalonia

Avalonia UI follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Represent the data and business logic
- **ViewModels**: Expose data and commands to the View
- **Views**: Define the UI structure and appearance

### ReactiveUI

Avalonia works well with ReactiveUI, which provides a reactive programming model for .NET applications.

```csharp
public class MainViewModel : ReactiveObject
{
    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }
    
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    
    public MainViewModel()
    {
        SearchCommand = ReactiveCommand.Create(() => Search());
    }
    
    private void Search()
    {
        // Implementation
    }
}
```

## OCI Registry API Structure

### Registry Structure

- Registry
  - Repository
    - Tag
      - Manifest
        - Layers
        - Config

### Authentication

OCI registries typically require authentication, which can be handled through:
- Basic authentication (username/password)
- Bearer tokens
- Docker credential helpers
 - Anonymous (no credentials)

Application Requirement: Support exactly three modes selectable by user:
1. Anonymous (default, send no Authorization header)
2. Basic (Base64 username:password)
3. Bearer (Authorization: Bearer <token>)

Validation Strategy:
* Perform a `GET /v2/` ping after initializing connection.
* If 401 with WWW-Authenticate for bearer and user selected bearer without token -> prompt.

### Manifest Format

OCI manifests are JSON documents that describe the content of an image or artifact.

Example manifest:
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

## Implementation Considerations

1. **Registry Authentication**: 
   - Support for various authentication methods
   - Secure storage of credentials

2. **Asynchronous Operations**:
   - Long-running operations should be asynchronous
   - Show progress indicators for operations

3. **Error Handling**:
   - Handle network errors
   - Handle authentication errors
   - Provide meaningful error messages to users

4. **UI Responsiveness**:
   - Keep UI responsive during operations
   - Use background threads for operations

5. **JSON Highlighting**:
   - Implement syntax highlighting for JSON
   - Make digest links clickable