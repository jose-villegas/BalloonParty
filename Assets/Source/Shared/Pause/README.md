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
| `PauseResumedGate` | `IReadyGate` implementation that resolves reactively when a source resumes — no polling |

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

### Gating async work on a resume

Register `PauseResumedGate` as the `IReadyGate` for a scope:

```csharp
// In a LifetimeScope:
builder.Register<PauseResumedGate>(Lifetime.Singleton)
    .WithParameter("awaitedSource", PauseSource.Cinematic)
    .As<IReadyGate>();
```

Then any `IReadyGate`-aware async flow will block until that source resumes, resolving
**in the same frame** as `Resume()` — unlike `UniTask.WaitUntil` polling.

## `Time.timeScale` is still permitted for visual effects

The cinematic slow-motion tween (`Time.timeScale` → 0.3) in `LevelUpTrailEffect` is a
**purely visual** effect and remains as-is. `PauseService` handles *logical* pause
coordination; the two are independent.

