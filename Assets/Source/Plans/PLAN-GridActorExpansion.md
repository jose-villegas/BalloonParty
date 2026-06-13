@page plan_grid_actor_expansion Grid Actor Expansion — Phase 8+

# Grid Actor Expansion — Phase 8+

> Working plan for procedural generation, actor archetypes, and difficulty coupling.
> Foundation (Phases 1–7.5) is in `PLAN-GridActorSystem-Foundation.md`.

---

## Orientation

Phases 1–7.5 built the capability-interface foundation: every actor declares what it
does through interfaces, not through class hierarchies or field conventions. Phase 8 builds
the content and systems on top of that foundation.

The four threads — in order:

```
8.0  Spawner Coordination     ✅ DONE — priority-based coordinator, IReadyGate gating, parallel-within-stage
8.1a Absorb Routing           ✅ DONE — OnAbsorb in ProjectileView; IsFree=false + DestroyProjectile; 3 tests
8.1b DamageContext Migration  ✅ DONE — DamageContext/DamageFlags(Normal/Piercing); IHitable migrated; Template Method (EvaluateNormalHit); ItemSettings.Flags; all callers + tests updated
8.1c UnbreakableBalloon       ✅ DONE — uses DamageContext; ScoreValue moved off BalloonModelBase; IHasDurability moved to concrete subclasses
8.2a Actor Archetypes: Structural  ✅ DONE — PuffObstacleModel + BushObstacleModel in Slots/Actor/Archetype/; StaticActorSpawner migrated
8.2b Actor Archetypes: Hitables    ✅ DONE — DeflectorActorModel + AbsorberActorModel + GridActorView/PoolChannel + GridActorPrefabEntry
8.2c Actor Archetypes: Gatekeeper  ✅ DONE — GatekeeperActorModel (IHasDurability) + GridActorHitController + NudgeOverrides off BalloonModelBase
8.3  Procedural Placement     — weighted, rule-based GridSpawner; retires BalloonSpawner
8.4  Difficulty + Levels      — tuning knobs driven by score-based level progression
```

**Why this order:** You can't write a placement algorithm worth tuning until you have a
diverse actor vocabulary (8.2a–c before 8.3). You can't tune difficulty until the algorithm
runs (8.3 before 8.4). The coordinator is pure infrastructure with no gameplay impact — it
goes first so all future spawners have a clean integration point.

---

## Actor Vocabulary — Design Reference

Before detailing the phases, this section specifies the archetypes that Phases 8.2a–c will
implement. They are the building blocks the procedural algorithm needs.

### Balloon archetypes

| Archetype | Model class | `EvaluateHit` | HitsToPop | `IHasItemSlot` | Notes |
|---|---|---|---|---|---|
| Simple | `BalloonModel` | PassThrough → Pop | 1 | ✅ | Default; paintable |
| Soap Cluster | `BalloonModel` (`BalloonType.BubbleCluster`) | PassThrough × N-1 → Pop | N (3–5) | ✅ | Cluster of iridescent soap bubbles; each hit pops one bubble visually — no crack sprites, cluster shrinks |
| Tough | `ToughBalloonModel` | Deflect × N-1 → Pop | N | ❌ | Hard outer shell; not paintable |
| Unbreakable | `UnbreakableBalloonModel` | Deflect always | ∞ | ❌ | Permanent obstacle; no `IHasDurability` |


### Grid actor archetypes

These all live in `Slots/Actor/` as separate model/view pairs, not subclasses of anything
balloon-specific.

| Archetype | Kind | `IPassThrough` | `IHitable` | Outcome | Durability | Role |
|---|---|---|---|---|---|---|
| **Puff** (current `StaticActorModel`) | Static | ✅ | ❌ | — | — | Structural support; spawn paths pass freely through it — think gas, mist, a permeable zone |
| **Bush** | Static | ❌ | ❌ | — | — | Structural support; spawn paths cannot cross it — requires rerouting (Phase 9). Currently logs a warning |
| **Deflector** | Static | ❌ | ✅ | `Deflect` | ❌ indestructible | Redirects projectile; creates predictable bounce lanes |
| **Absorber** | Static | ❌ | ✅ | `Absorb` | ❌ indestructible | Turn-ending hazard; player must route around it |
| **Gatekeeper** | Static | ❌ | ✅ | `Deflect` | ✅ N hits | Blocks column until destroyed; temporary obstacle |

