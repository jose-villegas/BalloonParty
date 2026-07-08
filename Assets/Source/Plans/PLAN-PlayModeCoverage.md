@page plan_playmode_coverage PlayMode Test Coverage

# PlayMode Test Coverage

> A plan for growing the PlayMode suite (`Assets/Tests/PlayMode/`) beyond its current
> three tests. PlayMode is reserved for behaviour the EditMode suite genuinely cannot
> reach — physics, pooling, DOTween/async, the GPU, and the real scene + DI graph.
> This is the deliberate complement to the EditMode test suite.

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

## Current PlayMode coverage (11 tests, all green 2026-07-09)

| Fixture | Covers |
|---|---|
| `RunRestartPlayModeTests` | Restart clears → repopulates the board (caught the prewarm "await twice" race) |
| `PressureLossPlayModeTests` (2) | Initial health + spawn-saturation → reject → HP drain → GameOver |
| `BombActivationPlayModeTests` | Bomb `OverlapCircle` blast pops a neighbour |
| `LaserActivationPlayModeTests` | Laser `CircleCast` cross removes a balloon |
| `DisturbanceFieldPlayModeTests` | GPU field stamps/ticks + publishes `_DisturbanceTex` without error |
| `BalanceSettlePlayModeTests` | Spawn wave → board settles (nothing left in transit) |
| `PoolIntegrityPlayModeTests` | Restart cycles repopulate without errors |
| `PaintActivationPlayModeTests` | Pooled splash flights run without error; count stable (recolour ≠ destroy) |
| `LevelUpCinematicPlayModeTests` | Cheat-driven level-up ceremony runs without throwing |
| `OverflowThrowerLockPlayModeTests` | Overflow hold locks the thrower (fire gated on `PauseService`) |

## Harness — `PlayModeGameTest` (DONE)

Every fixture derives from it; each new test is ~10 lines. It provides:

- `LoadGameScene()` — load + one settle frame so `GameLifetimeScope` builds and entry points run.
- `Resolve<T>()` — scope lookup + `Container.Resolve<T>()`.
- `WaitUntil(cond, timeout, message)` — fails loudly on timeout (never hangs the runner).
- `FillAndSettle(grid)` — packs a few lines, then waits for the overflow hold to release and all
  balance moves to leave transit.
- `TryFindInteriorBalloon` (requires a populated neighbourhood) + `WaitForColliderAt` (an
  `OverlapPoint` probe — the grid registers a balloon before its collider finishes flying there).
- `[SetUp] ResetNavigation()` — returns to Launch so a GameOver test doesn't leave the next one's
  spawn gate shut (PlayMode shares static state; no domain reload between tests).

## Conventions for PlayMode tests

- **Condition waits, not frame counts** — `WaitUntil(cond, timeout)`; never `WaitForSeconds(n)`
  as a proxy for "it's probably done".
- **Determinism** — where a specific outcome is asserted, drive it deterministically (single
  spawn line, place balloons by hand via the grid, fixed colours). Otherwise assert invariants.
- **No-throw is a valid assertion** — for orchestration we can't easily inspect (cinematic, GPU
  blit), "runs N frames / completes without an exception or error log" is a real regression guard
  (it would have caught the `Stamp`-before-`Start` NRE at integration level).
- **`LogAssert`** — assert the absence of error logs over the run where relevant.

## Shipped — notes where the test differs from the original sketch

Tier 1 (Bomb, Laser, Disturbance), Tier 2 (BalanceSettle, PoolIntegrity, Paint) and Tier 3
(LevelUpCinematic, OverflowThrowerLock) are all built and green. Right-sizings made when reality
diverged from the sketch (PlayMode can only assert what it can drive without running headless):

- **Paint** is a pooled-splash **smoke** test (runs without error, count stays stable). The recolour
  *correctness* stays in EditMode — the shipped blob radius is too sparse to reliably land on grid
  slots from an arbitrary aim, so a spatial recolour assertion is non-deterministic here.
- **Overflow** asserts the **lock** only. The recoverable release window drifts toward GameOver under
  continued saturation and is too timing-fragile to assert deterministically.
- **PoolIntegrity** asserts repopulate-across-cycles-without-errors — `PoolManager` exposes no
  idle/active counts, so a true Get/Return balance can't be inspected; a leak surfaces as an error.
- **LevelUpCinematic** asserts the ceremony leaves the `Playing` phase and runs frames without a
  throw (the no-throw guard for the fragile path), driven by the `Trigger Level Up` score cheat.

## Remaining

- **`ShotLoopPlayModeTests`** (Tier 1) — real projectile flight → pop → score. Needs a **test-only
  fire seam** on `ThrowerController`: launching the model directly (`IsFree`/`Direction`) fights its
  load/aim/fire state machine. Add a minimal internal `FireForTest(direction)` (or expose the active
  model) first, then this becomes straightforward.
- **`BushRustlePlayModeTests`** (Tier 3) — needs a **bush-containing level** loaded (level 1 has none)
  and an observable rustle signal to assert against (`BushRustleController` has no hook today).

## Open questions / constraints

- **CI** — PlayMode needs a licence + graphics. Decide whether to gate PlayMode on a dedicated
  job (graphics runner) or keep it a local pre-merge step until CI is revived.
- **Runtime budget** — scene-load per test is the dominant cost. If the suite grows, consider a
  shared-scene fixture for read-only tests (with careful reset) vs scene-per-test isolation.
- **Determinism hooks** — some tests want a fixed RNG seed or a single-colour palette; assess
  whether a test-only seam is worth adding or whether invariants suffice (prefer invariants).
