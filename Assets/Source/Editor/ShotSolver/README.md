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
| `ShotSolverWindow` | `Editor/ShotSolver/` | `Tools > BalloonParty > Shot Solver` — play-mode-only window. Snapshots the live board (`SlotGrid`, resolved via `GameLifetimeScope.Container`), thrower origin, and projectile contact radius; sweeps N angles across a configurable arc; refines qualifying-window edges by bisection; plots score vs. angle as a strip of `EditorGUI` rect fills; lists qualifying windows; can draw the best window's centre-angle flight path into the Scene view; **Fire Best** re-sweeps and forces the shot via `ThrowerController.FireAt`, then samples the latest ball in flight from `IProjectileRegistry.TryGetLatestInFlight` |
| `ShotSimulator` | `Solver/` | Pure, headless, static class — simulates one aim direction to completion, event-to-event (next analytic wall crossing, next analytic balloon-corridor entry, or next due balance pulse), never fixed-step. Reuses `ProjectileMotionResolver.TryComputeContactNormal` for deflect contacts. Mirrors the runtime's pop/deflect/shield/streak/rainbow rules, including Phase A's interactive statics (see below), on a `ShotBalloonSnapshot` board — no `MonoBehaviour`, no live model, no allocation beyond the caller-owned working-set buffer. Events carry timestamps (`t += distance / speed(segment)`), and speed mirrors the cruise ramp exactly |
| `ShotBoardSnapshot` | `Solver/` | The board's data types: `ShotBalloonSnapshot` (one target's geometry, `ColorProfile` colour identity, optional `BalanceProfile`, `ShotContactKind`, item), built only via its named factories (`ForColorTarget`/`ForToughTarget`/`ForBubbleClusterTarget`/`ForRainbowTarget`/`ForGoldenTarget`/`ForDiscoTarget`/`ForNukeTarget`/`ForStaticContact`) so every caller's field mapping stays honest as the struct grows; `ShotBalloonState`, its mutable per-simulation copy |
| `ShotFlightState` | `Solver/` | Mutable per-flight state passed by `ref` through `Simulate` — position/direction/shields/elapsed, cruise/pierce state, and the Phase D-core buff/streak/colour fields (`HasRainbowBuff`, `SpeedBuffMultiplier`, `StreakColor`/`StreakCount`, `DeferredPops`, `ProjectileColor`) |
| `ShotBoardGather` | `Solver/` | Snapshots the live board/thrower/projectile into the sim's input structs (`ShotBoardGather.Gather`) and converts sweep angles to world directions (`DirectionFromDegrees`). The sweep's sample grid (`ResolveSweepSampleCount`/`ResolveSweepAngle`, see below) lives on `Shared/AimAngleGrid` instead — `ShotBoardGather` consumes it rather than owning it. Called by `ShotSolverWindow` |
| `ShotRanking` | `Solver/` | Orders simulated shots for the Fire Best Shot cheat (and the bots and autoplay aiming with it): a golden balloon popped beats any score, then a heart balloon popped while its heart counts (up to the hearts missing, or any on a purple level), then a nuke popped (the room it clears outweighs score), then a disco ball popped (its rainbow board pays the shots after this one), then `RawScore` |
| `ShotBoardDynamics` | `Solver/` | The dynamic-board half (plan §7): owns a real headless `SlotGrid` + `GridBalanceQuery` + `BalancePlanner` over stub actors, schedules flight-rebalance pulses on the sim timeline, and keeps per-balloon nudge-impulse state. Built once per gather, reset per simulated flight |
| `ShotSimBoardActor` | `Solver/` | The stub actors (`ShotSimDynamicActor`/`ShotSimStaticActor`) the dynamics grid is populated with, plus the per-flight snapshot structs for non-target actors |
| `ShotMotionMath` | `Solver/` | Pure math: the nudge `Reach` envelope (mirrors `BalloonMotionTicker` exactly) and the moving-circle entry solve (relative-velocity quadratic, reduces to the exact static solve at zero velocity) |
| `ShotItemLayer` | `Solver/` | Item-carrier layer (plan Phase C) — resolves a popped host's `ItemType` into a `ShotItemOutcome` (shield/pierce/speed-buff grants) plus a list of `EffectHit`s against a bound `ShotSimEffectBoard`, draining chained activations breadth-first up to `MaxActivationsPerFlight` |
| `ShotBumperLayer` | `Solver/` | The meadow's mushrooms for the simulator (`Plans/PLAN-MeadowMushrooms.md`): the bumper circles copied at gather, each whole cap with its id and its wall (a cap a lance has cut is not in the field), with the game's own bounce rule, chain rules and the Snipe's speed buff for the lance a chain pays. A simulated lance cuts caps out of it for the rest of its flight (`Cut`), a bomb the flight sets off blows off the caps next to it (`BlowOff`, by the caps' slots, which the gather takes with them), and the next flight starts from the board as gathered. A nullable peer of `ShotBoardDynamics` and `ShotItemLayer`: `ShotBumperLayer.Gather` answers null on a board without mushrooms, and `Simulate` then runs the loop it always had. It holds no geometry of its own |
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
- **Plated reward balloons** (`ForGoldenTarget`, `ForDiscoTarget`) — deflect the shot like an unbreakable
  (`int.MaxValue` durability) and pop to a pierce, but are `Scoreless` (the golden's pop pays a card and
  the disco ball's paints the board, not points, and either leaves the streak alone) and `ImmuneToEffects`
  (no item hit reaches them but a laser activation's, mirroring `PlatedBalloonModel`'s `HitOutcome.None`
  for any hit without `DirectHit` or `Beam`). Colourless and item-free. Each snapshot carries its
  `ShotRewardKind` (`Scoreless` is any kind but `None`), and the result counts reward pops apart by it
  (`GoldenPops`, `HeartPops`, `NukePops`, `DiscoPops`), since they add nothing to `RawScore`; `ShotRanking` reads them
  to put a golden, then a heart that counts, then a nuke, then a disco ball, ahead of score.
- **The nuke** (`ForNukeTarget`) — a plated reward that also carries its hits left, so the ball deflects off it
  until the last dent. That dent, or a pierce, arms it (`ArmNuke`): it is counted once in `NukePops` and stays
  on the board, deflecting. The simulation leaves the rest of the board as it is, because live the blast comes a
  fuse later (`INukeFuseSettings.Seconds`), after the flight, and kills every shot still in it.
- **The disco ball's wave** — when the simulated ball pops a disco ball, every paintable balloon in the
  working set (coloured, not static, not already rainbow) turns rainbow at once and is marked
  `ShotBalloonState.OwnPaint`. The shot's later pops of those pay every allowed colour at its streak as it
  stands and grant no streak shield, as `DamageFlags.OwnPaint` does live. An approximation: live, the disc
  takes `IDiscoWaveSettings.Seconds` to cross the board and a pierced ball only pops at the next wall, so the
  simulation lets the rest of the flight score painted balloons a little early. Needs the gather's
  `rainbowColorId`; without one nothing is painted.
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
- **Colour adoption** — mirrors `ProjectileHitResolver`: the projectile's tracked
  colour adopts the popped balloon's colour (off the OLD colour, same order as
  `ApplyColorChange` running before the streak record). Shield grants come from items only
  (see `ShieldItemHandler`), controlled by `IRunConfig.StreakGrantsShields` (`ShotScoreRules.StreakGrantsShields`, read by
  `ShotBoardGather.Gather` so the window and the Fire Best Shot cheat agree with the live game):
  when true, a same-color streak of 2+ yields one shield per pop; when false (current), no streak-based shields.
  The condition itself is `Shared/StreakShieldRule.GrantsShield`, called by both the live resolver and this simulator.
