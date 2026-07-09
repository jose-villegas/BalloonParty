# Slots

The grid that holds all actors in play. Actors are anything that can occupy a slot — balloons today, static obstacles and spawners in the future.

## Actor abstraction

Every grid occupant is an `ISlotActor`. The grid never speaks directly in balloon types.

### Core interfaces

| Interface | Purpose |
|---|---|
| `ISlotActor` | Read-only grid contract — `SlotIndex : Vector2Int`, `Kind : SlotActorKind` |
| `IWriteableSlotActor` | Writable counterpart — adds `new Vector2Int SlotIndex { get; set; }` |
| `IDynamicSlotActor` | Extends `ISlotActor` — adds `IReadOnlyReactiveProperty<bool> IsStable`. Actors that can move or be in a transitioning state implement this. Static actors do NOT. |
| `IWriteableDynamicSlotActor` | Extends both `IDynamicSlotActor` and `IWriteableSlotActor` — adds writable `ReactiveProperty<bool> IsStable` |
| `ISlotActorView` | View contract — `transform`, `TweenTracker`, `ActorKind` |
| `SlotActorKind` | Enum — `Dynamic` (balancer can relocate, contributes weight) or `Static` (fixed, contributes weight, balancer skips) |

### Capability interfaces

Optional traits actors can advertise to consumers:

| Interface | Meaning |
|---|---|
| `IHasColor` | Actor has a read-only reactive color identity (string) — consumed by views and item VFX for tinting |
| `IPaintable` | Extends `IHasColor` — actor's color can be overwritten by the Paint item. Exposes `ReactiveProperty<string> Color`. Actors that don't implement this are immune to paint |
| `IHasScore` | Actor awards score when destroyed |
| `IHasScoreColor` | Score attribution contract — `ResolveScoreAttribution(DamageContext, IList<ScoreAttribution>)` called once by `ScoreController` at destruction. Implementations append `(colorId, points, breaksStreak)` entries. Consumed **only** by `ScoreController`; views never read it |
| `IHasNudge` | Actor participates in the nudge force system |
| `IHitable` | Actor participates in the hit system — `EvaluateHit(DamageContext)` returns a `HitOutcome` and is responsible for mutating any internal state (e.g. decrementing health). Takes a `DamageContext` containing `Damage` (int), `DamageFlags` (`Normal` or `Piercing`), and `SourceColorId` (the color of the hitting projectile — used by `Inherited` score strategies). `Piercing` forces an immediate `Pop` regardless of `HitsRemaining` |
| `IHasDurability` | Extends `IHitable` — actor also tracks `HitsRemaining`. Removal is determined by `HitsRemaining.Value <= 0` after `EvaluateHit` returns |
| `IHasItemSlot` | Actor can host an item — extends `IHasColor` (item visuals always tint to the host color). Exposes `IReadOnlyReactiveProperty<ItemType> Item` |
| `IPassThrough` | Actor's slot can be crossed by animation paths (spawn entry, balance moves). Actors that do NOT implement this block traversal; rerouting is deferred to a future phase. |
| `IWashesProjectileColor` | Marker — a projectile that contacts this actor has its stolen colour reset to none, like a fresh launch (the soap-bubble cluster). Checked in `ProjectileHitResolver` |

### Hit types

`DamageContext` wraps damage with optional flags:

| Type | Description |
|---|---|
| `DamageContext` | Readonly struct — `int Damage` + `DamageFlags Flags` + `string SourceColorId`. Default `Flags = DamageFlags.Normal`, `SourceColorId = ""`. `SourceColorId` carries the palette color name of the projectile or item responsible for the hit — used by `UnbreakableBalloonModel` for score attribution to the source color, and by `ToughBalloonModel` when scattering score to random palette colors |
| `DamageFlags` | `[Flags]` enum — `Normal = 0`, `Piercing = 1 << 0`. `Piercing` bypasses `HitsRemaining` and forces `Pop` |

Paintability is expressed purely through types: a `BalloonModel` implements `IPaintable`; a `ToughBalloonModel` does not — no runtime flag needed.

**Rainbow ("all colours") is a colour, not a flag.** A rainbow balloon carries the reserved id `GamePalette.RainbowColorId` in its `IHasColor.Color` — there is no separate `IsRainbow` capability. Detect it with `IGamePalette.IsRainbow(colorId)`; branch behaviour on that (score wildcard, colour-steal skip, banded material) rather than reading an arbitrary spawn colour. Paint converts a target simply by recolouring it to the wildcard id.

`IHitable` vs `IHasDurability`:

