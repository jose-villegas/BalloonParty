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
8.0  Spawner Coordination     — infrastructure; explicit ordering replaces implicit contract
8.1  Hit System Completion    — UnbreakableBalloonModel + Absorb routing (deferred from 7.5)
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

These all live in `Slots/Actors/` as separate model/view pairs, not subclasses of anything
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

### Phase 8.0 — Spawner Coordination

**Goal:** Replace the implicit ordering contract between `StaticActorSpawner` and
`BalloonSpawner` with an explicit priority-based coordination layer. Zero gameplay change —
purely infrastructure.

**Why now:** Every Phase 8+ spawner needs this integration point. One coordinator is
simpler and more testable than n implicit contracts.

#### Design

```csharp
public interface IGridSpawner
{
    int SpawnPriority { get; }  // lower = runs first
    UniTask SpawnAsync(CancellationToken ct);
}

internal class GridSpawnerCoordinator : IStartable
{
    // Owns the single NavigationState.Game wait.
    // Collects all IGridSpawner registrations via VContainer.
    // Calls spawners in SpawnPriority order, awaiting each before the next.
}
```

Priority convention:
```
0   Static actors   (must exist before balloons — balancer support)
100 Balloon actors  (fills around statics)
```

`StaticActorSpawner` and `BalloonSpawner` each implement `IGridSpawner`. The coordinator
takes over the `NavigationState.Game` wait; both spawners' `SpawnAsync` become purely
placement logic with no navigation dependency.

#### What changes in existing spawners
- `StaticActorSpawner.Start()` loses its synchronous coordination comment — the
  coordinator's ordering is now explicit.
- `BalloonSpawner.PrewarmAndPopulateAsync` still prewarms pools unconditionally on `Start()`;
  the population step moves into `SpawnAsync`.

#### Failing tests
```
GridSpawnerCoordinator_CallsSpawnersInPriorityOrder
  — two mock IGridSpawner; lower priority runs first, verified by call sequence
  — fails at compile until GridSpawnerCoordinator exists

GridSpawnerCoordinator_AwaitsEachSpawnerInSequence
  — second spawner does not start until first's SpawnAsync completes
```

---

### Phase 8.1 — Hit System Completion

**Goal:** Deliver the two items deferred from Phase 7.5 (`UnbreakableBalloonModel` and
`Absorb` routing), and introduce `DamageContext` so item mechanics can bypass the
unbreakable state when configured to do so.

#### `DamageContext` + `DamageFlags`

`IHitable.EvaluateHit(int damage)` is replaced by `IHitable.EvaluateHit(DamageContext context)`.
This is a breaking change across all callers — worth absorbing here since
`UnbreakableBalloonModel` is being added in the same phase.

```csharp
// Slots/Capabilities/
public readonly struct DamageContext
{
    public readonly int Damage;
    public readonly DamageFlags Flags;

    public DamageContext(int damage, DamageFlags flags = DamageFlags.None)
    {
        Damage = damage;
        Flags = flags;
    }
}

[Flags]
public enum DamageFlags
{
    None     = 0,
    Piercing = 1 << 0,  // ignores all defensive hit responses — pops regardless of HitsRemaining or permanent Deflect
    // Future: BypassesShield = 1 << 1, etc.
}
```

