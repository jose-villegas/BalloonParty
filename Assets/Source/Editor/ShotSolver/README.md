# ShotSolver

Editor tooling for the shot geometry system — sweeps aim angle against the live board
and reports which windows reach a target score. The shot is modelled as a deterministic billiard;
the simulation mirrors the runtime projectile rules.

## Contents

Only `ShotSolverWindow` lives in this folder. The simulation classes it drives live in
`Assets/Source/Solver/` (runtime `BalloonParty.Runtime` assembly, not `Editor/`) so they stay
unit-testable outside the editor.

| File | Location | What it does |
|---|---|---|
| `ShotSolverWindow` | `Editor/ShotSolver/` | `Tools > BalloonParty > Shot Solver` — play-mode-only window. Snapshots the live board (`SlotGrid`, resolved via `GameLifetimeScope.Container`), thrower origin, and projectile contact radius; sweeps N angles across a configurable arc; refines qualifying-window edges by bisection; plots score vs. angle as a strip of `EditorGUI` rect fills; lists qualifying windows; can draw the best window's centre-angle flight path into the Scene view; **Fire Best** re-sweeps and forces the shot via `ThrowerController.FireAt` |
| `ShotSimulator` | `Solver/` | Pure, headless, static class — simulates one aim direction to completion, event-to-event (next analytic wall crossing, next analytic balloon-corridor entry, or next due balance pulse), never fixed-step. Reuses `ProjectileMotionResolver.TryComputeContactNormal` for deflect contacts. Mirrors the runtime's pop/deflect/shield/streak/rainbow rules, including Phase A's interactive statics (see below), on a `ShotBalloonSnapshot` board — no `MonoBehaviour`, no live model, no allocation beyond the caller-owned working-set buffer. Events carry timestamps (`t += distance / speed(segment)`), and speed mirrors the cruise ramp exactly |
| `ShotBoardSnapshot` | `Solver/` | The board's data types: `ShotBalloonSnapshot` (one target's geometry, `ColorProfile` colour identity, optional `BalanceProfile`, `ShotContactKind`, item), built only via its named factories (`ForColorTarget`/`ForToughTarget`/`ForRainbowTarget`/`ForStaticContact`) so every caller's field mapping stays honest as the struct grows; `ShotBalloonState`, its mutable per-simulation copy |
| `ShotFlightState` | `Solver/` | Mutable per-flight state passed by `ref` through `Simulate` — position/direction/shields/elapsed, cruise/pierce state, and the Phase D-core buff/streak/colour fields (`HasRainbowBuff`, `SpeedBuffMultiplier`, `StreakColor`/`StreakCount`, `DeferredPops`, `ProjectileColor`) |
| `ShotBoardGather` | `Solver/` | Snapshots the live board/thrower/projectile into the sim's input structs (`ShotBoardGather.Gather`), and converts sweep angles to world directions (`DirectionFromDegrees`). Called by `ShotSolverWindow` |
| `ShotBoardDynamics` | `Solver/` | The dynamic-board half (plan §7): owns a real headless `SlotGrid` + `GridBalanceQuery` + `BalancePlanner` over stub actors, schedules flight-rebalance pulses on the sim timeline, and keeps per-balloon nudge-impulse state. Built once per gather, reset per simulated flight |
| `ShotSimBoardActor` | `Solver/` | The stub actors (`ShotSimDynamicActor`/`ShotSimStaticActor`) the dynamics grid is populated with, plus the per-flight snapshot structs for non-target actors |
| `ShotMotionMath` | `Solver/` | Pure math: the nudge `Reach` envelope (mirrors `BalloonMotionTicker` exactly) and the moving-circle entry solve (relative-velocity quadratic, reduces to the exact static solve at zero velocity) |
| `ShotItemLayer` | `Solver/` | Item-carrier layer (plan Phase C) — resolves a popped host's `ItemType` into a `ShotItemOutcome` (shield/pierce/speed-buff grants) plus a list of `EffectHit`s against a bound `ShotSimEffectBoard`, draining chained activations breadth-first up to `MaxActivationsPerFlight` |
| `ShotSimEffectBoard` | `Solver/` | The sim's `IEffectBoard` adapter (`Item/Effects/`) — rebuilds `EffectOccupant`s from the active working set (statics and the popped host excluded) over a `ShotSlotLattice` (`HexCoordinates` math only, no live grid/dynamics reference), so an item core selects identically whether or not a dynamic board is present |

