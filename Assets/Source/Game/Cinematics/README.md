# Cinematics

Orchestrates cinematic sequences during gameplay — trail slow-motion, camera zoom/pan, and restore transitions.

## Architecture

The system follows a **producer → director → scene** pattern:

| Role | Type | Responsibility |
|---|---|---|
| **Director** (`CinematicDirector`) | Plain C# `ITickable` + `ILateTickable` | Runs the active scene's tick callbacks, manages `Cinematic.Begin`/`End` lifecycle |
| **Scene** (`CinematicScene`) | Plain C# (sealed) | Value object holding `OnBegin`, `OnTick`, `OnLateTick`, `OnEnd` callbacks |
| **Runner** (`CameraRigCinematic`) | Plain C# | The reusable pan-in → restore shape; owns begin/end pairing and teardown |
| **Producer base** (`CameraRigCinematicProducer`) | Plain C# `abstract`, `IStartable`/`IDisposable` | Builds the runner from a subclass `BuildConfig()` on start, aborts it on dispose; subclasses add their trigger (`OnStart`) and teardown (`OnDispose`) |
| **Producer** (`LevelUpCinematic`, `HeartDrainCinematic`, `GameOverLossCinematic`) | Plain C# over the base | Detects its trigger, hands the runner a config (states, focus, end condition, hooks) |

The director does not know about level-ups, trails, or cameras. Producers define all domain-specific behaviour via scene callbacks. `LevelAscendCinematic` is a producer too but stands outside this base — it drives a transform descent, not the camera runner (see below).

## Level-Up Trail Flow

