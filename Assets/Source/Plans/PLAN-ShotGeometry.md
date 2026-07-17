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

## 6. Verification

Task 1: edit-mode tests for the backtrack math (head-on, oblique, grazing, degenerate inside
spawn); in-editor feel check that deflects read unchanged in normal play (angular corrections
are ≤ the old discretization error, so no retuning expected). Tasks 2–3 are editor tooling —
validated by use.