- **Walls** — analytic per-axis crossing time, then `Vector2.Reflect` off the (possibly summed, for
  an exact corner hit) wall normal — same rectangle and reflect convention as `WallLimits`. Each
  bounce costs a shield; shields dropping below zero ends the flight (`Died`).
- **Rainbow / wildcard scoring** (Phase D-core) — `ResolvePopScore` mirrors
  `ScoreController.RecordStreakMultiplier`'s own precedence: a rainbow-buffed shot scores every
  pop colour-agnostically (even an otherwise streak-breaking tough pop just keeps the multiplier
  climbing); absent that buff, a colourless pop still takes the flat/streak-breaking tough rule
  and skips the refund entirely; a rainbow target the shot didn't paint climbs the streak
  colour-agnostically and carries it to the next real colour, whether or not the projectile has a
  colour yet (`CarryStreak`, mirroring `ColorStreak.RecordWildcard`); an
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
  pulses fire at \f$\text{firstPulseIn} + k \times \text{FlightRebalanceInterval}\f$ (`firstPulseIn` is the live balancer's `SecondsUntilNextPulse`, since the pulse runs all through play; a full interval when there is no live board) running the REAL `BalancePlanner` over a real
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
- **Mushrooms** (`PLAN-MeadowMushrooms`) — on a meadow board the flight has a fourth kind of event beside
  the wall, the balloon and the balance pulse: the ball's entry into a mushroom's circle. Nothing about it
  is re-implemented here. The entry is found by the live flight's own swept test (`Shared/BumperSweep`),
  under its conditions: none once the stretch between two walls has had its bounces
  (`MushroomChainRule.IsSpent`), not the mushroom being left while the ball still flies the direction it
  left along, none whose contact lies beyond the walls, and for a ball found inside a circle (a deflect off
  a balloon beside a mushroom can set it there) the point its line came in, behind it. A mushroom is met
  before a wall, as the live step sweeps its stretch before it settles its walls, and before a balloon no
  nearer than it. At the bounce (`ShotSimulator.BounceOffBumper`) the calls are the live resolver's, in its
  order: `MushroomChainRule.OnBounce`, then the game's `IBumperBounceRule`
  with the exact entry point and the chain as counted (the forgiving kick, blind to balloons), then the
  turn. No shield is spent and nothing scores. The shot's cruise and its run of empty wall bounces
  end, its taps kept; the Sweep rule's segment starts again at the bounce. A wall the shot survives clears
  the chain (`MushroomChainRule.OnWall`), and the cruise lookahead counts a mushroom as in the way as the
  live one does (`BumperLookahead`: the ball's own leg excuses a spent stretch and the mushroom being left;
  every leg past a wall excuses nothing; a lance has none in its way). **The reward:** the live ball's fixed
  step ends on the mushroom that arms it and it is armed there, so the simulated ball becomes the lance at
  that bounce's contact point: `Arm` arms it with the Snipe's speed buff, as the game's `ISnipeArmer.ArmAt`
  does, and nothing else pays (`ShotSimulator.PayMushroomReward`). From there the flight is a lance's in
  every way the simulator has for one. **A lance cuts caps:** a simulated ball that is piercing bounces off
  no mushroom. The first whole cap its path enters before its next wall is cut and the ball flies on unturned
  (`ShotSimulator.TryCutCap`, the live `ProjectileMotionResolver.CutCaps`): the cap leaves the layer for the
  rest of the flight, the chain is not asked, and the cap that armed the ball is left alone while it flies
  the direction it left along. The
  result carries what the mushrooms did (`ShotSimulationResult.Mushrooms`: bounces, chains completed, the
  lance armed, caps cut, which mushrooms were hit) and `FinalPiercing`. `MushroomMirrorTests` flies
  the same shots through the game's resolver, chain controller and Snipe handler and through `Simulate`
  and holds them turn for turn; `MushroomAimGridTests` flies every layout of the baked wall catalogue from
  every angle of the aim grid and holds what it measures inside the settings' band (the catalogue is baked
  with this simulator: `Editor/MushroomWallBaker`).
