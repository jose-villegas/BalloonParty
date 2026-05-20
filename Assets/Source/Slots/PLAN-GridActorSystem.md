# Grid Actor System — Feature Plan

> Living plan for growing the SlotGrid into a multi-actor grid that supports procedural level
> generation, difficulty scaling, and actor-specific behaviors.
> Rename from `PLAN-SlotActors.md`. Update as phases complete.

---

## Motivation

The original goal was to abstract `SlotGrid` away from balloon-specific types — that foundation
is complete (Phases 1–5). The plan now covers what to build on top of it:

- **Static actors** — inert obstacles that block slots and stress-test the balancer
- **Durability abstraction** — generalise hit points from `IBalloonModel` to any actor
- **Grid/Level Spawner** — evolve `BalloonSpawner` into a procedural spawner that drives
  difficulty and eventually ties to level progression
- **Behavior-bound actors** — actors with active behaviors tied to game events (broad future space)

---

## Key Design Decisions

### Mobility axis: `SlotActorKind` ✅ Done

```
SlotActorKind
├── Dynamic   — balancer can relocate, contributes weight
└── Static    — fixed in place, contributes weight, balancer skips
```

`IsStable` belongs on dynamic actors only — static actors are always stable by definition
and the property is meaningless on them. In Phase 7, `IsStable` moves off `ISlotActor`
and onto a new `IDynamicSlotActor` interface:

```csharp
// ISlotActor — minimal base for any grid occupant
public interface ISlotActor
{
    IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
    SlotActorKind Kind { get; }
    // IsStable removed — only dynamic actors have animation state
}

// IDynamicSlotActor — actor that can be in a transitioning state
public interface IDynamicSlotActor : ISlotActor
{
    IReadOnlyReactiveProperty<bool> IsStable { get; }
}
```

Static actors implement `ISlotActor` directly. Dynamic actors implement `IDynamicSlotActor`.
Consumers (balancer, nudge) cast to `IDynamicSlotActor` instead of checking `Kind` + `IsStable`
separately — one check instead of two, and it's impossible to accidentally read animation
state from a static actor.

> **Future consideration** — `IDynamicSlotActor` at the type level already implies
> `Kind == Dynamic`. `SlotActorKind` on `ISlotActor` may become redundant once the actor
> hierarchy matures. Kept for now because `SlotGrid.IsKind` and `BalloonBalancer` use it
> as a runtime query without casting.
>
> **Future consideration — runtime kind transitions** — `Kind` is currently a plain
> getter, which works for permanently-typed actors. If an actor needs to transition
> between static and dynamic at runtime (e.g. a tile that moves and then locks in place),
> `Kind` should become `IReadOnlyReactiveProperty<SlotActorKind>` — consistent with how
> `SlotIndex` and `IsStable` already work. The two concerns coexist cleanly: `Kind`
> (reactive) expresses the actor's current mobility role; `IDynamicSlotActor` expresses
> that the actor *also* has animation state (`IsStable`) and is the right interface for
> any actor that ever moves — even if it temporarily locks in.
>
> A **Decorator** is the right fit when the kind change is imposed externally and the
> actor shouldn't know about it (e.g. a game-mode controller that temporarily freezes all
> dynamic actors). The actor-owns-state pattern used elsewhere in this codebase favours
> reactive `Kind` for actor-driven transitions and Decorator only for external imposition.

### Items and grid actors are separate hierarchies ✅ Done

| Concern | Items | Grid actors |
|---|---|---|
| **Slot ownership** | Hosted *on* a balloon — no slot of their own | Occupy their *own* slot |
| **Lifecycle** | Ephemeral — activate once, done | Persistent — live across turns |
| **Trigger** | `ActorHitMessage → ItemActivator` | Varied (hit, turn, proximity, timer…) |
| **Identity** | `ItemType` enum on a balloon model | Has its own model and view |

### Durability axis: `IHitable` + `IHasDurability` (Phase 7)

Not all actors respond to hits the same way, and not all actors that respond have health.
Two separate capability interfaces express this cleanly:

#### `IHitable` — "I declare what happens to the projectile when I'm hit"

```csharp
public interface IHitable
{
    HitOutcome EvaluateHit(int damage);
}
```

