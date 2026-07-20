# Game

The entry point that starts and runs the game.

## Contents

| File | What it does |
|---|---|
| `GameLifetimeScope` | VContainer composition root — registers all game services, entry points, MessagePipe brokers, configuration assets, and (in dev builds) cheats. `Awake()` pre-allocates DOTween capacity (`SetTweensCapacity(1000, 50)`) before building the container to avoid GC from array resizing during the initial balloon spawn burst |
| `LaunchLifetimeScope` | VContainer root for the Launcher scene — registers `GameDisplayConfiguration` and `OrthogonalSizeCameraController` for the launch camera |
| `LaunchDisturbanceStamp` | `ITickable`, registered in `GameScopeRegistration` — lets the player poke the shared disturbance field with a finger on the launch screen while the game pre-warms, using the same stamp the projectile wake uses. Only active while `NavigationState.Launch` is current |
| `LaunchPlayTrigger` | Launch screen's Play button `MonoBehaviour` (wired in the Launcher scene in place of the plain `NavigationTrigger`). Starts the `LaunchAscend` cloud scroll, waits for it to finish, then transitions to `NavigationState.Game` — so the game doesn't appear mid-scroll |
| `HitPipeline` | `IHitDispatcher` — the single entry point for actor hits; runs the order-dependent stages before broadcasting `ActorHitMessage` (see Architecture) |
| `Score/` | Score tracking, level progress, streaks, and trail orb management — see `Score/README.md` |
| `Run/` | Run lifecycle — end-of-run commit, restart reset ordering, best-run meta — see `Run/README.md` |
| `Health/` | Player hit-point pool and loss forecast — see `Health/README.md` |
| `Danger/` | Space-pressure early-warning signal — see `Danger/README.md` |
| `Cinematics/` | Cinematic director, camera rig, and the level-up / heart-drain producers — see `Cinematics/README.md` |

## Architecture

`GameLifetimeScope` is the sole composition root. All systems — spawner, balancer, nudge, thrower, score, score trails, cinematics, items — are wired here and inherit into child scopes automatically.

Scene-placed child scopes (`ScoreUILifetimeScope`, `LevelUpLifetimeScope`, `ShieldUILifetimeScope`, `HealthUILifetimeScope`, `DangerUILifetimeScope`, `GameOverLifetimeScope`, `ThrowerLifetimeScope`) extend `LifetimeScope` directly. Parent scope is wired via VContainer's `parentReference` field in the Inspector.

**Pooled prefabs (balloons, projectiles)** do not use child scopes. Their `[Inject]` fields are populated via `InjectingPoolChannel`, which injects from the parent container — skipping container creation entirely.

`HitPipeline` (implementing `IHitDispatcher` from `Shared/Messages`) is the single entry point for actor hits. Producers — `ProjectileHitResolver`, the item handlers, cheats — never publish `ActorHitMessage` directly; they call `Dispatch`, which runs the order-dependent stages explicitly (`ScoreController` streak/score recording, then the owning balloon's reaction via `BalloonControllerRegistry`) and only then broadcasts the message for order-independent observers. This replaces two implicit contracts: the streak-shield rule's read-after-publish (now structural) and per-balloon broadcast filtering (now a dictionary lookup).

`ScoreController` handles hits as `HitPipeline`'s first stage (invoked directly, not bus-subscribed) and subscribes to `ScoreTrailArrivedMessage`. On a qualifying hit (`Pop` or `PassThrough`), it casts the actor to `IHasScoreColor` and calls `ResolveScoreAttribution(msg.Context, attributions)` — the actor appends one `ScoreAttribution(colorId, points, breaksStreak)` per color bar it contributes to. `BalloonModel` appends a single entry for its own color when `HitsRemaining` reaches zero; `ToughBalloonModel` scatters score to random palette colors on pop; `UnbreakableBalloonModel` attributes score to the source (projectile) color on pop; `BubbleClusterModel` scatters one entry per remaining bubble to random palette colors with `BreaksStreak = true` (no streak bonus, streak resets). For each resolved attribution `ScoreController` publishes one `ScorePointsGroupMessage` carrying the group's total granted `Points` (post-multiplier, post-cap) and `LastScore` (the cumulative per-color number of the group's last point); `ScoreTrailService` fans those points out into individual trails. Score mutation is deferred to trail arrival: when `ScoreTrailArrivedMessage` is received, it adds `msg.Points` to per-color `_persistentScore` and `_totalScore`. When all colors' confirmed progress meets the level threshold — and the run is neither over nor doomed (`Navigation == Game`, `ILossForecast.LossImminent` false) — it increments the level, resets progress, and publishes `ScoreLevelUpMessage`. Score is run-scoped: no persistence beyond the best-run meta in `Run/`.

`ScoreTrailService` is a plain C# `IStartable` + `IDisposable`. It subscribes to `ScorePointsGroupMessage` and spawns one pooled `FlyingTrail` orb per point in the group. Each trail is keyed by `TrailId(Color, Score)`, its score walking `FirstScore..LastScore`. A single per-group task staggers the spawns, fanning them into a scatter ring. All in-flight trails are tracked via a `TrailFlightRegistry<TrailId>`, exposed as `Flights` for cinematic integration (e.g. `LevelUpCinematic` looks up the tipping trail). Each `ColorProgressBar` registers a `Func<Vector3>` target provider and receives `ScoreTrailArrivedMessage` on arrival for feedback.

## Interactions

- **BalloonSpawner** — spawns initial and subsequent balloon lines
- **BalloonBalancer** — rebalances the grid after spawn
- **NudgeService** — handles all balloon nudge animations
- **ScoreTrailService** — manages score trail orb spawning, flight, and arrival notification
- **Cinematics** — cinematic director and level-up trail effect (see `Cinematics/README.md`)
- **ThrowerController** — the player-facing launcher; reloads after each projectile death
- **SlotGrid** — the shared grid state all systems read and write
- **GameLifetimeScope config** — `GameConfiguration`, `BalloonsConfiguration`, `GridActorConfiguration`, `GamePalette`, `GameDisplayConfiguration`, `ItemConfiguration`, `OverflowSettings`, `CinematicsSettings`, `PuffCloudSettings`, `BushSettings`, `DisturbanceFieldSettings` all registered here as singletons; `FlyingTrail` prefab registered as instance
