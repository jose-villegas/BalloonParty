# Cinematics

Orchestrates cinematic sequences during gameplay — trail slow-motion, camera zoom/pan, and restore transitions.

## Architecture

The system follows a **producer → director → scene** pattern:

| Role | Type | Responsibility |
|---|---|---|
| **Director** (`CinematicDirector`) | Plain C# `ITickable` + `ILateTickable` | Runs the active scene's tick callbacks, manages `Cinematic.Begin`/`End` lifecycle |
| **Scene** (`CinematicScene`) | Plain C# (sealed) | Value object holding `OnBegin`, `OnTick`, `OnLateTick`, `OnEnd` callbacks |
| **Runner** (`CameraRigCinematic`) | Plain C# | The reusable pan-in → restore shape; owns begin/end pairing and teardown |
| **Producer** (`LevelUpCinematic`, `HeartDrainCinematic`) | Plain C# `IStartable` | Detects its trigger, hands the runner a config (states, focus, end condition, hooks) |

The director does not know about level-ups, trails, or cameras. Producers define all domain-specific behaviour via scene callbacks.

## Level-Up Trail Flow

`LevelUpCinematic` produces **two cinematics** per level-up, separated by the level-up popup (the runner's split-phase form: `TryBegin` … `EndPanIn` … gate … `TryBeginRestore`).

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
| **Prepare** | `TryBeginRestore()` → `BeginCinematic(LevelUpRestore)`, `Resume(Cinematic)`, `Time.timeScale` sampled from the restore segment's curve (popup's frozen 0 → 1), camera tweens back to base |
| **End** | `OnRestoreComplete` → `RestoreCamera()` enforces exact base values, `EndCinematic()`, `NavigationState.Game` |

### Pause Integration

| System | Mechanism |
|---|---|
| Projectile | `PauseService.IsAnyPaused` in `FixedUpdate`/`OnTriggerEnter2D` |
| Trail spawning | Not gated — projectile is frozen so no new pops occur; scatter-delayed trails from the triggering pop complete before the popup |
| Balloon animators/particles | popup claims `TimeScaleSource.LevelUpPopup` = 0 via `TimeScaleService` |
| Popup UI | `AnimatorUpdateMode.UnscaledTime` + `ignoreTimeScale` delays |
| Score trails (non-tipping) | Never paused — fly at normal speed during cinematic |

### Trail Identity

`TrailId(Color, Score, Level)` — color needed because `_projectedProgress` is per-color; level prevents post-reset collisions when progress restarts.


## Camera Reference

`CinematicCameraView` (on the Main Camera) is the single scene holder of the camera the shared rig drives — serialized reference with a lazy `Camera.main` fallback (avoids `Camera.main` fragility on Android when wired).

## Files

| File | Role |
|---|---|
| `LevelUpCinematic.cs` | Level-up producer (plain C# `IStartable`): intercepts the tipping trail and puppets it along the pan-in curve (gameplay paused — timeScale untouched), split phases around the popup gate, restore samples its curve from the popup's frozen 0. **The loss wins**: the show is gated on `Navigation == Game` + `ILossForecast.LossImminent` at every commit point and aborts mid-pan-in if the loss becomes certain (queued overflow charges ≥ remaining HP) |
| `CameraRigCinematic.cs` | **The reusable camera-rig cinematic runner** (plain C#): pan-in segment (per-tick timeScale drive + `Frame(focus, segment, dt)`) until an end condition, then a restore segment (timeScale eased to 1 from wherever it is + camera back to base). Owns begin/end pairing and teardown (`Abort`), guards concurrency via `CinematicDirector.TryBeginCinematic`. A producer = this + a trigger + a focus + an end condition |
| `HeartDrainCinematic.cs` | Overflow heart-drain producer (plain C# `IStartable`): on the first `OverflowHeartRequestedMessage`, runs the runner with `HeartDrain`/`HeartDrainRestore`, a `HeartTrailFocus`, and "pile drained or run over" as the end condition. Neither loss-blocking (the 0-HP game-over still fires) nor shake-blocking (each heart launch punches the camera through the pan, unscaled so slow-mo can't stretch it) |
| `CinematicDirector.cs` | Scene/state lifecycle (plain C#, Controller layer). `TryBeginCinematic` is the concurrency policy in one place (drop while busy); `BeginCinematic` switches state mid-cinematic (restore phases) |
| `CinematicCameraRig.cs` | The **one shared** cinematic camera driver (DI singleton): `PreparePanIn(segment)` / `Frame(focus, segment, dt)` / `PrepareRestore` / `Restore`. Tuning comes per call from the active state's segment; the focus supplies what to frame |
| `CinematicCameraView.cs` | Thin scene View holding the `Camera` the rig drives (lazy `Camera.main` fallback) — replaces the per-producer serialized camera refs and their per-scene prefab overrides |
| `ICinematicFocus.cs` + `PointFocus` / `HeartTrailFocus` | What the camera frames each tick: one live point (level-up tipping trail — hard-clamped in frustum) or the heart trails — **centred on the oldest heart** (the one about to land and pop) with the bounding box spanning all of them (pre-clamped so new far trails slide in; a plain centroid drifted up toward the UI with every launch and pushed the pops off frame) |
| `CinematicScene.cs` | Callback value object |
| `Shared/GameState/Cinematic.cs` | Static reactive state (`Current`, `IsPlaying`, `Begin`, `End`) |
| `Shared/GameState/CinematicState.cs` | Enum: `None`, `LevelUpPanIn`, `LevelUpRestore`, `HeartDrain`, `HeartDrainRestore` — identity only; behaviour and tuning live in `CinematicsSettings` |
| `Shared/GameState/CinematicTraits.cs` | `[Flags]` behavioural traits (`BlocksLoss`, `BlocksShake`); consumers query `ICinematicState.Has(trait)`. Level-up states declare both; `HeartDrain` declares none, so the 0-HP game-over fires and the camera shake punches through while it plays |
| `Configuration/CinematicsSettings.cs` (SO + `ICinematicsSettings`) | **The single setup point for cinematics**: one `CinematicStateEntry` per `CinematicState` (`[EnumIndexed]` drawn), composing `Traits` + a **uniform camera-rig segment** (`CameraRigCinematicSettings`: `TimeScaleCurve` — whose last key is the segment duration — + zoom/pan/followSpeed) + capability blocks (`TrackedTrailSettings`). Restores are ordinary segments (curve ramps back to 1, zoom/pan 0), so there are no special restore fields; the heart-drain's restore is its own state (`HeartDrainRestore`). Field initializers carry the authored values, so a fresh instance equals the shipped asset. Producers keep only their scene `Camera` reference. Unmapped states throw; `CinematicsSettingsTests` walks the enum so a missing declaration fails CI |
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
