# Game

The entry point that starts and runs the game.

## Contents

| File | What it does |
|---|---|
| `GameLifetimeScope` | VContainer composition root — registers all game services, entry points, MessagePipe brokers, configuration assets, and (in dev builds) cheats. `Awake()` pre-allocates DOTween capacity (`SetTweensCapacity(1000, 50)`) before building the container to avoid GC from array resizing during the initial balloon spawn burst |
| `LaunchLifetimeScope` | VContainer root for the Launcher scene — registers `GameDisplayConfiguration` and `OrthogonalSizeCameraController` for the launch camera |
| `Score/` | Score tracking, level progress, and trail orb management — see `Score/README.md` |
| `Cinematics/` | Cinematic director, scene definitions, and the level-up trail effect — see `Cinematics/README.md` |

## Architecture

`GameLifetimeScope` is the sole composition root. All systems — spawner, balancer, nudge, thrower, score, score trails, cinematics, items — are wired here and inherit into child scopes automatically.

Scene-placed child scopes (`ScoreUILifetimeScope`, `LevelUpLifetimeScope`, `ShieldUILifetimeScope`, `ThrowerLifetimeScope`) extend `LifetimeScope` directly. Parent scope is wired via VContainer's `parentReference` field in the Inspector.

**Pooled prefabs (balloons, projectiles)** no longer use child scopes. Their `[Inject]` fields are populated via `InjectingPoolChannel`, which calls `IObjectResolver.InjectGameObject()` from the parent container — skipping container creation entirely. `BalloonLifetimeScope`, `ProjectileLifetimeScope`, and `ItemViewScope` components remain on prefabs with `autoRun = false`.

`ScoreController` subscribes to `ActorHitMessage` and `ScoreTrailArrivedMessage`. On a qualifying hit (`Pop` or `PassThrough`), it casts the actor to `IHasScoreColor` and calls `ResolveScoreAttribution(msg.Context, attributions)` — the actor appends one `ScoreAttribution(colorId, points, breaksStreak)` per color bar it contributes to. `BalloonModel` appends a single entry for its own color when `HitsRemaining` reaches zero; `ToughBalloonModel` scatters score to random palette colors on pop; `UnbreakableBalloonModel` attributes score to the source (projectile) color on pop; `BubbleClusterModel` scatters one entry per point of damage to random palette colors with `BreaksStreak = true` (no streak bonus, streak resets). All attributions from one `ResolveScoreAttribution` call are published as one scatter group sharing a single `GroupSize` so trails fan out together. For each attribution `ScoreController` publishes one `ScorePointMessage` per individual point × streak multiplier, each carrying a pre-computed `Score` and `Level` — including next-level renumbering for points that exceed the level-up threshold. Score mutation is deferred to trail arrival: when `ScoreTrailArrivedMessage` is received, it increments per-color `_persistentScore` and `_totalScore`, then sets `_levelProgress` to the trail's score value. When all colors' confirmed progress meets the level threshold, it increments the level, resets progress, and publishes `ScoreLevelUpMessage`. Saves to `PlayerPrefs` on quit and focus-lost.

`ScoreTrailService` is a plain C# `IStartable` + `IDisposable`. It subscribes to `ScorePointMessage` and spawns one pooled `FlyingTrail` orb per message. Each trail is keyed by `TrailId(Color, Score, Level)` taken directly from the message. Multi-point pops produce staggered trails using `GroupIndex`/`GroupSize` for scatter positioning and delay. All in-flight trails are tracked via a `TrailFlightRegistry<TrailId>`, exposed as `Flights` for cinematic integration (e.g. `LevelUpCinematic` looks up the tipping trail). Each `ColorProgressBar` registers a `Func<Vector3>` target provider and receives `ScoreTrailArrivedMessage` on arrival for feedback.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid after spawn
- **NudgeService** — handles all balloon nudge animations
- **ScoreTrailService** — manages score trail orb spawning, flight, and arrival notification
- **Cinematics** — cinematic director and level-up trail effect (see `Cinematics/README.md`)
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **GameLifetimeScope config** — `GameConfiguration`, `BalloonsConfiguration`, `GridActorConfiguration`, `GamePalette`, `GameDisplayConfiguration`, `ItemConfiguration`, `DisturbanceFieldSettings` all registered here as singletons; `FlyingTrail` prefab registered as instance
