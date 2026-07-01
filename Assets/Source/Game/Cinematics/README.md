# Cinematics

Orchestrates cinematic sequences during gameplay — trail slow-motion, camera zoom/pan, and restore transitions.

## Architecture

The system follows a **producer → director → scene** pattern:

| Role | Type | Responsibility |
|---|---|---|
| **Director** (`CinematicDirector`) | Plain C# `ITickable` + `ILateTickable` | Runs the active scene's tick callbacks, manages `Cinematic.Begin`/`End` lifecycle |
| **Scene** (`CinematicScene`) | Plain C# (sealed) | Value object holding `OnBegin`, `OnTick`, `OnLateTick`, `OnEnd` callbacks |
| **Producer** (`LevelUpTrailEffect`) | `MonoBehaviour` | Detects level-up conditions, creates scenes, signals the director |

The director does not know about level-ups, trails, or cameras. Producers define all domain-specific behaviour via scene callbacks.

## Level-Up Trail Flow

`LevelUpTrailEffect` produces **two cinematics** per level-up, separated by the level-up popup.

### Cinematic 1 — Pan-In (`CinematicState.LevelUpPanIn`)

| Step | What happens |
|---|---|
| **Trigger** | `ScorePointMessage` received, `WillLevelUp()` returns true (projected progress) |
| **Setup** | Builds `TrailId(color, score, level)`, waits for it to register in `TrailFlightRegistry` |
| **Begin** | `BeginCinematic(LevelUpPanIn)`, `Pause(Cinematic)` freezes projectile + gates trail spawns. Tipping trail's move tween killed, scale tween paused, position/scale driven manually by `PanInTick` |
| **Tick** | `_slowDownCurve` modulates tipping trail speed (1.0 → 0.3). Other trails fly at normal `Time.timeScale` (unmodified). Camera pans toward tipping trail, clamped so the trail always stays within the orthographic frustum |
| **End trigger** | Tipping trail progress ≥ 1 → `Complete()` fires `onArrived` → `ScoreTrailArrivedMessage` |
| **End** | `CompleteAll()` finishes stragglers, `EndCinematic()` → gate opens → popup shows |

### Gate — Popup Wait + Glow Trails

`LevelUpLifetimeScope` registers `CinematicEndGate(CinematicState.LevelUpPanIn)` as `IReadyGate`. Popup waits until `Cinematic.Current != LevelUpPanIn`. When the gate opens, the popup sets `Time.timeScale = 0` to freeze balloons/particles.

After the appear animation finishes, `LevelUpPopUp` publishes `LevelUpGlowTrailsMessage` — each `ColorProgressBar` drains its slider in sync — then spawns decorative `FlyingTrail` orbs from each bar to the glow fill in unscaled time. When all glow trails arrive, the level label updates. No cinematic state is active during this phase.

### Cinematic 2 — Restore (`CinematicState.LevelUpRestore`)

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` (player pressed Continue) |
| **Prepare** | `BeginCinematic(LevelUpRestore)`, `Resume(Cinematic)`, ramp `Time.timeScale` via `_restoreCurve` (0.3 → 1.0), camera tweens back to base |
| **End** | `OnRestoreComplete` → `RestoreCamera()` enforces exact base values, `EndCinematic()`, `NavigationState.Game` |

### Pause Integration

| System | Mechanism |
|---|---|
| Projectile | `PauseService.IsAnyPaused` in `FixedUpdate`/`OnTriggerEnter2D` |
| Trail spawning | Not gated — projectile is frozen so no new pops occur; scatter-delayed trails from the triggering pop complete before the popup |
| Balloon animators/particles | `Time.timeScale = 0` when popup shows |
| Popup UI | `AnimatorUpdateMode.UnscaledTime` + `ignoreTimeScale` delays |
| Score trails (non-tipping) | Never paused — fly at normal speed during cinematic |

### Trail Identity

`TrailId(Color, Score, Level)` — color needed because `_projectedProgress` is per-color; level prevents post-reset collisions when progress restarts.


## Camera Reference

`LevelUpTrailEffect` holds the camera as `[SerializeField]` — avoids `Camera.main` fragility on Android.

## Files

| File | Role |
|---|---|
| `LevelUpTrailEffect.cs` | Level-up cinematic orchestrator (MonoBehaviour, View layer) |
| `HeartTrailCinematicEffect.cs` | Overflow heart-drain orchestrator (MonoBehaviour): on the first `OverflowHeartRequestedMessage` (the first heart launching toward the pile), slow-mo ramps and the camera follows the centroid of all in-flight heart trails (`HeartTrailTracker` + `CinematicCameraRig.FollowPoints`) until the pile drains or the run ends. Uses `CinematicState.HeartDrain` (non-loss-blocking, so the 0-HP game-over still fires) |
| `CinematicDirector.cs` | Scene/state lifecycle (plain C#, Controller layer) |
| `CinematicCameraRig.cs` | Cinematic camera (zoom/pan/restore). `FollowTrail` tracks one point; `FollowPoints` tracks the centroid + bounding-box of several |
| `CinematicScene.cs` | Callback value object |
| `Shared/GameState/Cinematic.cs` | Static reactive state + `ICinematicAware` listeners |
| `Shared/GameState/CinematicState.cs` | Enum: `None`, `LevelUpPanIn`, `LevelUpRestore`, `HeartDrain`. `ICinematicState.BlocksLoss` is true only for the level-up states — `HeartDrain` lets the 0-HP game-over fire while it plays |
| `Shared/GameState/CinematicEndGate.cs` | `IReadyGate` — waits for cinematic state change |
| `Shared/Pool/TrailFlight.cs` | Per-trail flight controller (Pause/Resume/Complete/Speed) |
| `Shared/Pool/TrailFlightRegistry.cs` | Registry with bulk operations (snapshot-safe `CompleteAll`) |
| `Shared/Pool/FlightPhase.cs` | Enum: `Idle`, `InFlight`, `Paused` |
| `Game/Score/ScoreTrailService.cs` | Trail spawning, flight registration |
| `Game/Score/ScoreController.cs` | Score tracking, `CheckLevelUp`, `WillLevelUp` |
| `UI/LevelUp/LevelUpPopUp.cs` | Popup display, dismiss, `Time.timeScale = 0`, glow trail spawning via `LevelUpGlowTrailsMessage` |
| `UI/LevelUp/LevelUpLifetimeScope.cs` | DI: registers `CinematicEndGate(LevelUpPanIn)` as `IReadyGate` |
| `Shared/Messages/LevelUpGlowTrailsMessage.cs` | Signal carrying trail-per-bar count and stagger delay for bar draining |
| `Projectile/View/ProjectileView.cs` | Checks `IsAnyPaused` to freeze movement |
