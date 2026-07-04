# EditorUI

Reusable UI building blocks for editor windows with sortable, selectable tables. Each file is a single-responsibility static class or type. Nothing here references runtime code — it compiles only in Editor builds.

## Contents

| File | What it provides |
|---|---|
| `SortableHeader` | `SortState` class + `SortableHeader.Draw()` (toolbar button with ▲/▼ indicator that toggles sort direction) + `SortableHeader.ApplySort()` (sorts any list via a column-comparison delegate). One `SortState` instance per table |
| `SelectionTracker` | `ISelectable` interface (`bool Selected { get; set; }`) + `DrawSelectAllToggle()` (toggle-all checkbox synced with visible items) + `DrawRowToggle()` (per-row checkbox) + `DrawSelectionCount()` ("N selected / M total" label) + `GetSelected()` (returns selected items). Implement `ISelectable` on your table entry class |
| `AssetLinkLabel` | `AssetLinkLabel.Draw()` — clickable label that pings and selects an asset in the Project view by path |
| `StyledRow` | `DrawStyledLabel()` (label with conditional bold styling) + `BeginHighlightedRow()` (starts a horizontal row with optional background tint; caller must call `EndHorizontal`) |
| `SearchFilterToolbar` | `SearchFilterToolbar.Draw<TEnum>()` — full toolbar with search text field, enum popup filter, and optional refresh button. Generic over any filter enum |
| `EditorAnimationLoop` | Play/pause/stop animation loop driven by `EditorApplication.update` — tracks delta time, pause state, and playback speed; the caller supplies a tick callback that returns `false` when the animation completes. Used by `EffectPreview/EffectViewPreviewPlayer` |
| `PaletteColorPicker` | Reusable palette color dropdown + swatch drawing, backed by `ConfigAssetCache<GamePalette>`. Used by the effect previews and `ColorableBalloonVariantEditor` |

## Usage

```csharp
using BalloonParty.Editor.EditorUI;

// In your EditorWindow:
private readonly SortState _sort = new();
private bool _selectAll;
private string _search = "";
private MyFilter _filter;

void OnGUI()
{
    _filter = SearchFilterToolbar.Draw(ref _search, _filter, filterLabels, OnRefresh);

    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
    _selectAll = SelectionTracker.DrawSelectAllToggle(_selectAll, filtered);
    SortableHeader.Draw("Name", 0, 160, _sort);
    SortableHeader.Draw("Size", 1, 80, _sort);
    EditorGUILayout.EndHorizontal();

    SortableHeader.ApplySort(filtered, _sort, MyCompare);

    foreach (var entry in filtered)
    {
        StyledRow.BeginHighlightedRow(entry.NeedsAttention, warningColor);
        SelectionTracker.DrawRowToggle(entry);
        AssetLinkLabel.Draw(entry.Name, entry.Path);
        EditorGUILayout.EndHorizontal();
    }

    SelectionTracker.DrawSelectionCount(allItems);
    var selected = SelectionTracker.GetSelected(allItems);
}
```

