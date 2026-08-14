# Solver

The runtime half of the shot solver (see the authoritative rule-mirroring/approximations doc in
`Assets/Source/Editor/ShotSolver/README.md`):
the pure event-to-event `ShotSimulator` (with `ShotFlightState`/`ShotBoardSnapshot` as its state/board
data types) and its dynamic-board companions (`ShotBoardDynamics`, `ShotSimBoardActor`,
`ShotMotionMath`), its item-carrier companions (`ShotItemLayer`, `ShotSimEffectBoard`), and
`ShotBoardGather`, which snapshots the live board/thrower/config into a `ShotSolveContext`.

`ShotBoardGather` also owns the sweep's sample grid (`ResolveSweepSampleCount`/`ResolveSweepAngle`):
with `IProjectileFlightConfig.AimAngleStepDegrees` at 0 (continuous aim), a sweep lerps a fixed count
of samples across the arc, same as before quantization existed; with a non-zero step, it instead
samples exactly the step's multiples that fall inside the arc — the same absolute grid
`ThrowerController.QuantizeAimDirection` snaps player input to — so a sweep can never recommend an
angle the player could not actually aim at. Both sweeping callers (`FireBestShotCheat`,
`ShotSolverWindow`) go through these two statics rather than each lerping the arc themselves.

Lives in the Runtime assembly so both the editor Shot Solver window AND runtime tooling (the
Fire Best Shot cheat, development builds on device) can run the same simulation — the editor
window keeps the sweeping/bisection UI, this folder owns the physics truth.
