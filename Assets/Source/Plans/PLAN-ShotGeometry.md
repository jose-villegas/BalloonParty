@page plan_shot_geometry Shot Geometry — the deterministic aim puzzle

# Shot Geometry — treating the shot as a solvable billiard

> Frames a shot as what it mathematically is — a one-parameter deterministic billiard — so
> boards can be authored/validated as geometry puzzles with provably fair "correct shot"
> windows, and players get a genuine think-then-aim moment.

---

## 1. The system, measured

One aim angle θ determines the entire flight. The pieces, with the shipped constants:

| Element | Behaviour | Constants |
|---|---|---|
| Motion | pure linear, constant speed, no gravity | speed 8, fixed dt 1/50 → 0.16 wu/step |
| Walls | analytic clamp + specular reflect, all four sides; each bounce costs a shield | rect x ∈ [−2.25, 2.25], y ∈ [−4.5, 4] (width 4.5 = 6 cols × 0.75) |
| Pops | pierce — the ray continues unbent; balloon removed synchronously | corridor half-width = r_balloon + r_projectile; r_balloon ≈ 0.271 (0.3125 × 0.866 scale) |
| Tough deflect | specular reflect off the circle's radial normal at contact | combined radius R ≈ r_balloon + r_projectile |
| Shields | wall bounces spend one; every streak ≥ 2 same-colour pop refunds one | start 1 |

Grid: 6 × 11 hex slots, column pitch 0.75 (staggered rows offset 0.375), row pitch 0.65.

## 2. Why it is exactly solvable

- **Walls unfold.** Axis-aligned mirrors mean any wall-bounce path is a straight line to a
  mirror image of the target in the reflected-lattice plane (x′ = 2k·4.5 ± x, same in y).
  Every banked shot is enumerable in closed form — this is the intuition players already have.
- **Pops are corridor membership.** Between bounces the shot is a ray; it pops exactly the
  balloons whose centres sit within r_balloon + r_projectile of it. The staggered-column
  clearance is 0.375, so **r_projectile ≈ 0.104 is the knife edge** between "one clean column"
  and "a three-column swathe" — a deliberate design constant, not an accident to discover.
- **The outcome map O(θ) is piecewise-smooth** and its only breakpoints are tangency angles —
  where the (unfolded) ray grazes some balloon image circle — every one of which is closed-form.
  Enumerate the critical angles, evaluate one θ per interval, and the exact solution set with
  exact window widths falls out. A board is a *fair* puzzle iff its winning window exceeds the
  input resolution (~0.1°/px of aim drag; threshold ≥ 1–2°).

## 3. The dispersion law (design constraint)

Tough deflects are convex scatterers — a Sinai billiard. One deflect multiplies an aim-angle
spread by ≈ 1 + 2L/(R·cos φ) (free flight L ≈ 1.5–3, combined R ≈ 0.37): **×10–20 per deflect**,
worse at grazing incidence. Consequences:

- Budget **at most one intentional deflect** before any precision-critical segment.
- Multi-deflect sequences are fair only into forgiving continuations (e.g. the tough-cleanup
  endgame, where the walls keep returning the shot to the only objects left).
- The solver must report window widths, not just existence — a solution narrower than input
  resolution is luck, not a puzzle.

## 4. The blocker: discretized deflect contact

Today the deflect normal is radial at wherever the fixed-step trigger caught the projectile —
up to 0.16 wu *inside* a ≈ 0.27 circle, displacing the contact point by up to ~30°. The flight
is still deterministic, but aim→outcome is a staircase whose treads depend on step phase; a
solver would have to emulate the stepping bit-exactly, and fair windows fragment.

**Fix (Task 1): exact-contact deflection.** Backtrack the analytic entry point along the
incoming direction — solve |p − t·d − C| = R for the smallest positive t (the same line–circle
entry math as `TraceHitGeometry.TryFindSurfaceHit`) — and reflect off the normal at that
point. Combined radius from the two colliders involved, threaded through the deflect message.
Falls back to the current radial normal on degenerate input. With this, the game IS the clean
billiard of §2.

## 5. Tasks

1. **Exact-contact deflect** — geometry helper (shared, edit-mode tested), radius plumbing
   (balloon collider world radius on the deflect message; projectile contact radius from its
   collider), `ProjectileMotionResolver.Deflect` rewritten to backtrack-then-reflect.
2. **Shot solver (editor)** — given a board snapshot: enumerate critical angles (tangencies to
   all reachable balloon images within the shield budget), evaluate intervals, plot score-vs-θ,
   report windows ≥ threshold. Editor window per the EffectPreview/maps-window tooling patterns.
   **Done** — `Assets/Source/Editor/ShotSolver/` (`ShotSimulator` + `ShotSolverWindow`, see its
   README). Sweeps N samples and refines window edges by bisection rather than the exact critical-
   angle enumeration described above — that enumeration is still v2.