**Puff vs Bush — the structural role:**
Both variants occupy a slot, so `SlotGrid.IsEmpty` returns false and both count as
structural support for any balloon above them. Neither is ever moved by the balancer.
Neither has a collider — they are not part of the hit pipeline.

The difference is purely **visual pathing**. Whenever the grid computes a movement path
— spawn animations, balancer relocations, any future path computation — it calls
`SlotGrid.IsTraversable`, which returns true only for empty slots or `IPassThrough`
occupants. A balloon's animation can arc through a Puff slot; it cannot travel through
a Bush slot. Structural support is identical regardless.

**On `IPassThrough`:** it is a marker interface, not a property. An actor either is or
is not traversable at the type level — there is no runtime toggle. This means Puff
and Bush must be separate model classes (`PuffObstacleModel`, `BushObstacleModel`)
rather than a single class with a flag. The type IS the capability signal, consistent
with the rest of the codebase.


**Rerouting note:** A `Bush` in any computed path currently causes `ComputePath`
to emit a warning and proceed anyway (Phase 6 decision). Full rerouting — finding a
path around solid obstacles for both spawn and balance animations — is deferred to Phase 9.

**Why Deflector, Absorber, and Gatekeeper first:**
- A **Deflector** gives the grid intentional geometry — the player can exploit bounce angles
  or be forced to navigate around it.
- An **Absorber** creates genuine danger zones — a column with one requires a different shot
  path, and the difficulty knobs control how many appear and where.
- A **Gatekeeper** introduces a spatial sub-objective — destroy the blocker to reach the
  balloons behind it. Pairing a Gatekeeper with Tough or Unbreakable balloons gives the
  procedural algorithm a way to gate difficulty without just increasing density.


### Hit controller pattern for non-balloon actors

`BalloonController` is balloon-specific (it knows about `BalloonView.PlayPopEffect`,
`IHasItemSlot`, etc.). Grid actors need a general hit-response controller.

**`GridActorHitController`** (Phase 8.2c) — a single `IStartable` that subscribes to
`ActorHitMessage` and handles removal for any grid actor that is not an `IBalloonModel`:

```csharp
// Filters out IBalloonModel — BalloonController handles those.
// For any other IHasDurability actor: remove from grid when HitsRemaining <= 0.
// Does NOT publish score — grid actors are not IHasScore unless explicitly intended.
private void OnActorHit(ActorHitMessage msg)
{
    if (msg.Actor is IBalloonModel) return;
    if (msg.Actor is not IHasDurability durable) return;
    if (durable.HitsRemaining.Value > 0) return;
    _grid.Remove(msg.Actor.SlotIndex.Value);  // SlotIndex via ISlotActor (plain)
}
```

Indestructible actors (`IHitable` but not `IHasDurability`) never trigger removal — the
hit outcome alone is sufficient.

---

## Phases

> **TDD rule:** write failing tests before implementation. Real objects over mocks for
> plain C# types; NSubstitute only for interfaces and ScriptableObjects.

> **Style rule:** follow `Assets/Source/README.md`. Comments only on the *why*.
> No block headers. No XML docs on obvious internal API.

---

### ✅ Phase 8.0 — Spawner Coordination

**Status: Complete.** Zero gameplay change — purely infrastructure.

#### What was built

**`IGridSpawner`** (`Slots/Spawner/`):
```csharp
internal interface IGridSpawner
{
    SpawnStage SpawnPriority { get; }
    UniTask SpawnAsync(CancellationToken ct);
}
```

**`SpawnStage`** (`Slots/Spawner/`):
```csharp
internal enum SpawnStage
{
    StaticActors  = 0,   // must exist before balloons — balancer support
    DynamicActors = 50,  // reserved for Phase 8.3 GridSpawner
    BalloonActors = 100, // fills around statics
}
```

