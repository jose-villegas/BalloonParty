# Balloon

Represents balloons in the game — their state, appearance, spawning, and destruction.

## Folder structure

| Folder | What it owns |
|---|---|
| `Model/` | `IBalloonModel` (read-only interface — extends `IDynamicSlotActor`, `IHitable`, `IHasNudge`), `IWriteableBalloonModel` (mutable — extends `IWriteableDynamicSlotActor`), `BalloonModelBase` (abstract — implements `IWriteableBalloonModel`; constructed via `BalloonModelConfig`; `EvaluateHit` supports `DamageFlags.Piercing` for forced `Pop`), `BalloonModel` (concrete — extends `BalloonModelBase`, also implements `IPaintable`, `IHasDurability`, `IHasScore`, `IHasScoreColor`, and `IHasWriteableItemSlot` — paintable, item-capable, scores in its own color; no attribution when `HitsRemaining > 0` after hit), `ToughBalloonModel` (concrete — extends `BalloonModelBase`, implements `IHasDurability`, `IHasScore`, `IHasScoreColor`; NOT paintable, NOT item-capable; `EvaluateHit` returns `Deflect` on survival; on pop scatters score to random palette colors), `BubbleClusterModel` (concrete — extends `BalloonModelBase`, implements `IHasDurability`, `IHasScoreColor`; no palette color, no item slot; on each hit scatters one score point per damage to random palette colors with `BreaksStreak = true`), `UnbreakableBalloonModel` (concrete — implements `IHitable`, `IHasScore`, `IHasScoreColor`; always `Deflect`; `Piercing` forces `Pop`; on pop scatters score to random palette colors). Item eligibility is determined structurally — `BalloonModel` implements `IHasWriteableItemSlot`; other types do not. Consumers use the narrowest interface: views receive `IBalloonModel`; controllers, spawners, and grid operations use `IWriteableBalloonModel`. `IHasColor` is NOT on `IBalloonModel` — only `BalloonModel` implements it; consumers that need color cast to `IHasColor` |
| `View/` | `BalloonView` — MonoBehaviour implementing `IPoolable` and `ISlotActorView` that binds to a model and renders color, sorting order, and pop VFX. `ActorKind` returns `Dynamic`. Uses `ColorableRendererExtensions.BindColor` to reactively update all `ColorableRenderer[]` children when the model's color changes (guarded by `model is IHasColor` cast). Caches `IBalloonViewBinding[]` and `IBalloonVariant` in `Awake()`. Exposes `Variant` for `BalloonSpawner`. `TweenTracker` and `ItemDisplayService` are `[SerializeField]` references |
| `Controller/` | `BalloonController` — wires model to view and handles hit/deflect/pop routing via `msg.Outcome` switch; `BalloonBalancer` — rebalances the grid after each turn |
| `Spawner/` | `BalloonSpawner` — creates balloon lines at game start and after each projectile death; `BalloonPoolChannel` — pool channel using `InjectingPoolChannel` with `IObjectResolver.Instantiate` |
| `Type/` | `BalloonType` enum (`Simple`, `Tough`, `Unbreakable`, `BubbleCluster`), `IBalloonVariant`, `IBalloonViewBinding`, `ColorableBalloonVariant`, `SimpleBalloonVariant`, `ToughBalloonVariant`, `SoapBubbleClusterVariant` — per-prefab variant components that initialize type and color on the model. `IBalloonViewBinding` is a secondary interface that any prefab component can implement to receive `Bind(model, disposables)` calls from `BalloonView` — used by `ToughBalloonVariant` to drive shader damage state and by `SoapBubbleClusterVariant` to drive cluster count, time, and rotation (see `Type/README.md`) |
| `BalloonLifetimeScope` | Legacy scope on balloon prefabs — `autoRun` disabled in Inspector; injection handled by `InjectingPoolChannel` |

`View/` also contains `SpriteColorableRenderer` (`ColorableRenderer<SpriteRenderer>` — sets `renderer.color`) and `ParticleColorableRenderer` (`ColorableRenderer<ParticleSystem>` — sets `startColor` and immediately recolors live particles via `Clear`+`Play`).

### Rendering and GPU instancing

Balloon materials (`BalloonMaterial`, `SpecularBalloonBlur`, etc.) have GPU instancing enabled. Per-balloon color is applied via `SpriteRenderer.color`, which Unity streams to the shader as `unity_SpriteRendererColorArray` in the instancing buffer. All 6 custom shaders follow the `UnitySprites.cginc` pattern with a `_RendererColor` fallback for non-SpriteRenderer components. Balloon sprites are packed into `Balloons.spriteatlas` for draw call reduction. See `Assets/Shaders/BalloonParty/README.md` for the instancing policy and `Assets/Sprites/README.md` for atlas groupings.

## Behaviour

A balloon knows its type, how many hits it can absorb, where it sits in the grid, whether it has settled into position, and whether it carries an item. `BalloonModel` additionally knows its color (`IPaintable`). When any reactive property changes, the view updates automatically via UniRx subscriptions.

`BalloonController` receives `ActorHitMessage`s filtered by identity (`msg.Actor is IBalloonModel && ReferenceEquals`) and routes on the pre-computed `msg.Outcome`:

- **`PassThrough`** — balloon survived; projectile continues. `HitsRemaining` was already decremented inside `BalloonModelBase.EvaluateHit`. Crack animation is driven reactively by `HitsRemaining`.
- **`Deflect`** — balloon survived and projectile bounced. Publishes `BalloonDeflectedMessage` and `BalloonNudgeMessage(Deflect)`.
- **`Pop`** — balloon destroyed: plays VFX, removes from grid, returns to pool (deferred if the balloon carries an item). Overkill damage is not tracked.
- **`Absorb`** — projectile is destroyed by the actor. `ProjectileView.OnAbsorb` handles the terminal path — publishes `ActorHitMessage(Absorb)` and destroys the projectile. The absorbing actor is not removed from the grid.

`EvaluateHit` on `BalloonModelBase` is **state-mutating** — it decrements `HitsRemaining` (or zeroes it for `Piercing`) and returns the outcome in a single call. `ProjectileView` calls it once and embeds the outcome in `ActorHitMessage`; `BalloonController` reads `msg.Outcome` without calling `EvaluateHit` again.

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
- **ProjectileView** — calls `EvaluateHit(new DamageContext(1))` on collision, embeds pre-computed outcome in `ActorHitMessage`
- **ScoreController** — on each `ActorHitMessage` with `Outcome == Pop` or `PassThrough`, casts actor to `IHasScoreColor` and calls `ResolveScoreAttribution(msg.Context, attributions)`. All returned `ScoreAttribution` entries are published together as one scatter group — a single `GroupSize` covers every point from the pop so the UI fans them out together.
- **PoolManager** — `BalloonView` instances are pooled via `BalloonPoolChannel`; pop VFX pooled via `ParticlePoolChannel`
- **TweenTracker** — generic `MonoBehaviour` in `Shared/` that manages tween sequencing (append, replace, kill)
- **Item system** — balloons are one host for the game-wide item system (in `Item/`). `BalloonView` calls `ItemDisplayService.Bind()`/`Unbind()` to connect item visuals; the item system itself has no knowledge of balloons
- **BalloonsConfiguration** — balloon prefab entries, spawn animation timing, balance timing, nudge defaults, pop VFX
- **GamePalette** — resolves color name → `UnityEngine.Color` for renderer tinting
