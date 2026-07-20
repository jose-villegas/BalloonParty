# Score/Behaviours

The trail choreography seam: decouples *what a score group is worth* (`ScorePointsGroupMessage`, one message
per resolved color per pop) from *how it flies*. `ScoreTrailService` resolves each group to an
`IScoreTrailBehaviour` by its point total and hands off; the handler owns everything from spawn to arrival.
See `Game/Score/README.md` (**Trail Behaviour Seam**, **`BigScore` + `ShapeFormationTicker` + `ShapeCatalog`**)
for the full walkthrough — this README only maps the files.

## Contents

| File | What it does |
|---|---|
| `IScoreTrailBehaviour` | The handler contract: `GetPrincipalId` nominates the trail id the level-up cinematic tracks; `Begin` owns a group from spawn to final arrival (report cumulative points summing to the total, register/unregister its own flights, return every pooled instance) |
| `ScoreTrailBehaviourId` | Enum identifying a handler (`DefaultScore`, `BigScore`) — what `IScoreTrailBehaviourConfiguration`'s table and the DI handler dictionary key on |
| `ScoreTrailBehaviourResolver` | Maps a group's point total to its handler: highest-`MinPoints` entry the total clears wins (table authored in `IScoreTrailBehaviourConfiguration`, descending order). Shared by `ScoreTrailService` (spawns) and `LevelUpCinematic` (derives the tipping id via `PrincipalIdFor`) so the id can never diverge from what the handler registers |
| `ScoreTrailContext` | Readonly struct bundling everything a handler needs for one group: color/origin/hit direction/points/score range, the `ITrailEndpoint` target, `TrailSpawner`, `Flights` registry, a fresh (not pooled) `IScoreTrailReporter`, `IGameConfiguration`, and a group-scoped `CancellationToken` |
| `IScoreTrailReporter` | Sink a handler reports arrivals through; the implementation publishes one `ScoreTrailArrivedMessage` per call. Flight registration is NOT the reporter's concern — that stays with the handler |
| `DefaultScoreTrailBehaviour` | Reproduces the pre-seam pipeline byte-for-byte: one pooled trail per point, scatter-fan origin, `0.02 s` stagger, one point reported per landing, first trail (`msg.FirstScore`) is the principal |
| `BigScoreTrailBehaviour` | The confluence handler for large awards: decomposes the group total into a catalog of 3D shapes (`Decompose`, optimal coin change over `ShapeCatalog.Denominations`), spreads their sub-centres around the pop, and launches every shape via `ShapeFormationTicker` simultaneously. The largest shape is the principal |
| `ShapeCatalog` | Hand-authored 3D shape data, one entry per denomination (vertex count == score value == pen count). Built once (`Warm()` forces this at scope start), zero-alloc lookup thereafter |
| `ShapeFormationTicker` | `ILateTickable` — the analytic, pooled, zero-alloc per-frame driver for every in-flight shape formation: pen orbit, travel toward the bar, tumble, ribbon re-framing, and the pause/snap/slow-mo transport bridge |
