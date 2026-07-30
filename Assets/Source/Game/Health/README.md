# Health

The player's **hit-point pool** — the only loss trigger under the line-based spawn-saturation
model. Each heart represents one full spawn line. When the board can't absorb all the lines in
a spawn wave, the player loses one heart per full line of deficit; partial-line shortfalls are
forgiven. Reaching zero hearts ends the run.

## Contents

| File | What it does |
|---|---|
| `PlayerHealthController` | Plain C# entry point (`IStartable`, `IRunResettable`, `IDisposable`). Holds `ReactiveProperty<int> Current`, initialised and reset to `IRunConfig.StartingHitPoints` (clamped to a hard internal cap of 999). Subscribes to `WaveDamageMessage`; each message spends `HeartsLost` points in one shot. Also refills to full on every `ScoreLevelUpMessage`. When `Current` crosses to zero it publishes `EndRunRequestedMessage` exactly once. |
| `IPlayerHealth` | Read-only seam (`Current`) — what UI binders and `SpaceDanger` inject instead of the concrete controller |
| `WaveDeficitCalculator` | Pure static function: `Calculate(availableSpace, neededSlots, rowLength) → WaveDeficit`. The core formula: `heartsLost = floor(max(0, needed - available) / rowLength)` |
| `WaveDeficit` | Readonly struct result: `HeartsLost` (full lines of deficit) and `UnspawnedSlots` (total shortfall) |
| `ILossForecast` / `LossForecast` | Loss is imminent when HP is already at zero (damage is immediate under the line model, no pending charges) |
| `HeartTrailTracker` | The heart trails currently in flight (health UI → grid area), launch order preserved. `HeartTrailController` (`UI/Health/`) adds/removes them; the heart-drain cinematic frames the hearts in this set |

## How it works

```
BalloonSpawner (wave start)
  ├─ CountEmptySlots()
  ├─ WaveDeficitCalculator.Calculate(available, spawnLines×columns, columns)
  │       → WaveDeficit { HeartsLost, UnspawnedSlots }
  │
  ├─ if HeartsLost > 0:
  │       publish WaveDamageMessage ──► PlayerHealthController.Damage(HeartsLost)
  │                                          │  Current == 0
  │                                          ▼
  │                                 EndRunRequestedMessage
  │                                          │
  │                                          ▼
  │                                 RunController.EndRun()
  │
  └─ Spawn only (spawnLines - HeartsLost) effective lines
         └─ blocked columns → RejectedBalloonEffect.Play() [visual only]
```

## Damage formula

- `deficit = neededSlots - availableSpace` (clamped ≥ 0)
- `heartsLost = deficit / rowLength` (integer division — only full lines count)
- Partial remainders (< one full line) are simply not spawned, costing no heart.

Example (6 columns, 4 spawn lines = 24 needed):
- 18 available → deficit 6 → 1 heart lost
- 21 available → deficit 3 → 0 hearts lost (partial line forgiven)
- 12 available → deficit 12 → 2 hearts lost

## Registration

`GameLifetimeScope`: `RegisterEntryPoint<PlayerHealthController>().AsSelf().As<IRunResettable>().As<IPlayerHealth>()`.
The deficit computation lives in `BalloonSpawner`; visual feedback (camera shake, heart trails,
cinematic) subscribes to `WaveDamageMessage` independently.
