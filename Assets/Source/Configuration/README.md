# Configuration

All game data is split across focused ScriptableObjects. Each is registered as a singleton in `GameLifetimeScope` and injected wherever needed.

## Contents

### ScriptableObjects

| Asset | Interface | What it holds |
|---|---|---|
| `GameConfiguration` | `IGameConfiguration` (in `Shared/`) | Projectile settings, slot grid dimensions, prediction trace params, score trail timing, score points scatter delay, points-per-level formula |
| `BalloonsConfiguration` | `IBalloonsConfiguration` | Balloon-specific configuration — `BalloonPrefabEntry` entries with per-type weight/cap/nudge/VFX, default pop VFX, spawn line counts, spawn animation range, balance delay, global nudge defaults |
| `GridActorConfiguration` | `IGridActorConfiguration` | Grid actor entries with per-type weight, max-count cap, and `HitsToPop` for destructible actors (Gatekeeper). Used by `StaticActorSpawner` and the procedural `GridSpawner` (Phase 8.3) |
| `GamePalette` | `IGamePalette` | Array of `PaletteEntry` (name + `Color`) — the single source for all balloon colors; `GetColor(name)` resolves name → `UnityEngine.Color` |
| `GameDisplayConfiguration` | `IGameDisplayConfiguration` | Reference world dimensions and `GetOrthogonalSize()` for camera sizing |
| `ItemConfiguration` | `IItemConfiguration` | Per-item tuning — one `ItemSettings` entry per `ItemType`: activation frequency, weight, max cap, damage, and type-specific effect params |
| `PuffCloudSettings` | `IPuffCloudSettings` | Puff cloud visual tuning — noise animation speed, visual padding, sorting layer/order, and the `CloudPrefab` reference |
| `DisturbanceFieldSettings` | `IDisturbanceFieldSettings` | Shared disturbance field tuning — RT resolution (`TexelsPerUnit`), diffusion rate/reform speed/tick interval, wind speed/smoothing/decay, pressure, displacement amount/decay, performance thresholds, shader references, and per-source `StampProfile[]` with `StampSource` flags |

### Data types

| File | What it does |
|---|---|
| `BalloonPrefabEntry` | Serializable entry in `BalloonsConfiguration` — holds a `BalloonView` prefab reference, spawn weight, optional max-count cap, `HitsToPop` (how many hits before popping; -1 = unbreakable), `ScoreValue` (points awarded on pop), per-type `NudgeOverride[]`, and optional pop VFX override. `BalloonType` drives which model class is created (`Simple` → `BalloonModel`, `Tough` → `ToughBalloonModel`). Item eligibility is determined by whether the model implements `IHasWriteableItemSlot` — not a flag. Pool key is derived from the prefab's GameObject name. |
| `GridActorPrefabEntry` | Serializable entry in `GridActorConfiguration` — holds a `GridActorView` prefab reference, `GridActorType`, spawn weight, `MinCount`, optional `MaxCount` cap, `MaxPerCluster` (caps individual cluster size for `Cluster` placement mode), `SlotPlacementMode` (`Random` or `Cluster`), and `HitsToPop` (relevant only for `Gatekeeper`). Implements `IWeightedEntry`. Pool key is derived from the prefab's GameObject name. |
| `IWeightedEntry` | Shared interface for weighted random selection — `Weight`, `MaxCount`, `PoolKey`. Implemented by `BalloonPrefabEntry`, `GridActorPrefabEntry`, and `ItemSettings`. |
| `ItemSettings` | Per-item tuning data for `ItemConfiguration` — common fields (type, frequency, weight, cap, visuals, `Damage`) plus type-specific fields (bomb radius, laser cast params, lightning timing). `Damage` controls how many hit-points a single activation removes from each affected balloon; defaults to 1 |
| `PaletteEntry` | Serializable name + `Color` pair used in `GamePalette` |
| `ItemType` | Enum — `None`, `Shield`, `Bomb`, `Laser`, `Lightning` |
| `PaletteColorMaskAttribute` | `PropertyAttribute` that marks an `int` field as a bitmask over `GamePalette.Colors` — rendered in the Inspector as per-color checkboxes via `PaletteColorMaskDrawer` |
| `PaletteColorNameAttribute` | `PropertyAttribute` that marks a `string` field as a palette color name — rendered in the Inspector as a popup dropdown with color swatch via `PaletteColorNameDrawer` |
| `StampProfile` | Serializable struct in `DisturbanceFieldSettings` — `StampSource` flags, `Radius`, `Strength`, `Duration`. Defines per-source disturbance parameters |
| `StampSource` | `[Flags]` enum — `Projectile`, `BalloonPath`, `BalloonPop`, `Bomb`, `Laser`, `Paint`. Identifies which game system a `StampProfile` applies to |