## Rule mirroring

The simulator reproduces these runtime rules without touching a live `IBalloonModel`:

- **Deflect vs. pop** — `HitsRemaining > 1` survives as a deflect (mirrors
  `BalloonModelBase.EvaluateNormalHit`, direct-hit damage is always 1); `== 1` pops. The deflect
  normal comes from the real `ProjectileMotionResolver.TryComputeContactNormal` — the simulator's own
  analytic entry point already sits exactly on the contact circle, so that method's backtrack
  resolves to zero and returns the exact contact normal, not an approximation. Unbreakables enter
  the board as `int.MaxValue`-durability targets: permanent deflectors that never pop and score
  nothing on deflect, exactly like the game. Live itself used to disagree with this on grazing
  contacts — the swept capsule collider's nose reaches past the analytic circle on ~47% of live
  deflects, sending them down a degenerate fallback that never re-anchored the flight segment and
  could teleport the shot; fixed in `5d401097`, so the analytic path above is now what live
  actually takes on every deflect, closing what had been the dominant accuracy gap on
  deflect-heavy shots.
- **Interactive statics** (Deflector/Gatekeeper/Absorber, Phase A) — collide with the shot while
  still occupying their balance-grid slot. A Deflector has no durability capability at all, so it
  gets the same `int.MaxValue` permanent-deflect treatment as an Unbreakable balloon. A
  Gatekeeper pops on its final hit but scores nothing AND leaves the streak untouched — not merely
  "no score": the live `ScoreController` never even sees a Gatekeeper's pop (no `IHasScoreColor`),
  so nothing resets the streak either, unlike an ordinary tough pop. An Absorber ends the flight
  outright (`ShotContactKind.Absorb`, from a live `EvaluateHit` returning `HitOutcome.Absorb`) —
  no score, no streak mutation, no removal, exactly like the live Absorber staying on the grid
  forever; the solver window surfaces this as a distinct `Absorbed` outcome, never conflated with
  `Died`. A static never nudges its neighbours on a hit, mirroring `NudgeService.OnActorHit`'s
  `IHasNudge` requirement — no static archetype implements it. None of the three are live-reachable
  yet (no prefab/collider/spawner wires one onto a real board), so this path is verified only by
  `ShotStaticContactTests` (EditMode) — flag it if the solver is ever pointed at a board that
  actually has one.
- **Radii, never hardcoded** — each target's contact circle is its live view's `ContactRadius`
  (`CircleCollider2D.radius × lossyScale.x`), plus the projectile's own contact radius per test.
  The differing collider setups (coloured 0.3125 at prefab scale ~0.866 ≈ 0.271 world; tough 0.325
  at scale 1; unbreakable 0.325 at scale ~1.097 ≈ 0.357 world) flow through per view — the same
  `ContactRadius` the deflect message itself carries, so game and sim can't disagree per balloon.
  Both that derivation and a static archetype's own collider now share one helper,
  `ContactRadius.FromCollider` (`Shared/`), so the two paths can't drift apart.
- **Colourless vs. coloured scoring** — a `ShotBalloonSnapshot.ColorId` of null/empty models a
  balloon that does NOT implement `IHasColor` (`ToughBalloonModel`); non-null models one that does
  (`BalloonModel`). Tough pops score a flat `ScoreValue` and reset the streak — mirrors
  `ScoreController.RecordStreakMultiplier` collapsing `ToughBalloonModel`'s per-point
  `breaksStreak: true` attributions to a locked ×1 multiplier. Green pops score
  \f$\text{ScoreValue} \times \text{streak}\f$, where streak follows `ColorStreakTracker.Record`'s same-colour-extends /
  different-colour-resets rule.
