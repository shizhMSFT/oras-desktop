# Artifact Component Refactoring Specification

## Executive Summary

This refactoring extracts the artifact display functionality from `MainViewModel` into a dedicated component hierarchy. The goal is to achieve proper separation of concerns, making the codebase more maintainable and testable.

## Current Problems

1. **MainViewModel is too large** (~1576 lines) with too many responsibilities
2. **Tight coupling** between UI concerns, business logic, and service coordination
3. **Navigation logic in ViewModel** - `TryNavigateToReferenceAsync` should be in `ManifestLoadCoordinator`
4. **Mixed concerns** - referrer loading, platform calculations, JSON display, clipboard operations all in one class
5. **Hard to test** - monolithic ViewModel makes unit testing difficult

## Architecture Overview

### New Component Hierarchy

```
MainViewModel
├── ConnectionViewModel (existing)
├── RepositorySelectorViewModel (existing)
├── TagSelectorViewModel (existing)
├── ReferenceHistoryViewModel (existing)
├── StatusBarViewModel (existing)
└── ArtifactViewModel (NEW)
    ├── JsonViewerViewModel (NEW)
    ├── ReferrerViewModel (NEW)
    └── PlatformSizesViewModel (NEW)
```

### Dependency Flow

```
ArtifactViewModel
  → depends on: ArtifactService, ManifestService, StatusService, IRegistryService
  → contains: JsonViewerViewModel, ReferrerViewModel, PlatformSizesViewModel
  → exposes: Commands (Delete, Copy), Tab selection, Manifest display coordination

ReferrerViewModel
  → depends on: ArtifactService, IRegistryService, ILogger
  → manages: Referrer tree loading, selection, context menu
  
PlatformSizesViewModel
  → depends on: ArtifactService, ILogger
  → manages: Platform size calculations and display

JsonViewerViewModel
  → depends on: JsonHighlightService, ManifestService, ILogger
  → manages: JSON display, syntax highlighting, digest navigation
```

---

## Phase 1: Create New ViewModels

### 1.1 ArtifactViewModel

**File:** `OrasProject.OrasDesktop/ViewModels/ArtifactViewModel.cs`

**Responsibilities:**
- Coordinate artifact display (manifest JSON, referrers, platform sizes)
- Handle delete manifest operations
- Handle copy reference operations (tag and digest)
- Manage tab selection (Manifest vs Referrers)
- Expose commands for artifact actions
- Delegate to child ViewModels for specific concerns

**Properties:**
```csharp
// Child ViewModels
public JsonViewerViewModel JsonViewer { get; }
public ReferrerViewModel Referrer { get; }
public PlatformSizesViewModel PlatformSizes { get; }

// State
public int SelectedTabIndex { get; set; } // 0=Manifest, 1=Referrers
public bool CanModifyArtifact => _artifactService.CanDeleteManifest();

// Commands
public ReactiveCommand<Unit, Unit> DeleteManifestCommand { get; }
public ReactiveCommand<Unit, Unit> CopyReferenceWithTagCommand { get; }
public ReactiveCommand<Unit, Unit> CopyReferenceWithDigestCommand { get; }
public ReactiveCommand<Unit, Unit> ArtifactActionsCommand { get; }
public ReactiveCommand<PlatformImageSize, Unit> ViewPlatformManifestCommand { get; }
```

**Methods to move from MainViewModel:**
- `DeleteManifestAsync()` (line ~685)
- `CopyReferenceWithTagAsync()` (line ~762)
- `CopyReferenceWithDigestAsync()` (line ~804)
- `ViewPlatformManifestAsync()` (line ~1111)
- Event handlers for manifest changes

**Dependencies:**
- `ArtifactService` - for current artifact state
- `ManifestService` - for loading manifests
- `StatusService` - for status updates
- `IRegistryService` - for delete operations
- `ILogger<ArtifactViewModel>`

