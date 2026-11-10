@page plan_water_scenario Water Scenario & Bubble Merge

# Water scenario, rising bubbles and the bubble merge

> A third backdrop beside the sky and the meadow: a **water surface** that ripples where the
> ball and the balloons pass, read from the same disturbance field the grass bends to. On a
> water level soap bubbles keep landing on the board, more often the longer the level runs, and
> two bubbles **merge** whenever they sit next to each other.
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

**The water surface has no clouds or bushes, and nothing on it bounces shots but the
unbreakable balloon** (owner rules). `IContentPiece.KeptOffWater` marks what a water level
leaves out: puff clouds, bushes, the Tough and Tougher balloons (they deflect until their last
hit), and the Deflector and Gatekeeper actors. `LevelGenerator` drops those pieces before any
budget is spent, as a sky level drops ground-only ones, so their budget buys the level's other
threats rather than a pick that comes out empty. Clouds aren't faded out as they are over a
clearing meadow: on water they're simply absent, and `ScenarioStretch`'s puff-cloud and bush
shares are 0 there. `RequiresGround` stays a ground-only flag beside it.

### 2. The water backdrop

`GroundView` becomes the view of every **surface** scenario: the floor quad swaps its material
between the meadow floor and a new `BalloonParty/Scenario/Water` material, and the grass blades
and their wake draw on ground only. The mist reveal, the depth scaling, `_GroundCoverage` and the
sky hiding all apply to water unchanged (the planner and the curve read "not sky" instead of
"ground"). The water settings (material, colours, ripple tuning) live on `GroundSettings` next to
the floor's, through `IGroundSettings`.

