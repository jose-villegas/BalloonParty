# Editor UI Library

Reusable IMGUI building blocks for Unity editor windows. Eliminates repeated
charting, table, icon, and style-caching patterns across editor tooling.

## Quick Start

This is an embedded UPM package at `Packages/com.balloonparty.editorui/`.
Unity discovers it automatically — no manifest edit needed for local development.

To consume from another project, add to `Packages/manifest.json`:

```json
"com.balloonparty.editorui": "https://github.com/jose-villegas/editorui.git"
```

Reference the assembly in your editor asmdef:

```json
{
  "references": ["com.balloonparty.editorui.Editor"]
}
```

Then import the namespace you need:

```csharp
using BalloonParty.EditorUI.Charts;
using BalloonParty.EditorUI.Tables;
using BalloonParty.EditorUI.Layout;
using BalloonParty.EditorUI.Palette;
using BalloonParty.EditorUI.Utilities;
```

## Components

### Charts (`BalloonParty.EditorUI.Charts`)

| Type | Purpose |
|------|---------|
| **BarChart** | Clickable bar chart with selection highlight, optional `ThresholdLine` overlay, and per-bar `BarColorResolver` callback. |
| **PolylineOverlay** | Normalized polyline via `Handles.DrawAAPolyLine`. |
| **PlotGrid** | Horizontal grid lines with Y-axis and X-axis labels. |
| **PlotLegend** | Swatch + label entries for chart legends. |
| **PlotMarker** | Diamond and circle marker shapes. |

### Tables (`BalloonParty.EditorUI.Tables`)

| Type | Purpose |
|------|---------|
| **SortableHeader** | Toolbar button that toggles sort direction. |
| **SelectionTracker** | Checkbox-based row selection with select-all. |
| **ISelectable** | Row contract for `SelectionTracker`. |
| **StyledRow** | Highlighted rows and bold labels. |
| **RowColorResolver** | Priority-based row color (focused > active > fallback > striped). |
| **TableDrawHelper** | Column layout (`ComputeColumnX`, `HasGapBefore`), separators, group backgrounds, cell insets. |
| **PropertyCellDrawer** | SerializedProperty fields in table cells. |
| **TypedEntrySelectorCell\<TEnum\>** | Generic expand/collapse cell for enum-typed weighted arrays. |
| **FieldSpec** | Column definition for `TypedEntrySelectorCell`. |
| **CellConfig** | Per-cell sizing/padding configuration. |

### Layout (`BalloonParty.EditorUI.Layout`)

| Type | Purpose |
|------|---------|
| **FoldoutSection** | Persistent foldout — EditorPrefs-backed overload or delegate getter/setter overload for custom persistence. |
| **NavigationHeader** | ◀ [Label] [IntField] ▶ navigation row. |
| **SearchFilterToolbar** | Search field + enum popup filter + refresh button. |

### Palette (`BalloonParty.EditorUI.Palette`)

| Type | Purpose |
|------|---------|
| **IColorPalette** | Minimal read-only color palette interface (`Count`, `GetName`, `GetColor`). |
| **PaletteColorPicker** | Color popup + swatch for any `IColorPalette` implementation. |

### Utilities (`BalloonParty.EditorUI.Utilities`)

| Type | Purpose |
|------|---------|
| **StyleCache** | Lazy-init `GUIStyle` factory with automatic domain-reload invalidation (`[InitializeOnLoad]`). Eliminates per-frame allocations. |
| **EditorAssetCache\<T\>** | Lazy ScriptableObject finder with invalidation. Injectable `Func<T[]>` finder for testability. |
| **IconButtonHelper** | Cached icon lookup with fallback glyph (`Get`) and one-call draw (`DrawButton`). |
| **AssetLinkLabel** | Clickable label that pings assets in the Project view. |
| **EditorAnimationLoop** | Play/pause/stop animation loop via `EditorApplication.update`. |

## Package Structure

```
Packages/com.balloonparty.editorui/
├── Editor/
│   ├── Charts/          (5 files)
│   ├── Tables/          (10 files)
│   ├── Layout/          (3 files)
│   ├── Palette/         (2 files)
│   └── Utilities/       (5 files)
├── Tests/Editor/        (15 test files)
└── Samples~/
    └── TableWindowExample/
```

## Requirements

- Unity 2022.3+
- Editor-only (no runtime dependency)
- `autoReferenced: false` — consumers must add an explicit asmdef reference
