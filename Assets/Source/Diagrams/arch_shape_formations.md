@page arch_shape_formations Score Shape Formations

# Score Shape Formations

@dot
digraph ShapeFormations {
    rankdir=TB;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_scoring {
        label="Scoring";
        style=filled;
        fillcolor="#e8f5dc";

        ScoreController [label="ScoreController\n(HitPipeline stage)\npublishes one\nScorePointsGroupMessage\nper resolved colour"];
    }

    subgraph cluster_seam {
        label="Choreography seam";
        style=filled;
        fillcolor="#f5f5dc";

        Service  [label="ScoreTrailService\nOnScorePointsGroup:\nlooks up endpoint,\nbuilds ScoreTrailContext"];
        Resolver [label="ScoreTrailBehaviourResolver\nResolve(Points):\nhighest MinPoints\nthat clears wins"];
        ConfigSO [label="IScoreTrailBehaviourConfiguration (SO)\nEntries: (Id, MinPoints)\n+ BigScoreFormationSettings"];
        Reporter [label="ScoreTrailReporter\n(one per group, not pooled)\nReportArrival — asserts\nnever-overshoot, exact sum"];
        Default  [label="DefaultScoreTrailBehaviour\nclassic per-point fan-out\n(< MinPoints)"];
    }

    subgraph cluster_formation {
        label="Formation simulation";
        style=filled;
        fillcolor="#dce8f5";

        Big [label="BigScoreTrailBehaviour\nDecompose (optimal coin\nchange over Denominations)\nFitScale/ClampCenter, TumbleAxis"];
        Catalog [label="ShapeCatalog (static)\ndenomination -> FormationShape\nunit-sphere verts, closed walks,\nperimeters, PensPerWalk\nwarmed at scope Start()"];
        Ticker [label="ShapeFormationTicker : ILateTickable\npooled FormationGroup/State\nC(t) travel, Q(t) tumble,\nscale(t) curve, pen arc-length orbit"];
        Pens [label="Pens + anchor + guide\npooled FlyingTrail pens\n(TrailRenderer ink),\nanchor Transform = principal,\nguide traces the travel path"];
        Flights [label="TrailFlightRegistry<TrailId>\nanchor registered as the\ngroup's principal flight"];
    }

    subgraph cluster_downstream {
        label="Downstream consumers";
        style=filled;
        fillcolor="#ffe8cc";

        LevelController  [label="LevelController\nconfirmed watermark:\nMax(current, Min(Score,\nprojected)) per colour"];
        ColorProgressBar [label="ColorProgressBar\n: ITrailEndpoint\nbar fill + landing target"];
    }

    subgraph cluster_levelup {
        label="Level-up seam";
        style=filled;
        fillcolor="#f5dce8";

        Cinematic  [label="LevelUpCinematic\nFlights.Get(principal)\nPause / Complete\n(see @ref arch_score_cinematic)"];
        Transition [label="LevelTransitionController\nHoldOutgoing() on every\nITransitionOutgoingContent"];
    }

    ScoreController -> Service [label="ScorePointsGroupMessage\n(Points, LastScore,\nMultiplier, HitDirection)"];
    Service -> Resolver [label="Resolve(Points)"];
    ConfigSO -> Resolver [label="Entries", style=dashed];
    ConfigSO -> Big [label="BigScoreFormationSettings", style=dashed];
    Service -> Reporter [label="new(colour, total)\nper group"];
    Resolver -> Default [label="points < MinPoints"];
    Resolver -> Big [label="points >= MinPoints"];

    Big -> Catalog [label="TryGet(denomination)\n-> FormationShape"];
    Big -> Ticker [label="BeginGroup (anchor)\nLaunchFormation × denomination"];
    Big -> Reporter [label="ReportArrival\nper formation\n(remainder-free split)"];

    Ticker -> Pens [label="Acquire()/Release()\nper FormationState"];
    Ticker -> Flights [label="Register/Unregister\nCarrierId"];
    Ticker -> Reporter [label="ReportArrival\n(RangeLast, Value)\none per formation"];

    Default -> Flights [label="Register/Unregister\nper-point TrailId"];
    Default -> Reporter [label="ReportArrival\n(1 point each)"];

    Reporter -> LevelController  [label="ScoreTrailArrivedMessage"];
    Reporter -> ScoreController  [label="ScoreTrailArrivedMessage\n(OnTrailArrived)"];
    Reporter -> ColorProgressBar [label="ScoreTrailArrivedMessage"];

    Cinematic  -> Flights    [label="Get(principal id)\nPause / Complete", lhead=cluster_formation];
    Transition -> Service    [label="HoldOutgoing()"];
    Service    -> Flights    [label="-> Flights.CompleteAll()", lhead=cluster_formation];
}
@enddot

## What this diagram shows

How a single big pop becomes a cluster of tumbling 3D polyhedra whose edges are drawn
live by orbiting trail-renderer "pens" — and how that machinery slots into the existing
score pipeline without the rest of the game knowing shapes exist.

**Scoring → choreography seam:** `ScoreController` publishes one `ScorePointsGroupMessage`
per resolved colour (see @ref arch_score_cinematic for the attribution step before this).
`ScoreTrailService.OnScorePointsGroup` allocates a per-group `ScoreTrailReporter` — a
small, deliberately unpooled object whose `ReportArrival` asserts the group's arrivals
never overshoot and eventually sum to exactly `Points` — then asks
`ScoreTrailBehaviourResolver.Resolve(Points)` for a handler. The resolver evaluates
`IScoreTrailBehaviourConfiguration`'s `Entries` in descending `MinPoints` order and
returns the first threshold the group clears: `DefaultScoreTrailBehaviour` below the
configured floor, `BigScoreTrailBehaviour` at or above it. Both handlers share the same
`ScoreTrailContext` and report through the same `IScoreTrailReporter` — the resolver
is the only thing that knows shapes exist at all.