- The surface moves by a cheap shallow-water simulation on the GPU (`WaterRippleBuffer`,
  `WaterRipple.shader`), 16 cells per world unit over the disturbance field's bounds, stepped 120
  times a second of game time. It has flow (water runs from high to low, so waves travel and a
  ball drags a wake), surface tension (short ripples outrun long ones, so a splash spreads into a
  train of wavelets), resonance (the screen's edges are walls the waves reflect off), height (crests
  read lighter, troughs darker) and breakup (a crest past the break slope loses flow, and foam
  forms only on sharp peaks, where the normal turns suddenly, and fades fast). Every impact the
  game reports (`ImpactEventBus`: each step of a ball's flight, pops, bombs, balloons gliding in)
  presses the water for 0.12 s and drags it along the impact's heading. The surface's slope
  (`Include/WaterSurface.cginc`) is the simulation's plus a slow swell of two noise layers on
  different headings, the smaller slower, travelling with the content root. Each wave also throws a
  bright and a dark band on the sand and sheens the surface whatever the sun's angle, so it reads at
  any time of day.
- The water has depth. A noise map lays sandbanks and deep channels (0.6 to 4.5 world units). The
  bottom is seen as from a camera above the middle of the screen, so a deeper bottom shrinks toward
  the middle and slides slower when the board scrolls; the surface's slope bends the view of it in
  proportion to depth. The water absorbs each colour by its own rate (red first) and scatters its
  own colour back: shallows read as sand, deep water as teal. The sand carries ripple bands.
- Caustics are a second texture rendered under the surface, as the owner proposed: each frame
  `WaterCausticBuffer` draws one tile of a moving caustic net (`WaterCaustics.shader`) into a
  256² R8 target that wraps, with mips. The net is the borders between drifting cells, warped so
  they curve, bright on the border and fading into each cell. `Water.shader` lays it under the
  surface twice, at two scales and angles: the first draws the lines, the second, read softer,
  varies their brightness along them, so the net never looks tiled. It's wobbled by the noise,
  shifted away from the time-of-day sun (`_SceneLightDir`), split into colour fringes along it,
  and blurred by up to 1.5 mips where the water is deep.
- Layers, bottom up: the sand lit by the caustics (shaded around its own colour, never added past
  it, so nothing blooms), the water body's absorption and scatter, then a light veil of water
  colour. On top, the slopes facing the sun catch a soft highlight, and the crests turned just so
  between the sun and the eye glint.
- Lit by `SceneLightTintAt` (at 0.7 influence, so a warm evening tints the water rather than
  turning it olive; light above full is eased in so a pop's glow doesn't clip to white) and
  premultiplied by `GroundCoverageAt`, like `Ground.shader`.
- What didn't work: the first look read as neon and dark (bright additive caustics over saturated
  deep blues). Caustics drawn from the surface's own curvature into a low-resolution buffer came
  out as soft blobs that didn't read as caustics. Sparkles from fine noise waves read as static.
  Rings drawn straight from the field (`sin(k·push + ωt)·push`) kept the push's shape and wobbled
  in place like gel, and a flat two-colour bottom gave the water no volume (owner: "behaves more
  like a gel, ripples don't travel. Also lacks depth"). A wave-equation simulation came next.
  Driven by the soft, board-wide field it only sloshed in big blobs; driven by the impacts, its
  dozens of rings a second interfered into a chop in which no ring read (owner: "I don't see
  ripples"). Rings drawn one by one from each impact read, but never met anything (owner: "there
  is no flow, tension or resonance, no height or breakup"): the shallow-water simulation replaced
  them, pressed smoothly by each impact rather than re-driven by the whole field every frame. Its
  first foam came from every steep slope and every big splash and covered half the screen after a
  board clear (owner: "it should be bound to where the normals suddenly change, so around peaks"),
  and its waves barely showed through the swell and the caustics until they stood taller, shaded
  their crests and bent the bottom more. Tuning that against one shot on calm water misled twice:
  a crest that breaks too early kept a normal hit's peaks under the foam threshold and its waves
  faint (owner: "barely visible", "is the foam even present?"), while letting it break later,
  with the waves lasting 2.5 s and the walls reflecting them, built up so much energy in real play
  that the water saturated and foamed everywhere. The sim is tuned while the game plays: waves fade
  over a second, crests break past 0.15 a cell, and foam sits above the background's slosh and
  below a fresh hit's peaks.
  The tile's cell wrap uses `id - n·floor((id + 0.5)/n)`, not `fmod`: GPU division is
  approximate, `fmod(10, 5)` can return 5, and the tile then shows a seam.

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

Two bubble clusters in neighbouring slots merge, whatever brought them there: a spawn, the board
settling, a pop opening the way. `BubbleMergeService` (`Balloon/Controller/`, a tick) looks again
whenever the grid has changed (`SlotGrid.MutationVersion`) and merges every neighbouring pair
whose bubbles fit in one cluster. `BubbleMergeRule` is the pure rule, tested on its own.

- The smaller cluster flows into the larger, which survives (between equals, the slot order
  decides). The survivor's `HitsRemaining` becomes the sum, and the absorbed one leaves the grid
  silently and slides into it.
- A cluster between two neighbours joins the one that makes the bigger cluster.
- Each cluster merges at most once a frame. The absorb changes the grid, so a cluster that grew
  merges again on the next frame if a neighbour still fits: chains collapse a step a frame.
- A first version merged only when a nudge pushed two bubbles closer; the owner dropped that:
  "Just merge them when next to each other, no nudge".

## Owner decisions (2026-11-10)

1. **How often water comes up:** a coin flip for every surface stretch after the first meadow.
2. **"Over balloons":** "they spawn more than balloons increasingly": bubbles come in the lines,
   their share growing with time on the level.
3. **The curve:** the ramp from the proposed 6 s grace over 60 s (the proposal was a spawn wait
   shortening from 8 s to 2 s; carried over as a share rising from 15% to 65%).
4. **Clusters bigger than two:** a merge happens only when the sum fits in 5 bubbles; otherwise the
   two stay apart. A merged cluster merges again with any neighbour that still fits, up to 5.
6. **When they merge:** whenever two sit next to each other, no nudge needed (it first followed
   the direction above, "only on nudge"; the owner changed it after trying it).
5. **What water leaves out:** "water surface has no clouds or bushes", and "no deflector balloons
   except for unbreakable": no Tough, Tougher, Deflector or Gatekeeper on water either.

## Phases

| Phase | What | Tests |
|---|---|---|
| 1 | `ScenarioKind.Water`, the schedule rolls, `ScenarioStretch` shares | schedule determinism with three kinds, water only from `WaterFromLevel`, never two surfaces back to back |
| 2 | `GroundView` as the surface view, `Water.shader`, settings + material | planner/curve with water; the shader needs an in-editor look |
| 3 | `BubbleRiseCurve`, `SpawnClock.SecondsOnLevel`, the roll in `BalloonSpawner`, settings | the curve as a pure function of time on level; seconds on level under the clock's holds |
| 4 | `BubbleMergeRule`, `BubbleMergeService`, the absorb in `BalloonController` | neighbours merge, the smaller into the larger; the cap; one merge per cluster a pass |

Each phase builds Runtime, Editor and Tests.EditMode, and passes `Tools/style_audit.py`.