- **Ripe clusters** (`PLAN-RipeBubbleCluster`) — `ForBubbleClusterTarget` carries the cluster's `Ripe`
  flag at gather. A ripe cluster bursts whole on any damaging hit (the shot's contact, an item's hit, a
  ripple), mirroring `BubbleClusterModel.EvaluateHit`: it scores the bubbles it held through the
  flat/streak-breaking tough rule, washes the shot as any cluster does, and is never ploughed through
  (`HitsUnderCoat` is 1, so a pierce bursts it too). Its burst is a blast over a radius
  (`RunRipples`): it reaches every target whose slot centre is within `ShotSolveContext.RippleRadius` of the
  burst's slot centre, by the game's own rule (`RipeBubbleRule.RippleReaches`, which `RipeBubbleBurst` calls
  too), in the game's order (column, then row). The centres are the slots' own, not the views': each snapshot
  carries `SlotCenter` beside the `Position` its view is drawn at (`ShotBoardGather.TryBuildTargetSnapshot`
  sets it from `SlotGrid.IndexToWorldPosition`; a snapshot built without one sits on its slot). Each target
  reached takes one hit of `ShotSolveContext.RippleDamage` (both read from
  `IBalloonsConfiguration.RipeBubble` at gather; `Simulate` has no default for the damage, and a damage of 0
  or less is no hit; its `rippleRadius` defaults to 0, which reaches nothing, and `SimulateAt` passes the
  context's), flagged `DamageFlags.Ripple`, if it is one
  `RipeBubbleRule.RippleTakes` would take — not a reward balloon, not a static, not a target that
  deflects an ordinary hit. A ripple hit strips a coat layer and stops, pops a one-hit balloon, takes a
  bubble off an unripe cluster, or bursts a ripe one, which queues its own ripple, so a chain runs to its
  end. Its pops score at \f$\max(1, \text{streak})\f$ and never change the streak (`ScoreController`'s
  `Ripple` branch); a rainbow it pops pays as any item-popped rainbow does, the ripple carrying no
  source colour (`RipeBubbleBurst` sends `string.Empty`). A carrier it pops chains its item through the item queue, as an item's pop
  does. The simulation resolves a ripple and its whole chain at the contact; the game lands each one
  `BurstSeconds` (0.22) later, after the cluster's fold, while the shot flies on. So where the shot reaches
  a rippled balloon inside that window, or its streak changes in it, the two can differ by that balloon's
  score. `RipeClusterMirrorTests` runs the game with `BurstSeconds` at 0 for that reason. The fuse's foam
  needs no mirror: it runs on the level's clock and gives no hit or score.
- **Foam coats** — `ShotBalloonSnapshot.Coat` carries a coloured or rainbow balloon's coat layers
  (`ICoatable`) at gather. The shot's non-piercing contact on a coated balloon strips one layer, flies
  on unbent, washes the shot, scores nothing, leaves the streak alone and is not a pop; the segment's
  sweep is invalidated as on any contact. A piercing contact takes the coat with the balloon: it pops
  and the shot adopts its colour as an ordinary pop. An item's hit strips one layer unless it is
  `Piercing` or `Beam`, which pops the balloon under it (`BalloonModel.EvaluateHit`). The wash is
  decided per contact, since a coat can come off mid-flight: a contact washes when the target is soap,
  or when it wears a coat and the hit is not piercing; the snapshot's `WashesProjectileColor` stays keyed to the real
  `IWashesProjectileColor` marker, which is what the item effect board's resist-paint rule reads.
- `ShotSimulationResult.FinalColor` and `FinalStreak` report the colour and streak the shot ends the
  flight with, so the mirror tests can compare them with the game's.

## Accepted approximations (plan §7)

- **Known divergence, not yet mirrored (2026-08-17):** live's `ScoreController.RecordStreakMultiplier`
  now gates streak GROWTH on `DamageFlags.DirectHit` — an item/AOE pop (Bomb/Laser/Lightning) scores
  at whatever multiplier is already live but can no longer climb it, only the shot's own contact/pierce-
  discharge hits can. `ShotSimulator`'s mirror (`RecordColor`, called from `ResolvePopScore`) still
  increments unconditionally for every same-colour pop regardless of `cause.Flags`, so the solver
  currently OVER-predicts streak/score for any shot chain with item/AOE pops — `FireBestShotCheat` and
  `ShotSolverWindow` will rank those chains above what live actually pays out. Needs `RecordColor`
  threading `cause.Flags.HasFlag(DamageFlags.DirectHit)` the same way live threads it into
  `ColorStreakTracker.Record`'s `canIncrement`.
- **Mushrooms.** A lance a chain pays is a Snipe's lance, so what is said below of a Snipe grant holds
  for it: it rides for the rest of the simulated flight where the live one ends at the wall that discharges
  it. So past that wall the live ball is an ordinary one and bounces off the caps that are whole, while the
  simulated one goes on cutting them. Every cap a simulated lance cuts banks it a charge, as in the game, but
  for the same reason the charges are never spent here: live they keep the lance a lance at that wall.
  Nothing grows back within a simulated flight: the game regrows a cut
  cap on the board's beat, seconds later, which no flight here models. Nor does a flight see a cap another
  ball cuts after the board was gathered: the layer is the board as it stood at the gather. A bomb the
  flight sets off takes the caps next to it on both sides, but at once here and a frame after the pop in the
  game (the item stagger below), so a ball that reaches such a cap inside that frame bounces live and flies
  through simulated; a bomb's sway and a nuke's blast, which takes every cap, are not modelled at all. The
  live ball gives up what was left of the fixed step it was paid in (under a step's length, once a
  reward), which the timeline does not: from there the game runs up to one step behind it. The live step
  bounces off 8 mushrooms at most before flying on untested, which no dealt board reaches in one step.
  A ball found inside a circle bounces where its line came in, behind it, and the timeline counts no time
  for the way back (live carries that distance forward instead: the same path, a few milliseconds apart).
  And two things older than the mushrooms that their mirror tests had to step round: a fixed step that
  crosses two walls spends one shield live and two here, and a deflect whose carried stretch reaches a
  wall is clamped there live and reflected here.
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
- Heavy step budgets reset per simulated shot; in-game the wave's budget may be part-spent at fire
  time (unknowable from a snapshot).
- Pulses never relocate roamers — matches the live code, where one roamer moves only on the unbreakables'
  cycle, which the solver doesn't predict (nor a snake's step).
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
- Bubble clusters gather through `ForBubbleClusterTarget` as `ShotContactKind.PassThrough`: a surviving
  ordinary hit lets the shot fly on unbent and the cluster loses a bubble, where the sim's other
  `HitsRemaining > 1` targets deflect (live, only `ToughBalloonModel`/`UnbreakableBalloonModel` return
  `HitOutcome.Deflect`; `BubbleClusterModel` returns `PassThrough`). Every hit on a cluster, surviving or
  popping, scores the bubbles it held through the flat/streak-breaking tough rule, mirroring
  `BubbleClusterModel.ResolveScoreAttribution`; only the pop counts as a tough cleared. An item's (or a
  ripple's) surviving hit on a cluster scores the same way.