**Constructor:**
```csharp
public ArtifactViewModel(
    ArtifactService artifactService,
    ManifestService manifestService,
    StatusService statusService,
    IRegistryService registryService,
    JsonHighlightService jsonHighlightService,
    ILogger<ArtifactViewModel> logger,
    ILoggerFactory loggerFactory)
{
    _artifactService = artifactService;
    _manifestService = manifestService;
    _statusService = statusService;
    _registryService = registryService;
    _logger = logger;
    
    // Create child ViewModels
    JsonViewer = new JsonViewerViewModel(jsonHighlightService, manifestService, loggerFactory.CreateLogger<JsonViewerViewModel>());
    Referrer = new ReferrerViewModel(artifactService, registryService, loggerFactory.CreateLogger<ReferrerViewModel>());
    PlatformSizes = new PlatformSizesViewModel(artifactService, loggerFactory.CreateLogger<PlatformSizesViewModel>());
    
    // Create observable for commands
    var canDeleteObservable = Observable.Create<bool>(observer => { /* ... */ });
    var canCopyTagObservable = Observable.Create<bool>(observer => { /* ... */ });
    
    // Initialize commands
    DeleteManifestCommand = ReactiveCommand.CreateFromTask(DeleteManifestAsync, canDeleteObservable);
    CopyReferenceWithTagCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithTagAsync, canCopyTagObservable);
    CopyReferenceWithDigestCommand = ReactiveCommand.CreateFromTask(CopyReferenceWithDigestAsync, canDeleteObservable);
    ArtifactActionsCommand = ReactiveCommand.Create(() => { }, canDeleteObservable);
    ViewPlatformManifestCommand = ReactiveCommand.CreateFromTask<PlatformImageSize>(ViewPlatformManifestAsync);
    
    // Wire up events
    _artifactService.ManifestChanged += OnManifestChanged;
}
```

---

### 1.2 ReferrerViewModel

**File:** `OrasProject.OrasDesktop/ViewModels/ReferrerViewModel.cs`

**Responsibilities:**
- Load and manage referrer tree
- Handle referrer node selection
- Manage referrer context menu
- Track loading state

**Properties:**
```csharp
public ObservableCollection<ReferrerNode> Referrers { get; }
public ReferrerNode? SelectedReferrerNode { get; set; }
public bool IsLoading { get; set; }
public ReferrerNodeContextMenuViewModel ContextMenu { get; }
```

**Methods to move from MainViewModel:**
- `LoadReferrersAsync()` (line ~902)
- `UpdateReferrerNodeContextMenu()` (line ~1293)
- `OnSelectedReferrerNodeChanged()` logic

**Dependencies:**
- `ArtifactService` - for current manifest/repository state
- `IRegistryService` - for loading referrers
- `ILogger<ReferrerViewModel>`

**Key Logic:**
```csharp
public async Task LoadReferrersAsync()
{
    if (_artifactService.CurrentManifest == null || 
        _artifactService.CurrentRepository == null || 
        _artifactService.CurrentRegistry == null)
    {
        Referrers.Clear();
        return;
    }
    
    IsLoading = true;
    try
    {
        var registry = _artifactService.CurrentRegistry;
        var repository = _artifactService.CurrentRepository;
        var manifest = _artifactService.CurrentManifest;
        
        var rootDigest = manifest.Digest;
        var repositoryPath = repository.FullPath.Replace($"{registry.Url}/", string.Empty);
        
        // Use IRegistryService.GetReferrersRecursiveAsync
        var referrers = await _registryService.GetReferrersRecursiveAsync(
            repositoryPath, 
            rootDigest, 
            progress: null, 
            cancellationToken: default);
            
        Referrers.Clear();
        foreach (var referrer in referrers)
        {
            Referrers.Add(referrer);
        }
    }
    finally
    {
        IsLoading = false;
    }
}
```

---

### 1.3 PlatformSizesViewModel

**File:** `OrasProject.OrasDesktop/ViewModels/PlatformSizesViewModel.cs`

