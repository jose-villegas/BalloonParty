# Configuration

All game data is split across focused ScriptableObjects. Each is registered as a singleton in `GameLifetimeScope` and injected wherever needed.

## Contents

### ScriptableObjects

| Asset | What it holds |
|---|---|
| `GameConfiguration` | Implements `IGameConfiguration` — projectile settings, slot grid dimensions, prediction trace params, score trail timing, points-per-level formula |
| `BalloonsConfiguration` | Balloon-specific configuration — `BalloonPrefabEntry[]` entries with per-type weight/cap/nudge/VFX, default pop VFX, spawn line counts, spawn animation range, balance delay, global nudge defaults |
| `GamePalette` | Array of `PaletteEntry` (name + `Color`) — the single source for all balloon colors; injected into `BalloonView`, `ColorableBalloonType`, `ScoreController`, `ItemDisplayService`, and anywhere a color name must be resolved to a `UnityEngine.Color` |
| `GameDisplayConfiguration` | Aspect-ratio → orthographic-size lookup for camera sizing |
| `ItemConfiguration` | Per-item tuning — one `ItemSettings` entry per `ItemType`: activation frequency, weight, max cap, effect params |

### Data types

| File | What it does |
|---|---|
| `BalloonPrefabEntry` | Serializable entry in `BalloonsConfiguration` — holds a `BalloonLifetimeScope` prefab reference, spawn weight, optional max-count cap, per-type `NudgeOverride[]`, optional pop VFX override, and `CanHoldItem` flag (controls whether items can be assigned to this balloon type). Pool key is derived from the prefab's GameObject name. |
| `PaletteEntry` | Serializable name + `Color` pair used in `GamePalette` |
| `ItemType` | Enum — `None`, `Shield`, `Bomb`, `Laser`, `Lightning` |
| `ItemSettings` | Per-item tuning data for `ItemConfiguration` |
| `PaletteColorMaskAttribute` | `PropertyAttribute` that marks an `int` field as a bitmask over `GamePalette.Colors` — rendered in the Inspector as per-color checkboxes via `PaletteColorMaskDrawer` |

### Editor

| File | What it does |
|---|---|
| `BalloonPrefabEntryDrawer` | Custom `PropertyDrawer` for `BalloonPrefabEntry` — compact layout with foldout for nudge overrides and pop VFX fields; includes the `CanHoldItem` toggle |
| `GameDisplayConfigurationDrawer` | Custom `PropertyDrawer` for `GameDisplayConfiguration` — inline aspect-ratio / orthographic-size pair editing |
| `PaletteColorMaskDrawer` | Custom `PropertyDrawer` for `PaletteColorMaskAttribute` — renders a bitmask int as labeled checkboxes matching the current `GamePalette` entries |

## Design Rules

- **Never hardcode** values that exist in a configuration asset.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems. If a system needs a value, it injects the relevant configuration object.
- New configuration fields are added to the appropriate asset type. When a domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.
- `IGameConfiguration` (in `Shared/`) is the read-only interface for `GameConfiguration`; all other SOs are injected by their concrete type.
