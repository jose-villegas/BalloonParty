# BalloonParty — Architecture Diagram

> Single-page map of systems, data flow, and scope hierarchy.
> For style rules see `Assets/Source/README.md`. For per-feature details see each folder's `README.md`.

---

## Scope Hierarchy

```
GameLifetimeScope (composition root, -5001)
│
├── ThrowerLifetimeScope          → ThrowerController, ThrowerView, PredictionTraceView
│
├── ScoreUILifetimeScope          → ColorProgressBar[], ScoreCounterLabel, LevelLabel
│
├── LevelUpLifetimeScope          → LevelUpPopUp
│
├── ShieldUILifetimeScope         → ShieldCounterLabel[], ShieldCounterAnimation,
│                                    ShieldTrailController (entry point)
│
└── (Pooled prefabs — no child scope, injected via InjectingPoolChannel)
     ├── BalloonView + BalloonController
     └── ProjectileView
```

---

## System Map

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        GameLifetimeScope                                 │
│                                                                         │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────────────┐   │
│  │ SlotGrid     │   │PoolManager   │   │ Configuration SOs        │   │
│  │ (shared      │   │(singleton)   │   │ IGameConfiguration       │   │
│  │  grid state) │   │              │   │ BalloonsConfiguration    │   │
│  └──────┬───────┘   └──────┬───────┘   │ GamePalette             │   │
│         │                   │           │ GameDisplayConfiguration │   │
│         │                   │           │ ItemConfiguration        │   │
│         │                   │           └──────────────────────────┘   │
│  ┌──────┴───────────────────┴──────────────────────────────────────┐   │
│  │                     Entry Points (IStartable)                    │   │
│  │                                                                  │   │
│  │  BalloonSpawner ─── BalloonBalancer ─── NudgeService            │   │
│  │  ThrowerController                                               │   │
│  │  ScoreController ─── ScoreTrailService                           │   │
│  │  CinematicDirector                                               │   │
│  │  ItemAssigner                                                    │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Message Flow

```
ActorHitMessage
  ProjectileView / ItemHandlers / Cheats
    → BalloonController     (pop/deflect logic — filters IBalloonModel)
    → ScoreController       (score computation, streak — filters IHasColor+IHasScore)
    → NudgeService          (visual knockback — filters IHasNudge)
    → ItemActivator         (item activation trigger — filters IBalloonModel)

ScorePointMessage  (one per point × streak)
  ScoreController
    → ScoreTrailService     (spawns FlyingTrail orbs)
    → ColorProgressBar      (streak notice display)
    → LevelUpTrailEffect    (cinematic detection)

ScoreTrailArrivedMessage
  ScoreTrailService
    → ScoreController       (confirm progress, check level-up)
    → ColorProgressBar      (bar fill + hit feedback)
    → LevelUpTrailEffect    (tipping trail arrival → end scene)

ScoreLevelUpMessage
  ScoreController
    → LevelUpPopUp          (Time.timeScale = 0, show popup)
    → ColorProgressBar      (reset bar)

LevelUpDismissedMessage
  LevelUpPopUp
    → LevelUpTrailEffect    (restore camera, resume trails)

ShieldGainedMessage
  ShieldItemHandler
    → ShieldTrailController (spawn shield trail orb)

ProjectileDestroyedMessage
  ProjectileView
    → ThrowerController     (reload)
    → BalloonSpawner        (spawn new lines)
```

---

## Score & Cinematic Pipeline

```
Balloon Pop
    │
    ▼
ScoreController.OnActorHit
    │  streak tracking (consecutive same-color multiplier)
    │  publishes N × ScorePointMessage (score × streak)
    │
    ▼
ScoreTrailService                    LevelUpTrailEffect
    │  spawns FlyingTrail orbs           │  detects WillLevelUp (all projected ≥ required)
    │  per-color TrailSpawner            │  calls Tracker.TrackTrail(tippingId)
    │  TrailTracker<TrailId>             │
    │         │                          │
    │         ▼                          ▼
    │  Trail in-flight ◄──── CinematicDirector.PlayScene(PanInTick)
    │         │                    slow-mo, zoom, camera pan
    │         ▼
    │  Trail arrives
    │         │
    ▼         ▼
ScoreController.OnTrailArrived
    │  confirms _levelProgress
    │  CheckLevelUp → ScoreLevelUpMessage
    │
    ▼
LevelUpPopUp (Time.timeScale = 0)
    │
    ▼ (player taps Continue)
LevelUpDismissedMessage
    │
    ▼
LevelUpTrailEffect.PrepareRestore
    │  tweens timeScale → 1, camera → base
    │  EndCinematic → resumes paused trails
```

