# Item

Items are game-wide collectible effects — Bomb, Laser, Lightning, Paint, and Shield. They can appear in different contexts throughout the game: on balloons, in UI previews, in reward screens, or anywhere else an item needs to be displayed or activated. The item system is intentionally **context-independent** — it knows how to display and activate items, but does not know or care what is hosting them.

## Contents

### Display

| File | What it does |
|---|---|
| `IItem` | Base interface — `ItemType Type` |
| `IBalloonItem` | Balloon-hosted activation adapter — `UniTask Activate(IBalloonModel, Vector3)`. Handlers are singletons and activations can overlap (AoE items trigger several in one frame; chain/splash effects resolve over time), so all per-activation state lives in locals captured by the activation, never in handler fields |
| `IItemView` | Contract for per-type visual components — `Activate(Color)`, `Deactivate()`, `ApplySortingOrder(int)` |
| `ItemDisplayService` | Plain MonoBehaviour on the host's item container (a serialized reference on `BalloonView` — no DI). `Bind()` bridges the host's reactive properties (`Item`, color name, slot index) to a pooled visual's lifecycle: gets the `ItemVisualView` for `ItemSettings.VisualPrefab` from `PoolManager.GetOrRegister()`, reparents it under itself, recolors immediately when the host's color changes, re-sorts on slot changes, and exposes the active visual's `ITransformCapture` (the laser's rotating body) to the host |
| `ItemVisualView` | MonoBehaviour on each item visual prefab (PU_Bomb, PU_Laser, etc.) — implements `IItemView` and `IPoolable`; `Activate(color)` shows and colors, `SetColor(color)` recolors without toggling visibility, `Deactivate()` hides. Sorting managed via `ApplySortingOrder(int)` |
| `SimplePoolChannel<ItemVisualView>` | `PoolChannel<ItemVisualView>` — one channel per visual prefab, keyed by prefab name. Managed by `ItemDisplayService` via `PoolManager.GetOrRegister()` |
| `LaserItemRotation` | MonoBehaviour on the laser body child — continuous Z-axis rotation at `_rotationSpeed`; resets angle and stops on `OnEnable` or `Stop()`. Implements `ITransformCapture` so the host can snapshot the rotation at hit time |
| `ITransformCapture` / `TransformSnapshot` | Capture contract for item visuals whose transform matters at hit time — `CaptureSnapshot()` returns position/rotation/scale. `BalloonController` snapshots the hit balloon's capture component and publishes `TransformCapturedMessage` (in `Shared/Messages/`) |
| `ItemEffectPlayer` | Plain C# — plays an item's one-shot activation effect: pulls the `EffectView` for `ItemSettings.ActivationEffectPrefab` from the pool, tints it by the popped balloon's color, returns it on completion. Shared by bomb/laser/shield; chain/splash effects drive their own two-phase setup |
| `BalloonOverlapQuery` | Plain C# — shared physics setup for AoE items (bomb, laser): a balloon-layer `ContactFilter2D` plus `TryResolveBalloon()` that maps a hit collider to a live balloon model, skipping recycled views and the popped balloon itself |

### Assignment

| File | What it does |
|---|---|
| `ItemAssigner` | `IStartable` — subscribes to `ItemCheckMessage`; picks a random newly-spawned balloon that implements `IHasWriteableItemSlot` and assigns an item based on turn frequency, weight, and per-type max-cap rules. Balloons that do not implement `IHasWriteableItemSlot` (e.g. `ToughBalloonModel`) are excluded from selection entirely. Cap counting pattern-matches grid actors on the same `IHasItemSlot` capability eligibility uses — counting a concrete model type would silently break the cap |

### Activation

