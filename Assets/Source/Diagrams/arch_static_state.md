@page arch_static_state Static State — Navigation & Cinematic

# Static State — Navigation & Cinematic

@image html static_state.svg "Static State — Navigation & Cinematic"

## What this diagram shows

Two static singleton classes that carry app-wide state without requiring DI wiring
across scene boundaries: `Navigation` and `Cinematic`.

**`Navigation`** holds a `ReactiveProperty<NavigationState>` and exposes
`TransitionTo(state)`. Any system can observe or transition the current state.
States: `Launch` → `Game` → `LevelUp` (→ `Game` on dismiss), and `Game` → `GameOver`
(`RunController.EndRun`) → `Game` on restart (`RunController.RestartRun`).

**`Cinematic`** holds a `ReactiveProperty<CinematicState>`. `Begin(state)` /
`End()` set the state; services that need to pause/resume during a cinematic
query `Cinematic.IsPlaying` or subscribe to `Cinematic.Current`.

**Why static and not injected?**
Both classes carry state that must survive scene transitions and be accessible from
both the Launcher scene and the Game scene simultaneously. A DI-registered singleton
lives inside a `LifetimeScope` which is tied to a scene — it cannot be shared across
additive scene boundaries without complex parent-scope wiring. Static state sidesteps
this cleanly; the trade-off (harder to mock in tests) is acceptable for these two
narrow, well-defined responsibilities.

## Guidance

**Observing navigation state:**
```csharp
Navigation.Current
    .Where(s => s == NavigationState.Game)
    .Subscribe(_ => OnGameStarted())
    .AddTo(_disposables);
```

**Reacting to cinematics in a service:**
```csharp
Cinematic.Current
    .Subscribe(state => { if (state == CinematicState.LevelUpPanIn) PauseWork(); })
    .AddTo(_disposables);

Cinematic.Current
    .Where(s => s == CinematicState.None)
    .Subscribe(_ => ResumeWork())
    .AddTo(_disposables);
```

**Do not add new static state** for feature-specific concerns — static state is
reserved for these two cross-scene coordination needs. Feature state belongs in
injected models registered in the appropriate `LifetimeScope`.

**`EditorNavigationBootstrap`** (editor-only): auto-transitions to `Game` when you
press Play directly in the game scene, bypassing the launcher. It checks that the
active scene is the game scene before transitioning — inert during additive preloading.

