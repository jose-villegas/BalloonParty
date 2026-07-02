@page arch_cinematics_architecture Cinematics Architecture

# Cinematics Architecture

@image html cinematics_architecture.svg "Cinematics pipeline and its interplay with other systems"

## What this diagram shows

The full cinematics pipeline after the 2026-07 restructure (see
`Plans/PLAN-CinematicsArchitecture.md` for the design history): configuration, producers,
the shared runner, the services it drives, and every other system a cinematic touches.

**One setup point — `CinematicsSettings` (SO):**
Every `CinematicState` declares one `CinematicStateEntry` in an enum-indexed collection:
its behavioural `CinematicTraits`, the uniform **camera-rig segment** it plays
(`TimeScaleCurve` — whose last key is the segment's duration — plus zoom / pan weight /
follow speed), and capability blocks (`TrackedTrailSettings`). A *restore* is not
special-cased — it is just another segment whose curve ramps timeScale back to 1 with
zoom/pan at 0. States are phases: the level-up spans `LevelUpPanIn` + `LevelUpRestore`,
the heart-drain `HeartDrain` + `HeartDrainRestore`.

**Producers are thin triggers:**
`LevelUpCinematic` and `HeartDrainCinematic` are plain C# `IStartable` entry points — a
trigger message, a focus, an end condition, and a `CameraRigCinematicConfig` handed to the
runner. The level-up runs the **split-phase** form (`TryBegin` … `EndPanIn` … popup gate …
`TryBeginRestore`), leaves timeScale alone during pan-in (gameplay is paused; the segment
curve modulates the tipping trail's playback speed instead) and restores by *sampling* its
curve out of the popup's frozen 0. The heart-drain runs the **continuous** form: a polled
end condition ("pile drained ∨ game over") rolls the pan-in straight into a restore that
tweens *from the current* timeScale, so an early game-over never snaps speed down first.

**The runner owns the mechanics:**
`CameraRigCinematic` is the reusable shape — begin/end pairing through
`CinematicDirector.TryBeginCinematic` (the drop-while-busy concurrency policy lives in the
director, not in producers), per-tick framing through the single DI
`CinematicCameraRig` (fed its `Camera` by the thin `CinematicCameraView`), timeScale
through `TimeScaleService`, and `Abort()` teardown so producers carry no repair code.

**Traits, not flags:**
Consumers never enumerate states. `ICinematicState.Has(trait)` reads the current state's
declared traits from the SO: `RunController` gates `EndRun` on `BlocksLoss` (level-up
blocks; the heart-drain lets the 0-HP game-over fire through — it *is* the loss
happening), `CameraShakeService` stands down on `BlocksShake` (its additive-offset shake
composes with the heart-drain pan, so it punches per heart launch), and
`CinematicEndGate` holds the popup until `LevelUpPanIn` ends.

**Time is claim-based:**
`TimeScaleService` is the only legal writer of `Time.timeScale` — enforced by the
`timescale-writes` style-audit rule. The lowest active claim wins: the popup's
`LevelUpPopup = 0` freeze beats the cinematic's ramp, and the popup releases its claim
*after* publishing `LevelUpDismissedMessage`, so the restore's claim is already active
and the hand-back never flashes full speed.

## Guidance

**Adding a cinematic** (e.g. the planned game-over loss beat):
1. Add its state(s) to `CinematicState` — append, to preserve serialized indices.
2. Declare each state's entry in `CinematicsSettings` (initializers are the canonical
   defaults; `CinematicsSettingsTests` fails CI on a missing declaration).
3. Write a plain C# producer: inject `CinematicDirector`, `CinematicCameraRig`,
   `TimeScaleService`, `ICinematicsSettings`; build a `CameraRigCinematicConfig` with a
   trigger, a focus (`PointFocus` / `HeartTrailFocus` / new `ICinematicFocus`) and either
   an end condition (continuous) or split-phase calls. Register as an entry point.
4. Do **not** write a MonoBehaviour, own a camera reference, or touch `Time.timeScale` —
   the audit blocks the latter.

**Related pages:** @ref arch_score_cinematic (the level-up flow end-to-end),
@ref arch_static_state (the `Cinematic` reactive state), @ref arch_trail_composition
(trail interception during the pan-in).
