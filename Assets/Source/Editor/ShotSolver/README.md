# ShotSolver

Editor tooling for `PLAN-ShotGeometry.md` Tasks 2 and 4 — sweeps aim angle against the live board
and reports which windows reach a target score. See `Assets/Source/Plans/PLAN-ShotGeometry.md` for
the math background (the shot as a deterministic billiard) and the rules this mirrors.

## Contents

| File | What it does |
|---|---|
| `ShotSimulator` | Pure, headless, static class — simulates one aim direction to completion, event-to-event (next analytic wall crossing, next analytic balloon-corridor entry, or next due balance pulse), never fixed-step. Reuses `ProjectileMotionResolver.TryComputeContactNormal` for deflect contacts. Mirrors the runtime's pop/deflect/shield/streak rules (see below) on a `ShotBalloonSnapshot` board — no `MonoBehaviour`, no live model, no allocation beyond the caller-owned working-set buffer. Events carry timestamps (`t += distance / speed(segment)`), and speed mirrors the cruise ramp exactly |
| `ShotSolverWindow` | `Tools > BalloonParty > Shot Solver` — play-mode-only window. Snapshots the live board (`SlotGrid`, resolved via `GameLifetimeScope.Container`), thrower origin, and projectile contact radius; sweeps N angles across a configurable arc; refines qualifying-window edges by bisection; plots score vs. angle as a strip of `EditorGUI` rect fills; lists qualifying windows; can draw the best window's centre-angle flight path into the Scene view; **Fire Best** re-sweeps and forces the shot via `ThrowerController.FireAt` |
| `ShotBoardDynamics` | The dynamic-board half (plan §7): owns a real headless `SlotGrid` + `GridBalanceQuery` + `BalancePlanner` over stub actors, schedules flight-rebalance pulses on the sim timeline, and keeps per-balloon nudge-impulse state. Built once per gather, reset per simulated flight |
| `ShotSimBoardActor` | The stub actors (`ShotSimDynamicActor`/`ShotSimStaticActor`) the dynamics grid is populated with, plus the per-flight snapshot structs for non-target actors |
| `ShotMotionMath` | Pure math: the nudge `Reach` envelope (mirrors `BalloonMotionTicker` exactly) and the moving-circle entry solve (relative-velocity quadratic, reduces to the exact static solve at zero velocity) |

## Rule mirroring

The simulator reproduces these runtime rules without touching a live `IBalloonModel`:

- **Deflect vs. pop** — `HitsRemaining > 1` survives as a deflect (mirrors
  `BalloonModelBase.EvaluateNormalHit`, direct-hit damage is always 1); `== 1` pops. The deflect
  normal comes from the real `ProjectileMotionResolver.TryComputeContactNormal` — the simulator's own
  analytic entry point already sits exactly on the contact circle, so that method's backtrack
  resolves to zero and returns the exact contact normal, not an approximation. Unbreakables enter
  the board as `int.MaxValue`-durability targets: permanent deflectors that never pop and score
  nothing on deflect, exactly like the game.
- **Radii, never hardcoded** — each target's contact circle is its live view's `ContactRadius`
  (`CircleCollider2D.radius × lossyScale.x`), plus the projectile's own contact radius per test.
  The differing collider setups (coloured 0.3125 at prefab scale ~0.866 ≈ 0.271 world; tough 0.325
  at scale 1; unbreakable 0.325 at scale ~1.097 ≈ 0.357 world) flow through per view — the same
  `ContactRadius` the deflect message itself carries, so game and sim can't disagree per balloon.
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
- **Cruise** — entry mirrors `ProjectileView.TryEnterCruise`: past the wall-bounce threshold, a
  walls-only lookahead of `threshold` more segments must be balloon-free (tested against
  time-evaluated centres) before the ramp engages. Speed mirrors `ProjectileMotionResolver.Step`:
  max multiplier `1 + CruiseSpeedPerShield × entry shields`, curve-sampled on the fraction of entry
  shields spent. Any balloon contact resets counter and cruise.
- **Balance & nudge (dynamic board)** — when the window supplies `ShotBoardDynamics`, rebalance
  pulses fire at `k × FlightRebalanceInterval` running the REAL `BalancePlanner` over a real
  `SlotGrid` (no mirrored rules — rule drift is impossible); moved balloons follow their hop
  waypoints as an arc-length polyline with OutQuad-eased progress over `TimeForBalloonsBalance`
  (mirroring `DOPath`'s constant-speed percentage under the project's DOTween default ease —
  `DOTweenSettings.asset`), and contacts against them solve the moving-circle quadratic
  linearized at the instantaneous eased velocity. Every contact nudges the target's occupied hex neighbours and deflects
  additionally shove the hit balloon, with the exact `Reach` impulse envelope; centres become
  `balancePosition(t) + Σ impulses(t)`, and a pulse landing mid-wobble seeds its path from the
  WOBBLED centre (waypoint 0 = view position in the live `StartBalanceTween`, with the ticker
  re-adding impulses on top of tween writes — the brief start-offset double-carry is faithful).
  Pops `Remove` from the dynamics grid so later pulses see the gaps. With no dynamics supplied the loop takes the original static fast path unchanged.

## Accepted approximations (plan §7)

- The live balancer notices an interval crossing on a render frame and defers the actual
  Balance() one more — modeled as a pulse execution delay the window estimates from the live
  frame time (~1.5 × `Time.smoothDeltaTime`), an estimate rather than the exact per-frame lag.
- Balance motion is the eased waypoint POLYLINE — Catmull-Rom's corner rounding between hops is
  the one part of the live path shape not reproduced.
- Heavy step budgets reset per simulated shot; in-game the turn budget may be part-spent at fire
  time (unknowable from a snapshot).
- Flight pulses never relocate roamers (`relocateRoamers: false`) — matches the live code.
- Idle sway/animator drift is not modeled (visual-only).
- Contact search against moving balloons linearizes at the segment-start instantaneous velocity,
  then re-solves once at the candidate hit time (two-pass fixed point) — the easing's curvature
  and the small, smooth nudge envelope are both absorbed by the refinement, not modeled exactly.
- (The live flight itself is now the exact billiard: walls mirror the overshoot and deflects carry
  the penetration remainder, so no truncation gap exists between game and sim at bounces.)

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
