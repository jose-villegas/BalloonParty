# Cinematics

Orchestrates cinematic sequences during gameplay — slow-motion, camera zoom/pan, trail tracking, and restore transitions.

## Architecture

The system follows a **producer → director → scene** pattern:

| Role | Type | Responsibility |
|---|---|---|
| **Director** (`CinematicDirector`) | Plain C# `ITickable` + `ILateTickable` | Runs the active scene's tick callbacks, manages `Cinematic.Begin`/`End` lifecycle across the full session |
| **Scene** (`CinematicScene`) | Plain C# (sealed) | Value object holding `OnBegin`, `OnTick`, `OnLateTick`, `OnEnd` callbacks that define a single phase |
| **Producer** (`LevelUpTrailEffect`) | `MonoBehaviour` | Detects level-up conditions, creates scenes, and signals the director when to start/complete them |

The director does not know about level-ups, trails, or cameras. It only knows how to play scenes and manage the `Cinematic` static state. Producers define all domain-specific behaviour via scene callbacks.

## Level-Up Trail Flow

`LevelUpTrailEffect` produces **two independent cinematics** per level-up, separated by the level-up popup. The popup is gated on the first cinematic ending — it waits for `CinematicState` to leave `LevelUpPanIn` before showing.

### Cinematic 1 — Pan-In (`CinematicState.LevelUpPanIn`)

| Step | What happens |
|---|---|
| **Trigger** | `ScorePointMessage` received, `ScoreController.WillLevelUp` returns true — checks projected progress for **all** colors so the cinematic registers even when multiple colors reach the threshold simultaneously |
| **Setup** | Builds `TrailId(color, score, level)` from the message and registers it with `ScoreTrailService.TrackTrail`. If the trail already spawned (subscription ordering — ScoreTrailService processes first), the retroactive path pauses it, switches its tweens to unscaled time, and fires the callback immediately |
| **Spawn callback** | `OnTippingTrailSpawned` calls `BeginCinematic(LevelUpPanIn)`, calls `PauseTrailsAbove` for next-level in-flight trails, starts slow-motion + camera zoom, and resumes the tipping trail. Pre-tipping trails (any color) keep flying |
| **Tick** | Camera pans toward the tipping trail's world position each frame. Trail scale is driven by `_trackedTrailScaleCurve` evaluated over unscaled time |
| **End trigger** | `ScoreTrailArrivedMessage` matches tipping `TrailId`. `ScoreController` processes the arrival first — triggers `CheckLevelUp` → `ScoreLevelUpMessage` + nav to `LevelUp`. Then the effect completes the scene |
| **End callback** | `CompleteScene()` then `EndCinematic()` — `CinematicState` returns to `None`. This opens the `CinematicEndGate(LevelUpPanIn)` gate that `LevelUpPopUp` is waiting on |

### Gate — Popup wait

`LevelUpLifetimeScope` registers a `CinematicEndGate(CinematicState.LevelUpPanIn)` as `IReadyGate`. `LevelUpPopUp` subscribes to `ScoreLevelUpMessage` and calls `WaitAsync` on this gate before showing. Since the gate is already open (pan-in ended in the same frame), the popup appears in the next frame. `Time.timeScale` is set to `0f` by the popup, freezing the game while it is shown.

