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
8.2  Actor Archetypes         — the vocabulary the procedural algorithm needs to be interesting
8.3  Procedural Placement     — weighted, rule-based GridSpawner; retires BalloonSpawner
8.4  Difficulty + Levels      — tuning knobs driven by score-based level progression
```

**Why this order:** You can't write a placement algorithm worth tuning until you have a
diverse actor vocabulary (8.2 before 8.3). You can't tune difficulty until the algorithm
runs (8.3 before 8.4). The coordinator is pure infrastructure with no gameplay impact — it
goes first so all future spawners have a clean integration point.

---

## Actor Vocabulary — Design Reference

Before detailing the phases, this section specifies the archetypes that Phase 8.2 will
implement. They are the building blocks the procedural algorithm needs.

### Balloon archetypes

| Archetype | Model class | `EvaluateHit` | HitsToPop | `IHasItemSlot` | Notes |
|---|---|---|---|---|---|
| Simple | `BalloonModel` | PassThrough → Pop | 1 | ✅ | Default; paintable |
| Cracking | `BalloonModel` (config only) | PassThrough × N-1 → Pop | N (3–5) | ✅ | Same class, different config; view reacts to `HitsRemaining` reactively |
| Tough | `ToughBalloonModel` | Deflect × N-1 → Pop | N | ❌ | Hard outer shell; not paintable |
| Unbreakable | `UnbreakableBalloonModel` | Deflect always | ∞ | ❌ | Permanent obstacle; no `IHasDurability` |

**Not yet designed — Phase 9 consideration:**
- *Chain* — pops adjacent same-color balloons when destroyed; needs neighbor query at pop time
- *Ghost* — `PassThrough` always (projectile travels through), pops after N passes

### Grid actor archetypes

These all live in `Slots/Actor/` as separate model/view pairs, not subclasses of anything
balloon-specific.

| Archetype | Kind | `IPassThrough` | `IHitable` | Outcome | Durability | Role |
|---|---|---|---|---|---|---|
| **Cloud** (current `StaticActorModel`) | Static | ✅ | ❌ | — | — | Structural support; spawn paths pass freely through it — think gas, mist, a permeable zone |
| **Block** | Static | ❌ | ❌ | — | — | Structural support; spawn paths cannot cross it — requires rerouting (Phase 9). Currently logs a warning |
| **Deflector** | Static | ❌ | ✅ | `Deflect` | ❌ indestructible | Redirects projectile; creates predictable bounce lanes |
| **Absorber** | Static | ❌ | ✅ | `Absorb` | ❌ indestructible | Turn-ending hazard; player must route around it |
| **Gatekeeper** | Static | ❌ | ✅ | `Deflect` | ✅ N hits | Blocks column until destroyed; temporary obstacle |

**Cloud vs Block — the structural role:**
Both variants occupy a slot, so `SlotGrid.IsEmpty` returns false and both count as
structural support for any balloon above them. Neither is ever moved by the balancer.
Neither has a collider — they are not part of the hit pipeline.

The difference is purely **visual pathing**. Whenever the grid computes a movement path
— spawn animations, balancer relocations, any future path computation — it calls
`SlotGrid.IsTraversable`, which returns true only for empty slots or `IPassThrough`
occupants. A balloon's animation can arc through a Cloud slot; it cannot travel through
a Block slot. Structural support is identical regardless.

**On `IPassThrough`:** it is a marker interface, not a property. An actor either is or
is not traversable at the type level — there is no runtime toggle. This means Cloud
and Block must be separate model classes (`CloudObstacleModel`, `BlockObstacleModel`)
rather than a single class with a flag. The type IS the capability signal, consistent
with the rest of the codebase.

**Future extension — `IPassThrough` as a behaviour surface (Phase 9 candidate):**

The current marker interface only answers "can I pass through?". Two natural extensions
that the actor vocabulary will eventually need:

*Density / passage resistance* — a Cloud-like actor could expose a `float Density` (0–1)
that the animation system uses to modulate travel speed. A thin mist barely slows the
path; a dense cloud visibly delays it. The interface extension:
```csharp
public interface IPassThrough
{
    float Density { get; }  // 0 = no resistance, 1 = maximum slow
}
```
The spawn animation driver reads `Density` and scales the DOTween duration multiplier
for the segment that crosses the slot. No behaviour change to the grid or hit system —
purely visual pacing.

*Pass-through triggers* — when a balloon's spawn animation crosses a traversable slot,
the occupant of that slot could react. A new capability interface covers this:
```csharp
public interface IOnPassThrough
{
    void OnActorPassedThrough(ISlotActor passing);
}
```
Example uses: a **Recolorer** cloud that tints any balloon whose path arcs through it; a
**PowerUp** cloud that assigns an item to the passing balloon; a **Curse** cloud that
reduces `HitsRemaining` by 1 on pass. None of these require structural changes to the
grid or the hit pipeline — `ComputePath` already returns a waypoint sequence, so the
animation driver just needs to call `OnActorPassedThrough` as each waypoint is reached.

Both extensions are additive — `IPassThrough` stays a marker today and gains members
(or a companion interface) only when a concrete actor type demands it.

**Rerouting note:** A `Block` in any computed path currently causes `ComputePath`
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

**Not yet designed — Phase 9 candidates:**
- *Recolorer* — static; changes adjacent balloon colors each turn (undermines paint strategy)
- *Mover* — dynamic; shuffles adjacent balloons to an adjacent empty slot each turn
- *Spawner* — static; places a new balloon into an adjacent empty slot each turn (fills gaps)
- *ShieldTower* — static; grants periodic shields to adjacent balloons

### Hit controller pattern for non-balloon actors

`BalloonController` is balloon-specific (it knows about `BalloonView.PlayPopEffect`,
`IHasItemSlot`, etc.). Grid actors need a general hit-response controller.

**`GridActorHitController`** (Phase 8.2) — a single `IStartable` that subscribes to
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
1. Publishes `ActorHitMessage` with `HitOutcome.Absorb` (so Phase 8.2 `GridActorHitController` can react).
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

### Phase 8.2 — Actor Archetypes

**Goal:** Implement the three grid actor archetypes (Deflector, Absorber, Gatekeeper) and
expose them to config so they can appear on the grid. Add `GridActorHitController`.

This phase is the content prerequisite for Phase 8.3. Without actor variety, the procedural
algorithm produces monotonous grids.

#### Files

All new actor files live flat in `Slots/Actor/` (namespace `BalloonParty.Slots.Actor`),
alongside the existing `StaticActorModel` and its view/pool/settings/spawner files.

| File | Role |
|---|---|
| `CloudObstacleModel.cs` | Rename/replace `StaticActorModel`; `IWriteableSlotActor` + `IPassThrough`; spawn paths traverse freely |
| `BlockObstacleModel.cs` | New; `IWriteableSlotActor` only; spawn paths are blocked |
| `DeflectorActorModel.cs` | `IWriteableSlotActor`, `IHitable` → `Deflect`; no durability |
| `AbsorberActorModel.cs` | `IWriteableSlotActor`, `IHitable` → `Absorb`; no durability |
| `GatekeeperActorModel.cs` | `IWriteableSlotActor`, `IHasDurability`; `Deflect` on survive, `Pop` on kill |
| `GridActorHitController.cs` | `IStartable`; handles `ActorHitMessage` for non-balloon actors |
| `GridActorPrefabEntry.cs` | `Configuration/` — serializable config entry: prefab, weight, maxCount, actor type |
| View + pool channel per actor | Same pattern as `StaticActorView` |

`GridActorType` enum: `Cloud`, `Block`, `Deflector`, `Absorber`, `Gatekeeper`.

`CloudObstacleModel` is the direct successor to `StaticActorModel` — same role, clearer
name. `StaticActorSpawner` migrates to spawn `CloudObstacleModel` or `BlockObstacleModel`
based on config. The existing `StaticActorView` prefab maps to Cloud by default.

#### `BalloonModelBase` cleanup opportunity
When `GatekeeperActorModel` is introduced, it will prove `ScoreValue` and `NudgeOverrides`
belong on concrete classes, not the base. If `Gatekeeper` does not score and is not
nudgeable, removing those fields from `BalloonModelBase` becomes a concrete compiler
demand rather than a design preference — do it at this phase.

#### Failing tests
New fixture **`GridActorTests`**:
```
CloudObstacleModel_KindIsStatic
CloudObstacleModel_IsIPassThrough
CloudObstacleModel_IsNotIHitable

