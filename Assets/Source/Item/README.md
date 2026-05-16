# Item

Items are game-wide collectible effects — Bomb, Laser, Lightning, Paint, and Shield. They can appear in different contexts throughout the game: on balloons, in UI previews, in reward screens, or anywhere else an item needs to be displayed or activated. The item system is intentionally **context-independent** — it knows how to display and activate items, but does not know or care what is hosting them.

## Contents

### Display

| File | What it does |
|---|---|
| `IItem` | Base interface — `ItemType Type`, `UniTask Activate()` |
| `IBalloonItem` | Balloon-hosted activation adapter — `Setup(IBalloonModel, Vector3)` called before `Activate()` |
| `IItemView` | Contract for per-type visual components — `Type`, `Activate(Color)`, `Deactivate()`, `ApplySortingOrder(int)` |
| `ItemDisplayService` | MonoBehaviour on the item container — bridges an external data source (e.g. a model's `Item` reactive property) to the active visual's lifecycle via `ItemVisualPoolChannel`. Subscribes to both item type and color name changes — recolors the active visual immediately when the host's color changes |
| `ItemViewScope` | Reusable `LifetimeScope` — registers `ItemDisplayService` and injects all `ItemVisualView` children via `RegisterBuildCallback`. Custom `FindParent()` walks the transform hierarchy so it parents to whatever ancestor scope hosts it |
| `ItemVisualView` | MonoBehaviour on each item type's visual (PU_Bomb, PU_Laser, etc.) — implements `IItemView` and `IPoolable`; `Activate(color)` shows and colors, `SetColor(color)` recolors without toggling visibility, `Deactivate()` hides. Sorting managed via `ApplySortingOrder(int)` |
| `ItemVisualPoolChannel` | `PoolChannel<ItemVisualView>` — one channel per visual prefab, keyed by prefab name. Managed by `ItemDisplayService` via `PoolManager.GetOrRegister()` |
| `LaserItemRotation` | MonoBehaviour on the laser body child — continuous Z-axis rotation at `_rotationSpeed`; resets angle and stops on `OnEnable` or `Stop()` |

### Assignment

| File | What it does |
|---|---|
| `ItemAssigner` | `IStartable` — subscribes to `ItemCheckMessage`; picks a random newly-spawned balloon that has `CanHoldItem = true` and assigns an item based on turn frequency, weight, and per-type max-cap rules. Balloons where `CanHoldItem` is false (e.g. tough balloons) are excluded from selection entirely |

### Activation

| File | What it does |
|---|---|
| `ItemActivator` | `IStartable` — subscribes to `BalloonHitMessage`; when the hit balloon carries an item, finds the matching `IBalloonItem` handler by type, calls `Setup` + `await Activate()`, then publishes `ItemActivatedMessage`. Yields one frame before activation to let all synchronous `BalloonHitMessage` subscribers finish first |
| `Bomb/BombItemHandler` | Area explosion — `Physics2D.OverlapCircleAll` at balloon position; destroys balloons in radius; nudges survivors with exponential distance falloff. Passes `ItemSettings.Damage` into each `BalloonHitMessage` |
| `Laser/LaserItemHandler` | Cross beam — 4× `Physics2D.CircleCastAll` in rotated directions; reads captured rotation from `ItemRotationCapturedMessage`. Passes `ItemSettings.Damage` into each `BalloonHitMessage` |
| `Lightning/LightningItemHandler` | Chain lightning — queries `SlotGrid` for all same-color balloons, sorts by distance, spawns `ChainLightningView` via `EffectPoolChannel`, hits each target sequentially with async delay. Passes `ItemSettings.Damage` into each `BalloonHitMessage` |
| `Lightning/ChainLightningView` | `EffectView` subclass for chain lightning — multiple `LineRenderer`s; `PrepareDisplay(positions, segMul, randomness, jumpTime, onTargetHit)` pre-computes jagged bolt segments; `Play()` starts `async UniTaskVoid` that grows forward jump-by-jump then retracts |
| `Shield/ShieldItemHandler` | Shield grant — increments `ShieldsRemaining` on the active projectile; spawns `PSVFX_ShieldGainPU` at the balloon's grid position. Does not deal damage |
| `Paint/PaintItemHandler` | Splatoon-style color spread — computes all 6 hex neighbor positions via `SlotGrid.HexNeighborIndices`, launches paint blob arcs toward each via `PaintSplashView`, and changes paintable different-color neighbors to the popped balloon's color on blob arrival |
| `Paint/PaintSplashView` | `EffectView` subclass — animates `ColorableRenderer` blobs along arc paths using `CurveUtility`; spawns fire-and-forget splash particles via `ParticlePoolChannel` on landing. Flight curves, duration, arc height, and scale are driven by `ItemConfiguration` |
| `Paint/PaintBlobRenderer` | MonoBehaviour on each blob child — assigns a random `_TimeOffset` to the PaintBlob shader via `MaterialPropertyBlock` so each blob's animation phase differs. GPU instancing is disabled on `PaintBlob` and `PaintFlyingBlob` materials because `_TimeOffset` is set per-instance via MPB (see `Assets/Shaders/BalloonParty/README.md`) |

## Architecture

The item display system is designed around a **reusable scope pattern**. `ItemViewScope` + `ItemDisplayService` + `ItemVisualView` children form a self-contained unit that can be dropped onto any prefab or GameObject hierarchy. The scope inherits all parent registrations automatically through VContainer's scope hierarchy — no manual wiring needed.

### Design principle: items are not balloons

The `Item/` folder has no dependency on `Balloon/`. `ItemDisplayService.Bind()` accepts individual reactive properties — it does not know whether the caller is a balloon, a UI panel, or a reward screen. Future contexts (shop previews, inventory, tutorial highlights) can host items by adding `ItemViewScope` to their prefab and calling `Bind()` with appropriate reactive properties.

`IBalloonItem` exists only as a thin adapter for the balloon-hosted activation flow. It is the balloon system's way of interacting with items, not the item system's knowledge of balloons.

### Scope hierarchy example (balloon prefab)

```
Balloon (root)         ← BalloonLifetimeScope
  └── Item             ← ItemViewScope, ItemDisplayService
       ├── PU_Bomb     ← ItemVisualView (Type = Bomb)
       ├── PU_Laser    ← ItemVisualView (Type = Laser)
       ├── PU_Lightning← ItemVisualView (Type = Lightning)
       └── PU_Shield   ← ItemVisualView (Type = Shield)
```

`ItemViewScope` extends `LifetimeScope` directly with a custom `FindParent()` that walks the transform hierarchy, not `FindFirstObjectByType` — so each pooled balloon's scope parents to its own balloon's root scope rather than a random one.

### Display flow

1. A host (e.g. `BalloonView.Bind()`) calls `ItemDisplayService.Bind(item, colorName, slotIndex, config, poolManager, itemConfig, sortingOffset)`
2. `ItemDisplayService` subscribes to the model's `Item` reactive property and `colorName`
3. When the item type changes to non-None, `ItemDisplayService` gets a `ItemVisualView` instance from `PoolManager.GetOrRegister()` keyed by the visual prefab name, reparents it, and calls `Activate(color)`
4. When `colorName` changes while a visual is active, `ItemDisplayService` calls `SetColor()` on the active visual — the item display always matches the host's current color
5. When `Unbind()` is called or the item changes back to None, the active visual is returned to its pool via `PoolManager.Return()`
6. Sorting order updates flow through `ItemDisplayService` → `ItemVisualView.ApplySortingOrder()` on the active instance

### Activation flow

1. `ProjectileView.OnTriggerEnter2D` publishes `BalloonHitMessage` for the hit balloon (always `Damage = 1`)
2. `BalloonController` receives it, calls `_view.Hide()` (disables collider and renderers), and waits for `ItemActivatedMessage` before returning to pool
3. `ItemActivator` receives the same `BalloonHitMessage`, yields one frame, then calls `Setup(balloon, worldPos)` + `await Activate()` on the matching handler
4. The handler runs its effect (may be async, e.g. lightning), publishing `BalloonHitMessage` for each secondary balloon with `Damage = settings.Damage`
5. `ItemActivator` publishes `ItemActivatedMessage` — `BalloonController` receives it and returns the item balloon to pool

## Item types

| Type | Visual | Activation effect | Damage |
|---|---|---|---|
| **Bomb** | Bomb icon, tinted to host color | Area-of-effect explosion — destroys nearby balloons in a radius with exponential nudge falloff | Configurable — set to 2 to instantly pop tough balloons |
| **Laser** | Rotating cross, tinted to host color | Cross-shaped beam — destroys balloons along four rotated axes; rotation is captured from `LaserItemRotation` at hit time | Configurable |
| **Lightning** | Lightning icon, tinted to host color | Chain lightning — hits all same-color balloons sequentially with a growing/retracting `LineRenderer` effect | Configurable |
| **Paint** | Paint blob, tinted to host color | Splatoon-style color spread — launches 6 arcing blobs to all hex neighbors; paintable different-color balloons adopt the popped balloon's color on splash | N/A — no damage dealt |
| **Shield** | Shield icon, tinted to host color | Grants the active projectile +1 bounce shield | N/A — no damage dealt |

## Damage

Each damaging item reads `ItemSettings.Damage` (configured per item in `ItemConfiguration`) and passes it as the `Damage` field of `BalloonHitMessage`. `BalloonController` subtracts that value from `HitsRemaining` in one step — exact kills and overkill both pop the balloon. Unbreakable balloons (`HitsRemaining = -1`) deflect regardless of damage value. Non-damaging items (Paint, Shield) do not use the `Damage` field — the `ItemSettingsDrawer` hides it for those types.

Setting `Damage = 1` (the default) reproduces normal one-hit behaviour. Setting it higher on Bomb, for example, allows a single blast to pop tough balloons that would otherwise survive.

## Interactions

- **Any host view** — calls `ItemDisplayService.Bind()`/`Unbind()` to connect/disconnect item display
- **BalloonController** — defers balloon pool return until `ItemActivatedMessage` arrives; captures laser rotation and publishes `ItemRotationCapturedMessage`; applies `BalloonHitMessage.Damage` to `HitsRemaining`
- **ItemActivator** — central orchestrator; routes activation to the correct handler after yielding one frame
- **SlotGrid** — `LightningItemHandler` queries all balloons of a given color; `ShieldItemHandler` resolves the balloon's grid-center world position
- **PoolManager** — item visual lifecycle via `ItemVisualPoolChannel`; activation effect lifecycle via `EffectPoolChannel`
- **IEffect / EffectView** — `ChainLightningView` extends `EffectView`; all item activation effects that need async Play/Stop extend `EffectView`
- **IGameConfiguration / ItemConfiguration** — color lookup, item settings (radius, nudge values, laser cast params, lightning timing, paint flight curves/duration/arc/scale, damage)
- **SlotGrid** — `LightningItemHandler` queries all balloons of a given color; `ShieldItemHandler` resolves the balloon's grid-center world position; `PaintItemHandler` uses `HexNeighborIndices` and `IndexToWorldPosition` for neighbor targeting
- **ColorableRenderer** — `PaintSplashView` uses `ColorableRenderer[]` for blobs so they participate in the standard color pipeline