**Responsibilities:**
- Calculate platform-specific sizes
- Expose platform size collection
- Provide summary information

**Properties:**
```csharp
public ObservableCollection<PlatformImageSize> PlatformImageSizes { get; }
public bool HasPlatformSizes => PlatformImageSizes.Count > 0;
public string ArtifactSizeSummary => _artifactService.GetArtifactSizeDisplay();
```

**Methods to move from MainViewModel:**
- Logic from `ArtifactSizeSummary` property getter
- Logic from `PlatformImageSizes` property getter
- Logic from `HasPlatformSizes` property getter

**Dependencies:**
- `ArtifactService` - for platform size data via `ArtifactSizeUpdated` event
- `ILogger<PlatformSizesViewModel>`

**Key Logic:**
```csharp
public PlatformSizesViewModel(ArtifactService artifactService, ILogger<PlatformSizesViewModel> logger)
{
    _artifactService = artifactService;
    _logger = logger;
    
    PlatformImageSizes = new ObservableCollection<PlatformImageSize>();
    
    // Subscribe to size updates
    _artifactService.ArtifactSizeUpdated += (s, e) =>
    {
        UpdatePlatformSizes();
        this.RaisePropertyChanged(nameof(ArtifactSizeSummary));
        this.RaisePropertyChanged(nameof(HasPlatformSizes));
    };
}

private void UpdatePlatformSizes()
{
    PlatformImageSizes.Clear();
    
    var sizes = _artifactService.GetPlatformImageSizes();
    foreach (var size in sizes)
    {
        PlatformImageSizes.Add(size);
    }
}
```

---

### 1.4 JsonViewerViewModel

**File:** `OrasProject.OrasDesktop/ViewModels/JsonViewerViewModel.cs`

**Responsibilities:**
- Display manifest JSON with syntax highlighting
- Handle digest clicks for navigation
- Manage TextBlock reference for inline display

**Properties:**
```csharp
public TextBlock? ManifestViewer { get; set; }
public string ManifestContent { get; private set; }
```

**Methods to move from MainViewModel:**
- `DisplayManifestAsync()` logic (currently embedded in OnManifestChanged)
- Digest click handling logic

**Dependencies:**
- `JsonHighlightService` - for syntax highlighting
- `ManifestService` - for requesting manifest loads on digest click
- `ILogger<JsonViewerViewModel>`

**Key Logic:**
```csharp
public void DisplayManifest(string manifestJson, string digest)
{
    ManifestContent = manifestJson;
    
    if (ManifestViewer == null) return;
    
    // Use JsonHighlightService to create highlighted inlines with clickable digests
    _jsonHighlightService.ApplyJsonHighlighting(
        ManifestViewer,
        manifestJson,
        digest,
        onDigestClick: HandleDigestClick);
}

private void HandleDigestClick(string digestReference)
{
    // Request manifest load through ManifestService
    _manifestService.RequestLoad(digestReference, Services.LoadSource.ReferenceBox, forceReload: false);
}
```

---

## Phase 2: Move Navigation Logic to ManifestLoadCoordinator

### 2.1 Add TryNavigateToReferenceAsync to ManifestLoadCoordinator

**File:** `OrasProject.OrasDesktop/Services/ManifestLoadCoordinator.cs`

**Why this belongs here:**
- ManifestLoadCoordinator already handles repository/tag coordination
- Navigation is about coordinating repository and tag selection
- Reduces MainViewModel responsibility

**Method signature:**
```csharp
/// <summary>
/// Attempts to navigate to a reference by parsing it and selecting the repository and tag in the UI
/// </summary>
public async Task<bool> TryNavigateToReferenceAsync(string reference, bool selectTag = true)
{
    // Parse reference into registry/repository/tag
    // Fire RepositorySelectionRequested event
    // Fire TagSelectionRequested event (if selectTag && !isDigest)
    // Return true if successful, false otherwise
}
```

**Move from:** `MainViewModel.TryNavigateToReferenceAsync` (line ~1188)