- **Colour adoption & shield refund** — mirrors `ProjectileHitResolver`: the projectile's tracked
  colour adopts the popped balloon's colour (off the OLD colour, same order as
  `ApplyColorChange` running before the streak record), then a shield is refunded when the streak
  reaches \f$\ge 2\f$ of the projectile's now-current colour — gated by `IRunConfig.StreakGrantsShields`
  (`ShotScoreRules.StreakGrantsShields`, read by `ShotBoardGather.Gather` so the window and the Fire
  Best Shot cheat can't disagree with each other or with the live game). The condition itself is
  `Shared/StreakShieldRule.GrantsShield`, the one place both the live resolver and this simulator
  read it from.
- **Walls** — analytic per-axis crossing time, then `Vector2.Reflect` off the (possibly summed, for
  an exact corner hit) wall normal — same rectangle and reflect convention as `WallLimits`. Each
  bounce costs a shield; shields dropping below zero ends the flight (`Died`).
- **Rainbow / wildcard scoring** (Phase D-core) — `ResolvePopScore` mirrors
  `ScoreController.RecordStreakMultiplier`'s own precedence: a rainbow-buffed shot scores every
  pop colour-agnostically (even an otherwise streak-breaking tough pop just keeps the multiplier
  climbing); absent that buff, a colourless pop still takes the flat/streak-breaking tough rule
  and skips the refund entirely; a rainbow target hit by a still-colourless projectile defers the
  streak instead of resetting it (a banked count that folds in as `1 + deferred` the next time the
  streak anchors on a real colour, mirroring `ColorStreakTracker`'s deferred-pop fold); an
  anchoring rainbow pop uses the projectile's own colour if the board still allows it, else the
  board's first allowed colour. Colour adoption always gates on the POPPED balloon being rainbow,
  never on the shot's own buff — an ordinary coloured pop steals the projectile's colour
  regardless of an active rainbow buff, and only a rainbow TARGET suppresses adoption. A rainbow
  pays `ScoreValue` × every board-allowed colour, pre-cap (mirrors
  `BalloonModel.ResolveRainbowAttribution`'s product — the sim never models
  `ILevelProgress.ClaimProgress`'s level cap); a Target Colour filter only narrows which score
  GROUP counts toward the milestone — it never zeroes a rainbow's count (a rainbow always counts
  under any filter), only how many colours it pays. Soap (`IWashesProjectileColor`) washes the
  projectile colourless on ANY contact outcome, deflects included — mirrors `ApplyColorChange`
  running ahead of, and independent from, the deflect-vs-pop branch in `ProjectileHitResolver`. A
  rainbow-buffed shot also converts its hex neighbours to rainbow on every pop it lands (mirrors
  `ProjectileHitResolver.ConvertNeighborsToRainbow`), scanned over the active working set so it
  works whether or not a dynamic board is supplied.
- **Cruise** — entry mirrors `ProjectileView.TryEnterCruise`: past the wall-bounce threshold, a
  walls-only lookahead of `threshold` more segments must be balloon-free (tested against
  time-evaluated centres) before the ramp engages. Speed mirrors `ProjectileMotionResolver.Step`:
  every cruise bounce adds one cumulative `SpeedGainPerTap` tap (13-bank → 13 taps). The
  per-tap ANIMATION (target × `CruiseTapCurve(elapsed/CruiseTapEaseDuration)`, the
  freeze-then-pickup beat) never bends the path, so the event sim folds it into a per-bounce
  timeline lag of \f$\text{duration} \times (1 - \text{mean curve value})\f$ — an approximation only when a segment is
  shorter than the ease window. Any balloon contact resets counter and cruise. Reaching
  `CruisePiercingTapThreshold` taps ARMS piercing for the rest of the flight (mirrors the
  resolver's buff grant): every later contact pops — unbreakables included — and flies on unbent.
  Approximation: a pierced colourless pop scores through the flat tough rule, ignoring the
  game's projectile-colour attribution nuance for unbreakables under a Target Colour filter.
- **Balance & nudge (dynamic board)** — when the window supplies `ShotBoardDynamics`, rebalance
  pulses fire at \f$k \times \text{FlightRebalanceInterval}\f$ running the REAL `BalancePlanner` over a real
  `SlotGrid` (no mirrored rules — rule drift is impossible); moved balloons follow their hop
  waypoints as an arc-length polyline with OutQuad-eased progress over a per-move duration of
  path length ÷ the balloon's resolved `MoveSpeed` (per-type `BalloonPrefabEntry.MoveSpeed`, or
  `DefaultBalloonMoveSpeed`) — mirroring the live `BalloonBalancer`'s speed-based duration and
  `DOPath`'s constant-speed percentage under the project's DOTween default ease
  (`DOTweenSettings.asset`); contacts against them solve the moving-circle quadratic
  linearized at the instantaneous eased velocity. Every contact nudges the target's occupied hex neighbours and deflects
  additionally shove the hit balloon, with the exact `Reach` impulse envelope; centres become
  \f$\text{balancePosition}(t) + \sum \text{impulses}(t)\f$, and a pulse landing mid-wobble seeds its path from the
  WOBBLED centre (waypoint 0 = view position in the live `StartBalanceTween`, with the ticker
  re-adding impulses on top of tween writes — the brief start-offset double-carry is faithful).
  Pops `Remove` from the dynamics grid so later pulses see the gaps. With no dynamics supplied the loop takes the original static fast path unchanged.
- **Items** (Phase C) — an item carrier is opt-in: `ShotItemLayer` is a nullable peer of
  `dynamics` on `Simulate` (`items: null` ⇒ the original loop runs byte-for-byte unchanged).
  Assignment is spawn-time (`ItemAssigner`), so which balloon carries what is a deterministic fact
  of the gather, never something the flight itself rolls. On a host pop the layer resolves the
  item's effect by running the same pure per-item core a future live repoint would share
  (`BombBlast`/`LaserCross`/`LightningChain`/`PaintSpread`, behind `IEffectBoard`'s two adapters —
  `GridEffectBoard`, live-side, and `ShotSimEffectBoard` here), draining a FIFO queue
  breadth-first — a popped item's own effect chaining into another item resolves on a LATER
  iteration, mirroring `ItemActivator`'s per-frame cadence — bounded by `MaxActivationsPerFlight`
  (32). Item pops score through the SAME pop-scoring dispatch a projectile contact uses, with
  colour adoption and the shield refund gated OFF (verified: both live only in
  `ProjectileHitResolver.ResolveContactPop`, which an item handler never reaches); they nudge
  their hit neighbours and chain their own carried item exactly like a direct pop would. Shield
  grants +1 projectile shield (a rainbow host additionally grants the until-wall rainbow buff);
  Snipe arms the lance (piercing, without entering cruise) plus the non-stacking speed buff (a
  rainbow host additionally grants the until-pierce-end rainbow buff) — there is deliberately
  **no `DamageFlags.DirectHit` gate** (José's ruling, 2026-07-25: the lance is recoverable off an
  AoE pop too, matching the live code, which has none). An armed lance is barred from later
  entering cruise (`ProjectileView.TryEnterCruise`'s own bar, mirrored by `HandleWallBounce`'s
  `!state.IsPiercing` guard), so it can never layer cruise's per-shield speed tap on top.

## Accepted approximations (plan §7)

- `ShotItemLayer.MaxActivationsPerFlight` (32) is a sim-only safety valve — live's `ItemActivator`
  has no cap, so a flight that would chain past it in-game keeps granting effects the sim silently
  drops from that point. Related edge: chained activations enqueue in the chain's hit order, and
  two EXACTLY equidistant lightning targets can receive swapped jump indices between sim and live
  (unstable sort) — observable only when a tie sits right at the budget wall.
- Item effects apply INSTANTANEOUSLY on the same event as the host's pop; live's `ItemActivator`
  yields one frame before calling `Activate()`. The sim has no per-frame concept to skip past — it's
  event-to-event, not framed — so this is a structural gap rather than a fidelity one: the eventual
  outcome is identical, only the (unmodeled) one-frame stagger between pop and effect is missing.
- A Laser's captured spin rate is extrapolated LINEARLY to the predicted contact time
  (`host.ItemSpinDegrees + host.ItemSpinRate × tHit`) — live's `LaserItemRotation` angle is sampled
  once at gather (a snapshot of an `Update`-driven rotation), never re-queried at hit time, so this
  is an estimate rather than the exact angle a live cast would read.
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
- `RainbowBuffUntilWall` always ends in the sim on the very next shield-losing wall bounce
  (`HandleWallBounce` resets it unconditionally) — exact for the Shield item's grant
  (`WallBounceEndCondition`, which really does end on the first wall bounce). Phase C's buff-grant
  seam now DOES tell the sim which end condition backs a grant (a separate
  `RainbowBuffUntilPierceEnd` field for the Snipe/pierce-riding case), but the sim can't yet act on
  it: `HandleWallBounce` only clears `IsPiercing`/`HasSpeedBuff`/`RainbowBuffUntilPierceEnd` on a
  bounce where `IsCruising` is ALSO true (the cruise-earned-pierce case) — a pure Snipe grant never
  sets `IsCruising`, so today it (and its riding speed/rainbow buffs) rides for the rest of the
  simulated flight instead of ending at the live pierce DISCHARGE wall
  (`PierceEndedEndCondition`). Phase E2's `PendingPierceHits`/discharge model is the fix.
- `Simulate`'s `in ShotFlightSeed seed` parameter (folded from four separate `starting*` params in
  Phase C0) is a test seam, not a live-gather input — `ShotBoardGather.Gather` always passes
  `default`, since a freshly loaded shot is a `new ProjectileModel` at config defaults with no
  buffs and no in-progress streak (G8). It exists so tests exercise D-core's scoring/end-condition
  mirrors and the item layer's mid-flight buff grants without needing a live projectile to seed
  from.
- Pre-existing gap (E4, found during the D-core review): the sim's non-piercing
  `HitsRemaining > 1` branch always deflects a surviving multi-hit target, but live only
  `ToughBalloonModel`/`UnbreakableBalloonModel` actually return `HitOutcome.Deflect` — a surviving
  multi-HP soap cluster (`BubbleClusterModel`) returns `PassThrough` and flies through unbent
  instead. Planned fix is Phase E4 (a survive-outcome discriminator on the snapshot).
- Balance fidelity honours `WeightBias`/`OmnidirectionalBalance` only — `ShoveVector` pop-pressure
  shoves are never exercised by a flight-rebalance pulse, so a board effect that leans on pressure
  propagation diverges from the sim.

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
6. **Target Colour** (empty = all): when set, only pops of that colour id count toward the target
   score — milestone-mask style; streaks/refunds still run unfiltered. A rainbow pop always counts
   under any filter (only how many colours it pays narrows).
7. **±Nudge robustness**: each window's centre is re-simulated with every contact circle fattened
   AND thinned by the nudge amplitude; windows that survive both are tagged ✓robust.
8. A window whose centre shot ends by hitting an Absorber is tagged **⊘absorbed** in the list, and
   the run summary counts absorbed runs alongside capped ones — the flight ended early, so read its
   score/pops with that in mind.
9. **Fire Best** freezes the prediction, forces the shot, and samples the real projectile against
   the predicted timeline every editor update — live divergence readout in the window, actual path
   drawn in yellow next to the red prediction.

The design-time follow-up (Task 3: choosing \f$r_{\text{projectile}}\f$ against the 0.104 knife edge, the fair
threshold, and optionally wiring the solver into spawn validation) is not implemented here.