- **The ripple's timing.** Live, a ripe burst's ripple lands `IRipeBubbleSettings.BurstSeconds` (0.22 s of
  level clock) after the hit that popped the cluster, as the big bubble it folds into pops, and each chained
  burst as long after its own hit; the sim resolves the whole chain at the contact that set it off (or,
  for an item's blast, once every hit of that activation has landed). It scores at the streak as it stands
  at that moment. A shot that reaches a balloon in the blast before the ripple would, or changes its streak
  in the meantime, is mispredicted: the sim gives the balloon to the ripple and the game to the shot.
- **The ripple's reach** is measured between the *gathered* slot centres, and its targets are looked up in
  the working set by their gathered slot (as the rainbow side-conversion and the item effect board do). A
  balance pulse mid-flight moves balloons but not their slots, so after one the ripple reaches what stood
  within its radius at gather, not what stands there now; for the same reason a balloon the board moves
  into the burst's own slot before the ripple lands is in the game's blast and not in the sim's.
- **No ripening, no fuse, no foam inside a shot.** Ripeness and coats are read once at gather; a cluster
  that ripens mid-flight, a fuse that runs out, or foam that coats a balloon during the flight is not
  simulated. The board the shot is ranked against is the board at fire time.
- **Blast-hit order and the streak.** The sim lands an item's hits in the effect board's order, which
  need not be the order the game's physics delivers them; when a blast mixes coloured pops with a
  cluster or tough that breaks the streak, the final streak can differ though the score agrees
  (`RipeClusterMirrorTests` compares only the score for that case).
- Balance fidelity honours `WeightBias`/`OmnidirectionalBalance` only — `ShoveVector` pop-pressure
  shoves are never exercised by a flight-rebalance pulse, so a board effect that leans on pressure
  propagation diverges from the sim.

## Sweep and refine

`ShotSolverWindow` samples N angles across a configurable arc (seeded from
`IProjectileFlightConfig.AimAngleMinDegrees`/`AimAngleMaxDegrees` — 5°–175° by default, measured from
+X — but user-editable from there for experimentation beyond what the player can actually aim at),
then finds contiguous runs where `RawScore >= target`. Each run's edges are refined by bisection to
~0.01° (the plan's §2 fair-window resolution threshold), not by enumerating exact tangency angles —
the plan calls that exact enumeration v2. The "best" window is the widest qualifying one; "Draw Best"
re-simulates its centre angle with the simulator's optional path-capture list and draws it via
`SceneDrawingHelper.DrawWorldPolyline`.

**Sample count.** With `IProjectileFlightConfig.AimAngleStepDegrees` at 0 (continuous aim, the
default), N is the editable **Samples** field (default 2048), lerped evenly across the arc — unchanged
from before quantization existed. Once a non-zero step is configured, the aim only ever lands on
multiples of that step (`ThrowerController.ClampAndQuantizeAimDirection`), so the window instead derives N
from the arc and the step (`AimAngleGrid.ResolveSweepSampleCount`) and samples exactly those
reachable angles (`AimAngleGrid.ResolveSweepAngle`) — the **Samples** field becomes a read-only display of that
derived count rather than an editable one, so it can't drift back to sampling off-grid angles. This is
both cheaper (a ~31° usable arc at a 5° step is ~7 samples instead of thousands) and more correct: a
sweep over off-grid angles could recommend, and via **Fire Best**, actually fire, a shot the player
could never aim at. Note this only covers the SWEEP itself — a qualifying window's centre (what
**Fire Best** actually fires when a window is found) is still bisection-refined between two on-grid
samples to ~0.01°, so it is not itself guaranteed to land back on the step grid; only the no-window
fallback path (drawn straight from a sample) is guaranteed on-grid. Snapping the fired centre to the
grid too is a follow-up, not done here.

## Usage

1. Enter Play Mode with the Game scene loaded.
2. `Tools > BalloonParty > Shot Solver`.
3. Set target score, min window width, and arc (or keep the defaults). **Samples** is editable with
   continuous aim (step 0) and shows the derived, read-only count once a non-zero
   `AimAngleStepDegrees` is configured.
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