**`GridSpawnerCoordinator`** (`Slots/Spawner/`) — `IStartable` + `IDisposable`:
- Injects `IEnumerable<IGridSpawner>` (VContainer collection injection) and `IReadyGate`
- Awaits `IReadyGate.WaitAsync` before spawning (backed by `NavigationReadyGate(Game)`)
- Groups spawners by `SpawnStage`, runs groups **sequentially**
- Spawners within the same stage run **in parallel** via `UniTask.WhenAll`
- Owns a `CancellationTokenSource`; disposes on scope teardown

**`IReadyGate`** (`Shared/`) — injectable precondition gate:
```csharp
internal interface IReadyGate
{
    UniTask WaitAsync(CancellationToken ct);
}
```
Implementations: `NavigationReadyGate(NavigationState)` and `CinematicEndGate(CinematicState)`.
Registered in `GameLifetimeScope` as `new NavigationReadyGate(NavigationState.Game)`.

**Spawner changes:**
- `StaticActorSpawner` — implements `IGridSpawner`. `Start()` registers the pool (synchronous). `SpawnAsync()` places static actors. `SpawnPriority = SpawnStage.StaticActors`.
- `BalloonSpawner` — implements `IGridSpawner`. `Start()` kicks off pool pre-warm as a stored `UniTask`. `SpawnAsync()` awaits pre-warm then populates the initial grid. `SpawnPriority = SpawnStage.BalloonActors`. Nav wait removed — coordinator owns it.

**Folder structure** (`Slots/` reorganised in this session):
```
Slots/
├── Grid/           BalloonParty.Slots.Grid
├── Actor/          BalloonParty.Slots.Actor   ← interfaces + StaticActor all flat
├── Capabilities/   BalloonParty.Slots.Capabilities
├── Spawner/        BalloonParty.Slots.Spawner
```

**Tests** (`Tests/EditMode/Slots/GridSpawnerCoordinatorTests.cs`):
- `GridSpawnerCoordinator_CallsSpawnersInPriorityOrder` ✅
- `GridSpawnerCoordinator_AwaitsHigherPriorityStageBeforeNext` ✅
- `GridSpawnerCoordinator_SamePriority_RunsInParallel` ✅

Test double: `ImmediateGate : IReadyGate` — `WaitAsync` returns immediately.

---

### ✅ Phase 8.1a — Absorb Routing

**Status: Complete.**

#### What was built

`ProjectileView.OnAbsorb(ISlotActor actor, Vector3 worldPos)` — `internal` terminal method called when `EvaluateHit` returns `HitOutcome.Absorb`:
1. Publishes `ActorHitMessage` with `HitOutcome.Absorb` (so Phase 8.2c `GridActorHitController` can react).
2. Sets `_model.IsFree = false` — stops `FixedUpdate` movement immediately.
3. Calls `DestroyProjectile()` — publishes `ProjectileDestroyedMessage` + `BalanceBalloonsMessage`, ending the turn.

`OnTriggerEnter2D` delegates to `OnAbsorb` and returns early when the outcome is `Absorb`.

`BalloonController` comment updated from "Phase 9" to "Phase 8.1a".

**Tests** (`Tests/EditMode/Projectile/ProjectileViewAbsorbTests.cs`):
- `ProjectileView_OnAbsorb_PublishesProjectileDestroyed` ✅
- `ProjectileView_OnAbsorb_SetsModelNotFree` ✅
- `ProjectileView_OnAbsorb_PublishesActorHitMessageWithAbsorbOutcome` ✅

---

### ✅ Phase 8.1b — DamageContext API Migration

**Status: Complete.** No gameplay change — pure API migration.

#### What was built

**`DamageFlags`** (`Slots/Capabilities/DamageFlags.cs`):
```csharp
[Flags]
public enum DamageFlags
{
    Normal   = 0,        // standard hit; no special treatment
    Piercing = 1 << 0,   // pops regardless of HitsRemaining or permanent Deflect
    // Future: BypassesShield = 1 << 1, etc.
}
```
Zero-value named `Normal` (not `None`) so call sites read as `DamageFlags.Normal` rather
than the ambiguous "no flags".

