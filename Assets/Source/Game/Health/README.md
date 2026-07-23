# Health

The player's **hit-point pool** — the only loss trigger under the spawn-saturation model. When
the board is so choked that an incoming balloon can't spawn, that balloon is rejected and costs
one hit point; reaching zero ends the run.

## Contents

| File | What it does |
|---|---|
| `PlayerHealthController` | Plain C# entry point (`IStartable`, `IRunResettable`, `IDisposable`). Holds `ReactiveProperty<int> Current`, initialised and reset to `IRunConfig.StartingHitPoints` (clamped to a hard internal cap of 999 — never displayed). Subscribes to `SpawnBlockedMessage`; each message spends one point. Also refills to full on every `ScoreLevelUpMessage` — the level-up ceremony resets HP as a clean slate, same clamp as the run-reset refill below. When `Current` crosses to zero it publishes `EndRunRequestedMessage` exactly once (`RunController` routes it to `EndRun`) — the local zero-guard plus the `GameOver` state gate prevent a second blocked spawn from ending the run again. It publishes rather than calling `RunController` directly: as an `IRunResettable` it sits in the collection `RunController` resolves, so a direct dependency would be a DI cycle |
| `IPlayerHealth` | Read-only seam (`Current`) — what UI binders and `SpaceDanger`/`LossForecast` inject instead of the concrete controller |
| `IPendingHealthCharges` / `ILossForecast` + `LossForecast` | The loss forecast — see below |
| `HeartTrailTracker` | The heart trails currently in flight (health UI → overflow pop), launch order preserved. `HeartTrailController` (`UI/Health/`) adds/removes them; the heart-drain cinematic frames the hearts in this set. Lives in the parent scope so both can reach it; cleared on run reset |

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
before any view tween, so when `BalloonPlacementResolver` finds no reachable slot for a column,
that column genuinely can't accept a balloon. Before costing HP the resolver looks past the column:
it re-homes the balloon into the nearest other column that can still take it, and failing that asks
`BalloonBalancer.TryRelievePressure` to **pressure-balance** — shove stable balloons aside to open
the column. Only when the whole board is out of
room does the would-be balloon join the overflow pile below the grid (`RejectedBalloonEffect`).
`SpawnBlockedMessage` is published **when that balloon's heart launches** from the health UI
(alongside `OverflowHeartRequestedMessage`), so the HP drain syncs with the heart leaving the bar;
the balloon pops when the heart lands. Popping real balloons to free space is what ultimately stops
the bleed — the core tension.

`Current` exposes only the live count; the UI (`UI/Health/HealthCounterLabel`) shows it as a numeric
label with no fixed maximum, mirroring `ShieldCounterLabel`. The label is **not** self-injected —
`HealthUILifetimeScope` (a child scope on the health UI hierarchy) gathers the labels via the shared
`RegisterBoundViews` helper (`UI/Binding/`) and binds them to `IPlayerHealth.Current` at `Start`. This
avoids the injection-timing trap where the parent scope (`[DefaultExecutionOrder(-5001)]`) injects a
MonoBehaviour before its own `Awake` has resolved the TMP component. HP resets to full on restart via
`IRunResettable` at the `Counters` stage, before the board is repopulated.

## Registration

`GameLifetimeScope`: `RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>().As<IPlayerHealth>()`.
The reject feedback (camera shake, pop VFX) lives in `Display/CameraShakeService` and the spawner;
this controller owns only the HP state and the loss trigger.

## Loss forecast

`IPendingHealthCharges` (implemented by `RejectedBalloonEffect`: queued, unlaunched overflow balloons — each will unconditionally cost one HP at its heart's launch) + `ILossForecast`/`LossForecast` (`PendingCharges >= Current`): the loss is knowable at reject-queue time, seconds before the Nth heart launch commits it. The level-up ceremony gates on this (no level-up after a lost run); the loss commit keeps its late timing so the heart-drain presentation plays.