| Actor | Implements | `EvaluateHit` behaviour |
|---|---|---|
| `BalloonModel` (soft) | `IHasDurability`, `IHasScoreColor` | `PassThrough` on survival, `Pop` on death; decrements `HitsRemaining`. `Piercing` flag → `Pop` immediately. No score attribution when `HitsRemaining > 0` after hit |
| `ToughBalloonModel` | `IHasDurability`, `IHasScoreColor` | `Deflect` on survival, `Pop` on death; decrements `HitsRemaining`. `Piercing` flag → `Pop` immediately. Score scattered to random palette colors on pop |
| `UnbreakableBalloonModel` *(Phase 7.5)* | `IHitable`, `IHasScoreColor` | Always `Deflect`; no `HitsRemaining`. `Piercing` forces `Pop`. Score attributed to source (projectile) color on pop |
| `BubbleClusterModel` | `IHasDurability`, `IHasScoreColor` | `PassThrough` on survival, `Pop` on death; decrements `HitsRemaining` (bubble count). Score scatters one entry per point of damage to random palette colors; `BreaksStreak = true` on all attributions |
| `DeflectorActorModel` *(Phase 8.2b)* | `IHitable` only | Always `Deflect`; no `HitsRemaining`; not a balloon |
| `AbsorberActorModel` *(Phase 8.2b)* | `IHitable` only | Always `Absorb`; kills the projectile |
| `GatekeeperActorModel` *(Phase 8.2c)* | `IHasDurability` | `Deflect` on survival, `Pop` on death; decrements `HitsRemaining`. Blocks a column until destroyed. |
| `StaticActorModel` | neither | No collider — not part of the hit pipeline |
| `PuffObstacleModel` | neither | Structural obstacle; `IPassThrough` — animation paths can cross it. `IClusterableSlotActor` — gains `ClusterId` linking to a visual `SlotCluster`. |

## Contents

| File / Folder | What it does |
|---|---|
| `Grid/` | `SlotGrid`, `SlotGridChangedEvent`, `SlotGridView`, `BalancePathHolder` — core grid data structure and balance transit tracking (namespace `BalloonParty.Slots.Grid`) |
| `Actor/` | Core actor interfaces, spawner, hit controller, and slot selection strategies — `ISlotActor`, `IWriteableSlotActor`, `IDynamicSlotActor`, `IWriteableDynamicSlotActor`, `ISlotActorView`, `SlotActorKind`, `StaticActorModel`, `StaticActorSpawner`, `GridActorHitController`, `ISlotSelectionStrategy`, `RandomSlotSelectionStrategy`, `ClusterSlotSelectionStrategy`, `SlotPlacementMode` (namespace `BalloonParty.Slots.Actor`) |
| `Actor/Cluster/` | Generic slot-cluster infrastructure — `SlotClusterRegistry<TModel>` (hex-adjacency flood-fill, merge/split, publishes `SlotClusterChangedEvent`), `SlotCluster`, `IClusterableSlotActor`, `ISlotClusterSource`, `ClusterView`, `ClusterViewController<TModel, TView, TSettings>`, `IClusterViewSettings` (namespace `BalloonParty.Slots.Actor.Cluster`) |
| `Actor/Archetype/` | Concrete grid actor models and the Puff/Bush cluster visual systems — see [Archetype README](Actor/Archetype/README.md) (namespace `BalloonParty.Slots.Actor.Archetype`) |
| `Capabilities/` | Optional capability interfaces — `IHasColor`, `IPaintable`, `IHasScore`, `IHasScoreColor`, `IHasNudge`, `IHasItemSlot`, `IHitable`, `IHasDurability`, `IPassThrough`, `HitOutcome`, `DamageContext`, `DamageFlags`, `ScoreAttribution` (namespace `BalloonParty.Slots.Capabilities`) |
| `Spawner/` | Spawner coordination — `IGridSpawner`, `SpawnStage`, `GridSpawnerCoordinator` (namespace `BalloonParty.Slots.Spawner`) |

## How it works

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by an actor model and its view. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from below), and what the best empty slot is for an actor to move into.

`OptimalNextEmptySlot` uses a recursive weight algorithm to decide between two candidate slots (directly above and diagonally above). The weight of a candidate = count of occupied slots in the tree above it. Higher weight = more support = preferred. On tie, the diagonal candidate wins (via `>=` comparison).

`Place` guards against double-occupation — if a slot is already occupied, the placement is rejected with an exception.

`BottomEmptySlotPerColumn` returns the lowest empty row index per column — used by `BalloonSpawner` when finding spawn targets for a new line.

`AllEmptySlots` yields every empty slot across the full grid — used by `StaticActorSpawner`.

