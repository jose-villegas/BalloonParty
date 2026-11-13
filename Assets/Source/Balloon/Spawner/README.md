# Balloon/Spawner

Responsible for introducing balloons into the grid — the initial fill at game start, then one new line at a time on a timer during play.

## Contents

| File | What it does |
|---|---|
| `SpawnClock` | `ITickable` + `IRunResettable` — the timer that brings lines in: publishes `SpawnBalloonLineMessage(1)` once per `ISpawnClockSettings.IntervalForLevel(level)` of scaled game time (times the level's `ILevelParameters.SpawnIntervalScale`: an extra-colour level, see `Configuration/README.md`, brings lines about half again as often), after `GraceSeconds` at each level start and run reset. Holds while not on the `Game` screen, while the level is not `Playing`, during any pause, and while the only ball in flight is doomed (so no line lands in its frozen last moment). One line per tick: a long frame never lands a backlog. `ISpawnClock.NextLineProgress` (0 → 1 across the current wait) feeds the danger heart's shine |
| `BalloonSpawner` | `IStartable` + `IGridSpawner` — creates and places balloons, manages per-type pool registration and active-count caps, and decides where each line's balloon goes. Each line first computes the deficit via `WaveDeficitCalculator`; if hearts would be lost it still spawns the line, tagged doomed, so `RejectedBalloonEffect` can show it, then publishes `WaveDamageMessage`. `SpawnPriority` is `SpawnStage.BalloonActors` (100). `Start()` registers pools and kicks off pre-warm asynchronously; `SpawnAsync()` awaits the pre-warm then populates the initial grid |
| `RejectedBalloonEffect` | **Purely visual** overflow pile for balloons that couldn't be placed — HP drain is handled at the wave level by `WaveDamageMessage`, not per-balloon. `Play` queues a would-be balloon into the overflow rows below the grid. `ITickable` runs the pile as a **per-column queue**; balloons compact upward when one pops. During spawning, `BalloonSpawner` brackets doomed-line balloons with `BeginDoomedLine(lineIndex)` / `EndDoomedLine()` — those balloons are **not** auto-popped by the heart-drain interval. Instead, `PopDoomedLine(int)` bulk-pops every balloon tagged with a given line index (triggered by `HeartTrailController` when its strikethrough sequence completes). Non-doomed overflow balloons still drain one at a time (front-most first, serialized via `PopIntervalSeconds`). `IRunResettable` returns any transients on restart. `IsOverflowActive` (pile non-empty) holds the level-up transition and the heart-drain cinematic, but nothing pauses: play carries on while the pile drains. Timing/motion come from `IOverflowSettings` |
| `ReinforcementPlan` | Pure: how many of the unbreakables the formations called in a line can bring, held to the ceiling and the line's open places. See `Balloon/Formation/README.md` |
| `BalloonFactory` | Assembles a single balloon: pulls a view from the pool, builds the model (via `BalloonModelFactory`) and its `BalloonController`, places it on the grid, and plays the entry animation. The spawner owns scheduling, per-type caps, and path computation; the factory owns the object-graph wiring |
| `BalloonPlacementResolver` | Decides which slot a line's balloon takes for a given column — own entry, re-home, or pressure-open (see "Placement under pressure") |
| `BalloonPoolChannel` | `InjectingPoolChannel<BalloonView>` — creates balloon instances via `IObjectResolver.Instantiate()`, injecting all `[Inject]` fields from the parent container without creating child scopes |

## Behaviour

`BalloonSpawner` implements `IGridSpawner` with `SpawnPriority = SpawnStage.BalloonActors`. `GridSpawnerCoordinator` calls `SpawnAsync()` after the Navigation gate opens and all lower-priority spawners (e.g. `StaticActorSpawner`) have completed. `SpawnAsync` awaits the pre-warm task started in `Start()` — so pool pre-warming and the navigation/static-actor wait overlap rather than serialize.

At game start `BalloonSpawner` spawns the initial grid rows — the row count is the active level's resolved `IActiveLevelParameters.Current.BoardLines`. For each empty slot it picks a balloon type via the level's resolved weighted pick (`IActiveLevelParameters.Current.PickBalloonEntry`, which gates types by the active range and respects each type's resolved max-count cap), then hands the entry to `BalloonFactory.Create`. Model configuration such as `TypeName`, `ScoreValue`, `HitsToPop`, `NudgeOverrides`, and `ItemActivationWeight` is bundled into a `BalloonModelConfig` struct and passed to the model constructor. `BalloonModelFactory.Create` (in `Balloon/Model/`) picks the model class from `entry.BalloonType` — shared with `RejectedBalloonEffect` so the switch lives in one place:

| `BalloonType` | Model class | Notes |
|---|---|---|
| `Simple` | `BalloonModel` | Paintable, item-capable |
| `BubbleCluster` | `BubbleClusterModel` | Not paintable, not item-capable; `HitsToPop = 5` maps hits → bubble count via `SoapBubbleClusterVariant`; scores scatter across the level's still-incomplete color bars |
| `Tough` | `ToughBalloonModel` | Not paintable, not item-capable |
| `Unbreakable` | `UnbreakableBalloonModel` | No `HitsRemaining`; only Piercing destroys |

The variant's `Initialize(model, levelAllowedColorsMask)` handles color (for colorable types) — the mask is the active level's color gate, intersected with the variant's own allowed-color mask so a level can withhold specific colors. Spawn animation follows the path returned by `SlotGrid.ComputePath`, driving a `DOPath(CatmullRom)` from the entry-row offset down to the target slot (run by `BalloonFactory.AnimateSpawn`).

Lines arrive on `SpawnClock`, not on shots: whatever the player does, a line enters every few seconds. The gap between lines is 1.7 s at level 1 and shrinks 5% per level until it reaches 0.9 s (around level 14), then 2% per level down to a 0.55 s floor (around level 38), after a 2.5 s grace at each level start; all of it lives in `LevelPacingConfiguration`'s `_spawnClock` block (`SpawnClockSettings`). The slower late rate matters because by level 14 the level generator has already bought every threat and the colour count is at its peak: past that point, faster lines are what makes a deeper level harder. Firing ties into the clock: a ball fired while another is still in flight (`ProjectileFiredMessage`, checked against `IProjectileRegistry`) takes `OverlapShotPull` (35%) of the current wait off the next line. One ball at a time is free, so every extra ball in the air costs board room. The pull only shortens the wait; the next tick lands the line, under the same holds as the timer. Board tension feeds it too: `BoardFill` (`IBoardFill`) measures balloons two ways, recounting only when `SlotGrid.MutationVersion` moves: `StartingFill`, against the level's starting fill (board lines × columns), and `Occupancy`, as a share of the slots grid actors leave open. Grid actors are never counted as balloons. The clock reads occupancy against `TargetOccupancy` (50%), blends the two by `OccupancyWeight` (half each), and runs up to `SparseBoardSpeedup` (8×) faster on an empty board, easing back to normal pace as the blend reaches full. Occupancy matters on later levels, whose starting fill is small: at level 5 a board at its starting fill is about a third full, so the clock still runs near 2× until it nears half full. Balls in the air add to that pace: the clock ages each in-flight ball while it runs (dropping it on `IProjectileRegistry.Retired`), and their summed flight seconds against `FlightPressureSeconds` (6 s) add up to `FlightSpeedup` − 1 (2×) on top. One ball cruising for a long ricochet, or a burst of rapid fire, brings lines in while it clears them. Clearing the board brings the next lines in fast instead of leaving a near-empty screen, except after a big clear, which earns a breather. When the board loses `BreatherClear` (a fifth) of its open slots within `BreatherClearWindow` (2 s), the clock's pace drops to `BreatherPace` (a fifth of its full pace) and eases back to full over `BreatherSeconds` (8 s), the sparse-board and flight speedups included, so the board refills ever faster towards the end. A ball fired over another pulls the next line by the same share. To spot a clear that fast, the clock keeps the board's highest recent occupancy, which falls at the rate that would drain `BreatherClear` in one window. Through a breather that peak follows the board, so the clear that started one can't start the next. After each, `BreatherCooldownSeconds` (16 s) of play must pass before a clear can earn the next: on a crowded board big clears come every 25 s or so whatever the bar, so the cooldown is what keeps releases rare. A new level or run ends both. Without the breather, deep levels were tension with no release: the sparse-board speedup refilled a cleared board at once. The clock publishes `SpawnBalloonLineMessage(1)`; the spawner handles every request with the same synchronous `SpawnLine()` — one line, no delays, no async. Requests are ignored until the pools are prewarmed. The cheat console publishes the same message, possibly with a larger count, and gets that many lines back to back. `SceneTransition` does not publish it: game start fills the board through `SpawnAsync`, and the clock's grace covers the first moments.

Lines group into **waves** of `ISpawnClockSettings.LinesPerWave` (3). Counting restarts at each level-up. Per-type quotas (the `WaveCountWeights` curves), roamer jumps and item cadence were tuned for the old multi-line turn, so they work per wave rather than per line:

- A wave's **first line** rolls the per-type quotas (later lines in the wave draw down what's left), calls `BalloonBalancer.Balance(atWaveStart: true)` — which relocates roamers and refreshes the heavy-step budget — and every line, first or not, runs a plain `Balance()` before placing.
- A wave's **last line** publishes `ItemCheckMessage(Wave)` over that line's balloons only (earlier lines' balloons may already be popped); `ItemAssigner` counts waves against `ItemCadence`. Other lines drop their new-balloon list without a check.
- Every line ends by publishing `BalanceBalloonsMessage` so the balancer settles the grid.

A doomed line (one the board has no room for) still spawns: `BeginDoomedLine` / `EndDoomedLine` tag its overflow balloons so they wait for the strikethrough, and `WaveDamageMessage` goes out after them. `SpaceDanger` assumes this one-line-per-tick shape. The line hold while the doomed ball flies alone lives in `SpawnClock`, not here (`Projectile/README.md`).

### Reinforcements

A clock line also brings the unbreakables the board's formations called in at this wave's start
(`IFormationReinforcements`, see `Balloon/Formation/README.md`). Like the initial fill's guaranteed
entries, they go into the batch first and bypass the level's pick and its wave quota. They also bypass
the level's cap for unbreakables: they aren't counted in the active counts, so the level's own
unbreakables keep their cap, and the spawner counts these apart, up to
`IUnbreakableFormationSettings.ReinforcementCeiling` alive at once. All unbreakables in a batch are alike,
so the first ones dealt are tagged as the reinforcements, and an undealt one hands back no count. The
unbreakable pool is prewarmed with room for the ceiling.

### Reward balloons

Each clock line asks `IRewardBalloonRequests.TryTakeDue` once, before the reinforcements, whether it
brings a reward balloon (the golden one on a purple level, a heart for a player missing hearts; see
`Balloon/Reward/README.md`). The answer takes one of the line's places, outside the level's caps and
counts, and goes to the front of the batch after the sort, so a crowded line drops something else. When
it lands, `Placed` hands back the callback the balloon calls on leaving, in place of the active-count
release. A line with no room for it leaves it due for the next. It stays out of the item roll. Without a
catalogue entry for the type nothing comes; each entry is prewarmed to one.

### Placement under pressure (clock lines)

A blocked column isn't lost immediately. `BalloonPlacementResolver.Resolve` picks a slot in order, scanning columns nearest-first (`TryNearestColumn`):

1. **Own column** — `FindFirstReachableEmptyRow` (the topmost slot reachable by rising from the entry).
2. **Re-home** — the nearest *other* column the balloon can rise straight into (`ResolveOpenEntry`); a line may over-fill a column.
3. **Pressure-open** — the nearest column `BalloonBalancer.TryRelievePressure` can shove open by pulling a balloon into a gap *anywhere* on the board (`ResolvePressureOpen`), including interior pockets no entry reaches directly.
4. **Reject** — only when nothing frees a slot: `RejectedBalloonEffect.Play` queues the would-be balloon into the visual overflow pile below the grid (purely decorative — actual HP loss is computed at the wave level by `WaveDeficitCalculator`).

Placement reach is a `PlacementReach` argument to `Resolve`:

- **Clock lines** pass `PlacementReach.Pressure` — all four steps above.
- **Initial grid fill** passes `PlacementReach.Rehome` — steps 1–2 only. On tight boards (static actors occupying cells) a column can be blocked at level start, so rehoming redistributes its allotment into columns that still have reachable room, keeping the total at `BoardLines × columns` whenever the board's reachable capacity allows. It never shoves (no step 3) or overflows below the grid (no step 4). `LogInitialFillDiagnostics` logs expected/achievable/spawned per column in editor and development builds.
- **Pop-spawn extras** (`SpawnLooseBalloons`) pass `PlacementReach.OwnColumn` — step 1 only; a blocked column is skipped.

### Spawn depth & tough layering

`BalloonPrefabEntry.SpawnWeight` controls *depth*, never *which* types spawn or *how many* (those come from the range pick weights and the `InitialCountWeights`/`WaveCountWeights` quota curves). Each batch (the initial fill, or one line) is dealt into the topmost open rows first, so the deal order is the vertical order. `PrepareSpawnBatch` sorts the batch **lightest-first** (`BySpawnWeightAscending`), so heavier types (Tough, weight `+3`) settle below lighter ones — one bottom-heavy gradient.

The **initial fill** overrides that with vertical layering (`ArrangeInitialLayers` + the pure `InitialLayerPlan.HeavyPerLine`). The board is split into segments `BalloonsConfiguration.ToughLayerSpacing` lines tall; the bottom line of each segment collects the heavy (positive-weight) types, and lighter types fill the rest. With spacing `3` that reads as *two light lines, one part-tough line, repeating*. Heavies are round-robined onto the layer lines deepest-first (an uneven count settles lower), capped at one board line each; any surplus beyond the layers' capacity sinks to the bottom as the plain gradient would place it. This is **arrangement-only** — the tough *count* is unchanged, just its depth. Spacing `0`/`1`, no heavies, or a board too short for a full segment all fall back to the plain gradient. The mapping is exact on an empty board (one pass ≈ one row) and approximate on tight boards where static actors offset a column's entry row.

## Interactions

- **SlotGrid** — queried for empty slots (`CountEmpty()`); all placed balloons registered here
- **WaveDeficitCalculator** — called before each line to determine hearts lost (pure static function in `Game/Health/`)
- **WaveDamageMessage** — published after doomed lines are spawned, carrying `HeartsLost`
- **BalloonBalancer** — notified via `BalanceBalloonsMessage` after each line; `Balance(atWaveStart)` before it
- **ItemAssigner** — notified via `ItemCheckMessage` carrying the newly spawned balloon models, once per wave (`Wave` = waves spawned this level)
- **SpawnBalloonLineMessage** — triggers a line spawn; published by `SpawnClock` (and by the Spawn Balloon Line cheat)
- **ILevelPacingConfiguration.SpawnClock** — `LinesPerWave`, plus the interval/grace the clock reads
- **BalloonsConfiguration** — `Entries`, `DefaultBalloonMoveSpeed`, `SpawnEntryRowOffset`. Spawn entry travels at a constant speed (per-type `BalloonPrefabEntry.MoveSpeed`, or this fallback): duration = path length ÷ speed, so a balloon entering from farther down takes proportionally longer instead of racing a fixed duration.
- **IActiveLevelParameters** — `BoardLines`, `PickBalloonEntry` (the per-level resolved spawn counts + weighted balloon pick)
- **DisturbanceFieldService** — stamps `Stamp()` along each spawn path using the `BalloonPath` stamp profile from `DisturbanceFieldSettings`; creates visible wakes through Puff clouds during spawn animations
