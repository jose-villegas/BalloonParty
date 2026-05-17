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

`LevelUpTrailEffect` produces two scenes per level-up:

### Scene 1 — Pan-In

| Step | What happens |
|---|---|
| **Trigger** | `BalloonScoredMessage` received, `ScoreController.WillLevelUp` returns true — checks projected progress for **all** colors so the cinematic registers even when multiple colors reach the threshold simultaneously |
| **Setup** | Builds a `TrailId(color, requiredPoints, level)` and registers it with `ScoreTrailService.TrackTrail`. No visual changes yet — slow-motion and camera effects start only when the tracked trail is actually spawned |
| **Spawn callback** | `ScoreTrailService` yields one frame then spawns trails sequentially. When the tipping trail spawns, it is paused immediately and `OnTippingTrailSpawned` fires. This begins the cinematic, calls `PauseTrailsAbove` to selectively pause only next-level in-flight trails, starts slow-motion + camera zoom, and resumes only the tipping trail (with unscaled time). Pre-tipping trails (any color) keep flying to their bars. New next-level trail spawns are gated behind `ids[i].Level > baseLevel`; current-level trails from other colors spawn freely so `CheckLevelUp` can confirm all colors |
| **Tick** | Camera pans toward the tipping trail's world position each frame |
| **End trigger** | `ScoreTrailArrivedMessage` matches tipping `TrailId`. `ScoreController` processes the arrival first — sets `_levelProgress` to the trail's score (which equals `requiredPoints`), triggering `CheckLevelUp` → `ScoreLevelUpMessage`. Then the effect completes the scene |
| **End callback** | No-op — the level-up popup appears independently from `ScoreLevelUpMessage` and pauses the game (`Time.timeScale = 0`) |

### Scene 2 — Restore

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` received (player pressed Continue) |
| **Prepare** | Tweens `Time.timeScale` from 0 back to 1 and camera back to base position/size (all using unscaled time) |
| **End trigger** | Restore tween completes (via `OnComplete` callback) |
| **End callback** | Re-enables `OrthogonalSizeCameraController`, calls `director.EndCinematic` (resumes post-tipping trails via `OnCinematicEnd`, unblocks trail spawning), resets `_sessionActive`, transitions navigation to `Game` |

### Trail Identity

Each score trail is identified by a `TrailId(Color, Score, Level)`. Color is required because `_projectedProgress` is per-color, so two colors can produce the same numeric score. Level prevents post-reset collisions when progress restarts from 1. The tipping trail's identity is `TrailId(tippingColor, requiredPoints, currentLevel)`.

### Selective Pause

Only next-level trails are paused during the cinematic — **any trail** (regardless of color) with `Level > tippingLevel`. This prevents next-level trails from arriving and overwriting current-level `_levelProgress` values in `ScoreController`. Pre-tipping trails from any color keep flying so their progress bar arrivals complete naturally. `LevelUpTrailEffect` calls `PauseTrailsAbove(tippingTrailId)` explicitly after `BeginCinematic`; the generic `OnCinematicBegin` callback is a no-op. `OnCinematicEnd` resumes only the cinematically-paused trails. The spawn gate in `SpawnTrailsAsync` only blocks next-level trails (`ids[i].Level > baseLevel`); current-level trails from all colors spawn freely so `CheckLevelUp` can confirm every color's progress.

### Tipping Trail Lifecycle

1. `LevelUpTrailEffect` builds `TrailId(color, requiredPoints, level)` and calls `TrackTrail(id, callback)` on `ScoreTrailService`
2. `SpawnTrailsAsync` yields one frame first, so `TrackTrail` is always registered before any spawn occurs
3. When the matching trail spawns, it is paused and the callback fires; if the trail was already in-flight (edge case), the callback fires immediately
4. The callback starts the cinematic, selectively pauses post-tipping trails, and resumes the tracked trail with unscaled time
5. Pre-tipping trails keep flying; post-tipping spawns are gated by `Cinematic.IsPlaying`
6. On trail arrival, `ClearTrackedTrail` resets the tracking state


## Interactions

- **`ScoreController`** — queried for `WillLevelUp` (all-projected check) and `GetRequiredPoints`; on tipping trail arrival, sets `_levelProgress` to the trail's score (= `requiredPoints`), which triggers `CheckLevelUp` → level-up
- **`ScoreTrailService`** — owns `TrackTrail`/`ClearTrackedTrail`/`PauseTrailsAbove` API for the producer; spawns trails with `useUnscaledTime` for the tracked trail; resumes cinematically-paused trails on `OnCinematicEnd`; gates `SpawnTrailsAsync` on `Cinematic.IsPlaying`
- **`Cinematic`** (static, `Shared/GameState/`) — the director calls `Begin`/`End` to broadcast state changes to all `ICinematicAware` services
- **`LevelUpPopUp`** — subscribes to `ScoreLevelUpMessage` independently; pauses the game (`Time.timeScale = 0`) on appear; publishes `LevelUpDismissedMessage` on Continue
- **`OrthogonalSizeCameraController`** — disabled during cinematic to prevent conflicting camera size changes