| File | What it does |
|---|---|
| `ItemActivator` | `IStartable` + `IDisposable` — subscribes to `ActorHitMessage`; when the hit actor is a balloon carrying an item, finds the matching `IBalloonItem` handler by type, calls `await Activate(balloon, worldPos)`, then publishes `ItemActivatedMessage`. Yields one frame before activation (cancelled on scope teardown) to let all synchronous `ActorHitMessage` subscribers finish first |
| `Bomb/BombItemHandler` | Area explosion — non-alloc `Physics2D.OverlapCircle` via `BalloonOverlapQuery`; dispatches a hit for every balloon in radius via `IHitDispatcher` and publishes a `Shockwave` `NudgeMessage` for survivors. Direct hex neighbors of the bomb always receive **piercing** damage (the blast core guarantees a kill); everything else gets `ItemSettings.Damage`. Plays its effect via `ItemEffectPlayer`; stamps `DisturbanceFieldService.Stamp()` on detonation with the `Bomb` profile |
| `Laser/LaserItemHandler` | Cross beam — 4× non-alloc `Physics2D.CircleCast` in rotated directions; reads the captured rotation from `TransformCapturedMessage` (keyed per balloon, consumed once on activation). Passes `ItemSettings.Damage` into each dispatched hit. Plays its effect via `ItemEffectPlayer`; stamps `DisturbanceFieldService.Stamp()` along beam segments with the `Laser` profile |
| `Lightning/LightningItemHandler` | Chain lightning — queries `SlotGrid` for all same-color balloons, sorts by distance nearest-first, spawns the chain effect via `SimplePoolChannel<EffectView>` and configures it through `IChainEffect.PrepareDisplay(positions, settings, onTargetHit)`. Hits are dispatched per jump as the bolt advances (`onTargetHit` callback), each with `ItemSettings.Damage`. Per-activation target lists live in locals — a second activation mid-chain must not touch them |
| `Lightning/IChainEffect` | Interface a pooled chain effect implements so the handler can configure it without downcasting to the concrete view |
| `Lightning/ChainLightningGeometry` | `internal static` bolt math — `BuildBoltBuffers` (fractal **midpoint displacement** via `PathHelper.MidpointDisplacement` with configurable `FractalDecay`) and `BuildGlowPath` (smooth **Catmull-Rom path** through per-jump centroids). Shared with the editor preview (single source of truth). Uses `VectorMathExtensions.Centroid`/`BoundingRadius` and `PathHelper.ResampleLinear`/`PrefixSum` |
| `Lightning/ChainLightningView` | `EffectView` subclass implementing `IChainEffect` — multiple `LineRenderer`s + a `SpriteRenderer` glow. `Play()` starts `async UniTaskVoid` that grows forward jump-by-jump then retracts; glow position is interpolated per-frame along the smooth path via `PathHelper.SampleAt` |
| `Shield/ShieldItemHandler` | Shield grant — increments `ShieldsRemaining` on the active projectile (tracked via `ProjectileLoadedMessage`), publishes `ShieldGainedMessage` with the balloon's slot index, and plays its activation effect via `ItemEffectPlayer`. Does not deal damage |
| `Paint/PaintItemHandler` | Splatoon-style color spread — computes all 6 hex neighbor positions via `HexCoordinates.HexNeighborIndices` + `SlotGrid.IndexToWorldPosition`, launches paint blob arcs toward each via `ISplashEffect.PrepareDisplay`, and changes paintable (`IPaintable`) different-color neighbors to the popped balloon's color on blob arrival. Stamps `DisturbanceFieldService.Stamp()` on neighbor hits and splash landing with the `Paint` profile |
| `Paint/ISplashEffect` | Interface a pooled splash effect implements so the handler can configure blob flights without downcasting to the concrete view |
| `Paint/PaintSplashView` | `EffectView` subclass — animates `ColorableRenderer` blobs along arc paths; spawns fire-and-forget splash particles via `ParticlePoolChannel` on landing. `PrepareDisplay` takes `ItemSettings` directly. `ComputeBlobFlight` and `ApplyBlobMaterial` are `internal static` — shared with editor preview (single source of truth for position, scale, and MPB math). Returns `BlobFlightSnapshot` struct. Blobs spin during flight at `PaintBlobSpinSpeed`. Each blob gets a unique `sortingOrder` to prevent transparent sprite flickering. Renderer is found via `GetComponentInChildren<Renderer>()` to support `CompositeColorableRenderer` hierarchies |
| `Paint/PaintBlobRenderer` | MonoBehaviour on each blob child — assigns a random `_TimeOffset` to the PaintBlob shader via `MaterialPropertyBlock` so each blob's animation phase differs. GPU instancing is disabled on `PaintBlob` and `PaintFlyingBlob` materials because `_TimeOffset` is set per-instance via MPB (see `Assets/Shaders/BalloonParty/README.md`) |

## Architecture

The item display system is a self-contained, DI-free unit: an `ItemDisplayService` MonoBehaviour on the host's item container, plus pooled `ItemVisualView` prefabs (one per `ItemType`, referenced from `ItemSettings.VisualPrefab`). The host owns the wiring — it holds a serialized reference to the service and passes every dependency (configs, palette, pool manager) through `Bind()`, so pooled hosts need no per-instance scope.

### Design principle: items are not balloons

The `Item/` folder's display side has no dependency on `Balloon/`. `ItemDisplayService.Bind()` accepts individual reactive properties — it does not know whether the caller is a balloon, a UI panel, or a reward screen. Future contexts (shop previews, inventory, tutorial highlights) can host items by adding an `ItemDisplayService` to their hierarchy and calling `Bind()` with appropriate reactive properties.

