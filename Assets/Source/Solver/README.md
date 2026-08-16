# Solver

The runtime half of the shot solver (see the authoritative rule-mirroring/approximations doc in
`Assets/Source/Editor/ShotSolver/README.md`):
the pure event-to-event `ShotSimulator` (with `ShotFlightState`/`ShotBoardSnapshot` as its state/board
data types) and its dynamic-board companions (`ShotBoardDynamics`, `ShotSimBoardActor`,
`ShotMotionMath`), its item-carrier companions (`ShotItemLayer`, `ShotSimEffectBoard`), and
`ShotBoardGather`, which snapshots the live board/thrower/config into a `ShotSolveContext`.

The sweep's sample grid (`ResolveSweepSampleCount`/`ResolveSweepAngle`) and its sibling
`ClampToReachableAngle` live in `Shared/AimAngleGrid` rather than here — that grid is a domain rule
about which angles the thrower can reach, consumed by four independent places (the live
`ThrowerController`, `FireBestShotCheat`, `ShotSolverWindow`, and the Scene-view aim fan overlay), not
solver-specific logic, so it lives in `Shared/` next to `WallLimits` for the same reason. `ShotBoardGather`
now *consumes* `AimAngleGrid` for its sweep rather than owning the grid math itself: with
`IProjectileFlightConfig.AimAngleStepDegrees` at 0 (continuous aim), a sweep lerps a fixed count
of samples across the arc, same as before quantization existed; with a non-zero step, it instead
samples exactly the step's multiples that fall inside the arc — the same absolute grid
`ThrowerController.ClampAndQuantizeAimDirection` snaps player input to — so a sweep can never
recommend an angle the player could not actually aim at. Both sweeping callers (`FireBestShotCheat`,
`ShotSolverWindow`) go through `AimAngleGrid`'s two statics rather than each lerping the arc themselves,
and both source the arc bounds from `IProjectileFlightConfig.AimAngleMinDegrees`/`AimAngleMaxDegrees` —
the same range the thrower clamps aim to — rather than a separate constant, so a sweep can never cover a
different set of angles than the player can actually reach.

`AimAngleGrid.ClampToReachableAngle` is the sibling the thrower's own clamp calls: given a raw aim
angle, it resolves the same grid-anchored "reachable" angle `ResolveSweepAngle` would sample at that
position — clamping the *rounded grid index*, not the angle itself, so a clamp-then-snap or
snap-then-clamp can't disagree with what the sweep considers reachable (see
`ThrowerController.ClampAndQuantizeAimDirection`'s own comment for why the naive orderings both
fail).

Lives in the Runtime assembly so both the editor Shot Solver window AND runtime tooling (the
Fire Best Shot cheat, development builds on device) can run the same simulation — the editor
window keeps the sweeping/bisection UI, this folder owns the physics truth.
