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