---

## Trail Utility Composition

```
TrailSpawner (Shared/Pool/)
├── Owns: PoolManager reference, pool key, channel factory
├── Spawn(from, to, duration, color?, onArrived?)
└── SpawnUnscaled(...)

TrailTracker<TId> (Shared/Pool/)
├── Register / Unregister
├── TrackTrail (forward + retroactive)
├── PauseWhere / ResumeTrail / ResumeAll
└── IsTracked / GetTrailTransform / ClearTrackedTrail

ScoreTrailService
├── Composes: per-color TrailSpawner + TrailTracker<TrailId>
├── ICinematicAware (OnCinematicEnd → ResumeAll)
└── Exposes: Tracker property, PauseTrailsAbove(threshold)

ShieldTrailController
├── Composes: single TrailSpawner
└── Fire-and-forget (no tracking, no cinematic)
```

---

## Static State

```
Navigation (Shared/GameState/)
├── Current: ReactiveProperty<NavigationState>
├── States: Launch → Game → LevelUp
└── TransitionTo(state)

Cinematic (Shared/GameState/)
├── Current: ReactiveProperty<CinematicState>
├── States: None, LevelUpTrail
├── IsPlaying: bool
├── Begin(state) / End()
└── ICinematicAware listeners (Register/Unregister)
```


## Slot Actor Abstraction

`SlotGrid` owns two parallel 2D arrays — `IWriteableSlotActor[,]` (model side) and `ISlotActorView[,]` (view side). Every grid occupant is referred to through these interfaces rather than balloon-specific types.

```
ISlotActor (read-only)
├── SlotIndex: IReadOnlyReactiveProperty<Vector2Int>
├── IsStable:  IReadOnlyReactiveProperty<bool>
└── Kind:      SlotActorKind (Dynamic | Static)

IWriteableSlotActor : ISlotActor
├── SlotIndex: ReactiveProperty<Vector2Int>   (new, writable)
└── IsStable:  ReactiveProperty<bool>          (new, writable)

ISlotActorView
├── transform:   Transform
├── TweenTracker: TweenTracker
└── ActorKind:   SlotActorKind
```

**Capability interfaces** — optional traits subscribers cast for at their call site:

| Interface | Meaning | Implemented by |
|---|---|---|
| `IHasColor` | Read-only color | `IBalloonModel` (all balloon types) |
| `IHasWriteableColor` | Paintable — writable color | `BalloonModel` only (not `ToughBalloonModel`) |
| `IHasScore` | Awards score on pop | `IBalloonModel` (all balloon types) |
| `IHasNudge` | Participates in nudge system | `IBalloonModel` (all balloon types) |
| `IPassThrough` | Slot can be crossed by animation paths | `StaticActorModel` |

Subscribers that previously cast to `IBalloonModel` now cast to the narrowest capability interface needed (`IHasColor`, `IHasScore`, `IHasNudge`). `BalloonController` is the only subscriber that still casts to `IBalloonModel` — it needs `EvaluateHit()` which is balloon-specific. See `Slots/README.md` for full detail.

---

## Folder → Namespace Mapping

| Folder | Namespace | Layer |
|--------|-----------|-------|
| `Balloon/` | `BalloonParty.Balloon.{Model,View,Controller}` | MVC |
| `Projectile/` | `BalloonParty.Projectile.{Model,View}` | MVC |
| `Thrower/` | `BalloonParty.Thrower` | Controller + View |
| `Slots/` | `BalloonParty.Slots` | Model (grid state) |
| `Game/` | `BalloonParty.Game` | Composition root |
| `Game/Score/` | `BalloonParty.Game.Score` | Controller |
| `Game/Cinematics/` | `BalloonParty.Game.Cinematics` | Controller + View |
| `Item/` | `BalloonParty.Item` | Handlers + Views |
| `Nudge/` | `BalloonParty.Nudge` | Service |
| `UI/` | `BalloonParty.UI.*` | Views |
| `Shared/` | `BalloonParty.Shared.*` | Utilities |
| `Configuration/` | `BalloonParty.Configuration` | SO definitions |
| `Display/` | `BalloonParty.Display` | Camera + rendering |
| `Prediction/` | `BalloonParty.Prediction` | Trajectory math |
| `Cheats/` | `BalloonParty.Cheats` | Dev-only |