**Formation simulation:** `BigScoreTrailBehaviour.Decompose` runs an optimal coin-change
DP over `ShapeCatalog.Denominations` (`100, 50, 30, 20, 15, 12, 10, 9, 8, 7, 6, 5, 4, 3, 2`),
picking the fewest pieces, largest-first. With both `2` and `3` on the ladder every total
this handler ever sees decomposes remainder-free — `AssertNoRemainder` asserts this holds
rather than silently falling back. Each denomination becomes one *formation*: `ShapeCatalog.TryGet` returns its
baked `FormationShape` (unit-sphere vertices plus the closed walks a pen orbits — a
polyhedron's edges partitioned into Hamiltonian-ish cycles and back-and-forth shuttles),
the behaviour fits every formation's radius inside the board (`FitScale`/`ClampCenter`
over `WallLimits`) and derives a shared tumble axis from the pop's `HitDirection`, then
hands the whole group to `ShapeFormationTicker` via `BeginGroup` (which registers one
bare anchor `Transform` in `TrailFlightRegistry<TrailId>` as the group's *principal*)
and one `LaunchFormation` call per denomination.

**The ticker is the whole simulation.** `ShapeFormationTicker.LateTick` evaluates, per
formation and per frame, a pen's world position as
\f[
C(t) + Q(t)\cdot\Big(\mathit{radius}\cdot \mathit{scale}(t)\cdot \mathit{local}_p(t)\Big)
\f]
`C(t)` lerps origin to the live-tracked
bar centre, `Q(t)` tumbles about the shared (or per-shape-overridden) spin axis, `scale(t)`
comes from the settings' bloom-hold-taper curve, and `localₚ(t)` is the pen's position on
its walk, parameterized by arc length so world-units-per-second pen speed is constant
regardless of segment length. Pens are pooled `FlyingTrail`s acquired from the group's
`TrailSpawner` — each one *is* a `TrailRenderer`; moving it live-draws the ink, and
`TransformRibbon` re-frames already-drawn ink by the same translate+tumble+scale delta
so old ink shrinks and travels with the shape instead of lagging behind. A `Guide` trail
rides the formation centre, tracing the genuine travel path underneath the shape. Only
the principal formation's centre is written back to the group's anchor each tick
(`WriteAnchor`) — that anchor transform is the one thing `TrailFlightRegistry` and the
level-up cinematic ever see.

**Downstream consumers:** every `Reporter.ReportArrival` call publishes one
`ScoreTrailArrivedMessage(colorName, score, points, at)`. `LevelController` folds it into
its per-colour confirmed watermark (`Max(current, Min(msg.Score, projected))`);
`ScoreController` banks it into `TotalScore` and the persistent per-colour score;
`ColorProgressBar` (an `ITrailEndpoint`) moves its fill and supplies the landing point
every formation's `C(t)` travels toward. None of the three know whether the points behind
an arrival travelled as a shape or a scatter of single trails.

**Level-up seam:** the cinematic intercept and pan-in belong to
@ref arch_score_cinematic and @ref arch_trail_composition — this page only shows where
formations plug into that machinery. `LevelUpCinematic` tracks the principal exactly like
any other trail, through `Flights.Get`/`Pause`/`Complete` — a formation's anchor
`Transform` satisfies the same contract a `FlyingTrail`'s transform does. When
`LevelUpPhase` reaches `Pending`, `Flights.PauseAll()` freezes every still-airborne
formation (the ticker inflates ribbon lifetimes so drawn ink survives the freeze without
decaying); when the level transition resolves outgoing content, `LevelTransitionController`
calls `ScoreTrailService.HoldOutgoing`, which calls `Flights.CompleteAll()` — the ticker
sees each surviving formation's `Flight.Phase` go `Idle`, reports its value immediately,
and fades its pens out on an unscaled `SnapFade` rather than snapping instantly.

## Key contracts this enforces

1. **The resolver is the only shape-aware branch point.** `ScoreTrailService`,
   `ScoreTrailContext`, and `IScoreTrailReporter` are identical for both handlers —
   adding a third tier (or changing the `MinPoints` floor) never touches the service.
2. **Every group reports exactly its total, in any order.** `ScoreTrailReporter` polices
   this with dev-build asserts; `TrailFlightRegistry.CompleteAll` deliberately fires
   arrivals in dictionary order, and both `LevelController`'s watermark and
   `ScoreController`'s tally are order-independent by construction.
3. **`ShapeCatalog` is pure baked data, read-only at runtime.** Tables build once in a
   static constructor and are warmed at scope start (`ScoreTrailService.Start`) — the
   ticker never mutates a `FormationShape`, so every formation of the same denomination
   shares one immutable geometry table.
4. **One registered flight per group, not per pen.** The cinematic and the transition
   seam only ever address the group's anchor; a formation's dozens of pens are pooled
   `FlyingTrail`s invisible to `TrailFlightRegistry` and are cleaned up entirely by
   `ShapeFormationTicker` (`ReleaseVertices`) once the group's `Flight` says so.

