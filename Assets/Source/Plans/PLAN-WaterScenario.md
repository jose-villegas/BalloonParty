@page plan_water_scenario Water Scenario & Bubble Merge

# Water scenario, rising bubbles and the bubble merge

> A third backdrop beside the sky and the meadow: a **water surface** that ripples where the
> ball and the balloons pass, read from the same disturbance field the grass bends to. On a
> water level soap bubbles keep landing on the board, more often the longer the level runs, and
> two bubbles **merge** when a nudge pushes one toward the other.
> Planned 2026-11-10; the owner settled the open decisions the same day. **Status: building.**

---

## Owner direction (2026-11-10)

"Another scenario, a water surface, same using disturbance to draw ripples over the background,
here bubbles spawn and the more we stay on this level the more bubbles spawn over balloons, this
session also finishes the merge logic for bubbles neighbors, bubbles merge only on nudge when the
nudge puts them closer to a neighbor bubble."

## What exists (verified 2026-11-10)

| Piece | Where | State |
|---|---|---|
| Scenario kinds | `Configuration/Level/ScenarioKind` | `Sky`, `Ground` |
| Schedule | `ScenarioSchedule` + `ScenarioScheduleSettings` (on `LevelGenerationSettings`) | opening sky, a fixed first meadow, then sky and ground stretches alternate, lengths rolled from the schedule salt mixed with the run seed |
| Scenario state | `ActiveScenario` (written by `Game/Level/ScenarioController`) | cut on run start/restart, mist reveal on the Ascent and the restart rise |
| Backdrop | `Scenario/View/GroundView` | floor quad + grass blades + mist reveal; `GroundRevealPlanner` and `GroundRevealCurve` assume the non-sky side is `Ground` |
| Ground-only content | `IContentPiece.RequiresGround`, `LevelGenerator` | bushes stay off sky levels |
| Disturbance field | `Shared/Disturbance/DisturbanceFieldService` | global `_DisturbanceTex`: R = signed density (0.5 rest), GB = displacement (0.5 rest), held and spread over seconds |
| Bubbles | `BalloonType.BubbleCluster`, `BubbleClusterModel`, `SoapBubbleClusterVariant`, shader `BalloonParty/Balloon/SoapBubbleCluster` | a balloon whose `HitsRemaining` (1–5) is its bubble count; the catalogue entry is `Withheld` until the merge exists |
| Bubbles drifting together | `BalanceBiasKind.Clump` | the balancer already prefers slots near another bubble |
| Merge | `PLAN-FutureIdeas.md` §5.1 | design sketch only: no merge code exists yet |
| Nudge | `Nudge/NudgeService` | a transient impulse (out and back) on `INudgeable` views: `Neighbor` (the six neighbours of a hit, away from it), `Deflect`, `Shockwave` (every actor, away from the origin) |

## Design

### 1. Water in the schedule

`ScenarioKind.Water = 2`. Sky stretches still separate every two surface stretches, so the mist
reveal is always sky ↔ surface and never ground ↔ water. Each surface stretch **after the first
meadow** rolls whether it is a meadow or water (`WaterShare`, 50%). A surface stretch's length is
rolled as before, in the meadow's range, before its kind: turning water on leaves every stretch
where it was, and only some meadows become water. The kind roll hashes the stretch's first level
with its own key, mixed with the schedule salt and the run seed like every other roll, so a run's
layout is stable across retries and each run gets its own. The first meadow stays fixed: bushes
still arrive on time.

`ScenarioStretch` treats water like sky for puff clouds and bushes (full puff clouds; bushes never,
since they require ground). `RequiresGround` stays a ground-only flag: no water-only content exists
yet, and generalising it to a "required scenario" is left until one does.

### 2. The water backdrop

`GroundView` becomes the view of every **surface** scenario: the floor quad swaps its material
between the meadow floor and a new `BalloonParty/Scenario/Water` material, and the grass blades
and their wake draw on ground only. The mist reveal, the depth scaling, `_GroundCoverage` and the
sky hiding all apply to water unchanged (the planner and the curve read "not sky" instead of
"ground"). The water settings (material, colours, ripple tuning) live on `GroundSettings` next to
the floor's, through `IGroundSettings`.

