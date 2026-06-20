# Health

The player's **hit-point pool** — the only loss trigger under the spawn-saturation model. When
the board is so choked that an incoming balloon can't spawn, that balloon is rejected and costs
one hit point; reaching zero ends the run.

## Contents

| File | What it does |
|---|---|
| `PlayerHealthController` | Plain C# entry point (`IStartable`, `IRunResettable`, `IDisposable`). Holds `ReactiveProperty<int> Current`, initialised and reset to `IGameConfiguration.StartingHitPoints` (clamped to a hard internal cap of 999 — never displayed). Subscribes to `SpawnBlockedMessage`; each message spends one point. When `Current` crosses to zero it publishes `EndRunRequestedMessage` exactly once (`RunController` routes it to `EndRun`) — the local zero-guard plus the `GameOver` state gate prevent a second blocked spawn from ending the run again. It publishes rather than calling `RunController` directly: as an `IRunResettable` it sits in the collection `RunController` resolves, so a direct dependency would be a DI cycle |

## How it works

```
BalloonSpawner (column saturated) ──SpawnBlockedMessage──► PlayerHealthController.Damage(1)
                                                                  │  Current == 0
                                                                  ▼
                                                       EndRunRequestedMessage
                                                                  │
                                                                  ▼
                                                       RunController.EndRun()
```

A blocked spawn is known **synchronously**: `BalloonBalancer.Balance()` updates the grid model
before any view tween, so when `BalloonSpawner.SpawnLineInternal` finds `FindFirstReachableEmptyRow`
returns `null` for a column, that column genuinely can't accept a balloon. The spawner shows the
would-be balloon popping at the entry line and publishes `SpawnBlockedMessage` **at the pop**, so
the HP drain syncs with the visual. Popping real balloons to free space is what stops the bleed —
the core tension.

`Current` exposes only the live count; the UI (`UI/Health/HealthCounterLabel`) shows it as a numeric
label with no fixed maximum, mirroring `ShieldCounterLabel`. The label is **not** self-injected —
`HealthUILifetimeScope` (a child scope on the health UI hierarchy, like `ShieldUILifetimeScope`)
gathers the labels and registers `HealthLabelBinder`, which binds them to `Current` at `Start`. This
avoids the injection-timing trap where the parent scope (`[DefaultExecutionOrder(-5001)]`) injects a
MonoBehaviour before its own `Awake` has resolved the TMP component. HP resets to full on restart via
`IRunResettable` at the `Counters` stage, before the board is repopulated.

## Registration

`GameLifetimeScope`: `RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>()`.
The reject feedback (camera shake, pop VFX) lives in `Display/CameraShakeService` and the spawner;
this controller owns only the HP state and the loss trigger.