BlockObstacleModel_KindIsStatic
BlockObstacleModel_IsNotIPassThrough
BlockObstacleModel_IsNotIHitable

DeflectorActor_EvaluateHit_ReturnsDeflect
DeflectorActor_IsNotIHasDurability
DeflectorActor_IsNotIBalloonModel

AbsorberActor_EvaluateHit_ReturnsAbsorb
AbsorberActor_IsNotIHasDurability

GatekeeperActor_EvaluateHit_Survives_ReturnsDeflect_AndDecrementsHits
GatekeeperActor_EvaluateHit_KillingBlow_ReturnsPop_AndHitsRemainingIsZero

GridActorHitController_OnActorHit_IBalloonModel_IsIgnored
GridActorHitController_OnActorHit_Gatekeeper_WhenHitsReachZero_RemovesFromGrid
GridActorHitController_OnActorHit_Deflector_IsNotRemoved
```

---

### Phase 8.3 — Procedural Placement Engine

**Goal:** Replace `BalloonSpawner` + `StaticActorSpawner` with a single `GridSpawner` that
drives weighted, rule-based placement for all actor types.

`BalloonSpawner` and `StaticActorSpawner` retire from `IGridSpawner` registration once
`GridSpawner` covers all responsibilities. No flag day — they co-exist until `GridSpawner`
is validated in-game.

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
Level 1–3:   Only Simple + occasional Tough; no statics beyond Cloud obstacle
Level 4–6:   Deflectors introduced; Tough ratio rises
Level 7–10:  Gatekeepers introduced; Cracking balloons appear
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
   `GatekeeperActorModel` implement `IHasScore`? Resolves in Phase 8.2.

2. **Nudge for grid actors** — Should a `Deflect` hit on a `Gatekeeper` trigger a nudge
   animation? Nudge is currently tied to `IHasNudge` on the model. If yes, `Gatekeeper`
   implements `IHasNudge` + `NudgeOverrides` moves off `BalloonModelBase`. Resolves in 8.2.

3. **`ScoreValue` + `NudgeOverrides` on `BalloonModelBase`** — `UnbreakableBalloonModel`
   in Phase 8.1 forces `ScoreValue` off the base (it doesn't score). Phase 8.2's
   `GatekeeperActorModel` forces `NudgeOverrides` off the base. Clean up at the phase that
   forces it — not before.

4. **Grid actor controller pattern** — `GridActorHitController` (Phase 8.2) handles
   removal. But behavior-bound actors need per-actor controllers (Phase 9). Define the
   boundary clearly in 8.2: `GridActorHitController` handles reactive removal only;
   per-actor behavior controllers are Phase 9's domain.

5. **Pool key for grid actors** — current `PoolKey` convention derives from prefab name.
   `GridActorPrefabEntry.PoolKey` follows the same convention. Confirm before 8.2.

---

## Current State

| Phase | Status |
|---|---|
| 8.0 — Spawner Coordination | ✅ Complete |
| 8.1a — Absorb Routing | ✅ Complete |
| 8.1b — DamageContext Migration | ✅ Complete |
| 8.1c — UnbreakableBalloonModel | ✅ Complete |
| 8.2 — Actor Archetypes | **Next** |
| 8.3 — Procedural Placement | Blocked on 8.2 |
| 8.4 — Difficulty + Levels | Blocked on 8.3 |
| Phase 9 — Behavior-bound actors | Future (broadly defined) |
