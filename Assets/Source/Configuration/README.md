# Configuration

All game data is split across focused ScriptableObjects. Each is registered as a singleton in `GameLifetimeScope` and injected wherever needed.

## Contents

### ScriptableObjects

| Asset | What it holds |
|---|---|
| `GameConfiguration` | Implements `IGameConfiguration` — projectile settings, slot grid dimensions, prediction trace params, score trail timing, score points scatter delay, points-per-level formula |
| `BalloonsConfiguration` | Balloon-specific configuration — `BalloonPrefabEntry[]` entries with per-type weight/cap/nudge/VFX, default pop VFX, spawn line counts, spawn animation range, balance delay, global nudge defaults |
| `GridActorConfiguration` | Grid actor configuration — `GridActorPrefabEntry[]` entries with per-type weight, max-count cap, and `HitsToPop` for destructible actors (Gatekeeper). Used by the procedural `GridSpawner` (Phase 8.3) and `StaticActorSpawner` |
| `GamePalette` | Array of `PaletteEntry` (name + `Color`) — the single source for all balloon colors; injected into `BalloonView`, `ColorableBalloonType`, `ScoreController`, `ColorProgressBar`, `ScoreTrailService`, `ItemDisplayService`, and anywhere a color name must be resolved to a `UnityEngine.Color` |
| `GameDisplayConfiguration` | Aspect-ratio → orthographic-size lookup for camera sizing |
| `ItemConfiguration` | Per-item tuning — one `ItemSettings` entry per `ItemType`: activation frequency, weight, max cap, damage, and type-specific effect params |
| `PuffCloudSettings` | Puff cloud visual tuning — noise animation speed, density field resolution/timing, wind, displacement, visual padding, sorting layer/order, disturbance radii/strengths, and the `CloudPrefab` reference. Injected into `PuffCloudViewController`. |
| `DisturbanceFieldSettings` | Shared disturbance field tuning — RT resolution (`TexelsPerUnit`), diffusion rate/reform speed/tick interval, wind speed/smoothing/decay, pressure, displacement amount/decay, performance thresholds (`MinStampStrength`, `MaxLerpStamps`), shader references (`DiffusionShader`, `StampBatchedShader`), and per-source `StampProfile[]` with `StampSource` flags. Injected into `DisturbanceFieldService` and all disturbance consumers |

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
| `PaletteColorNameDrawer` | Custom `PropertyDrawer` for `PaletteColorNameAttribute` — renders a string field as a popup listing all `GamePalette` color names with a color swatch beside the selected entry. Loads the palette lazily via `AssetDatabase.FindAssets("t:GamePalette")` |
| `StampProfileDrawer` | Custom `PropertyDrawer` for `StampProfile` — foldout header shows `StampSource` flags label; expanded view shows Sources, Radius, Strength, Duration fields |

## Design rules

- **Never hardcode** values that exist in a configuration asset.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems. If a system needs a value, it injects the relevant configuration object.
- New configuration fields are added to the appropriate asset type. When a domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.
- `IGameConfiguration` (in `Shared/`) is the read-only interface for `GameConfiguration`; all other SOs are injected by their concrete type.
- New common fields added to `ItemSettings` or `BalloonPrefabEntry` appear in the Inspector automatically — no drawer changes required. Only fields that need custom layout (variable height, conditional visibility, custom controls) must be added to the drawer's `ExcludedFields` set.
