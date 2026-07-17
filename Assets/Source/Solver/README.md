# Solver

The runtime half of the shot solver (see `Assets/Source/Plans/PLAN-ShotGeometry.md` and the
authoritative rule-mirroring/approximations doc in `Assets/Source/Editor/ShotSolver/README.md`):
the pure event-to-event `ShotSimulator` with its dynamic-board companions (`ShotBoardDynamics`,
`ShotSimBoardActor`, `ShotMotionMath`) and `ShotBoardGather`, which snapshots the live
board/thrower/config into a `ShotSolveContext`.

Lives in the Runtime assembly so both the editor Shot Solver window AND runtime tooling (the
Fire Best Shot cheat, development builds on device) can run the same simulation — the editor
window keeps the sweeping/bisection UI, this folder owns the physics truth.
