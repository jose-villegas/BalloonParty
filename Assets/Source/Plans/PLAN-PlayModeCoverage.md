@page plan_playmode_coverage PlayMode Test Coverage

# PlayMode Test Coverage

> A plan for growing the PlayMode suite (`Assets/Tests/PlayMode/`) beyond its current
> three tests. PlayMode is reserved for behaviour the EditMode suite genuinely cannot
> reach — physics, pooling, DOTween/async, the GPU, and the real scene + DI graph.
> This is the deliberate complement to @ref plan_test_gap, which is EditMode-only.

---

## Why PlayMode (and why sparingly)

EditMode (272 tests) covers pure-C# logic in milliseconds and is the default. PlayMode
exists only for what EditMode structurally can't drive:

- **Physics** — `Physics2D.OverlapCircle` / `CircleCast` (bomb blast, laser cross) need real
  colliders and a physics step.
- **Pooling** — `PoolManager` / `PoolChannel` `Get`/`Return`/prewarm instantiate real
  `MonoBehaviour`s under the player loop.
- **DOTween / async** — balance moves, trail flights, the level-up cinematic, `UniTask`
  spawn/reload flows resolve across frames.
- **GPU** — the disturbance field's RT ping-pong / `Graphics.Blit` only runs with a render
  device.
- **Scene + DI** — the real `Game` scene wired through `GameLifetimeScope`.

These are also exactly the systems the codebase marks **deferred** in `Assets/Tests/README.md`,
and the changes recently shipped with a "needs an in-editor playtest" caveat. PlayMode is how
we automate those caveats.

**Cost.** Each test loads the scene (seconds), tests are timing-sensitive, and they need a
Unity licence + graphics device — so the suite stays **small and local** until the paused CI
(see [[ci-test-runner-status]]) is revived with a graphics-capable runner. Assert **structural
invariants** (counts, settled-ness, state transitions, no-throw), never exact placements or
pixels — spawn/colour/cluster randomness makes specifics non-deterministic.

## Current PlayMode coverage (3 tests)

| Fixture | Covers |
|---|---|
| `RunRestartPlayModeTests` (1) | Restart clears → repopulates the board (caught the prewarm "await twice" race) |
| `PressureLossPlayModeTests` (2) | Spawn-saturation → reject → HP drain → GameOver |

## Harness

The two fixtures already share an (un-extracted) pattern:

```csharp
yield return LoadGameScene();                              // SceneManager.LoadSceneAsync("Game")
var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
var grid  = scope.Container.Resolve<SlotGrid>();
yield return WaitUntil(() => BalloonCount(grid) > 0);      // poll-with-yield
```

**First step: extract `PlayModeGameTest`** (base fixture) so each new test is ~10 lines:

- `LoadGameScene()` — load + one settle frame so `GameLifetimeScope` builds and entry points run.
- `Resolve<T>()` — scope lookup + `Container.Resolve<T>()`.
- `WaitUntil(Func<bool>, float timeoutSeconds = 5f)` — **must** take a timeout and fail loudly
  rather than hang the runner (the current helper loops forever).
- `[TearDown]` — unload/reset so fixtures don't bleed (each test gets a fresh scene).

## Conventions for PlayMode tests

- **Condition waits, not frame counts** — `WaitUntil(cond, timeout)`; never `WaitForSeconds(n)`
  as a proxy for "it's probably done".
- **Determinism** — where a specific outcome is asserted, drive it deterministically (single
  spawn line, place balloons by hand via the grid, fixed colours). Otherwise assert invariants.
- **No-throw is a valid assertion** — for orchestration we can't easily inspect (cinematic, GPU
  blit), "runs N frames / completes without an exception or error log" is a real regression guard
  (it would have caught the `Stamp`-before-`Start` NRE at integration level).
- **`LogAssert`** — assert the absence of error logs over the run where relevant.

## Candidate coverage (tiered by value × risk)

### Tier 1 — zero coverage today, highest gameplay risk

| Test | Setup → assertion | Reaches |
|---|---|---|
| `ShotLoopPlayModeTests` | Fire the loaded projectile at a placed balloon → assert it pops, a trail arrives, the bar/score increments, and the thrower reloads | ThrowerController→View, ProjectileHitResolver, ScoreTrailService, reload |
| `BombActivationPlayModeTests` | Place balloons in/out of blast radius, activate a Bomb → assert in-radius balloons popped, out-of-radius survived | `BombItemHandler.BlastBalloons` (`OverlapCircle`) — **0 coverage** |
| `LaserActivationPlayModeTests` | Place balloons along the cross, activate a Laser → assert only crossed balloons hit | `LaserItemHandler.CastCross` (`CircleCast`) — **0 coverage** |
| `DisturbanceFieldPlayModeTests` | Resolve the service, stamp from bomb/paint/projectile sources, tick ~30 frames → assert no exceptions/error logs and `_DisturbanceTex` global is set | RT ping-pong + `Graphics.Blit`; guards the `Stamp`/`Start` bug class |

### Tier 2 — async / pooling / animation

| Test | Setup → assertion | Reaches |
|---|---|---|
| `BalanceSettlePlayModeTests` | Spawn a wave with gaps → wait → assert the board settles (no unbalanced dynamic actor remains) | `BalloonBalancer` frame-deferred scan+move+DOTween |
| `PoolIntegrityPlayModeTests` | Run several spawn/clear cycles → assert pool `Get`/`Return` balance, returned views deactivated, no growth/leak | `PoolManager`/`PoolChannel`, prewarm |
| `PaintActivationPlayModeTests` | Activate Paint with mixed-colour neighbours → assert neighbours recolour, splash effect spawns/returns, no throw | `PaintItemHandler.Activate` + pooled `PaintSplashView` |

### Tier 3 — valuable, harder to assert

| Test | Setup → assertion | Reaches |
|---|---|---|
| `LevelUpCinematicPlayModeTests` | Drive a level-up → assert the cinematic plays, bars drain, state returns to `Game`, no throw | `CinematicDirector` / `LevelUpTrailEffect` (memory-flagged fragile) |
| `OverflowThrowerLockPlayModeTests` | Force an overflow that pops → assert the thrower is locked during the reject sequence and unlocks only if hearts remain | overflow-hold + `PauseSource.Overflow` |
| `BushRustlePlayModeTests` | Move the projectile across a bush → assert each slot rustles once on entry, re-arms on exit | `BushRustleController.Tick` |

## Sequencing

1. **Extract `PlayModeGameTest`** and migrate the two existing fixtures onto it (no behaviour
   change; kills the duplication, adds the `WaitUntil` timeout).
2. **Tier 1** — the shot loop + the two physics activations + the disturbance smoke. Largest
   risk, zero current coverage.
3. **Tier 2** as capacity allows.
4. **Tier 3** opportunistically, especially the cinematic (it's the most fragile and least
   covered orchestration).

## Open questions / constraints

- **CI** — PlayMode needs a licence + graphics. Decide whether to gate PlayMode on a dedicated
  job (graphics runner) or keep it a local pre-merge step until CI is revived.
- **Runtime budget** — scene-load per test is the dominant cost. If the suite grows, consider a
  shared-scene fixture for read-only tests (with careful reset) vs scene-per-test isolation.
- **Determinism hooks** — some tests want a fixed RNG seed or a single-colour palette; assess
  whether a test-only seam is worth adding or whether invariants suffice (prefer invariants).