**Changes needed:**
- Add events if not already present (they are)
- Call from MainViewModel event handlers instead of direct implementation

---

## Phase 3: Update MainViewModel

### 3.1 Remove from MainViewModel

**Lines to remove/refactor:**

1. **Properties (lines ~38-50):**
   - `_manifestViewer` → move to JsonViewerViewModel
   - `_manifestContent` → move to JsonViewerViewModel
   - `_referrers` → move to ReferrerViewModel
   - `_referrersLoading` → move to ReferrerViewModel
   - `_digestContextMenu` → move to JsonViewerViewModel
   - `_referrerNodeContextMenu` → move to ReferrerViewModel
   - `_selectedTabIndex` → move to ArtifactViewModel

2. **Commands (lines ~59-66):**
   - `DeleteManifestCommand` → move to ArtifactViewModel
   - `CopyReferenceCommand` → keep (used in other places)
   - `CopyReferenceWithTagCommand` → move to ArtifactViewModel
   - `CopyReferenceWithDigestCommand` → move to ArtifactViewModel
   - `ArtifactActionsCommand` → move to ArtifactViewModel
   - `ViewPlatformManifestCommand` → move to ArtifactViewModel

3. **Observable creation (lines ~118-185):**
   - `canDeleteObservable` → move to ArtifactViewModel
   - `canCopyTagObservable` → move to ArtifactViewModel
   - Command initialization → move to ArtifactViewModel

4. **Properties (lines ~300-450):**
   - `ManifestViewer` → remove, access via `Artifact.JsonViewer.ManifestViewer`
   - `SelectedTabIndex` → remove, access via `Artifact.SelectedTabIndex`
   - `Referrers` → remove, access via `Artifact.Referrer.Referrers`
   - `ReferrersLoading` → remove, access via `Artifact.Referrer.IsLoading`
   - `DigestContextMenu` → remove, access via `Artifact.JsonViewer.ContextMenu`
   - `ReferrerNodeContextMenu` → remove, access via `Artifact.Referrer.ContextMenu`
   - `SelectedReferrerNode` → remove, access via `Artifact.Referrer.SelectedReferrerNode`
   - `ArtifactSizeSummary` → remove, access via `Artifact.PlatformSizes.ArtifactSizeSummary`
   - `PlatformImageSizes` → remove, access via `Artifact.PlatformSizes.PlatformImageSizes`
   - `HasPlatformSizes` → remove, access via `Artifact.PlatformSizes.HasPlatformSizes`
   - `CanModifySelectedTag` → keep (used for other operations)

5. **Methods (lines ~685-850):**
   - `DeleteManifestAsync()` → move to ArtifactViewModel
   - `CopyReferenceToClipboardAsync()` → keep (used elsewhere)
   - `CopyReferenceWithTagAsync()` → move to ArtifactViewModel
   - `CopyReferenceWithDigestAsync()` → move to ArtifactViewModel

6. **Methods (lines ~850-1100):**
   - `LoadReferrersAsync()` → move to ReferrerViewModel
   - All referrer-related helper methods

7. **Methods (lines ~1111-1180):**
   - `ViewPlatformManifestAsync()` → move to ArtifactViewModel

8. **Methods (lines ~1188-1280):**
   - `TryNavigateToReferenceAsync()` → move to ManifestLoadCoordinator

9. **Methods (lines ~1293-1310):**
   - `UpdateReferrerNodeContextMenu()` → move to ReferrerViewModel

### 3.2 Add to MainViewModel

**New property:**
```csharp
public ArtifactViewModel Artifact { get; }
```

**In constructor:**
```csharp
// Initialize ArtifactViewModel
Artifact = new ArtifactViewModel(
    _artifactService,
    _manifestService,
    _statusService,
    _registryService,
    _jsonHighlightService,
    loggerFactory.CreateLogger<ArtifactViewModel>(),
    loggerFactory);
```