`Piercing` is the flag name — it borrows from established game-design vocabulary ("piercing
damage ignores armor") and generalises correctly: it applies to *any* defensive posture an
actor holds, not just unbreakable. A 5-hitpoint `ToughBalloonModel` hit with `Piercing`
pops in one shot; an `UnbreakableBalloonModel` hit with `Piercing` also pops. One flag,
consistent semantics across all actor types.

`BalloonModelBase.EvaluateHit` handles `Piercing` before normal durability logic:
```csharp
public override HitOutcome EvaluateHit(DamageContext context)
{
    if (context.Flags.HasFlag(DamageFlags.Piercing))
    {
        HitsRemaining.Value = 0;
        return HitOutcome.Pop;
    }
    var survives = HitsRemaining.Value - context.Damage > 0;
    HitsRemaining.Value -= context.Damage;
    return survives ? HitOutcome.PassThrough : HitOutcome.Pop;
}
```

**Callers:**
- `ProjectileView` — passes `new DamageContext(msg.Damage)` (no flags; standard hit)
- Item handlers (bomb, laser, lightning, …) — pass `new DamageContext(damage, _itemConfig.DamageFlags)`
- All tests — wrap raw `int` in `new DamageContext(n)`

**`ItemSettings`** gains `[SerializeField] private DamageFlags _damageFlags`. Bomb and laser
can be toggled in the SO to include `Piercing`; all others default to `None`.

#### `UnbreakableBalloonModel`

```csharp
internal class UnbreakableBalloonModel : BalloonModelBase, IHitable
{
    // EvaluateHit returns Pop only when BypassesUnbreakable is set — otherwise Deflect.
    // No IHasDurability, no HitsRemaining.
}
```

```csharp
public override HitOutcome EvaluateHit(DamageContext context) =>
    context.Flags.HasFlag(DamageFlags.Piercing)
        ? HitOutcome.Pop
        : HitOutcome.Deflect;
```

`BalloonSpawner` switch gains:
```csharp
BalloonType.Unbreakable => new UnbreakableBalloonModel(config),
```

#### `ScoreValue` on `BalloonModelBase`

`UnbreakableBalloonModel` does not score — it is a permanent obstacle, not a target.
This makes `ScoreValue` on `BalloonModelBase` a misplaced field. Remove it from the base
at this phase; `BalloonModel` and `ToughBalloonModel` hold their own `ScoreValue` from
config. `IHasScore` (already exists) is the read interface for `ScoreController`.

#### `Absorb` routing in `ProjectileView`

On `HitOutcome.Absorb`: call `ForceKill()` on the projectile tween, then publish
`ProjectileDestroyedMessage` to end the turn immediately. `BalloonController` already has
the stub case — no change needed there.

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

| File | Location | Role |
|---|---|---|
| `CloudObstacleModel.cs` | `Slots/Actors/` | Rename/replace `StaticActorModel`; `IWriteableSlotActor` + `IPassThrough`; spawn paths traverse freely |
| `BlockObstacleModel.cs` | `Slots/Actors/` | New; `IWriteableSlotActor` only; spawn paths are blocked |
| `DeflectorActorModel.cs` | `Slots/Actors/` | `IWriteableSlotActor`, `IHitable` → `Deflect`; no durability |
| `AbsorberActorModel.cs` | `Slots/Actors/` | `IWriteableSlotActor`, `IHitable` → `Absorb`; no durability |
| `GatekeeperActorModel.cs` | `Slots/Actors/` | `IWriteableSlotActor`, `IHasDurability`; `Deflect` on survive, `Pop` on kill |
| `GridActorHitController.cs` | `Slots/Actors/` | `IStartable`; handles `ActorHitMessage` for non-balloon actors |
| `GridActorPrefabEntry.cs` | `Configuration/` | Serializable config entry: prefab, weight, maxCount, actor type |
| View + pool channel per actor | `Slots/Actors/` | Same pattern as `StaticActorView` |

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

`GridSpawner` is the Phase 8.0 `IGridSpawner` implementor with `SpawnPriority = 50`.

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
2. Disable `BalloonSpawner`/`StaticActorSpawner` VContainer registration when `GridSpawner`
   passes in-game validation.
3. Remove the retired spawners.

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
Level 1–3:   Only Simple + occasional Tough; no statics beyond Obstacle
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
| 8.0 — Spawner Coordination | Next |
| 8.1 — Hit System Completion | Next (can run in parallel with 8.0) |
| 8.2 — Actor Archetypes | Blocked on 8.1 |
| 8.3 — Procedural Placement | Blocked on 8.2 |
| 8.4 — Difficulty + Levels | Blocked on 8.3 |
| Phase 9 — Behavior-bound actors | Future (broadly defined) |