`LevelUpCinematic` produces **one** cinematic per level-up (the runner's split-phase form: `TryBegin` … `EndPanIn` … gate). The pan-in zooms the camera onto the tipping trail; `EndPanIn` ends the cinematic but leaves the camera zoomed. On dismiss, `LevelUpCinematic` only resumes and cleans up its session — the camera un-zoom is driven by the Ascent (`Game/Level/LevelTransitionController.cs`) via `CinematicCameraRig.RestoreTweened`, timed by the `LevelUpRestore` segment's own `TimeScaleCurve` duration — independent of the (concurrent) board-clear effect — not by this producer. `LevelController` owns the navigation return to `Game` once the ceremony resolves. The Ascent moves the incoming *scenario*, not the camera (see `LevelAscendCinematic.cs` below).

### Cinematic 1 — Pan-In (`CinematicState.LevelUpPanIn`)

| Step | What happens |
|---|---|
| **Trigger** | `ScorePointsGroupMessage` received, `WillLevelUp()` returns true (projected progress) |
| **Setup** | Builds `TrailId(color, score, level)`, waits for it to register in `TrailFlightRegistry` |
| **Begin** | `BeginCinematic(LevelUpPanIn)`, `Pause(Cinematic)` freezes the projectile (and thrower input). Tipping trail's move tween killed, scale tween paused, position/scale driven manually by `PanInTick` |
| **Tick** | `_slowDownCurve` modulates tipping trail speed (1.0 → 0.3). Other trails fly at normal `Time.timeScale` (unmodified). Camera pans toward tipping trail, clamped so the trail always stays within the orthographic frustum |
| **End trigger** | Tipping trail progress \f$\ge 1\f$ → `Complete()` fires `onArrived` → `ScoreTrailArrivedMessage` |
| **End** | `EndCinematic()` → gate opens → popup shows. **No `CompleteAll` here** — the surviving trails stay frozen through the popup (see *Pausing survivors through the ceremony* below) |

### Gate — Popup Wait + Glow Trails

`LevelUpLifetimeScope` registers `CinematicEndGate(CinematicState.LevelUpPanIn)` by concrete type; `LevelUpPopUp` injects it directly and waits until `Cinematic.Current != LevelUpPanIn`. When the gate opens, the popup claims `TimeScaleSource.LevelUpPopup = 0` via `TimeScaleService` to freeze balloons/particles.

After the appear animation finishes, `LevelUpPopUp` publishes `LevelUpGlowTrailsMessage` — each `ColorProgressBar` drains its slider in sync — then spawns decorative `FlyingTrail` orbs from each bar to the glow fill in unscaled time. When all glow trails arrive, the level label updates. No cinematic state is active during this phase.

### Dismiss — hand-off to the Ascent

| Step | What happens |
|---|---|
| **Trigger** | `LevelUpDismissedMessage` (player pressed Continue) |
| **`LevelUpCinematic.OnDismissed`** | `Resume(Cinematic)` and finalizes its own session state. It does **not** touch navigation or the camera — the nav return to `Game` is `LevelController`'s job after the transition completes, and the un-zoom is the Ascent's. `CinematicState.LevelUpRestore` is no longer played (kept in the enum/settings for serialized-index stability) |
| **`LevelTransitionController`** | Driven by `ILevelProgress.Phase → Transitioning` (the dismissal advances the phase; the Ascent watches the phase, not the message). Waits for `!Cinematic.IsPlaying` (safety net) then for overflow drain, un-zooms the camera (`RestoreTweened`, timed by the `LevelUpRestore` segment's own `TimeScaleCurve` duration — independent of the concurrent board effect), and runs the Ascent. Runs an injected `IBoardEffect` to clear the old balloons — bound to `BoardFloatAwayEffect`, so level-up balloons float away rather than pop. Both halves of the old level travel with the descent: statics ride via `ITransitionOutgoingContent`, and the old balloons are detached off the grid + reparented under the `ScenarioContentRoot` (`BalloonControllerRegistry.DetachOutgoing`) by the float effect's `Collect()` — hence the root-reset happens *before* `Collect()` so their reparent lands at the right offset. The level-up's **frozen score trails** are outgoing content too: `ScoreTrailService.HoldOutgoing` resolves them here (`CompleteAll`) so they disappear under the Ascent (see *Pausing survivors through the ceremony*) |

### Pause Integration

| System | Mechanism |
|---|---|
| Projectile | `PauseService.IsAnyPaused` in `FixedUpdate`/`OnTriggerEnter2D` |
| Trail spawning | Not gated — projectile is frozen so no new pops occur; scatter-delayed trails from the triggering pop complete before the popup |
| Balloon animators/particles | popup claims `TimeScaleSource.LevelUpPopup` = 0 via `TimeScaleService` |
| Popup UI | `AnimatorUpdateMode.UnscaledTime` + `ignoreTimeScale` delays |
| Score trails (non-tipping) | Fly at normal speed **during** the pan-in — their arrivals must land to confirm the level-up (`LevelController` needs every colour's trails to arrive before it flips to `Pending`). Frozen only once the ceremony is confirmed (see below) |

### Pausing survivors through the ceremony

The level-up used to **clear** every still-airborne trail with `CompleteAll()` at the end of the pan-in.
Now that big scores fly as drawn constellations, snapping them away read as harsh — so the survivors are
**paused through the popup and resolved as outgoing-level content**, exactly like the old board's balloons.

1. **Freeze at confirmation, not at pan-in start.** `LevelUpCinematic` subscribes to `ILevelProgress.Phase`
   and calls `Flights.PauseAll()` the moment it becomes `Pending` (the popup is up). This is deliberately
   *not* done at `BeginCinematic`: pausing everything up front would freeze the very trails whose arrivals
   *confirm* the level-up, and with `CompleteAll` gone from the pan-in end nothing else would force those
   arrivals — the popup would never fire (a soft-lock, worst for a small `DefaultScore` final pop where the
   tracked trail alone can't reach the colour's threshold). Letting them fly until `Pending` means every
   confirming arrival has already landed, so the freeze can only ever catch pure visual survivors. The
   `ShapeFormationTicker` freezes each frozen formation and inflates its ribbons; `TrailFlight.Pause` does the
   same for a plain `DefaultScore` orb (`FlyingTrail.FreezeRibbon`) so its tail doesn't decay behind the popup.
2. **Resolve at the transition seam.** `ScoreTrailService` implements `ITransitionOutgoingContent`; its
   `HoldOutgoing` (called by `LevelTransitionController` mid-Ascent, while the phase is still `Transitioning`)
   calls `Flights.CompleteAll()`. Each survivor's arrival fires — `ScoreController` banks its points (they were
   never banked while frozen, so total score stays correct) and the formations snap-fade out. The Ascent's
   board effect visually covers their exit, so they "safely disappear" there. Because this runs *before*
   `LevelTransitionCompletedMessage` returns the phase to `Playing`, the arrivals (carrying the finished
   level's numbering) land in a non-`Playing` phase and are ignored by `LevelController`/`ColorProgressBar` —
   they can't step the **new** level's progress, bar, or watermark.
3. **Abort/loss recover the controller too.** A loss committing mid-pan-in (`AbortSession`) still
   `CompleteAll()`s at once — that path never reaches a transition, so it must not leave anything frozen.
   It also publishes `LevelUpAbortedMessage`, letting `LevelController` reset `Phase` back to `Playing`
   and return navigation to `Game` while the level is still pending. Every session exit disposes the
   freeze subscription; abort/dismiss/dispose are all leak-audited.

### Trail Identity

`TrailId(Color, Score, Level)` — color needed because `_projectedProgress` is per-color; level prevents post-reset collisions when progress restarts.


## Loss Ceremony Flow

`GameOverLossCinematic` produces the loss beat, **split around the GameOver screen exactly like the
level-up** (pan-in → screen → dismiss → restore): a slow-mo push-in holds the camera *in* while the
screen is up, and the camera only pulls back once the player dismisses. The run itself restarts
**mid-transition** the moment dismiss triggers the scenery swap — not on the button press directly,
and not when the pull-back ends — while the pull-back and the board pop play out over the swap. It
listens for `GameOverMessage` (published by `RunController.EndRun`), then:

| Step | What happens |
|---|---|
| **Arm** | `GameOverPresentationGate.Arm()` closes the gate synchronously so the screen's reveal must wait |
| **Wait for the director** | The overflow-death path fires game-over *while the heart-drain is still winding down* (heart-drain doesn't block loss). The producer yields (bounded timeout) until `!Cinematic.IsPlaying`, so the loss beat cleanly takes the camera rather than being dropped by the runner's busy-director policy |
| **Pan-in** | `CameraRigCinematic.TryBegin` (`GameOverLoss`) + `Pause(Cinematic)`. Drives the `TimeScaleCurve` into slow-mo; a `PointFocus` at the board centre frames the push-in (author `PanWeight = 0` for a pure zoom) |
| **Hold + reveal** | After the pan-in's curve duration the producer calls `EndPanIn` (ends the cinematic but **leaves the camera pushed in** — no restore yet) and `Gate.Open()`. `GameOverScreen` (labels already populated on the message) was `await`-ing the gate; it now shows over the held frame. The game stays paused (`PauseSource.Cinematic`) throughout |
| **Dismiss → scenery swap + rise** | The Restart button hides the screen and publishes `GameOverDismissedMessage`. The producer detaches the lost level's balloons and scenery as outgoing content (`BoardPopWave.Collect`, `HoldOutgoingContent`) and calls `RunController.RestartRun(resetBoard: false)` **mid-transition** — resetting run state (score/level/health/counters) while leaving the board swap to this cinematic. It clears the old statics, spawns the new scenery below view, then rises it into place (`RiseScenarioAsync`, timed by `LevelAscend.RestartRiseCurve`) while the `BoardPopWave` pops the outgoing balloons and the new balloons spawn on a cue mid-rise — the reverse of the Ascent's descent, with a pop instead of a float |
| **Settle** | Once *both* the pop wave and the camera pull-back finish (`UniTask.WhenAll`) — the run has already restarted, above — the producer releases the outgoing content and resumes the pause. The pull-back's duration is `LevelAscend.RestartRiseCurve.Duration()`, falling back to the authored `GameOverLossRestore` curve length only when that rise curve is zero-length |

On any path where the beat can't run — director never frees, or no rig authored — the producer opens
the gate anyway (screen reveals bare) and, on dismiss, restarts immediately with no restore, so the
screen never soft-locks.

Unlike the level-up gate (a passive `CinematicEndGate` observing `Cinematic.Current`), the loss gate is
**producer-driven** (`GameOverPresentationGate`): the loss beat starts a few frames *after* the message
(it waits out the heart-drain), so a state-observing gate would read "not yet started" as "already done"
and reveal early. The explicit arm/open handshake closes that race.

## Camera Reference

`CinematicCameraView` (on the Main Camera) is the single scene holder of the camera the shared rig drives — serialized reference with a lazy `Camera.main` fallback (avoids `Camera.main` fragility on Android when wired).

## Files

| File | Role |
|---|---|
| `LevelUpCinematic.cs` | Level-up producer (plain C# `IStartable`): intercepts the tipping trail and puppets it along the pan-in curve (gameplay paused — timeScale untouched), ends on the popup gate. The pan-in zooms the camera in; on dismiss it only resumes and cleans up its own session — the camera un-zoom is the Ascent's job (`LevelTransitionController` calls `CinematicCameraRig.RestoreTweened`, timed by the `LevelUpRestore` segment's own curve), and `LevelController` owns the nav return to `Game`. **The loss wins**: the show is gated on `Navigation == Game` + `ILossForecast.LossImminent` at every commit point and aborts mid-pan-in if the loss becomes certain (queued overflow charges \f$\ge\f$ remaining HP), publishing `LevelUpAbortedMessage` so the pending ceremony can recover cleanly |
| `CameraRigCinematicProducer.cs` | Thin `abstract` base for the runner-driven producers (`IStartable`/`IDisposable`): holds the four runner deps + the `CameraRigCinematic` (exposed as `Runner`), builds it from the subclass's `BuildConfig()` on `Start`, and aborts it on `Dispose`. Subclasses override `OnStart` (subscribe to the trigger) and `OnDispose` (dispose subscriptions / release pauses). Kills the begin/abort boilerplate the three producers used to repeat. Named `Runner`, not `Cinematic`, to avoid colliding with the static `Cinematic` state class. `LevelAscendCinematic` doesn't extend it (no camera runner) |
| `CameraRigCinematic.cs` | **The reusable camera-rig cinematic runner** (plain C#): pan-in segment (per-tick timeScale drive + `Frame(focus, segment, dt)`) until an end condition, then a restore segment (timeScale eased to 1 from wherever it is + camera back to base). Owns begin/end pairing and teardown (`Abort`), guards concurrency via `CinematicDirector.TryBeginCinematic`. A producer = this + a trigger + a focus + an end condition. `LevelAscendCinematic` does **not** use this runner (see below) |
| `HeartDrainCinematic.cs` | Overflow heart-drain producer (plain C# `IStartable`): on the first `OverflowHeartRequestedMessage`, runs the runner with `HeartDrain`/`HeartDrainRestore`, a `HeartTrailFocus`, and "pile drained or run over" as the end condition. Neither loss-blocking (the 0-HP game-over still fires) nor shake-blocking (each heart launch punches the camera through the pan, unscaled so slow-mo can't stretch it) |
| `GameOverLossCinematic.cs` | Loss-ceremony producer (plain C# `IStartable`), split around the screen like level-up: on `GameOverMessage`, arms `GameOverPresentationGate`, waits out any in-flight cinematic (the heart-drain), then `TryBegin` the `GameOverLoss` pan-in + `Pause(Cinematic)`. After the pan-in's curve duration it `EndPanIn`s (camera stays pushed in) and opens the gate → screen shows. On `GameOverDismissedMessage` it detaches the lost level as outgoing content and calls `RunController.RestartRun(resetBoard: false)` **mid-transition** (before the swap plays), then runs the `GameOverLossRestore` camera pull-back **and** a `BoardPopWave` (shared with the Ascent) concurrently over the new scenery's rise, releasing the outgoing content once both finish (`WhenAll`). Opens the gate / restarts bare (full `RestartRun()`, immediate) on any path where the beat can't run, so the screen never soft-locks |
| `IBoardEffect.cs` | Contract for a board-wide clear effect (plain C#): `Collect()` (snapshot while populated) / `EstimateSeconds()` (reports the effect's own play duration) / `PlayAsync(ct)`. Two implementations today — `BoardPopWave` and `BoardFloatAwayEffect` — with more expected. `LevelTransitionController` injects the interface (a swappable level-clear strategy, bound to the float effect); `GameOverLossCinematic` injects the concrete `BoardPopWave`. Neither caller currently reads `EstimateSeconds()` to time anything else — both restores are timed off their own segment/rise curves instead |
| `BoardPopWave.cs` | Slow-mo board pop (`IBoardEffect`): snapshots balloons by anti-diagonal band and pops them from the far corners inward via the registry (scoreless — no `ActorHitMessage`), claiming `TimeScaleSource.LevelTransition` for the slow-mo. Now the **game-over** clear effect (the Ascent moved to the float-away); tuning is the `LevelAscend` pop settings |
| `BoardFloatAwayEffect.cs` | Float-away board clear (`IBoardEffect`), the **level-up** effect. `Collect()` graduates the old balloons via `BalloonControllerRegistry.DetachOutgoing` — unregistered, off the grid, views reparented under the `ScenarioContentRoot`. `PlayAsync` then rises each balloon on a curve (per-balloon randomized rise height) while it sways side-to-side on a sine and tilts into the sway — animating **local** position (so they ride the root's descent *and* float up), unscaled, no slow-mo — then `ReturnOutgoing` hands them all back to the pool. Tuned by `ICinematicsSettings.BoardFloatAway`. Because the balloons are unregistered up front, the descent's spawn-cue `ClearAll` no longer yanks them mid-float |
| `Game/Run/GameOverPresentationGate.cs` | DI singleton driving the loss reveal: starts closed, armed by the producer on game-over, opened when the pan-in hold ends. `GameOverScreen` awaits it before showing. Producer-driven rather than state-observing because the loss beat starts *after* its trigger message (it waits out the heart-drain). Implements `IReadyGate` for consistency with the other gates, but is injected by concrete type — the producer needs `Arm`/`Open`, and binding it `.As<IReadyGate>()` would collide with the scope's `NavigationReadyGate(Game)` |
| `LevelAscendCinematic.cs` | Level-transition producer (plain C#, not `IStartable` — constructed and driven directly by `Game/Level/LevelTransitionController.cs`): **no camera work at all** — descends the shared `ScenarioContentRoot` (`Slots/Actor/`) from an elevated start down to `Vector3.zero` via its own per-frame `UniTask` loop off a curve sample. Every piece of scenario static content (cluster views, slot markers) parents ITSELF under that root and renders relative to its transform (see `ClusterView`/`BushView` — local-space so a moved root moves the visuals), so the incoming scenario slides into place while the camera stays fixed. Earlier attempts drove `CinematicCameraRig` for a literal camera translate (abrupt jump, read poorly), then moved a transform without local-space rendering (nothing moved, since Puff/Bush draw in world space). Fires a caller-supplied callback once, partway through the descent, so the new level's balloons are already mid-spawn by the time the scenario settles. Reads its own `LevelAscendSettings` (`ICinematicsSettings.LevelAscend`) — not a camera-rig entry, since it's a transform-descent: `DescentCurve` VALUE = a 1→0 height fraction, `Height` = the root's starting height in world units, `BalloonSpawnCue` = balloon-spawn-cue fraction of the descent, `Speed` = descent-speed multiplier (\f$\text{real time} = \text{curve duration} / \text{Speed}\f$; 0 falls back to the curve's pace). The same `LevelAscendSettings` object also carries `PopSlowMoTimeScale`/`PopWaveBandSeconds`/`RestartRiseCurve`/`RestartBalloonCue`, read by `BoardPopWave` (now the **game-over**-only clear effect, per its row above) and the loss cinematic's own scenery rise — not by the Ascent itself, which uses the float-away effect instead |
| `CinematicDirector.cs` | Scene/state lifecycle (plain C#, Controller layer). `TryBeginCinematic` is the concurrency policy in one place (drop while busy); `BeginCinematic` switches state mid-cinematic (restore phases) |
| `CinematicCameraRig.cs` | The **one shared** cinematic camera driver (DI singleton): `PreparePanIn(segment)` / `Frame(focus, segment, dt)` / `PrepareRestore` (a runner segment step) / `RestoreTweened(duration)` (standalone tweened return that re-enables the ortho controller on completion — called by the Ascent/`LevelTransitionController` with the `LevelUpRestore` segment's own duration) / `Restore` (instant). Not used by `LevelAscendCinematic`, which moves the scenario root instead (see above) |
| `CinematicCameraView.cs` | Thin scene View holding the `Camera` the rig drives (lazy `Camera.main` fallback) — replaces the per-producer serialized camera refs and their per-scene prefab overrides |
| `ICinematicFocus.cs` + `PointFocus` / `HeartTrailFocus` | What the camera frames each tick: one live point (level-up tipping trail — hard-clamped in frustum) or the heart trails — **centred on the oldest heart** (the one about to land and pop) with the bounding box spanning all of them (pre-clamped so new far trails slide in; a plain centroid drifted up toward the UI with every launch and pushed the pops off frame) |
| `CinematicScene.cs` | Callback value object |
| `Shared/GameState/Cinematic.cs` | Static reactive state (`Current`, `IsPlaying`, `Begin`, `End`) |
| `Shared/GameState/CinematicState.cs` | Enum: `None`, `LevelUpPanIn`, `LevelUpRestore`, `HeartDrain`, `HeartDrainRestore`, `LevelAscend`, `GameOverLoss`, `GameOverLossRestore` — identity only; behaviour and tuning live in `CinematicsSettings`. Values are append-only (never reordered/removed) since `CinematicsSettings._states` is a hand-authored array indexed by ordinal — `LevelUpRestore` is no longer *played* as a live cinematic, but its segment's `TimeScaleCurve` duration is still read by the Ascent to time `RestoreTweened`'s un-zoom, so it stays in place and keeps its serialized index |
| `Shared/GameState/CinematicTraits.cs` | `[Flags]` behavioural traits (`BlocksLoss`, `BlocksShake`); consumers query `ICinematicState.Has(trait)`. Level-up states declare both; `HeartDrain` declares none, so the 0-HP game-over fires and the camera shake punches through while it plays; `GameOverLoss` should declare `BlocksShake` (it hard-owns the camera) but not `BlocksLoss` (the run is already over) |
| `Configuration/CinematicsSettings.cs` (SO + `ICinematicsSettings`) | **The single setup point for cinematics**: one `CinematicStateEntry` per `CinematicState` (`[EnumIndexed]` drawn), composing `Traits` + a **uniform camera-rig segment** (`CameraRigCinematicSettings`: `TimeScaleCurve` — whose last key is the segment duration — + zoom/pan/followSpeed) + capability blocks (`TrackedTrailSettings`). Restores are ordinary segments (curve ramps back to 1, zoom/pan 0), so there are no special restore fields; the heart-drain's restore is its own state (`HeartDrainRestore`). Plus a top-level `LevelAscendSettings` — the Ascent is a transform-descent, not a camera move, so it has its own fields instead of a rig entry. All values are authored in the asset; the serialized types carry no code defaults or constructors. `OnValidate` grows `_states` to match the enum. Producers keep only their scene `Camera` reference. Unmapped states throw |
| `Shared/GameState/CinematicEndGate.cs` | `IReadyGate` — waits for cinematic state change |
| `Shared/Pool/TrailFlight.cs` | Per-trail flight controller (Pause/Resume/Complete/Speed) |
| `Shared/Pool/TrailFlightRegistry.cs` | Registry with bulk operations (snapshot-safe `CompleteAll`) |
| `Shared/Pool/FlightPhase.cs` | Enum: `Idle`, `InFlight`, `Paused` |
| `Game/Score/ScoreTrailService.cs` | Trail spawning, flight registration |
| `Game/Score/ScoreController.cs` | Score tracking, `CheckLevelUp`, `WillLevelUp` |
| `UI/LevelUp/LevelUpPopUp.cs` | Popup display, dismiss, `Time.timeScale = 0`, glow trail spawning via `LevelUpGlowTrailsMessage` |
| `UI/LevelUp/LevelUpLifetimeScope.cs` | DI: registers `CinematicEndGate(LevelUpPanIn)` by concrete type (the popup injects it directly, not via `IReadyGate`) |
| `Shared/Messages/LevelUpGlowTrailsMessage.cs` | Signal carrying trail-per-bar count and stagger delay for bar draining |
| `Projectile/View/ProjectileView.cs` | Checks `IsAnyPaused` to freeze movement |
