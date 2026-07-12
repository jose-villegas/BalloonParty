@page plan_unified_hex_motion Unified Hex Motion

# Unified Hex Motion — one weight model for balance and pressure

## Motivation

Balloon movement currently has two disjoint algorithms:

- **Balance** (`GridBalanceQuery` + `BalloonBalancer`): buoyancy — a balloon considers only its two
  *up* neighbours, scored by support-cone weight plus the actor's `IBalanceInfluence.WeightBias`
  (separation, diagonal color lines). Race-ordered by `BalancePriority`, budgeted per turn by
  `MaxBalanceSteps`.
- **Pressure** (`PressureCascade` + `TryRelievePressure`): a undirected BFS that rays out in all six
  hex directions from a blocked spawn entry, finds the shortest chain of cells to a free slot, and
  shoves each link one step. It knows nothing about weights, tendencies, or direction of force.

This plan unifies both into a single evaluation: **every balloon scores all six hex neighbours as
move candidates through one weight function**. Buoyancy is the always-on preference for the two up
neighbours; sideways and down moves are only unlocked by an active **shove** — a directed pressure
vector — whose contribution dominates every other term.

## The weight model

For an actor `a` at slot `S`, a neighbour slot `n` (one of the six from
`HexCoordinates.HexNeighborIndices`), and an optional shove `v`:

```
side/down candidates exist only while a shove is active, and only when
dot(dir(S→n), v.Direction) >= 0            (never move back into the shover)

W(a, S, n, v) = Buoyancy(n)                (up neighbours only)
              + a.WeightBias(grid, n)      (existing tendencies, all classes)
              + PressureGain * max(0, dot(dir(S→n), v.Direction))   (shove active)

Buoyancy(n)  = support-cone weight (GridBalanceQuery.CalculateWeight semantics)
dir(S→n)     = normalized world-space delta (IndexToWorldPosition), hex-correct
PressureGain = internal const, orders of magnitude above any buoyancy+bias total
               (support caps ≈ 2×Rows; biases are small ints) — pressure always wins
```

Degenerate case — **no shove**: only the two up neighbours are candidates and the score reduces to
exactly today's `OptimalNextEmptySlot` (support weight + bias). **Behavioral parity is a hard
requirement**: straight-up evaluated first, parity-shifted second, accepted on `>=` so the shifted
slot still wins ties. The authored tendencies (tough separation, soap clump, diagonal color) must
produce identical decisions before and after.

## The shove and its propagation

`ShoveVector`: a world-space direction plus the origin actor/slot. Spawn pressure enters at the
column's blocked entry cell with direction = board-up. Each propagation hop re-derives the
direction: when balloon `B` (shoved along `v`) cannot move, it shoves an occupied neighbour `O`
with direction `dir(B→O)` — the vector bends along the path, exactly "where the shove comes from".

Resolution (`PressurePropagation`, replaces `PressureCascade`):

1. Start at the entry's lowest non-traversable blocker (same seed rule as today). Frontier holds
   `(actor, incomingShove, parent)`; visited set over slots; all buffers reused (zero steady-state
   allocation — this runs during overflow crunch).
2. Pop a node; score its **empty** neighbours with `W` under the incoming shove. Any valid
   candidate → **mover found**: take the best-scoring slot.
3. No empty candidate → propagate: for each **occupied** neighbour holding an `IPressureMovable`,
   enqueue with the re-derived direction. Alignment < 0 is not propagated (no backflow).
   The frontier is ordered by **cumulative path score** (best-first, deterministic ties by
   insertion order):

   ```
   Heaviness(O) = O.MaxBalanceSteps == 0 ? 0 : HeavinessPenalty / O.MaxBalanceSteps
   hopScore(B→O) = dot(dir(B→O), incomingShove) − Heaviness(O)
   pathScore     = Σ hopScores along the propagation chain
   ```

   `MaxBalanceSteps` is thus ignored as a *gate* under pressure (a shoved balloon moves regardless)
   but counted as a *cost*: when several paths could relieve the pressure, chains through heavy
   movers score lower and are explored later — heavies are less likely to be shoved when
   alternatives exist, yet still shove when they're the only way. `HeavinessPenalty` is an internal
   tunable const (~0.75: a perfectly aligned hop through a 1-step heavy scores below a half-aligned
   hop through a light balloon).
4. `PushResponse` is honoured mid-propagation, as today: a `RelocateNearest`/`RelocateFarthest`
   actor (unbreakable) is a **terminal** — it vacates by relocating to its free slot and the chain
   ends at its vacated cell.
5. Statics (puff/bush — not `IPressureMovable`) block: not move targets, not propagation targets.
   The traversability rule used to seed the search (`TryFindLowestBlocker`) is preserved.
6. Frontier exhausted → resolution fails → the existing overflow/rejection path (unchanged).

**Saturation guarantee**: pressure must be able to fill every *reachable* empty slot (reachable =
not sealed behind statics). A rejected spawn charges the player a hit point, so the directed model
may never reject while the old cascade could have placed. If the strict no-backflow pass exhausts
its frontier while reachable space remains, `TryResolve` falls back once to a relaxed pass —
undirected (alignment term dropped, heaviness costs kept) — before reporting failure. The shove
direction is a preference for *which* path wins, never a reason to lose a placement. Saturation
tests drive repeated resolve→execute→spawn loops across scenarios (bent pockets, corner gaps,
heavy blockers, relocation terminals, static seals) and assert the board fills completely except
sealed pockets.

