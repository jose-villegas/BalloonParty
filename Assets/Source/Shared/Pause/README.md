# Pause

Broadcast architecture for pausing and resuming gameplay systems.

## Why not `Time.timeScale`?

`Time.timeScale` is a global scalar — every system must opt out individually (`SetUpdate(true)`, `ignoreTimeScale: true`, etc.). A missed annotation is a silent bug. It also conflates two distinct concerns: the **visual slow-motion** of a cinematic and the **logical freeze** of gameplay.

This package separates those concerns.

## Core types

| Type | Role |
|---|---|
| `PauseSource` | Enum identifying *why* something is paused (`Cinematic`, `LevelUp`, …) |
| `PausedMessage` | Published via MessagePipe when a source transitions from unpaused → paused |
| `ResumedMessage` | Published via MessagePipe when a source transitions from paused → unpaused |
| `PauseService` | Singleton coordinator. Reference-counted per source, drives the broadcast |
| `TimeScaleService` / `TimeScaleSource` | The only legal writer of `Time.timeScale` (audit-enforced): claim/release, lowest active claim wins, run-reset clears |

## Usage

### Signalling a pause

```csharp
// Inject PauseService, then:
_pauseService.Pause(PauseSource.Cinematic);   // begin
_pauseService.Resume(PauseSource.Cinematic);  // end
```

Calls nest safely — a second `Pause` without a matching `Resume` won't broadcast twice.

### Reacting to a pause

```csharp
// Inject ISubscriber<PausedMessage> and ISubscriber<ResumedMessage>, then:
_resumedSubscriber.Subscribe(msg =>
{
    if (msg.Source == PauseSource.Cinematic)
        ResumeWork();
}).AddTo(_disposable);
```

## `TimeScaleService` — the only writer of `Time.timeScale`

Direct `Time.timeScale` writes are **banned by a style-audit rule** (`timescale-writes`,
`[ERROR]`) everywhere except `TimeScaleService`. Callers claim a value under their
`TimeScaleSource` and release it when done:

```csharp
_timeScaleService.Claim(TimeScaleSource.LevelUpPopup, 0f);   // freeze while the popup shows
_timeScaleService.Release(TimeScaleSource.LevelUpPopup);     // falls back to the next claim, or 1
```

The **lowest active claim wins** (the popup's 0 beats a cinematic's slow-mo ramp) and no
claims means normal speed — so restores are automatic and the "who forgot to set it back
to 1" bug class is gone. `IRunResettable` clears all claims on restart.

Current claimants:
- **`CameraRigCinematic`** (`TimeScaleSource.Cinematic`) — per-tick slow-mo ramp of a
  timeScale-driving pan-in segment, and both restore forms (tween-from-current /
  curve-sampled). Note the level-up **pan-in** does not claim at all: its tipping trail is
  slowed via a curve-modulated progress rate, other trails fly at normal speed.
- **`LevelUpPopUp`** (`TimeScaleSource.LevelUpPopup`) — freezes balloon animators and
  particles at 0 while visible; releases on dismiss *after* publishing
  `LevelUpDismissedMessage`, so the restore cinematic's claim is already in place and the
  hand-back never flashes full speed.
- **`PierceDischargeEffects`** (`TimeScaleSource.PierceDischarge`, `Projectile/Controller/`) —
  a brief real-time dip when a piercing shot discharges the toughs it plowed through. A fresh
  discharge cancels and restarts the dip rather than layering; see
  `Projectile/README.md` § Pierce & Discharge Feel.

`PauseService` handles *logical* pause coordination (projectile, trail spawning);
`TimeScaleService` handles *visual* time warping. The two are independent.