**Update event handlers:**
```csharp
// Where we currently call LoadReferrersAsync()
await Artifact.Referrer.LoadReferrersAsync();

// Where we currently call UpdateReferrerNodeContextMenu()
Artifact.Referrer.UpdateContextMenu();

// Where we navigate
await _manifestLoadCoordinator.TryNavigateToReferenceAsync(reference);
```

### 3.3 Simplified MainViewModel Responsibilities

After refactoring, MainViewModel should only:
1. Coordinate high-level application flow
2. Manage login/connection
3. Wire up component ViewModels
4. Handle top-level events
5. Manage reference history coordination
6. Delegate to specialized ViewModels for specific concerns

---

## Phase 4: Create Views

### 4.1 Create ArtifactView.axaml

**File:** `OrasProject.OrasDesktop/Views/ArtifactView.axaml`

**Structure:**
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:OrasProject.OrasDesktop.ViewModels"
             x:Class="OrasProject.OrasDesktop.Views.ArtifactView"
             x:DataType="vm:ArtifactViewModel">
  
  <Grid RowDefinitions="Auto,*">
    
    <!-- Header with title and actions button -->
    <Grid Grid.Row="0" ColumnDefinitions="*,Auto">
      <TextBlock Text="Artifact Details" Classes="section-header" />
      
      <Button Grid.Column="1" 
              Command="{Binding ArtifactActionsCommand}"
              ToolTip.Tip="Artifact actions">
        <Button.Flyout>
          <MenuFlyout>
            <MenuItem Header="Copy fully qualified tag reference" 
                      Command="{Binding CopyReferenceWithTagCommand}" />
            <MenuItem Header="Copy fully qualified digest reference" 
                      Command="{Binding CopyReferenceWithDigestCommand}" />
            <Separator />
            <MenuItem Header="Delete Manifest" 
                      Command="{Binding DeleteManifestCommand}" />
          </MenuFlyout>
        </Button.Flyout>
        <!-- ... button content ... -->
      </Button>
    </Grid>
    
    <!-- Tab Control -->
    <TabControl Grid.Row="1" SelectedIndex="{Binding SelectedTabIndex}">
      
      <!-- Manifest Tab -->
      <TabItem Header="Manifest">
        <Grid RowDefinitions="Auto,Auto,*">
          
          <!-- Digest display -->
          <StackPanel Grid.Row="0">
            <TextBlock Text="Digest:" />
            <TextBlock Text="{Binding JsonViewer.CurrentDigest}" />
          </StackPanel>
          
          <!-- Platform sizes -->
          <Grid Grid.Row="1" IsVisible="{Binding PlatformSizes.HasPlatformSizes}">
            <ItemsControl ItemsSource="{Binding PlatformSizes.PlatformImageSizes}">
              <!-- ... platform size template ... -->
            </ItemsControl>
          </Grid>
          
          <!-- JSON Viewer -->
          <ScrollViewer Grid.Row="2">
            <TextBlock Name="ManifestViewerTextBlock" 
                       TextWrapping="Wrap"
                       FontFamily="Consolas,Courier New,monospace" />
          </ScrollViewer>
          
        </Grid>
      </TabItem>
      
      <!-- Referrers Tab -->
      <TabItem Header="Referrers">
        <Grid RowDefinitions="Auto,*">
          
          <ProgressBar Grid.Row="0" 
                       IsVisible="{Binding Referrer.IsLoading}"
                       IsIndeterminate="True" />
          
          <TreeView Grid.Row="1"
                    ItemsSource="{Binding Referrer.Referrers}"
                    SelectedItem="{Binding Referrer.SelectedReferrerNode}">
            <!-- ... referrer tree template ... -->
          </TreeView>
          
        </Grid>
      </TabItem>
      
    </TabControl>
    
  </Grid>
  