Any actor that implements `IHitable` is an active participant in the hit system.
Actors **without** `IHitable` have no collider or are simply not part of the hit pipeline
(no convention-based fallback needed — the projectile doesn't trigger on them at all).

#### `IHasDurability` — "I can also be worn down over hits"

```csharp
public interface IHasDurability : IHitable
{
    // Read-only to callers — mutated by EvaluateHit internally
    IReadOnlyReactiveProperty<int> HitsRemaining { get; }
}
```

`IHasDurability` extends `IHitable`: every durable actor is hitable, but not every hitable
actor has health. The inherited `EvaluateHit` is responsible for decrementing
`HitsRemaining`; the caller reads `HitsRemaining.Value <= 0` afterwards to decide removal.

#### Why the split matters

| Actor | Implements | `EvaluateHit` returns | `HitsRemaining` |
|---|---|---|---|
| Standard balloon (1 hit) | `IHasDurability` | `Pop` on kill | decremented → 0 |
| Tough balloon (3 hits) | `IHasDurability` | `Deflect` × 2, `Pop` on kill | decremented per hit |
| Unbreakable balloon | **`IHitable` only** | Always `Deflect` | — |
| Non-deflecting tough actor | `IHasDurability` | Always `Pop` | decremented per hit |
| Indestructible absorbing wall | **`IHitable` only** | Always `Absorb` | — |
| Decorative / no-collider actor | neither | — (never called) | — |

`HitsRemaining` is **removed from `IBalloonModel`**. Only concrete implementations that
opt into `IHasDurability` carry it. The unbreakable balloon becomes `UnbreakableBalloonModel`
— implements `IHitable`, no `HitsRemaining`. `BalloonPrefabEntry` selects the model class
at spawn time.

#### Unified hit routing

```csharp
if (msg.Actor is IHitable hitable)
{
    var outcome = hitable.EvaluateHit(msg.Damage);
    // Deflect → projectile bounces
    // Pop     → projectile continues
    // Absorb  → ForceKill() on projectile, turn ends

    if (msg.Actor is IHasDurability durable && durable.HitsRemaining.Value <= 0)
    {
        // remove actor from grid regardless of which outcome was returned
    }
}
```

No convention-based fallback. Every actor that can be hit declares its response explicitly.

#### `HitOutcome` values

```
HitOutcome
├── Deflect  — projectile changes direction and continues flying
├── Pop      — projectile continues flying, direction unchanged
└── Absorb   — projectile is killed immediately — turn ends, new turn starts
```

Actor removal is always a separate step: check `HitsRemaining.Value <= 0`
*after* `EvaluateHit` returns any outcome.

### Procedural generation axis (Phase 8)

`BalloonSpawner` today knows only one actor type and is driven by fixed config lines.
The future `GridSpawner` / `LevelSpawner` needs to:

- Manage a **pool of actor types** (balloons, static obstacles, future actors)
- Drive **placement procedurally** — slot selection, actor distribution, density
- Expose **difficulty knobs** that the level system can control:
  - Overall grid density (% of slots filled)
  - Per-actor-type weight and max-count
  - Static obstacle ratio relative to balloons
- Eventually couple to **level progression** so difficulty evolves across levels

This is an evolution, not a rewrite — `BalloonSpawner` stays functional until the new
spawner is ready, then they swap.

### Behavior-bound actors (Phase 9, broadly defined)

Actors with active behaviors triggered by game events — turn ticks, neighbor pops,
projectile proximity, etc. The grid provides the `Place` / `Remove` / `At` API; the
actor's controller subscribes and acts.

Examples of behaviors being considered (not committed):
- **Mover** — periodically slides adjacent balloons to empty nearby slots
- **Recolorer** — changes neighbor balloon colors on a trigger (turn, hit proximity)
- **Absorber** — consumes adjacent balloons to restore its own durability or grant shields
- **Spawner** — places new balloons into adjacent empty slots each turn

These differ from items: they occupy their own permanent slot, persist across turns, and
trigger independently without any `ItemActivator` routing. Architecture is intentionally
left underspecified until a concrete first actor is chosen.

---

## Interface Design ✅ Done

Full interface design documented in the original plan (Phases 1–5, all complete).
Key types in `Slots/`: `ISlotActor`, `IWriteableSlotActor`, `ISlotActorView`,
`SlotActorKind`, `IHasColor`, `IHasWriteableColor`, `IHasScore`, `IHasNudge`.
`ActorHitMessage` in `Shared/Messages/` replaces the old `BalloonHitMessage`.

---

## Phases

> **TDD rule for all future phases:** write the failing tests listed under each phase
> *before* writing any implementation. Tests that reference not-yet-existing types will
> fail at compile time first — that's fine. Stub the type (empty class, no logic), watch
> the test compile but fail at runtime, then drive the implementation to green.
> Follow the same conventions as `Assets/Tests/README.md`: real objects over mocks for
> plain C# types; NSubstitute only for interfaces and ScriptableObjects.

### Phase 1 — Define actor interfaces ✅ Complete

New files: `ISlotActor`, `IWriteableSlotActor`, `ISlotActorView`, `SlotActorKind`,
`IHasColor`, `IHasWriteableColor`, `IHasScore`, `IHasNudge`, `ActorHitMessage`.
Existing balloon types widened to conform; `BalloonHitMessage` removed.

### Phase 2 — Refactor `SlotGrid` to use actor interfaces ✅ Complete

Internal arrays widened to `IWriteableSlotActor[,]` / `ISlotActorView[,]`.
New methods: `ActorAt<T>`, `ActorViewAt<T>`, `IsKind`.

### Phase 3 — Update consumers ✅ Complete

All `BalloonHitMessage` publishers/subscribers migrated to `ActorHitMessage`.
`BalloonBalancer` skips `Static` actors. `PaintItemHandler` filters by `IHasWriteableColor`.

### Phase 4 — Update tests ✅ Complete

`SlotGridTests` tests `IsKind`. `ScoreControllerTests` tests filters on capability interfaces.

### Phase 5 — Update documentation ✅ Complete

`Slots/README.md`, `Balloon/README.md`, `ARCHITECTURE.md` updated.

---

### Phase 6 — Static actor evaluation *(next iteration)*

**Goal:** Place a handful of inert static actors in the grid and observe how the balancer
handles them in practice. This is deliberately small — no special visuals, no damage
response, no config editor. The purpose is to learn, not to ship.

#### Scope

- `StaticActorModel : IWriteableSlotActor` — minimal concrete model, `Kind == Static`.
  No `IHasDurability` (indestructible), no `IHasColor`, no `IHasScore`.
- `StaticActorView : MonoBehaviour, ISlotActorView` — placeholder view. Can be a visible
  solid-coloured sprite or Editor-only gizmo sphere. Pooled via `InjectingPoolChannel`.
- `StaticActorSpawner : IStartable` — places N random static actors at game start by
  sampling from **all empty slots** across the full grid. Count configured in `GameConfiguration`
  (two fields: `MinStaticActors`, `MaxStaticActors`) so it can be tweaked per run without
  code changes.
- No config SO yet — values live in `GameConfiguration` until the SO is justified.
- No item interaction — projectile hits deflect off static actors unchanged
  (no `IHasDurability`, so no hit outcome evaluated).

#### What to evaluate

| Question | How to observe |
|---|---|
| Does the balancer skip static actors? | `BalloonBalancer` logs + visual inspection |
| Do static actors count as support for balloons above them? | Weight calculation — balloons above statics should not fall |
| Can balloons fill in around statics correctly? | Spawn multiple lines, watch gaps close |
| Do items interact cleanly (paint, lightning, bomb)? | Paint skips non-`IHasWriteableColor`; lightning skips non-`IHasColor`; bomb nudges only `IHasNudge` |
| Any edge cases in edge/corner static positions? | Manual testing |

#### Files

| File | Location | What it does |
|---|---|---|
| `StaticActorModel.cs` | `Slots/` | Minimal `IWriteableSlotActor`, `Kind = Static` |
| `StaticActorView.cs` | `Slots/` | Placeholder `ISlotActorView` MonoBehaviour |
| `StaticActorSpawner.cs` | `Slots/` | `IStartable` — places random static actors at start |
| `StaticActorPoolChannel.cs` | `Slots/` | `InjectingPoolChannel<StaticActorView>` |

#### Failing tests — write first

New fixture **`StaticActorTests`** in `Assets/Tests/EditMode/Slots/`:

```
StaticActorModel_KindIsStatic
  — new StaticActorModel().Kind == SlotActorKind.Static
  — fails at compile until StaticActorModel exists
```

Additions to existing **`SlotGridTests`**:

```
IsUnbalanced_BalloonAboveStaticActor_ReturnsFalse
  — place StaticActorModel at (2,0); place BalloonModel at (2,1); IsUnbalanced(2,1) == false
  — verifies static actors count as structural support, same as any occupant

IsUnbalanced_BalloonAboveStaticActor_DiagonalSupport_ReturnsFalse
  — even-row balloon at (2,2) with StaticActorModel at (1,1) as diagonal support
  — ensures staggered-grid support logic works with non-balloon actors
```

New fixture **`StaticActorSpawnerTests`** in `Assets/Tests/EditMode/Slots/`:

```
Spawn_PlacesExactCount_WhenGridHasEnoughEmptySlots
  — configure Min=Max=3; call Start(); grid has 3 static actors placed
  — fails at compile until StaticActorSpawner exists

Spawn_PlacedActors_AreAllStatic
  — all actors placed by the spawner have Kind == Static

Spawn_DoesNotExceedAvailableSlots
  — grid has only 2 empty slots; configure Min=Max=5; only 2 actors placed, no exception
```

> `StaticActorSpawner` can be tested by passing a real `SlotGrid` (mock `IGameConfiguration`)
> and a mock `PoolManager` that returns a minimal `StaticActorView`-like stub. No DOTween,
> no MonoBehaviour lifecycle needed for placement logic.

---

### Phase 7 — `IHitable` + Durability abstraction *(next iteration)*

Introduce `IHitable` as the base hit-response capability and `IHasDurability` extending
it for actors that also track damage. Lift both from `IBalloonModel` into `Slots/` and
move `HitOutcome` there too so all hit-system types live in the shared layer.

#### Changes

- Move `HitOutcome` enum to `Slots/` and add `HitOutcome.Absorb`.
- Add `IHitable` to `Slots/`.
- Add `IHasDurability : IHitable` to `Slots/`.
- **Remove `IsStable` from `ISlotActor`** — add `IDynamicSlotActor : ISlotActor` with
  `IsStable`. Add `IWriteableDynamicSlotActor : IDynamicSlotActor, IWriteableSlotActor`
  with writable `ReactiveProperty<bool> IsStable`. `IBalloonModel` extends
  `IDynamicSlotActor`; balloon models satisfy the contract with no logic changes.
- **Remove `HitsRemaining` from `IBalloonModel`** — it moves to `IHasDurability`.
- **Add `UnbreakableBalloonModel : IBalloonModel, IHitable`** — `EvaluateHit` always
  returns `Deflect`, no `HitsRemaining`. `BalloonPrefabEntry` selects the model class
  at spawn time.
- `BalloonModel` and `ToughBalloonModel` add `: IHasDurability` — no logic changes.
- `IBalloonModel` adds `: IHitable` (via `IHasDurability` on durable types, directly on
  `UnbreakableBalloonModel`).
- **Caller pattern** — consumers check `IDynamicSlotActor` for stability, `IHitable` for
  hit outcome, `IHasDurability` for removal:
  ```csharp
  if (msg.Actor is IHitable hitable)
  {
      var outcome = hitable.EvaluateHit(msg.Damage);
      if (msg.Actor is IHasDurability durable && durable.HitsRemaining.Value <= 0)
          // remove actor from grid
  }
  // Balancer / nudge:
  if (actor is IDynamicSlotActor dynamic && dynamic.IsStable.Value) { ... }
  ```
- `ProjectileView` handles `Absorb` outcome via `ForceKill()` → `ProjectileDestroyedMessage`.
- Writable `ReactiveProperty<int> HitsRemaining` stays on `IWriteableBalloonModel` for
  spawn-time writes on durable types. `IHasDurability` exposes only the read-only view.

#### Consumer migration

| Consumer | Before | After |
|---|---|---|
| `BalloonController` | `msg.Actor is IBalloonModel` for all hit routing | `msg.Actor is IHitable` for outcome; `msg.Actor is IHasDurability` for removal; still downcasts to `IBalloonModel` for item/pool logic |
| `BalloonBalancer` | `actor.Kind == Static → skip`; `actor.IsStable` | `actor is not IDynamicSlotActor dynamic → skip`; `dynamic.IsStable` |
| `NudgeService` | `IHasNudge` filter + `actor.IsStable` | `IHasNudge` filter + `actor is IDynamicSlotActor d && d.IsStable` |
| `ScoreController` | `msg.Actor is not IBalloonModel \|\| EvaluateHit != Pop` | `msg.Actor is not IHitable h \|\| h.EvaluateHit(…) != Pop`; `Absorb` never scores |
| `ProjectileView` | `ProjectileDestroyedMessage` only on shield depletion | Also on `ForceKill()` for `Absorb` outcome |
| `ItemActivator` | `msg.Actor is IBalloonModel` | Unchanged |

#### Failing tests — write first

Additions to existing **`BalloonModelTests`** (regression guard):

```
BalloonModel_ImplementsIDynamicSlotActor
  — (new BalloonModel()) is IDynamicSlotActor == true
  — fails at compile until IDynamicSlotActor exists and IBalloonModel extends it

BalloonModel_ImplementsIHitable
  — (new BalloonModel()) is IHitable == true

BalloonModel_ImplementsIHasDurability
  — (new BalloonModel()) is IHasDurability == true

BalloonModel_EvaluateHit_IntermediateHit_StillDeflects
  — BalloonModel with HitsRemaining=3; EvaluateHit(1) == Deflect; HitsRemaining == 2

BalloonModel_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero
  — BalloonModel with HitsRemaining=1; EvaluateHit(1) == Pop; HitsRemaining == 0
```

Additions to existing **`StaticActorTests`**:

```
StaticActorModel_IsNotIDynamicSlotActor
  — (new StaticActorModel()) is IDynamicSlotActor == false
  — static actors have no animation state
```

New fixture **`UnbreakableBalloonModelTests`**:

```
UnbreakableBalloonModel_IsIHitable
  — (new UnbreakableBalloonModel()) is IHitable == true
  — fails at compile until UnbreakableBalloonModel exists

UnbreakableBalloonModel_IsNotIHasDurability
  — (new UnbreakableBalloonModel()) is IHasDurability == false

UnbreakableBalloonModel_EvaluateHit_AlwaysDeflects_RegardlessOfDamage
  — EvaluateHit(1) == Deflect; EvaluateHit(99) == Deflect
  — no magic number check — just the declared return value
```

New fixture **`HitableTests`** in `Assets/Tests/EditMode/Slots/`:

```
HitOutcome_AbsorbVariantExists
  — HitOutcome.Absorb compiles and has a distinct value from Deflect and Pop

IndestructibleAbsorbingActor_IsIHitable_NotIHasDurability
  — IHitable-only impl, not IHasDurability — same pattern as unbreakable balloon

IndestructibleAbsorbingActor_EvaluateHit_ReturnsAbsorb

NonDeflectingActor_EvaluateHit_AlwaysReturnsPop_AndDecrementsHits
  — IHasDurability impl with HitsRemaining=3, always returns Pop
  — EvaluateHit(1) == Pop; HitsRemaining == 2

NonDeflectingActor_HitsRemainingReachesZero_OnFinalHit
  — HitsRemaining=1; EvaluateHit(1); HitsRemaining == 0

HitRouting_IHitableWithoutIHasDurability_RemovalCheckSkipped
  — actor is IHitable but not IHasDurability
  — `msg.Actor is IHasDurability` == false → no removal attempt
```

Additions to existing **`ScoreControllerTests`**:

```
OnActorHit_IHitable_WithIHasColorAndIHasScore_PopOutcome_PublishesScore
  — any IHitable + IHasColor + IHasScore actor that returns Pop scores normally

OnActorHit_AbsorbOutcome_DoesNotScore
  — actor returns Absorb; ScoreController publishes no ScorePointMessage
```

> `HitableTests` uses minimal hand-written inner classes —
> `class AbsorbWall : IHitable { public HitOutcome EvaluateHit(int d) => HitOutcome.Absorb; }`
> No NSubstitute needed.

---

### Phase 8 — Grid Spawner / Level Spawner

Evolve `BalloonSpawner` into a `GridSpawner` that manages multiple actor types,
drives procedural placement, and eventually couples to level difficulty.

#### Spawner coordination — known limitation from Phase 6

`StaticActorSpawner` and `BalloonSpawner` are currently coordinated implicitly:
`StaticActorSpawner.Start()` is **synchronous** — it runs inline during VContainer init and
completes before any frame yields. `BalloonSpawner.PrewarmAndPopulateAsync` yields immediately
for pool prewarming and then waits for `NavigationState.Game`, so balloons are always placed
after statics. This holds as long as `StaticActorSpawner.Start()` never yields. The contract
is documented in a code comment, but it is still implicit.

**When Phase 8 introduces `GridSpawner`**, replace the implicit ordering with a
`GridSpawnerCoordinator : IStartable` that:

- Owns a single `WaitAndSpawnAsync` — one navigation-state wait shared across all spawners
- Calls spawners in explicit priority order (e.g. `SpawnPriority.Static` before `SpawnPriority.Dynamic`)
- Gives Phase 8's `GridSpawner` a clean API to participate in the sequence

```
IGridSpawner (future)
├── Priority: int
└── SpawnAsync(CancellationToken): UniTask

GridSpawnerCoordinator : IStartable (future)
├── Collects all IGridSpawner registrations
├── Waits for NavigationState.Game
└── Calls spawners sorted by Priority
```

Until then, `StaticActorSpawner.Start()` must **not yield** — see the coordination
contract comment in the class.

#### Design principles

- `BalloonSpawner` stays operational throughout this phase. The new spawner is built
  alongside and plugged in when ready — no flag day.
- Actor type selection is **weighted and capped** — same pattern as `BalloonsConfiguration.PickRandom`.
  The weight and cap live in a per-actor config entry.
- Procedural generation operates on **slot selection rules** — not randomness alone.
  Rules can include: row bias, neighbor constraints, min-distance between statics, etc.
  Start with the simplest rule set (uniform random over empty slots) and add rules as
  gameplay feedback demands.

#### Difficulty sliders

The spawner exposes difficulty parameters that the level system can dial:

| Knob | Effect | Range |
|---|---|---|
| `GridDensity` | % of slots filled per spawn pass | 0–1 |
| Static obstacle ratio | Weight share of static actors vs balloons | 0–1 |
| Per-actor-type weight overrides | Increase tough balloon weight at high levels | Per entry |
| Min empty slots preserved | Ensure projectile has space to navigate | Absolute count |

These do **not** live as raw floats — they map to named difficulty presets or are computed
from a `DifficultyProfile` SO that the level system sets. The level system owns the
profile; the spawner reads it.

#### Level coupling

`DifficultyProfile` (new SO, introduced when the first difficulty axis is needed):

```
DifficultyProfile
├── GridDensity: float
├── StaticRatio: float             (0 = no statics, 1 = max statics)
├── ActorWeightOverrides: entry[]  (per-type multiplier on base weight)
└── MinEmptySlots: int
```

`ScoreController` (or a thin `DifficultyService`) updates the active profile when
`ScoreLevelUpMessage` is received. The spawner reads the profile on each spawn pass —
no subscriber wiring needed beyond the level-up message.

#### Migration path

1. Add `GridSpawner` alongside `BalloonSpawner` — both registered, `BalloonSpawner` still
   runs game-start spawning.
2. Move balloon spawn logic into `GridSpawner`; `BalloonSpawner` delegates or retires.
3. Add static actor spawn support to `GridSpawner`.
4. Add `DifficultyProfile` SO and level-up update hook.
5. Wire procedural slot selection rules incrementally.

#### Failing tests — write first

New fixture **`GridSpawnerTests`** in `Assets/Tests/EditMode/Game/` (or `Slots/`):

```
PickActorType_SingleEntry_AlwaysReturnsIt
  — one actor config entry with MaxCount=0 (unlimited); PickActorType always returns it
  — same deterministic pattern as BalloonsConfigurationTests.SingleCandidate
  — fails at compile until GridSpawner / actor config entry exists

PickActorType_AllEntriesAtMax_ReturnsNull
  — all entries MaxCount reached; PickActorType returns null
  — matches BalloonsConfigurationTests.AllAtMax pattern

PickActorType_CappedEntryExcluded_OtherSelected
  — entry A at max, entry B not; PickActorType always returns B
  — verifies cap filtering works across actor types, not just balloon types

PickActorType_RespectsWeight_HighWeightAlwaysWinsWithSingleCandidate
  — same single-candidate determinism pattern; avoids Random.Range flakiness
```

New fixture **`DifficultyProfileTests`** (when `DifficultyProfile` SO is introduced):

```
DifficultyProfile_Default_HasNoWeightOverrides
  — freshly created profile has empty ActorWeightOverrides

ApplyProfile_IncreasesWeightForTargetType
  — entry base weight 1; profile override multiplier 3; effective weight == 3
  — verifies multiplier is applied before PickActorType runs

ApplyProfile_DoesNotAffectUnconfiguredEntries
  — entry not in override list keeps its base weight unchanged
```

> All `GridSpawnerTests` use a mock `SlotGrid` (via `Substitute.For<IGameConfiguration>`)
> and test only the pure selection and weighting logic — no pool manager, no async,
> no MonoBehaviours. Same pattern as `BalloonsConfigurationTests`.

---

### Phase 9 — Behavior-bound actors *(future, broadly defined)*

Actors with game-event-driven behaviors that modify the grid state. This phase is
deliberately not architected yet — the specific actor to build first will determine
what controller patterns are needed.

**What distinguishes these from items:**
- They occupy their own permanent slot (not passengers on balloons)
- They persist across turns
- They trigger independently — no `ItemActivator` routing

**Ideas under consideration** (not committed to any):

| Concept | Trigger | Grid effect | Hit outcome |
|---|---|---|---|
| Mover | Turn tick | Slides adjacent dynamic balloons to a new empty slot | Deflect |
| Recolorer | Neighbor pop | Changes adjacent balloon colors to a configured color | Deflect |
| Absorber | Hit | Consumes adjacent balloons to restore its own `HitsRemaining` | **Absorb** — kills the projectile, ends the turn |
| Spawner | Turn tick | Places a new balloon into an adjacent empty slot | Deflect |
| Repulsor | Projectile proximity | Nudges nearby balloons outward each turn | Deflect |

`HitOutcome.Absorb` is the natural fit for actors that should feel "dangerous" to hit — the player must navigate around them rather than bouncing off them cheaply.

```
BehaviorActor (Kind = Static or Dynamic)
├── Model: implements IWriteableSlotActor
│         optionally implements IHasDurability, IHasColor, etc.
├── View:  implements ISlotActorView
└── Controller: IStartable
      subscribes to game triggers (turn events, ActorHitMessage, etc.)
      reads SlotGrid for context (HexNeighborIndices, IsEmpty, etc.)
      calls SlotGrid.Place / Remove / At to modify the grid
```

**Failing tests — write first:**
Defined when the first concrete behavior-bound actor is chosen. The behavior logic
(e.g. target selection, grid modification, trigger filtering) is the testable core —
write those tests before writing the controller.

---

## Future consideration — Actor identity

The grid has no identity query beyond capability interfaces. Options when needed
(e.g. level design tools, serialization, analytics):

- `string ActorId` on `ISlotActor` — config-driven, stringly-typed
- `SlotActorType` enum on `ISlotActor` — compile-time safe, coarse-grained
- Additional capability interfaces per specific trait

Deferred until a concrete consumer appears.

---

## What NOT to change (updated)

- **Item system stays separate** — items are passengers on actors, not actors themselves
- **`IBalloonModel` keeps all its fields** — durability lifts to `IHasDurability` as
  an additive supertype; nothing is removed from `IBalloonModel`
- **No `StartCoroutine`** — all async via UniTask
- **`BalloonsConfiguration` is not replaced** — the Grid Spawner reads it for balloon
  entries; the SO gains new actor-type sections incrementally rather than being replaced
- **`BalloonSpawner` stays until `GridSpawner` is validated** — no flag day migrations
- **`DifficultyProfile` SO is not introduced until the first difficulty axis is needed** —
  don't pre-build the config surface before the gameplay feedback loop exists

---

## Current State

| Phase | Status |
|---|---|
| 1 — Define actor interfaces | ✅ Complete |
| 2 — Refactor SlotGrid | ✅ Complete |
| 3 — Update consumers | ✅ Complete |
| 4 — Update tests | ✅ Complete |
| 5 — Update documentation | ✅ Complete |
| 6 — Static actor evaluation | ✅ Complete |
| 7 — Durability abstraction | Future |
| 8 — Grid Spawner / Level Spawner | Future |
| 9 — Behavior-bound actors | Future (broadly defined) |