The water shader reads `_DisturbanceTex` directly (no wake buffer: ripples should linger as long
as the field holds the push):

- Base: a deep-to-shallow colour gradient with slow caustic bands from the shared cloud noise,
  scrolling with the content root like the meadow.
- Ripples: rings travel outward along the field's intensity, `sin(k·I + ωt)` scaled by `I`, so a
  ball's wake spreads as concentric rings that fade as the field relaxes.
- The rings' height gradient bends the caustics (a refraction offset) and adds a soft highlight.
- Lit by `SceneLightTintAt` and premultiplied by `GroundCoverageAt`, exactly like `Ground.shader`.

### 3. Bubbles that build up on water

Bubbles arrive **with the clock's lines**, taking a growing share of each line the longer the level
runs, until a line brings more bubbles than balloons. `SpawnClock` counts the level's playing
seconds (`ISpawnClock.SecondsOnLevel`, under the clock's own holds: Game screen, `Playing`, not
paused; reset on level-up and run reset). On a water level, `BalloonSpawner` rolls each pick of a
clock line against the bubble share before the level's weighted pick: a hit takes a bubble in that
place, a miss the usual balloon. The initial fill has no bubbles.

The share is a pure function of seconds on the level (`BubbleRiseCurve.ShareAt(settings,
seconds)`): none during `GraceSeconds` (6 s), then it climbs linearly from `StartShare` (15%) to
`MaxShare` (65%) over `RampSeconds` (60 s) and holds. `MaxBubbles` (12) caps the bubbles on the
board at once; past it the pick falls back to the usual balloon. A water bubble starts as
`StartBubbles` (1) bubbles, so merges have room to grow it. Settings: `IBubbleRiseSettings`, a
`[Serializable]` block on `LevelPacingConfiguration`.

The bubble uses the balloon catalogue's `BubbleCluster` prefab entry, even while the level
generator's entry is `Withheld`: the water rise is its own path.

### 4. The merge

On every nudge, `BubbleMergeService` (`Balloon/Controller/`) looks at each nudged bubble and its
neighbouring bubbles. A pair merges when the nudge **moves them closer**: with the rest positions
`a`, `b` and the nudge displacements `da`, `db` (zero for one that isn't nudged by this event),
they merge when `|(a + da) − (b + db)| < |a − b| − ε`. Sitting adjacent never merges; a spawn never
merges; a nudge that pushes them apart or slides them past each other at the same distance doesn't.
`BubbleMergeRule` is the pure rule, tested on its own.

- The bubble that was pushed flows into the one it was pushed toward, which survives. Its
  `HitsRemaining` becomes the sum, and the absorbed one leaves the grid silently and slides into it.
- If a bubble moves closer to more than one neighbour, it merges with the one it approaches most.
- One merge per bubble per nudge event: no cascades in the same nudge.

## Owner decisions (2026-11-10)

1. **How often water comes up:** a coin flip for every surface stretch after the first meadow.
2. **"Over balloons":** "they spawn more than balloons increasingly": bubbles come in the lines,
   their share growing with time on the level.
3. **The curve:** the ramp from the proposed 6 s grace over 60 s (the proposal was a spawn wait
   shortening from 8 s to 2 s; carried over as a share rising from 15% to 65%).
4. **Clusters bigger than two:** a merge happens only when the sum fits in 5 bubbles; otherwise the
   two stay apart. Merged clusters merge again on a later nudge, up to 5.

## Phases

| Phase | What | Tests |
|---|---|---|
| 1 | `ScenarioKind.Water`, the schedule rolls, `ScenarioStretch` shares | schedule determinism with three kinds, water only from `WaterFromLevel`, never two surfaces back to back |
| 2 | `GroundView` as the surface view, `Water.shader`, settings + material | planner/curve with water; the shader needs an in-editor look |
| 3 | `BubbleRiseCurve`, `SpawnClock.SecondsOnLevel`, the roll in `BalloonSpawner`, settings | the curve as a pure function of time on level; seconds on level under the clock's holds |
| 4 | `BubbleMergeRule`, `BubbleMergeService`, the absorb in `BalloonController` | closer merges; farther or unchanged doesn't; the cap |

Each phase builds Runtime, Editor and Tests.EditMode, and passes `Tools/style_audit.py`.
