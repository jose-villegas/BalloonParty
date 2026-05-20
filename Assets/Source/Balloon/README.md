# Balloon

Represents balloons in the game — their state, appearance, spawning, and destruction.

## Folder structure

| Folder | What it owns |
|---|---|
| `Model/` | `IBalloonModel` (read-only interface — extends `ISlotActor`, `IHasColor`, `IHasScore`, `IHasNudge`), `IWriteableBalloonModel` (mutable — extends `IWriteableSlotActor`), `BalloonModel` (concrete — also implements `IHasWriteableColor`, paintable), `ToughBalloonModel` (concrete — does NOT implement `IHasWriteableColor`, not paintable). Consumers use the narrowest interface: views receive `IBalloonModel`; controllers, spawners, and grid operations use `IWriteableBalloonModel` |
| `View/` | `BalloonView` — MonoBehaviour implementing `IPoolable` and `ISlotActorView` that binds to a model and renders color, sorting order, and pop VFX. `ActorKind` returns `Dynamic`. Uses `ColorableRendererExtensions.BindColor` to reactively update all `ColorableRenderer[]` children when the model's color changes. Caches `IBalloonViewBinding[]` and `IBalloonVariant` in `Awake()`. Exposes `Variant` for `BalloonSpawner`. `TweenTracker` and `ItemDisplayService` are `[SerializeField]` references |
| `Controller/` | `BalloonController` — wires model to view and handles hit/deflect/pop routing; `BalloonBalancer` — rebalances the grid after each turn |
| `Spawner/` | `BalloonSpawner` — creates balloon lines at game start and after each projectile death; `BalloonPoolChannel` — pool channel using `InjectingPoolChannel` with `IObjectResolver.Instantiate` |
| `Type/` | `BalloonType` enum, `IBalloonVariant`, `IBalloonViewBinding`, `ColorableBalloonVariant`, `SimpleBalloonVariant`, `ToughBalloonVariant` — per-prefab variant components that initialize type and color on the model. `IBalloonViewBinding` is a secondary interface that any prefab component can implement to receive `Bind(model, disposables)` calls from `BalloonView` — used by `ToughBalloonVariant` to drive shader damage state (see `Type/README.md`) |
| `BalloonLifetimeScope` | Legacy scope on balloon prefabs — `autoRun` disabled in Inspector; injection handled by `InjectingPoolChannel` |

`View/` also contains `SpriteColorableRenderer` (`ColorableRenderer<SpriteRenderer>` — sets `renderer.color`) and `ParticleColorableRenderer` (`ColorableRenderer<ParticleSystem>` — sets `startColor` and immediately recolors live particles via `Clear`+`Play`).

### Rendering and GPU instancing

Balloon materials (`BalloonMaterial`, `SpecularBalloonBlur`, etc.) have GPU instancing enabled. Per-balloon color is applied via `SpriteRenderer.color`, which Unity streams to the shader as `unity_SpriteRendererColorArray` in the instancing buffer. All 6 custom shaders follow the `UnitySprites.cginc` pattern with a `_RendererColor` fallback for non-SpriteRenderer components. Balloon sprites are packed into `Balloons.spriteatlas` for draw call reduction. See `Assets/Shaders/BalloonParty/README.md` for the instancing policy and `Assets/Sprites/README.md` for atlas groupings.

## Behaviour

A balloon knows its color, type, how many hits it can absorb, where it sits in the grid, whether it has settled into position, and whether it carries an item. When any reactive property changes, the view updates automatically via UniRx subscriptions.

`BalloonController` routes incoming `ActorHitMessage`s (filtered via `msg.Actor is IBalloonModel`) based on `HitsRemaining` and the message's `Damage` value:

- **Unbreakable (`-1`)** — deflect regardless of damage: publishes `BalloonDeflectedMessage` and `BalloonNudgeMessage(Deflect)`; never pops.
- **`HitsRemaining - Damage > 0`** — decrement by `Damage` and deflect. A high-damage hit (e.g. Bomb with `Damage = 2`) can bypass the deflect threshold entirely if it removes enough hits in one blow.
- **`HitsRemaining - Damage ≤ 0`** — pop: plays VFX, removes from grid, returns to pool (deferred if the balloon carries an item). Overkill damage is not tracked.

