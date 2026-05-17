# Game

The entry point that starts and runs the game.

## Contents

| File | What it does |
|---|---|
| `GameLifetimeScope` | VContainer composition root — registers all game services, entry points, MessagePipe brokers, configuration assets, and (in dev builds) cheats. `Awake()` pre-allocates DOTween capacity (`SetTweensCapacity(200, 50)`) before building the container to avoid GC from array resizing during the initial balloon spawn burst |
| `LaunchLifetimeScope` | VContainer root for the Launcher scene — registers `GameDisplayConfiguration` and `OrthogonalSizeCameraController` for the launch camera |
| `Score/` | Score tracking, level progress, and trail orb management — see `Score/README.md` |
| `Cinematics/` | Cinematic director, scene definitions, and the level-up trail effect — see `Cinematics/README.md` |

## Architecture

`GameLifetimeScope` is the sole composition root. All systems — spawner, balancer, nudge, thrower, score, score trails, cinematics, items — are wired here and inherit into child scopes automatically.

Scene-placed child scopes (`ScoreUILifetimeScope`, `LevelUpLifetimeScope`, `ShieldUILifetimeScope`, `ThrowerLifetimeScope`) extend `LifetimeScope` directly. Parent scope is wired via VContainer's `parentReference` field in the Inspector.

**Pooled prefabs (balloons, projectiles)** no longer use child scopes. Their `[Inject]` fields are populated via `InjectingPoolChannel`, which calls `IObjectResolver.InjectGameObject()` from the parent container — skipping container creation entirely. `BalloonLifetimeScope`, `ProjectileLifetimeScope`, and `ItemViewScope` components remain on prefabs with `autoRun = false`.

`ScoreController` subscribes to `BalloonHitMessage` and `ScoreTrailArrivedMessage`. On a qualifying pop it publishes `BalloonScoredMessage` carrying the current projected progress but does not mutate confirmed score state. Score mutation is deferred to trail arrival: when `ScoreTrailArrivedMessage` is received, it increments per-color `_persistentScore` and `_totalScore`, then sets `_levelProgress` to the trail's score value (the trail score IS the progress point it represents). When all colors' confirmed progress meets the level threshold, it increments the level, resets per-color progress and projected progress, publishes `ScoreLevelUpMessage`, transitions navigation to `LevelUp`, and sets `Time.timeScale = 0`. It saves to `PlayerPrefs` on quit and focus-lost.

`ScoreTrailService` is a plain C# `IStartable` + `IDisposable` that follows the same pattern as `NudgeService`. It subscribes to `BalloonScoredMessage` and spawns pooled `ScorePointTrail` orbs — one per point earned — from the balloon's world position to a per-color target. Each trail is identified by its `int score` value derived from the message's `CurrentProgress`. Multi-point scores produce staggered trails fanning out in a circle, delayed by `ScorePointsScatterDelay`. The `TrackTrail` API allows external systems (e.g. `LevelUpTrailEffect`) to intercept specific trails at spawn time. Trail spawning is gated behind `Cinematic.IsPlaying` so no new trails appear during cinematics. Each `ColorProgressBar` registers a `Func<Vector3>` target provider (returning a random world-space point within the bar's rect) and receives `ScoreTrailArrivedMessage` (carrying the trail's color, score, and world-space landing position) on arrival for animator feedback and point notice placement.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid after spawn
- **NudgeService** — handles all balloon nudge animations
- **ScoreTrailService** — manages score trail orb spawning, flight, and arrival notification
- **Cinematics** — cinematic director and level-up trail effect (see `Cinematics/README.md`)
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **GameLifetimeScope config** — `GameConfiguration`, `BalloonsConfiguration`, `GamePalette`, `GameDisplayConfiguration`, `ItemConfiguration` all registered here as singletons; `ScorePointTrail` prefab registered as instance