### Editor

All custom `PropertyDrawer` implementations in this folder extend `AutoFieldPropertyDrawer` (in `Source/Editor/`) unless they need fully custom rendering. Common fields are discovered automatically via serialized-property iteration and rendered using nicified names — drawers only need to declare which fields require special layout in `ExcludedFields` and handle those manually.

| File | What it does |
|---|---|
| `BalloonPrefabEntryDrawer` | Extends `AutoFieldPropertyDrawer` — auto-draws common fields; manually handles `_nudgeOverrides` (variable-height array), `_overridePopVfx` (toggle), and `_popVfxPrefab` (conditional on toggle) |
| `ItemSettingsDrawer` | Extends `AutoFieldPropertyDrawer` — auto-draws common fields (including `Damage`); manually handles type-specific sections (Bomb, Laser, Lightning, Paint) with section headers. Paint section includes: Blob Flight Duration, Blob Arc Curve, Blob Scale Curve, Blob Shadow Scale Curve, Blob Sprite Scale Curve, Blob Spin Speed. `_damage` is drawn only for damaging types (hidden for Paint and Shield); overrides `BuildFoldoutLabel` to append `[ItemType]` to the header |
| `GameDisplayConfigurationDrawer` | Custom `PropertyDrawer` for `GameDisplayConfiguration` — inline aspect-ratio / orthographic-size pair editing |
| `PaletteColorMaskDrawer` | Custom `PropertyDrawer` for `PaletteColorMaskAttribute` — renders a bitmask int as labeled checkboxes matching the current `GamePalette` entries |
| `PaletteColorNameDrawer` | Custom `PropertyDrawer` for `PaletteColorNameAttribute` — renders a string field as a popup listing all `GamePalette` color names with a color swatch beside the selected entry. Uses `ConfigAssetCache<GamePalette>` for lazy-cached palette lookup |
| `StampProfileDrawer` | Custom `PropertyDrawer` for `StampProfile` — foldout header shows `StampSource` flags label; expanded view shows Sources, Radius, Strength, Duration fields |

## Design rules

- **Never hardcode** values that exist in a configuration asset.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems. If a system needs a value, it injects the relevant configuration object.
- **Always inject the read-only interface**, not the concrete SO type. Every configuration SO has a corresponding `I`-prefixed interface (`IBalloonsConfiguration`, `IGamePalette`, etc.) that exposes only read-only properties. `GameLifetimeScope` registers each SO via `RegisterInstance<IInterface>(so)`. This prevents accidental mutation of SO fields at runtime and decouples consumers from the SO class.
- New configuration fields are added to the appropriate asset type. When a domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.
- `IGameConfiguration` lives in `Shared/` (not `Configuration/`) because it is consumed by assemblies that do not reference the `Configuration` namespace. All other configuration interfaces live alongside their SO in `Configuration/`.
- New common fields added to `ItemSettings` or `BalloonPrefabEntry` appear in the Inspector automatically — no drawer changes required. Only fields that need custom layout (variable height, conditional visibility, custom controls) must be added to the drawer's `ExcludedFields` set.
- Collection properties on interfaces use `IReadOnlyList<T>` to prevent element assignment and mutation of the collection structure. The backing `T[]` or `List<T>` in the SO satisfies this implicitly.
- **Editor config lookups** must use `ConfigAssetCache<T>` (in `Shared/`) instead of inline `AssetDatabase.FindAssets` + `LoadAssetAtPath`. This caches the result after the first lookup and avoids repeated asset-database queries. The class is guarded by `#if UNITY_EDITOR` so it compiles away in builds, but is available to both runtime `OnValidate` blocks and editor code. The `config-asset-cache` audit rule enforces this in editor files and `#if UNITY_EDITOR` blocks everywhere.