3. **Design pass** — measure and *choose* r_projectile against the 0.104 knife edge; decide the
   fair-window threshold; optionally wire the solver into spawn validation ("reroll toughs
   until a ≥ threshold window exists") for authored puzzle levels.
4. **Dynamic-board simulation** — see §7. Model the two systems that move the board *during*
   the shot (flight rebalance pulses, nudge wobble), so long ricochet predictions stop
   diverging after the first few hits.

## 6. Verification

Task 1: edit-mode tests for the backtrack math (head-on, oblique, grazing, degenerate inside
spawn); in-editor feel check that deflects read unchanged in normal play (angular corrections
are ≤ the old discretization error, so no retuning expected). Tasks 2–3 are editor tooling —
validated by use. Task 4: planner-extraction refactor must keep every existing balancer
edit-mode/play-mode test green (behavior-identical); sim fidelity validated with Fire Best —
fire the drawn path on a live board and compare where reality diverges.

## 7. Task 4 — dynamic-board simulation (balance + nudge + N colors)

The static-board sim is exact only until the board reacts. Two reaction systems run mid-flight:

- **Flight rebalance** (`BalloonBalancer.TickFlightRebalance`): every `FlightRebalanceInterval`
  seconds while a shot is free, unbalanced actors move to `GridBalanceQuery.OptimalNextEmptySlot`
  (intervention-priority order, heavy `MaxBalanceSteps` caps), views tweening over
  `TimeForBalloonsBalance`. Pops open gaps → the next pulse reflows the board under the shot.
- **Nudge wobble** (`NudgeService` → `BalloonMotionTicker`): every hit nudges the target's
  `IHasNudge` neighbors (`NudgeType.Neighbor`); deflects additionally shove the hit balloon
  (`NudgeType.Deflect`, direction = projectile heading). Displacement is an impulse stack:
  `offset(t) = Σ amplitude·dir · Reach(elapsed/duration)`, `Reach` = analytic out-and-back
  (ease-out-quad to peak at progress 0.5, mirrored back to zero). Colliders ride the view, so
  wobble genuinely changes contacts.

### Design

**(a) Timeline.** Events gain timestamps: `dt = distance / speed(t)`. Speed mirrors base speed
and the cruise ramp (`CruiseWallBounceThreshold` / `CruiseMaxSpeedMultiplier` / `CruiseRampCurve`
against shields spent since cruise entry) — the sim already counts wall bounces.

**(b) Balance via the real rules, not a mirror.** `SlotGrid` and `GridBalanceQuery` are plain
headless classes (edit-mode tests already construct them). Extract the decision core of
`BalloonBalancer.Balance` (pass collection → priority sort → `TryBalanceSlot` loop, minus views/
tweens/path-holder) into a pure `BalancePlanner` that returns moves `(actor, from, to)`; the
balancer consumes it (behavior-identical refactor), and the sim instantiates a real
`SlotGrid` + `GridBalanceQuery` + planner over stub actors built from the snapshot (slot index,
dynamic-or-static, `BalancePriority`, `MaxBalanceSteps`). Rule drift becomes impossible.
Pulses fire at `t = k·FlightRebalanceInterval`; each move animates as **linear** motion
from start to final slot over `TimeForBalloonsBalance` (approximating the Catmull-Rom path);
contacts against a moving balloon solve the same quadratic with relative velocity
`|p − c₀ + t(s·d − v)| = r`.

**(c) Nudge as analytic impulses.** The sim keeps an impulse list per balloon with the exact
`Reach` envelope, fed by the same triggers (hit → neighbors, deflect → target; distances/
durations from `NudgeOverrideResolver` defaults). Centers become `home(t) + Σ impulses(t)` —
smooth and small, so contact finding freezes centers at the segment's start time, then refines
once at the candidate hit time (two-pass fixed point).

**(d) N colors.** Scoring is already colour-id-string keyed (streak = consecutive same colour,
mirroring `ColorStreakTracker`); the dynamic extension must keep it that way — no single-colour
assumptions. Multi-colour boards then work unchanged; window reporting can later gain a
per-colour filter for milestone masks.

### Accepted approximations (log, don't hide)

- The balancer defers a requested balance one frame; the sim applies it at pulse time.
- Multi-waypoint Catmull-Rom balance paths ≈ straight line to the final slot.
- Heavy step budgets reset per shot in the sim; in-game the turn budget may be part-spent at
  fire time (unknowable from a snapshot).
- Flight pulses never relocate roamers (`relocateRoamers: false`) — matches the code.
- Idle sway/animator drift is not modeled (visual-only; colliders follow the motion ticker's
  base + impulses, which we do model).

### Staging

- **4a** — `BalancePlanner` extraction + edit-mode tests (runtime refactor; all existing
  balancer tests stay green). **Done** — `Assets/Source/Balloon/Controller/BalancePlanner.cs`.
- **4b** — sim timeline + pulse schedule + moving-circle contacts (editor, consumes 4a).
  **Done** — `ShotBoardDynamics` + `ShotMotionMath` + timestamped `ShotSimulator` events; the
  cruise ramp (shield-scaled max, lookahead-confirmed entry) is mirrored in the same pass.
- **4c** — nudge impulse modeling (editor). **Done** — exact `Reach` envelope, hit-neighbour and
  deflect-target triggers, two-pass contact refinement (see the ShotSolver README's
  accepted-approximations list).
- **4d** — polish: per-colour window filters, robustness bands (does the window survive ±nudge
  amplitude), Fire Best divergence readout. **Done** — Target Colour attribution filter
  (score-only; mechanics unfiltered), ±nudge radius-bias robustness tags, and the live
  predicted-vs-actual divergence tracker (max/when/yellow overlay).
