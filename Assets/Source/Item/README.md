# Item

Items are game-wide collectible effects — Bomb, Laser, Lightning, and Shield. They can appear in different contexts throughout the game: on balloons, in UI previews, in reward screens, or anywhere else an item needs to be displayed or activated. The item system is intentionally **context-independent** — it knows how to display and activate items, but does not know or care what is hosting them.

## Contents

| File | What it does |
|---|---|
| `IItem` | Base interface for all items — `Activate()` |
| `IBalloonItem` | Context-specific interface for items hosted on balloons — `Type`, `Setup(IBalloonModel)` |
| `IItemView` | Contract for per-type visual components — `Type`, `Activate(Color)`, `Deactivate()`, `ApplySortingOrder(int)` |
| `ItemDisplayService` | MonoBehaviour on the item container — bridges an external data source (e.g. a model's `Item` property) to reactive properties (`ActiveItem`, `ActiveColor`, `SortingStartOrder`) that visual views observe |
| `ItemViewScope` | Reusable `LifetimeScope` — registers `ItemDisplayService` and injects all `ItemVisualView` children via `RegisterBuildCallback`. Custom `FindParent()` walks the transform hierarchy so it parents to whatever ancestor scope hosts it |
| `ItemVisualView` | MonoBehaviour on each item type's visual (PUBomb, PULaser, etc.) — implements `IItemView`; receives `ItemDisplayService` via `[Inject]`; subscribes to active item changes and toggles visibility/color/sorting |
| `LaserItemRotation` | MonoBehaviour on the laser body child — continuous Z-axis rotation at `_rotationSpeed`; resets angle on enable |

## Architecture

The item display system is designed around a **reusable scope pattern**. `ItemViewScope` + `ItemDisplayService` + `ItemVisualView` children form a self-contained unit that can be dropped onto any prefab or GameObject hierarchy. The scope inherits all parent registrations (config, messages, etc.) automatically through VContainer's scope hierarchy — no manual wiring needed.

### Design principle: items are not balloons

The `Item/` folder has no dependency on `Balloon/`. `ItemDisplayService.Bind()` accepts a model interface and configuration — it does not know whether the caller is a balloon, a UI panel, or a reward screen. Future contexts (shop previews, inventory, tutorial highlights) can host items by adding `ItemViewScope` to their prefab and calling `Bind()` with appropriate data.

`IBalloonItem` exists only as a thin adapter for the balloon-hosted activation flow (Phase 15d). It is the balloon system's way of interacting with items, not the item system's knowledge of balloons.

### Scope hierarchy example (balloon prefab)

```
Balloon (root)         ← BalloonLifetimeScope
  └── Item             ← ItemViewScope, ItemDisplayService
       ├── PUBomb      ← ItemVisualView (Type = Bomb)
       ├── PULaser     ← ItemVisualView (Type = Laser)
       ├── PULightning ← ItemVisualView (Type = Lightning)
       └── PUShield    ← ItemVisualView (Type = Shield)
```

`ItemViewScope` extends `LifetimeScope` directly (not `GameChildLifetimeScope`) with a custom `FindParent()` that walks the transform hierarchy to find the nearest ancestor `LifetimeScope`. This makes it host-agnostic — it chains to whatever scope contains it, whether that's a `BalloonLifetimeScope`, a UI panel scope, or the game scope itself.

### Display flow

1. A host (e.g. `BalloonView.Bind()`) calls `ItemDisplayService.Bind(model, config, sortingOffset)`
2. `ItemDisplayService` subscribes to the model's `Item` property
3. When the item type changes to non-None, `ItemDisplayService` looks up the `VisualPrefab` from `ItemSettings` in the config, instantiates it under its own transform, and calls `ItemVisualView.Activate(color)` on the instance
4. When the item type changes again or `Unbind()` is called, the active visual instance is destroyed
5. Sorting order updates flow through `ItemDisplayService` → `ItemVisualView.ApplySortingOrder()` on the active instance

## Item types

| Type | Visual | Activation effect (Phase 15d) |
|---|---|---|
| **Bomb** | Bomb icon, tinted to host color | Area-of-effect explosion — destroys nearby balloons in a radius |
| **Laser** | Rotating cross, tinted to host color | Cross-shaped beam — destroys balloons along four axes |
| **Lightning** | Lightning icon, tinted to host color | Chain lightning — hits all same-color balloons sequentially |
| **Shield** | Shield icon, tinted to host color | Grants the active projectile +1 bounce shield |

## Interactions

- **Any host view** — calls `ItemDisplayService.Bind()`/`Unbind()` to connect/disconnect item display. Currently `BalloonView` is the only host; future hosts follow the same pattern
- **Host model** — an `IReadOnlyReactiveProperty<ItemType>` drives which visual is active
- **Host scope** — `ItemViewScope` chains to the nearest ancestor `LifetimeScope` via transform hierarchy
- **IGameConfiguration** — provides color lookup and item settings
- **SortingHelper** — shared utility for sorting order calculation