Execution: the propagation returns the move list (actor, from, to) along the parent chain, from the
mover backwards, so every destination is empty at move time. `BalloonBalancer` stays the sole grid
mutator: it executes the moves (Reserve → Remove → Place → `RecordPath`) and animates them in the
same `AnimatePaths` batch as balance moves — one motion pipeline, one visual language.

## Interplay rules

- **Pressure dominates movement, weighs heaviness**: shove-driven moves ignore the
  `MaxBalanceSteps` budget as a gate and `BalancePriority` (as `TryRelievePressure` does today) —
  but the budget re-enters as the propagation's heaviness cost above, steering shove paths around
  heavy movers when lighter routes exist. The step budget only *blocks* spontaneous buoyancy.
- **Tendencies still whisper**: `WeightBias` is added under a shove too, so between two equally
  aligned escapes a tough still prefers away-from-tough, colors still prefer their diagonals — but
  the `PressureGain` term guarantees they never override the force direction.
- Balance keeps its race (priority-ordered rounds), roam pre-pass, and per-turn budgets untouched.
- **Priority still reigns above the weights**: `BalancePriority` orders who acts each round in the
  unified model too, and the unbreakable's roam contract (`IPreBalanceRelocatable`) keeps letting it
  pick any resting slot — the weight function governs *where a move goes*, not *who gets to move*.

## Motion style — direct animation for heavy movers

During a resolve an actor can accumulate several waypoints (`RecordPath` appends every step; roam +
race moves chain into one `DOPath`). For most balloons the multi-waypoint float reads right, but a
heavy mover like the unbreakable should be seen moving **from its start to its final slot in one
motion**, not touring every intermediate position of the resolve.

Per-entry knob `DirectBalanceMotion` (`BalloonPrefabEntry` → `BalloonModelConfig` → exposed via
`IBalanceInfluence`, like the other physicality knobs; authored true for Unbreakable). In
`AnimatePaths`, an actor with direct motion animates to the **last** recorded waypoint only (single
target — straight tween from wherever it currently is), discarding the intermediate ones. Note the
disturbance stamping along the tween then follows the direct line rather than the resolve path —
intended: the visual and the field wake agree.

## Gizmos

A debug recorder captures each propagation (and optionally the last balance round): nodes with
incoming shove direction and processing order, propagation edges with alignment weight, per-node
candidate scores, and the chosen chain. Recording methods are
`[Conditional("UNITY_EDITOR")]` so release builds pay nothing.

`BalanceGizmos` (MonoBehaviour, gizmo fields and `OnDrawGizmos` guarded per the project's gizmo
convention, drawing via `GizmoDrawingHelper`): shove arrows colored by alignment, the executed
chain highlighted, rejected/blocked nodes marked, candidate weights as editor labels. Events fade
after a few seconds; a ring buffer keeps the last N resolutions inspectable after the fact.

## File map

| Change | File |
|---|---|
| new | `Slots/Grid/ShoveVector.cs` (struct: world direction + origin slot) |
| new | `Slots/Grid/MoveWeightEvaluator.cs` (pure scorer over `SlotGrid`; owns the support-cone weight + memo, absorbed from `GridBalanceQuery`) |
| rewrite | `Slots/Grid/GridBalanceQuery.cs` (`IsUnbalanced` unchanged; `OptimalNextEmptySlot` delegates to the evaluator with no shove) |
| new | `Balloon/Controller/PressurePropagation.cs` (directed BFS → move list; replaces `PressureCascade`) |
| delete | `Balloon/Controller/PressureCascade.cs` (once parity tests pass) |
| modify | `Balloon/Controller/BalloonBalancer.cs` (`TryRelievePressure` drives the propagation + executes moves) |
| new | `Balloon/Controller/BalanceDebugRecorder.cs` (+ `Balloon/View/BalanceGizmos.cs`) |
| tests | `MoveWeightEvaluatorTests`, `PressurePropagationTests` (port `PressureCascade` scenarios) |

## Constraints

- **Zero steady-state GC** in both paths (reused buffers, cached comparers, no LINQ) — this runs in
  the frenetic loop.
- **Deterministic**: stable orderings everywhere; no randomness the current paths don't have.
- **Parity first**: the no-shove path must reproduce current balance decisions exactly; the
  propagation must pass the ported `PressureCascade` test scenarios (straight chain, relocation
  terminal, blocked board) before the old class is deleted.
- New `.cs` files need `.meta` (Unity generates with the editor open) and a manual
  `<Compile Include>` in the csprojs if the watcher lags.
- Verify: `dotnet build` on Runtime + Editor + Tests.EditMode, `python3 Tools/style_audit.py`.
  Gizmo drawing itself needs an in-editor check.

## Phases

1. **Plan** — this document. ✅
2. **Foundation** — `ShoveVector`, `MoveWeightEvaluator` (+ tests), `GridBalanceQuery` delegation
   with parity tests.
3. **Propagation** — `PressurePropagation` (+ ported/new tests), balancer integration, delete
   `PressureCascade`, README updates.
4. **Visualization** — recorder + `BalanceGizmos` (component added to the Game scene in-editor).
5. **Playtest & tune** — in-editor: parity feel-check of balance, overflow crunch scenarios,
   gizmo review; tune `PressureGain` only if a tendency visibly fights a shove.