`IBalloonItem` exists only as a thin adapter for the balloon-hosted activation flow. It is the balloon system's way of interacting with items, not the item system's knowledge of balloons.

### Display flow

1. A host (e.g. `BalloonView.Bind()`) calls `ItemDisplayService.Bind(item, colorName, slotIndex, …)` with the reactive properties plus the config/palette/pool dependencies and sorting inputs
2. `ItemDisplayService` subscribes to the model's `Item` reactive property and `colorName`
3. When the item type changes to non-None, `ItemDisplayService` gets a `ItemVisualView` instance from `PoolManager.GetOrRegister()` keyed by the visual prefab name, reparents it, and calls `Activate(color)`
4. When `colorName` changes while a visual is active, `ItemDisplayService` calls `SetColor()` on the active visual — the item display always matches the host's current color
5. When `Unbind()` is called or the item changes back to None, the active visual is returned to its pool via `PoolManager.Return()`
6. Sorting order updates flow through `ItemDisplayService` → `ItemVisualView.ApplySortingOrder()` on the active instance

### Activation flow

1. `ProjectileView.OnTriggerEnter2D` hands the hit to `ProjectileHitResolver`, which evaluates the outcome (always `Damage = 1`) and dispatches an `ActorHitMessage` through `IHitDispatcher` (`Game/HitPipeline`) — score stage first, then the owning `BalloonController`, then the broadcast
2. The hit balloon's `BalloonController` snapshots any `ITransformCapture` child (publishing `TransformCapturedMessage` for the laser), calls `_view.Hide()` (disables collider and renderers), and waits for `ItemActivatedMessage` before returning to pool
3. `ItemActivator` receives the broadcast `ActorHitMessage`, yields one frame, then calls `await Activate(balloon, worldPos)` on the matching handler
4. The handler runs its effect (may be async, e.g. lightning), dispatching an `ActorHitMessage` for each secondary balloon with `Damage = settings.Damage`
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

Each damaging item reads `ItemSettings.Damage` (configured per item in `ItemConfiguration`) and passes it inside the `DamageContext` of the dispatched `ActorHitMessage` (along with `DamageFlags` and the source color). The outcome is pre-computed by `ActorHitMessage.From`, which calls `EvaluateHit(context)` on the hit actor — `BalloonController` reads `msg.Outcome` and routes accordingly. Setting `Damage = 1` (the default) reproduces normal one-hit behaviour. Setting it higher on Bomb, for example, allows a single blast to pop tough balloons that would otherwise survive.

Non-damaging items (Paint, Shield) do not use the `Damage` field — the `ItemSettingsDrawer` hides it for those types.

> **Unbreakable balloons** — `UnbreakableBalloonModel` returns `Deflect` from `EvaluateHit` regardless of damage. Only `DamageFlags.Piercing` (e.g. the bomb's direct-neighbor blast core) forces a `Pop`.

## Interactions

- **Any host view** — calls `ItemDisplayService.Bind()`/`Unbind()` to connect/disconnect item display
- **BalloonController** — defers balloon pool return until `ItemActivatedMessage` arrives; snapshots `ITransformCapture` children and publishes `TransformCapturedMessage`; routes on `msg.Outcome` switch (`PassThrough`/`Deflect`/`Pop`)
- **ItemActivator** — central orchestrator; routes activation to the correct handler after yielding one frame
- **IHitDispatcher (`Game/HitPipeline`)** — all handler hits are dispatched through it, never published to the broker directly
- **SlotGrid** — `LightningItemHandler` queries all balloons of a given color; `PaintItemHandler` uses `HexCoordinates.HexNeighborIndices` and `SlotGrid.IndexToWorldPosition` for neighbor targeting
- **PoolManager** — item visual lifecycle via `SimplePoolChannel<ItemVisualView>`; activation effect lifecycle via `SimplePoolChannel<EffectView>` (`ItemEffectPlayer` for one-shot effects)
- **IEffect / EffectView** — `ChainLightningView` extends `EffectView`; all item activation effects that need async Play/Stop extend `EffectView`
- **IGameConfiguration / ItemConfiguration** — color lookup, item settings (radius, nudge values, laser cast params, lightning timing/segments/randomness/glow subdivisions/fractal decay, paint flight duration/arc curve/scale curve/shadow scale curve/sprite scale curve/spin speed, damage)
- **ColorableRenderer** — `PaintSplashView` uses `ColorableRenderer` blobs so they participate in the standard color pipeline