### Cinematic 2 — Restore (`CinematicState.LevelUpRestore`)

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` received (player pressed Continue on the popup) |
| **Prepare** | `BeginCinematic(LevelUpRestore)`, then tweens `Time.timeScale` from `_restoreCurve.Evaluate(0)` (~0.3) back to 1 and camera back to base position/size — all via DOTween `SetUpdate(true)` (unscaled) so they advance while `Time.timeScale` is still near 0 |
| **End trigger** | Restore tween completes (via DOTween `OnComplete` → `director.CompleteScene()`) |
| **End callback** | `OnRestoreComplete` — re-enables `OrthogonalSizeCameraController`, calls `director.EndCinematic()` (resumes post-tipping trails via `OnCinematicEnd`, unblocks trail spawning), resets `_sessionActive`, transitions navigation to `Game` |

### Trail Identity

Each score trail is identified by a `TrailId(Color, Score, Level)`. Color is required because `_projectedProgress` is per-color, so two colors can produce the same numeric score. Level prevents post-reset collisions when progress restarts from 1. The tipping trail's identity is `TrailId(tippingColor, requiredPoints, currentLevel)`.

### Selective Pause

Only next-level trails are paused during the cinematic — **any trail** (regardless of color) with `Level > tippingLevel`. This prevents next-level trails from arriving and overwriting current-level `_levelProgress` values in `ScoreController`. Pre-tipping trails from any color keep flying so their progress bar arrivals complete naturally. `LevelUpTrailEffect` calls `PauseTrailsAbove(tippingTrailId)` explicitly after `BeginCinematic`; the generic `OnCinematicBegin` callback is a no-op. `OnCinematicEnd` resumes only the cinematically-paused trails. New next-level trail spawns are gated by the `NextLevel` flag on each `ScorePointMessage` — current-level trails from all colors spawn freely so `CheckLevelUp` can confirm every color's progress.

Between the two cinematics (`CinematicState = None`, popup is showing) `_sessionActive` remains `true`, so no new cinematic session can start until the restore completes.

### Tipping Trail Lifecycle

1. `LevelUpTrailEffect` receives the tipping `ScorePointMessage` and calls `TrackTrail(id, callback)` on `ScoreTrailService`
2. Due to subscription ordering (`IStartable` before `MonoBehaviour`), the trail typically spawns before `TrackTrail` is called. The retroactive path in `TrackTrail` pauses the trail, switches its tweens to unscaled time via `DOTween.TweensByTarget`, and fires the callback immediately
3. If the trail is delayed (scatter `groupIndex > 0`), the forward path applies — trail is paused at spawn with `useUnscaledTime` set from creation
4. The callback starts `BeginCinematic(LevelUpPanIn)`, selectively pauses post-tipping trails, and resumes the tracked trail
5. Pre-tipping trails keep flying; next-level spawns are gated by `Cinematic.IsPlaying && NextLevel`
6. On trail arrival, `ClearTrackedTrail` resets the tracking state

## Camera Reference

`LevelUpTrailEffect` holds the camera as a `[SerializeField]` — assigned in the Inspector. This avoids `Camera.main` (which is fragile on Android where the activity pause/resume cycle can destroy and recreate the main camera, leaving a stale destroyed-object reference that passes C# `?.` checks but throws on access).

## Interactions

- **`ScoreController`** — queried for `WillLevelUp` (all-projected check); on tipping trail arrival, sets `_levelProgress` to the trail's score (= `requiredPoints`), which triggers `CheckLevelUp` → level-up
- **`ScoreTrailService`** — owns `TrackTrail`/`ClearTrackedTrail`/`PauseTrailsAbove`/`ResumeTrail` API for the producer; retroactive `TrackTrail` switches tweens to unscaled time; resumes cinematically-paused trails on `OnCinematicEnd`; gates next-level spawns via `Cinematic.IsPlaying && NextLevel`
- **`Cinematic`** (static, `Shared/GameState/`) — the director calls `Begin`/`End` to broadcast state changes to all `ICinematicAware` services; transitions are `LevelUpPanIn → None → LevelUpRestore → None`
- **`LevelUpPopUp`** — subscribes to `ScoreLevelUpMessage` independently; awaits `IReadyGate` (backed by `CinematicEndGate(LevelUpPanIn)`) before showing; pauses the game (`Time.timeScale = 0`) on appear; publishes `LevelUpDismissedMessage` on Continue
- **`OrthogonalSizeCameraController`** — disabled during cinematics to prevent conflicting camera size changes; re-enabled in `OnRestoreComplete`