**`DamageContext`** (`Slots/Capabilities/DamageContext.cs`):
```csharp
public readonly struct DamageContext
{
    public readonly int Damage;
    public readonly DamageFlags Flags;
    public DamageContext(int damage, DamageFlags flags = DamageFlags.Normal) { ... }
}
```

**`IHitable`** — signature changed: `EvaluateHit(int damage)` → `EvaluateHit(DamageContext context)`.

**Template Method on `BalloonModelBase`** — `EvaluateHit` is now non-virtual and owns the
Piercing fast-path unconditionally. Subclasses override `EvaluateNormalHit` for their
type-specific non-piercing logic:
```csharp
// Non-virtual — Piercing is always handled here, no subclass can bypass it.
public HitOutcome EvaluateHit(DamageContext context)
{
    if (context.Flags.HasFlag(DamageFlags.Piercing))
    {
        HitsRemaining.Value = 0;
        return HitOutcome.Pop;
    }
    return EvaluateNormalHit(context);
}

// Override point for subclasses — called only when Piercing is NOT set.
protected virtual HitOutcome EvaluateNormalHit(DamageContext context)
{
    var survives = HitsRemaining.Value - context.Damage > 0;
    HitsRemaining.Value -= context.Damage;
    return survives ? HitOutcome.PassThrough : HitOutcome.Pop;
}
```

**`ToughBalloonModel`** — overrides `EvaluateNormalHit` only; Piercing handling removed
(base owns it):
```csharp
protected override HitOutcome EvaluateNormalHit(DamageContext context)
{
    var survives = HitsRemaining.Value - context.Damage > 0;
    HitsRemaining.Value -= context.Damage;
    return survives ? HitOutcome.Deflect : HitOutcome.Pop;
}
```

**`SlotActorExtensions.EvaluateHit`** — extension signature updated to `DamageContext context`.

**`ItemSettings`** — gains `[SerializeField] private DamageFlags _damageFlags` + `public DamageFlags Flags` property. Bomb and Laser can be toggled to `Piercing` in the SO; all others default to `Normal`.

**Callers updated:**
- `ProjectileView` — `new DamageContext(1)`
- `BombItemHandler` — `new DamageContext(settings.Damage, settings.Flags)`
- `LaserItemHandler` — `new DamageContext(settings.Damage, settings.Flags)`
- `LightningItemHandler` — `new DamageContext(settings.Damage, settings.Flags)`
- `BalloonRemoverCheat` — `new DamageContext(1)`

**Tests updated:** `HitableTests`, `BalloonModelTests`, `ScoreControllerTests` — all call sites and test-double implementations migrated to `DamageContext`. `BalloonModelTests` gains:
- `BalloonModel_EvaluateHit_PiercingFlag_PopsRegardlessOfHitsRemaining`

---


### Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup

**Goal:** Introduce `UnbreakableBalloonModel` using `DamageContext.Piercing`, and remove
`ScoreValue` from `BalloonModelBase` — a cleanup `UnbreakableBalloonModel` forces because
it is a permanent obstacle that awards no score.

**Depends on:** 8.1b (`DamageContext` must exist).

#### `UnbreakableBalloonModel`

`UnbreakableBalloonModel` extends `BalloonModelBase` but has no `IHasDurability` — it
is a permanent obstacle. Because `BalloonModelBase.EvaluateHit` already handles Piercing
unconditionally, `UnbreakableBalloonModel` only overrides `EvaluateNormalHit`:

```csharp
internal class UnbreakableBalloonModel : BalloonModelBase
{
    // No IHasDurability. HitsRemaining never changes. Deflects all non-piercing hits.
    // Piercing is handled by BalloonModelBase.EvaluateHit before EvaluateNormalHit is called.

    protected override HitOutcome EvaluateNormalHit(DamageContext context) => HitOutcome.Deflect;
}
```

`BalloonSpawner` switch gains:
```csharp
BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
```

#### `ScoreValue` on `BalloonModelBase`

`UnbreakableBalloonModel` does not score — making `ScoreValue` on the base a lie.
Remove it: `BalloonModel` and `ToughBalloonModel` each hold their own `ScoreValue` from
config. `IHasScore` is the read interface for `ScoreController` — nothing else changes.