`IsTraversable(col, row)` returns true if the slot is empty or the occupant implements `IPassThrough` — used by `ComputePath` to build spawn animation waypoints.

`ComputePath(source, target)` returns world-space waypoints along the straight-line grid path between two slot indices. Either endpoint may be outside grid bounds. Non-traversable in-bounds slots and in-transit balance slots emit warnings; rerouting is deferred.

## Balance Path Holder

`BalancePathHolder` tracks grid slots that are in-transit due to balance animations. When the balancer relocates a balloon from slot A to slot B, both A and B are reserved as in-transit under that actor. This lets `ComputePath` warn when a spawn animation path crosses a slot that a balance animation is currently traversing — even if the grid data already reflects the post-balance state.

Transit slots are tracked per-actor and released via `Release(actor)` when the balance animation's `OnComplete` fires. This means transit data persists across multiple balance passes — a second balance triggered after spawning does not erase transit from the first balance's still-running animations.

## Spawner Coordination

`GridSpawnerCoordinator` owns the sequencing of all grid population. Individual spawners (`StaticActorSpawner`, `BalloonSpawner`) implement `IGridSpawner` and declare their priority via `SpawnStage`. The coordinator:

1. **Waits** on an injected `NavigationReadyGate(Game)` — nothing spawns until the game scene is fully active. (The gate reads an injected `INavigation`, so it's unit-testable without touching the static `Navigation`.)
2. **Groups** all registered spawners by `SpawnStage`, sorted ascending.
3. **Runs** each group with `UniTask.WhenAll` (parallel within a stage) before advancing to the next stage.

```
NavigationReadyGate opens
        │
        ▼
SpawnStage.StaticActors (0)  ──→  StaticActorSpawner.SpawnAsync
        │
        ▼
SpawnStage.DynamicActors (50) ──→  (future GridSpawner)
        │
        ▼
SpawnStage.BalloonActors (100) ──→  BalloonSpawner.SpawnAsync
```

Spawners are injected as `IEnumerable<IGridSpawner>` via VContainer's collection injection — each registered with `.As<IGridSpawner>()` in `GameLifetimeScope`.

`StaticActorSpawner.Start()` registers the pool immediately (synchronous, runs at scene start before the gate opens). `BalloonSpawner.Start()` starts pre-warming pools asynchronously and stores the task; `SpawnAsync` awaits it before populating the grid — so pre-warm and navigation wait overlap rather than serialize.

## Interactions

- **BalloonSpawner** — calls `Balance()` once before all line spawns so existing balloons consolidate upward first; then calls `Place` for each new balloon into remaining empty slots; uses `ComputePath` for spawn animation waypoints; publishes `BalanceBalloonsMessage` after spawning for a final settling pass
- **StaticActorSpawner** — iterates `GridActorConfiguration.Entries`, spawns the per-type count resolved for the active level (`IActiveLevelParameters.Current.TryGetGridActorCount` — absent types are gated out), selects slots via the entry's `ISlotSelectionStrategy`, and calls `Place` per slot
- **BalloonController** — calls `Remove` when a balloon is popped; subscribes to `ActorHitMessage`
- **BalloonBalancer** — reads occupancy to find gaps; skips `Static` actors (or actors that are not `IDynamicSlotActor`); calls `Remove` + `Place` to relocate dynamic actors; uses `ViewAt` to reach views for animation. `Balance()` is `internal` for synchronous pre-spawn consolidation
- **NudgeService** — uses `GetNeighbors` and `IndexToWorldPosition` to direct nudge animations; filters by `IHasNudge`
- **PaintItemHandler** — uses `HexNeighborIndices`; casts `At()` result to `IPaintable` for painting
- **IGameConfiguration** — provides `SlotsSize`, `SlotSeparation`, `SlotsOffset` for grid construction and position calculations

## Slot Selection Strategies

`ISlotSelectionStrategy` abstracts how `StaticActorSpawner` picks which empty slots an actor type should occupy. Each `GridActorPrefabEntry` declares a `SlotPlacementMode` enum that maps to a strategy. Strategies are lazily cached — one instance per mode.

| Strategy | Mode | Behaviour |
|---|---|---|
| `RandomSlotSelectionStrategy` | `Random` | Shuffles all empty slots and picks the first N. Default for actor types with no spatial preference. |
| `ClusterSlotSelectionStrategy` | `Cluster` | Seeds a cluster at a random slot, then greedily expands into hex neighbors. Each cluster is capped at `MaxPerCluster` slots (from `GridActorPrefabEntry`). When a cluster is full or has no more neighbors, the next seed is biased toward the opposite side of the grid — maximising the minimum distance to all previous cluster centroids. Produces multiple small, spatially distributed clusters. |

