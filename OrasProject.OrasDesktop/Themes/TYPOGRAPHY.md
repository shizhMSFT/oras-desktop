# Typography System

This document describes the professional typography system implemented for ORAS Desktop.

## Design Philosophy

The typography system follows professional desktop application standards (similar to Visual Studio, VS Code, and Azure Portal) with:
- **Consistent font sizing** across all UI elements
- **Clear hierarchy** between different types of content
- **Professional appearance** with careful attention to detail
- **Accessibility** with readable font sizes

## Font Size Scale

| Element Type | Font Size | Usage |
|-------------|-----------|-------|
| Small text | 11pt | Status bar, metadata, secondary information |
| Default UI | 12pt | Standard buttons, textboxes, labels, list items |
| Headers | 13pt | Section headers (Repositories, Tags, Artifact Details) |
| Titles | 14pt | Dialog titles, main headings |

## Font Families

### UI Font (Default)
- **Standard text**: System default (Segoe UI on Windows, SF Pro on macOS)
- Used for: Labels, buttons, status text, tooltips

### Monospace Font
- **Technical text**: `Cascadia Code, Consolas, Menlo, Monaco, monospace`
- Used for: Digests, JSON, repository references, code snippets
- Size: **12pt** for consistency with UI

## CSS Classes

### Content Classes
- `.small` - 11pt for secondary information (Image size, status bar)
- `.label` - 12pt normal weight for form labels
- `.code` - 12pt monospace for technical text
- `.section-header` - 13pt semibold for section titles
- `.artifact-label` - 12pt semibold for artifact field labels
- `.artifact-value` - 12pt monospace for artifact field values
- `.platform-name` - 12pt monospace semibold for platform identifiers

### Container Classes
- `.status-bar` - Applies 11pt to all child TextBlocks
- `.reference-bar` - Applies 12pt + monospace to TextBox, 12pt to TextBlocks

## Implementation

The typography system is defined in `/Themes/Typography.axaml` and imported in `App.axaml`:

```xml
<StyleInclude Source="/Themes/Typography.axaml" />
```

### Global Defaults
All base controls (TextBox, TextBlock, Button, ListBox, TreeView, etc.) default to **12pt**.

### Specific Overrides
Use CSS classes to override for specific purposes:
- Status information: `Classes="small"`
- Technical text: `Classes="code"`
- Headers: `Classes="section-header"`

## Benefits

1. **Professional appearance** - Consistent sizing makes the app look polished
2. **Easier maintenance** - Change font sizes in one place
3. **Better readability** - Appropriate sizes for different content types
4. **Tooltip consistency** - All tooltips automatically use 12pt
5. **Scalability** - Easy to adjust the entire scale if needed

## Comparison to Before

### Before
- Repository items: 12pt (manually set)
- Tag items: 12pt (manually set)
- Image size: 11pt (manually set)
- Reference TextBox: 13pt (manually set)
- Status bar: default (14pt)
- Tooltips: default (variable)
- Artifact labels: 12pt (in App.axaml)
- Section headers: 13pt (in App.axaml)

### After
- **All UI elements**: 12pt (from Typography.axaml)
- **Status bar**: 11pt (secondary info)
- **Image size**: 11pt (metadata)
- **Reference TextBox**: 12pt (consistent)
- **Tooltips**: 12pt (professional)
- **All code/technical text**: 12pt monospace (unified)
- **Headers**: 13pt semibold (clear hierarchy)

## Future Enhancements

Consider adding:
- Font size preferences (small/medium/large)
- High DPI scaling adjustments
- Custom font family selection
- Accessibility options (minimum size enforcement)