#### Failing tests
New fixture **`UnbreakableBalloonModelTests`**:
```
UnbreakableBalloonModel_IsIHitable
UnbreakableBalloonModel_IsNotIHasDurability

UnbreakableBalloonModel_EvaluateHit_NoFlags_ReturnsDeflect
  — EvaluateHit(new DamageContext(1)) == Deflect
  — EvaluateHit(new DamageContext(99)) == Deflect

UnbreakableBalloonModel_EvaluateHit_PiercingFlag_ReturnsPop
  — EvaluateHit(new DamageContext(1, DamageFlags.Piercing)) == Pop

UnbreakableBalloonModel_EvaluateHit_PiercingFlag_DoesNotMutateState
  — no HitsRemaining to decrement; model state unchanged after any hit
```

Addition to **`BalloonModelTests`**:
```
BalloonModel_EvaluateHit_DamageContext_SurvivesWithPassThrough
BalloonModel_EvaluateHit_PiercingFlag_PopsRegardlessOfHitsRemaining
  — model with HitsToPop=5; EvaluateHit(new DamageContext(1, DamageFlags.Piercing)) == Pop; HitsRemaining == 0
```

---

### Phase 8.2a — Structural Actors (Puff + Bush)

**Goal:** Rename `StaticActorModel` to `PuffObstacleModel` and add `BushObstacleModel`.
No hit pipeline involvement — purely structural/visual. Lowest-risk 8.2 step; unblocks
the config groundwork that 8.2b and 8.2c depend on.

#### Folder structure

New archetype models live in `Slots/Actor/Archetype/`
(namespace `BalloonParty.Slots.Actor.Archetype`). Existing interfaces, infrastructure
(`StaticActorView`, `StaticActorSpawner`, pool channel, settings) remain flat in
`Slots/Actor/` until the spawner is retired in Phase 8.3.

```
Slots/Actor/
├── ISlotActor.cs, IDynamicSlotActor.cs, …   ← interfaces, unchanged
├── SlotActorKind.cs                          ← unchanged
├── StaticActorView.cs, StaticActorPoolChannel.cs, …  ← legacy infra, unchanged
├── StaticActorSpawner.cs                     ← updated to spawn PuffObstacleModel
└── Archetype/
    ├── GridActorType.cs                      ← enum: Puff, Bush (extended in 8.2b–c)
    ├── PuffObstacleModel.cs                 ← IWriteableSlotActor + IPassThrough
    └── BushObstacleModel.cs                 ← IWriteableSlotActor only
```

#### Files

| File | Role |
|---|---|
| `Archetype/GridActorType.cs` | Enum seed: `Puff = 0`, `Bush = 1` |
| `Archetype/PuffObstacleModel.cs` | Direct successor to `StaticActorModel`; `IWriteableSlotActor + IPassThrough` |
| `Archetype/BushObstacleModel.cs` | `IWriteableSlotActor` only; blocks spawn-path traversal |

`StaticActorModel` is **not deleted** yet — the spawner still references it until Phase 8.3
retires the legacy path. `StaticActorSpawner.SpawnStaticActors` switches to
`new PuffObstacleModel(slot)` so the grid starts using the canonical type.

#### Failing tests
New fixture **`StructuralActorTests`** (`Tests/EditMode/Slots/`):
```
PuffObstacleModel_KindIsStatic
PuffObstacleModel_IsIPassThrough
PuffObstacleModel_IsNotIHitable

BushObstacleModel_KindIsStatic
BushObstacleModel_IsNotIPassThrough
BushObstacleModel_IsNotIHitable
```

---

### Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)

**Goal:** Add `DeflectorActorModel` and `AbsorberActorModel` — grid actors that participate
in the hit pipeline but have no durability. Also introduces `GridActorPrefabEntry` so both
types can be wired to prefabs and spawned via config.

#### Files

```
Slots/Actor/Archetype/
├── DeflectorActorModel.cs     ← IWriteableSlotActor + IHitable → Deflect; no IHasDurability
├── AbsorberActorModel.cs      ← IWriteableSlotActor + IHitable → Absorb; no IHasDurability
└── (GridActorType.cs updated) ← adds Deflector = 2, Absorber = 3

Configuration/
└── GridActorPrefabEntry.cs    ← [Serializable]; prefab, weight, maxCount, GridActorType, PoolKey
```