Neighbor nudges for every hit are handled independently by `NudgeService` in `Nudge/`, which subscribes to `ActorHitMessage` (filtering by `IHasNudge`) and dispatches push-out → return animations to all 6 grid neighbors that also implement `IHasNudge`.

### Pooling

Balloon views are pooled via `PoolManager` / `BalloonPoolChannel`. A `CompositeDisposable` replaces `AddTo(this)` for reactive subscriptions — cleared on each `Bind()` and `OnDespawned()`. External subscribers (e.g. `BalloonController`'s hit subscription) register via `RegisterDisposeOnDespawn()` so they're cleaned up on pool return. `IBalloonViewBinding` components (e.g. `ToughBalloonType`) receive the same `CompositeDisposable` so their subscriptions are cleared automatically in the same lifecycle.

### Animation phases (turn-based)

Balloon animations are organized into sequential, non-overlapping phases per turn:

1. **Hit phase** — projectile bounces and pops or deflects balloons. Each hit nudges neighbors outward and back (handled by `NudgeService`). No rebalancing during flight.
2. **Spawn phase** — after the projectile dies, new balloon lines spawn with a delay between each line. Spawn uses standalone `DOMove` + `DOScale` tweens (not tracked).
3. **Balance phase** — a single balance pass runs after all spawning completes. `BalloonBalancer` scans for unbalanced balloons and moves them upward along the optimal path using `DOPath(CatmullRom)`.

### Tween composition

A `TweenTracker` component on each balloon view manages position tween sequencing:

- **Nudge** calls `tracker.Replace(sequence)` — kills any existing tracked tween (previous nudge) and stores the new push-out → return sequence. Also kills standalone spawn tweens via `transform.DOKill()` so nudge cleanly takes over position. If the balloon was mid-scale-up, a parallel `DOScale` smoothly finishes scaling to full size.
- **Balance** calls `tracker.Append(tween)` — if a nudge is still playing, the balance path chains after it. If the tracker is idle, starts a new sequence.
- **Despawn** calls `tracker.Kill()` + `transform.DOKill()` — cleans up everything.

### Scale recovery

When nudge or balance interrupts a spawning balloon, `transform.DOKill()` kills the standalone scale tween mid-animation. Both nudge and balance capture the current scale beforehand and create a parallel `DOScale(Vector3.one, duration)` if the balloon hasn't reached full size — so the balloon smoothly finishes scaling alongside its movement.

## Interactions

- **SlotGrid** — balloons occupy positions in it; stores models and views in parallel arrays. Spawner places, controller removes, balancer relocates. `_grid.ViewAt(slotIndex)` reaches views from models.
- **BalloonBalancer** — moves balloons when gaps appear; fires once per turn after spawning
- **NudgeService** — subscribes to `ActorHitMessage` and `BalloonNudgeMessage`; nudges neighboring and deflecting balloon views
- **ProjectileView** — triggers destruction on collision via `ActorHitMessage`
- **ScoreController** — records each qualifying pop via `ActorHitMessage` (filters `IBalloonModel`)
- **PoolManager** — `BalloonView` instances are pooled via `BalloonPoolChannel`; pop VFX pooled via `ParticlePoolChannel`
- **TweenTracker** — generic `MonoBehaviour` in `Shared/` that manages tween sequencing (append, replace, kill)
- **Item system** — balloons are one host for the game-wide item system (in `Item/`). `BalloonView` calls `ItemDisplayService.Bind()`/`Unbind()` to connect item visuals; the item system itself has no knowledge of balloons
- **BalloonsConfiguration** — balloon prefab entries, spawn animation timing, balance timing, nudge defaults, pop VFX
- **GamePalette** — resolves color name → `UnityEngine.Color` for renderer tinting