</UserControl>
```

**Code-behind:**
```csharp
public partial class ArtifactView : UserControl
{
    public ArtifactView()
    {
        InitializeComponent();
        
        this.WhenActivated(disposables =>
        {
            if (DataContext is ArtifactViewModel vm)
            {
                // Wire up TextBlock reference
                vm.JsonViewer.ManifestViewer = this.FindControl<TextBlock>("ManifestViewerTextBlock");
            }
        });
    }
}
```

### 4.2 Update MainView.axaml

**File:** `OrasProject.OrasDesktop/Views/MainView.axaml`

**Change:**
Replace the entire artifact details Grid (currently lines ~68-250+) with:

```xml
<!-- Artifact Display Pane -->
<Border Grid.Column="2" Classes="pane">
  <views:ArtifactView DataContext="{Binding Artifact}" />
</Border>
```

**Add namespace:**
```xml
xmlns:views="using:OrasProject.OrasDesktop.Views"
```

---

## Phase 5: Service Injection Updates

### 5.1 Update ServiceLocator or DI Container

**File:** `OrasProject.OrasDesktop/ServiceLocator.cs` (or wherever DI is configured)

**Add registrations:**
```csharp
services.AddTransient<ArtifactViewModel>();
services.AddTransient<ReferrerViewModel>();
services.AddTransient<PlatformSizesViewModel>();
services.AddTransient<JsonViewerViewModel>();
```

---

## Migration Strategy

### Step-by-step execution order:

1. ✅ **Create JsonViewerViewModel** - smallest, no dependencies on other new VMs
2. ✅ **Create PlatformSizesViewModel** - small, independent
3. ✅ **Create ReferrerViewModel** - medium, independent
4. ✅ **Create ArtifactViewModel** - depends on above 3
5. ✅ **Move TryNavigateToReference** to ManifestLoadCoordinator
6. ✅ **Create ArtifactView.axaml** + code-behind
7. ✅ **Update MainViewModel** - remove old code, add Artifact property
8. ✅ **Update MainView.axaml** - replace artifact pane
9. ✅ **Test and fix** any binding issues

### Testing checkpoints:

After each phase:
- ✅ Code compiles
- ✅ Application launches
- ✅ Can connect to registry
- ✅ Can load manifest
- ✅ Can view referrers
- ✅ Can delete manifest
- ✅ Can copy references
- ✅ Platform sizes display correctly

---

## Risks and Mitigations

### Risk 1: Breaking existing bindings
**Mitigation:** Update all XAML bindings systematically, test after each change

### Risk 2: Event subscription leaks
**Mitigation:** Ensure all ViewModels implement IDisposable and unsubscribe from events

### Risk 3: Circular dependencies
**Mitigation:** Keep dependency flow unidirectional (services → ViewModels → Views)

### Risk 4: Loss of functionality
**Mitigation:** Create comprehensive checklist of all features, test each after refactoring

---

## Success Criteria

✅ MainViewModel reduced from ~1576 lines to ~800 lines  
✅ Each ViewModel has single, clear responsibility  
✅ No business logic in Views  
✅ All tests pass  
✅ No regression in functionality  
✅ Code is more maintainable and testable  

---

## Questions for Review

1. **Scope**: Should we also refactor ConnectionViewModel and other components in this pass?
2. **Naming**: Are the ViewModel names clear and consistent? (`ArtifactViewModel`, `ReferrerViewModel`, etc.)
3. **Events**: Should ViewModels expose events or use ReactiveUI messaging?
4. **DI**: Should we use constructor injection for all dependencies or property injection for optional ones?
5. **Testing**: Should we write unit tests as part of this refactoring or after?

---

## Timeline Estimate

- Phase 1 (Create ViewModels): 2-3 hours
- Phase 2 (Move navigation): 30 minutes
- Phase 3 (Update MainViewModel): 1-2 hours
- Phase 4 (Create Views): 1 hour
- Phase 5 (Testing & fixes): 1-2 hours

**Total: 6-9 hours** of focused development time

---

*Document Version: 1.0*  
*Last Updated: 2025-10-20*  
*Status: Awaiting approval to proceed*