`GridActorPrefabEntry.PoolKey` follows the existing convention: derived from the prefab
GameObject name, no manual key assignment needed.

Views and pool channels for Deflector and Absorber follow the exact same pattern as
`StaticActorView` / `StaticActorPoolChannel`. They live in `Slots/Actor/Archetype/` next
to their models.

#### Failing tests
New fixture **`HitableActorTests`** (`Tests/EditMode/Slots/`):
```
DeflectorActor_EvaluateHit_ReturnsDeflect
DeflectorActor_IsNotIHasDurability
DeflectorActor_IsNotIBalloonModel

AbsorberActor_EvaluateHit_ReturnsAbsorb
AbsorberActor_IsNotIHasDurability
```

---

### Phase 8.2c — Gatekeeper + GridActorHitController

**Goal:** Add `GatekeeperActorModel` (the first grid actor with `IHasDurability`) and the
`GridActorHitController` that handles removal when any non-balloon `IHasDurability` actor
reaches zero hits.

This is the most complex 8.2 step because it introduces durability tracking on a grid actor
and the reactive controller that responds to it. It also forces the `NudgeOverrides` cleanup
on `BalloonModelBase` — Gatekeeper is not nudgeable, so keeping `NudgeOverrides` on the
base would be the same lie that `ScoreValue` was before 8.1c cleaned it up.

#### Files

```
Slots/Actor/Archetype/
├── GatekeeperActorModel.cs    ← IWriteableSlotActor + IHasDurability; Deflect→Pop on zero hits
└── (GridActorType.cs updated) ← adds Gatekeeper = 4

Slots/Actor/
└── GridActorHitController.cs  ← IStartable; subscribes ActorHitMessage; removes durable non-balloon actors at zero hits
```

**`GridActorHitController`** lives flat in `Slots/Actor/` (not in `Archetype/`) because
it is infrastructure that reacts to *any* grid actor, not an archetype itself.

#### `GatekeeperActorModel`

```csharp
// Deflects projectiles until HitsRemaining reaches zero, then reports Pop.
// IHasDurability exposed so GridActorHitController can watch HitsRemaining.
internal class GatekeeperActorModel : IWriteableSlotActor, IHasDurability
{
    public ReactiveProperty<int> HitsRemaining { get; }
    IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

    public HitOutcome EvaluateHit(DamageContext context)
    {
        HitsRemaining.Value = Mathf.Max(0, HitsRemaining.Value - context.Damage);
        return HitsRemaining.Value > 0 ? HitOutcome.Deflect : HitOutcome.Pop;
    }
    // …
}
```

#### `GridActorHitController`

```csharp
// Filters out IBalloonModel — BalloonController handles those.
// For any other IHasDurability actor: remove from grid when HitsRemaining <= 0.
// Does NOT publish score — grid actors are not IHasScore unless explicitly designed to be.
private void OnActorHit(ActorHitMessage msg)
{
    if (msg.Actor is IBalloonModel) return;
    if (msg.Actor is not IHasDurability durable) return;
    if (durable.HitsRemaining.Value > 0) return;
    _grid.Remove(msg.Actor.SlotIndex.Value);
}
```

#### `NudgeOverrides` cleanup on `BalloonModelBase`

