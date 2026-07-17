# ShotSolver

Editor tooling for `PLAN-ShotGeometry.md` Task 2 — sweeps aim angle against the live board and
reports which windows reach a target score. See `Assets/Source/Plans/PLAN-ShotGeometry.md` for the
math background (the shot as a deterministic billiard) and the rules this mirrors.

## Contents

| File | What it does |
|---|---|
| `ShotSimulator` | Pure, headless, static class — simulates one aim direction to completion, event-to-event (next analytic wall crossing or next analytic balloon-corridor entry), never fixed-step. Reuses `ProjectileMotionResolver.TryComputeContactNormal` for deflect contacts. Mirrors the runtime's pop/deflect/shield/streak rules (see below) on a `ShotBalloonSnapshot` board — no `MonoBehaviour`, no live model, no allocation beyond the caller-owned working-set buffer |
| `ShotSolverWindow` | `Tools > BalloonParty > Shot Solver` — play-mode-only window. Snapshots the live board (`SlotGrid`, resolved via `GameLifetimeScope.Container`), thrower origin, and projectile contact radius; sweeps N angles across a configurable arc; refines qualifying-window edges by bisection; plots score vs. angle as a strip of `EditorGUI` rect fills; lists qualifying windows; can draw the best window's centre-angle flight path into the Scene view |

## Rule mirroring

The simulator reproduces these runtime rules without touching a live `IBalloonModel`:

- **Deflect vs. pop** — `HitsRemaining > 1` survives as a deflect (mirrors
  `BalloonModelBase.EvaluateNormalHit`, direct-hit damage is always 1); `== 1` pops. The deflect
  normal comes from the real `ProjectileMotionResolver.TryComputeContactNormal` — the simulator's own
  analytic entry point already sits exactly on the contact circle, so that method's backtrack
  resolves to zero and returns the exact contact normal, not an approximation.
- **Colourless vs. coloured scoring** — a `ShotBalloonSnapshot.ColorId` of null/empty models a
  balloon that does NOT implement `IHasColor` (`ToughBalloonModel`); non-null models one that does
  (`BalloonModel`). Tough pops score a flat `ScoreValue` and reset the streak — mirrors
  `ScoreController.RecordStreakMultiplier` collapsing `ToughBalloonModel`'s per-point
  `breaksStreak: true` attributions to a locked ×1 multiplier. Green pops score
  `ScoreValue × streak`, where streak follows `ColorStreakTracker.Record`'s same-colour-extends /
  different-colour-resets rule.
- **Colour adoption & shield refund** — mirrors `ProjectileHitResolver`: the projectile's tracked
  colour adopts the popped balloon's colour (off the OLD colour, same order as
  `ApplyColorChange` running before the streak record), then a shield is refunded when the streak
  reaches ≥ 2 of the projectile's now-current colour.
- **Walls** — analytic per-axis crossing time, then `Vector2.Reflect` off the (possibly summed, for
  an exact corner hit) wall normal — same rectangle and reflect convention as `WallLimits`. Each
  bounce costs a shield; shields dropping below zero ends the flight (`Died`).
- **Rainbow / wildcard balloons and buffs are out of scope** — the board snapshot has no wildcard
  flag, matching the plan's own scope (§1: pops, tough deflects, shields). Note this if the solver is
  ever pointed at a board with rainbow balloons or an active rainbow-buffed shot.

## Sweep and refine

`ShotSolverWindow` samples N angles (default 2048) across a configurable arc (default 10°–170°,
measured from +X), then finds contiguous runs where `RawScore >= target`. Each run's edges are
refined by bisection to ~0.01° (the plan's §2 fair-window resolution threshold), not by enumerating
exact tangency angles — the plan calls that exact enumeration v2. The "best" window is the widest
qualifying one; "Draw Best" re-simulates its centre angle with the simulator's optional path-capture
list and draws it via `SceneDrawingHelper.DrawWorldPolyline`.

## Usage

1. Enter Play Mode with the Game scene loaded.
2. `Tools > BalloonParty > Shot Solver`.
3. Set target score, min window width, arc, and sample count (or keep the defaults).
4. **Run Sweep** — reads the live board/thrower/projectile once, sweeps, refines, and lists windows.
5. Toggle **Draw Best** to see the widest qualifying window's flight path in the Scene view.

The design-time follow-up (Task 3: choosing `r_projectile` against the 0.104 knife edge, the fair
threshold, and optionally wiring the solver into spawn validation) is not implemented here.
