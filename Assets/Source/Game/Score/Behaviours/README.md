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
| `ScoreTrailContext` | Readonly struct bundling everything a handler needs for one group: color/origin/hit direction/points/score range, the `ITrailEndpoint` target, `TrailSpawner`, `Flights` registry, a fresh (not pooled) `IScoreTrailReporter`, the three narrowed configs a handler might touch (`IScoreTrailConfig`, `ISlotGridConfig`, `IProjectileFlightConfig`), and a group-scoped `CancellationToken` |
| `IScoreTrailReporter` | Sink a handler reports arrivals through; the implementation publishes one `ScoreTrailArrivedMessage` per call. Flight registration is NOT the reporter's concern — that stays with the handler |
| `DefaultScoreTrailBehaviour` | Reproduces the pre-seam pipeline byte-for-byte: one pooled trail per point, scatter-fan origin, `0.02 s` stagger, one point reported per landing, first trail (`msg.FirstScore`) is the principal |
| `BigScoreTrailBehaviour` | The confluence handler for large awards: decomposes the group total into a catalog of 3D shapes (`Decompose`, optimal coin change over `ShapeCatalog.Denominations`), spreads their sub-centres around the pop on a golden-angle spiral sized off `GroupMaxRadius` (this group's own largest fitted shape, not the catalog-wide worst case), and launches every shape via `ShapeFormationTicker` simultaneously. The largest shape is the principal |
| `ShapeCatalog` | Hand-authored 3D shape data, one entry per denomination (vertex count == score value == pen count). Built once (`Warm()` forces this at scope start), zero-alloc lookup thereafter |
| `ShapeFormationTicker` | `ILateTickable` — the analytic, pooled, zero-alloc per-frame driver for every in-flight shape formation: pen orbit, travel toward the bar, tumble, the ink (one baked `ShapeInkView` per shape, or with `ShapeInkSettings.Baked` off a pooled trail per pen and its ribbon re-framing), the pause/snap/slow-mo transport bridge, and (see **Overlap Resolution** below) a cross-group separation pass so concurrently airborne formations don't interpenetrate. Also `IScoreShapeCover`: `CoveredArea` sums its live formations' bounding circles at their current scale, which the spawn clock reads to start a breather when shapes cover most of the board (`Balloon/Spawner/README.md`) |
| `ShapeInkMeshBuilder` | Bakes a `FormationShape` into the static wire mesh its ink is drawn on: a ribbon piece for every segment of every walk, a joint square at every sharp corner, and a pen's square for every segment, each vertex carrying its distance along the walk and the gap between two pens. Two submeshes with the same triangles, for the far and near halves. See **How a shape is drawn** in `Game/Score/README.md` |
| `ShapeInkRamp` | Fills the strip of pixels the ink shader reads by the ink's age: width, opacity and how far the colour has turned to the tail's, from `ShapeInkSettings`' three curves |
| `ShapeInkPool` | Owned by `ShapeFormationTicker`: hands out a pooled `ShapeInkView` per shape in flight, and keeps what they share (each shape's baked mesh, the two copies of the ink material, the ramp texture). Views are created in code and tracked in `LayerPresence`, so the effects map that draws their layer renders while one is alive |
| `FormationOverlapResolver` | Pure, allocation-free pairwise "push circles apart" math — no Unity-object dependency, so it's independently unit-tested. Called once per tick by `ShapeFormationTicker` |
| `BigScoreFormationGizmos` | Editor-only scene-view overlay (`_showBounds` toggle) — draws each in-flight formation's current bounding sphere as a flat XY circle (`GizmoDrawingHelper.DrawWorldCircle`) at `ShapeFormationTicker.ForEachActiveBounds`'s center/radius |