`GatekeeperActorModel` is not nudgeable. Keeping `NudgeOverrides` on `BalloonModelBase`
would make it a base-class concern for types that have nothing to do with nudge. Move it:
`BalloonModel` and `ToughBalloonModel` each declare their own `NudgeOverrides` from config.
`UnbreakableBalloonModel` gets `NudgeOverrides => null` (permanent obstacles don't nudge).
`IBalloonModel : IHasNudge` stays — only concrete balloon types implement it directly.

#### NudgeService decoupling (done alongside 8.2c)

`GatekeeperActorModel` introduced the first non-balloon `IHitable` actor, which exposed a
coupling problem: `BalloonNudgeMessage` carried an `IBalloonModel` target, and `NudgeService`
drove `BalloonView` directly. Neither was valid for non-balloon actors. Both were fixed:

- `BalloonNudgeMessage` renamed to `NudgeMessage`; field `Balloon: IBalloonModel` replaced
  by `Actor: IHasNudge` — any nudgeable actor can now be a nudge target.
- `INudgeable` added to `Nudge/` — view-side interface: `Nudge(slotPosition, direction,
  distance, duration, onComplete)`. `NudgeService` calls `_grid.ViewAt(slot) as INudgeable`.
- `BalloonView` implements `INudgeable`. Stability tracking (`_isNudging`, `IsStable`)
  moved from `NudgeService` into `BalloonView.Nudge()` — the view owns the animation state.
- `ISpawnGate` pruned from `Slots/` — its body was empty. Canonical type is `IReadyGate`
  in `Shared/`.

#### Failing tests
New fixture **`GatekeeperActorTests`** (`Tests/EditMode/Slots/`):
```
GatekeeperActor_EvaluateHit_Survives_ReturnsDeflect_AndDecrementsHits
GatekeeperActor_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero
```

New fixture **`GridActorHitControllerTests`** (`Tests/EditMode/Slots/`):
```
GridActorHitController_OnActorHit_IBalloonModel_IsIgnored
GridActorHitController_OnActorHit_Gatekeeper_WhenHitsReachZero_RemovesFromGrid
GridActorHitController_OnActorHit_Deflector_IsNotRemoved
```

---

### Phase 8.3 — Procedural Placement Engine

> **Pre-requisite:** All actor prefabs and the `GridActorConfiguration` SO must exist
> before this phase runs meaningfully in-game. See `PLAN-ContentProduction.md` for the
> full art and asset checklist.

**Goal:** Replace `BalloonSpawner` + `StaticActorSpawner` with a single `GridSpawner` that
drives weighted, rule-based placement for all actor types.

`BalloonSpawner` and `StaticActorSpawner` retire from `IGridSpawner` registration once
`GridSpawner` covers all responsibilities. No flag day — they co-exist until `GridSpawner`
is validated in-game.

**Depends on:** 8.2c (all archetypes must exist before the procedural engine can place them).

#### Design

`GridSpawner` implements `IGridSpawner` with `SpawnPriority = SpawnStage.DynamicActors`
(value 50). It slots between the retired static and balloon spawners in the coordinator's
stage ordering — when both legacy spawners are removed, only this stage remains.

```
GridSpawner
├── Reads BalloonsConfiguration for balloon entries (weight, maxCount, BalloonType)
├── Reads GridActorConfiguration for grid actor entries (weight, maxCount, GridActorType)
├── PickEntry(activeCounts) — same weighted+capped pattern as BalloonsConfiguration.PickRandom
└── SlotSelector — pluggable; starts with UniformRandom over AllEmptySlots()
```

**Slot selection rules (start simple, expand):**

| Rule | Purpose | Phase |
|---|---|---|
| Uniform random | Baseline — same as current spawners | 8.3 |
| Row bias | Weight bottom rows higher for statics | 8.3 |
| Min-distance between statics | Prevent static actor clumping | 8.3 |
| Column avoidance for Absorbers | Don't place Absorbers adjacent to each other | 8.4 |
| Neighbor constraints (for future behavior actors) | Phase 9 |

#### Migration path
1. `GridSpawner.SpawnAsync` duplicates `BalloonSpawner`'s population logic — test parity.
2. Register `GridSpawner` as `IGridSpawner` alongside the legacy spawners. Coordinator runs all three in stage order (`StaticActors=0`, `DynamicActors=50`, `BalloonActors=100`).
3. Disable legacy spawner registrations when `GridSpawner` passes in-game validation.
4. Remove the retired spawners; `DynamicActors=50` becomes the only active stage.

#### Failing tests
New fixture **`GridSpawnerTests`**:
```
PickEntry_SingleEntry_AlwaysReturnsIt
PickEntry_AllEntriesAtMax_ReturnsNull
PickEntry_CappedEntryExcluded_OtherSelected
PickEntry_RespectsWeight_HighWeightWinsWithSingleCandidate

SlotSelector_Uniform_ReturnsEmptySlot
SlotSelector_MinDistance_ExcludesNearbySlots
SlotSelector_RowBias_PrefersTargetRows
```

---

### Phase 8.4 — Difficulty + Level Coupling

**Goal:** Connect tunable difficulty knobs to score-driven level progression.

This phase is deliberately last — you can't tune what you haven't built. Design the
`DifficultyProfile` only after watching the procedural algorithm run.

#### Design

```
DifficultyProfile  (ScriptableObject)
├── GridDensity: float              (0–1; % of slots filled per spawn pass)
├── StaticActorRatio: float         (0–1; grid actor weight share vs balloons)
├── AbsorberAllowed: bool           (gate — off at low levels, on at higher)
├── ActorWeightOverrides: entry[]   (per-type multiplier on base weight)
└── MinEmptySlots: int              (minimum clear slots; clamps density)
```

`DifficultyService : IStartable` subscribes to `ScoreLevelUpMessage` and updates the
active `DifficultyProfile`. `GridSpawner` reads the active profile on each spawn pass.

**Knob graduation — suggested starting curve:**
```
Level 1–3:   Only Simple + occasional Tough; no statics beyond Puff obstacle
Level 4–6:   Deflectors introduced; Tough ratio rises
Level 7–10:  Gatekeepers introduced; Soap Cluster balloons appear
Level 11+:   Absorbers introduced; Unbreakable balloons; density climbs
```

This is a starting point — real values come from playtesting, not design documents.

#### Failing tests
```
DifficultyService_OnLevelUp_UpdatesActiveProfile
DifficultyProfile_Default_HasNoWeightOverrides
GridSpawner_AppliesWeightOverride_FromActiveProfile
GridSpawner_AbsorberGated_NotSpawnedWhenFlagFalse
```

---

## Open Questions

These are known design gaps to resolve during implementation:

1. **Score for grid actor kills** — `Gatekeeper` destroyed: does the player score? Should
   `GatekeeperActorModel` implement `IHasScore`? Resolves in Phase 8.2c.

2. **Nudge for grid actors** — Should a `Deflect` hit on a `Gatekeeper` trigger a nudge
   animation? Nudge is currently tied to `IHasNudge` on the model. If yes, `Gatekeeper`
   implements `IHasNudge` + `NudgeOverrides` moves off `BalloonModelBase`. Resolves in 8.2c.

3. **`NudgeOverrides` on `BalloonModelBase`** — `GatekeeperActorModel` in Phase 8.2c
   forces `NudgeOverrides` off the base (it doesn't nudge). Clean up is scheduled in 8.2c —
   `ScoreValue` was already removed in 8.1c.

4. **Grid actor controller pattern** — `GridActorHitController` (Phase 8.2c) handles
   removal. But behavior-bound actors need per-actor controllers (Phase 9). Define the
   boundary clearly in 8.2c: `GridActorHitController` handles reactive removal only;
   per-actor behavior controllers are Phase 9's domain.

5. **Pool key for grid actors** — current `PoolKey` convention derives from prefab name.
   `GridActorPrefabEntry.PoolKey` follows the same convention. Confirm before 8.2b.

---

## Current State

| Phase | Status |
|---|---|
| 8.0 — Spawner Coordination | ✅ Complete |
| 8.1a — Absorb Routing | ✅ Complete |
| 8.1b — DamageContext Migration | ✅ Complete |
| 8.1c — UnbreakableBalloonModel | ✅ Complete |
| 8.2a — Structural Actors (Puff + Bush) | ✅ Complete |
| 8.2b — Indestructible Hitables (Deflector + Absorber) | ✅ Complete |
| 8.2c — Gatekeeper + GridActorHitController | ✅ Complete |
| 8.3 — Procedural Placement | **Blocked on content** — Puff, Bush, and the `GridActorConfiguration` asset are done; remaining blocker is Deflector / Absorber / Gatekeeper art+prefabs+config. See `PLAN-ContentProduction.md` |
| 8.4 — Difficulty + Levels | Blocked on 8.3 |
| Phase 9 — Behavior-bound actors | Future (broadly defined) |
