# Slots

The grid that holds all balloons in play.

## Contents

| File | What it does |
|---|---|
| `SlotGrid` | Core data structure — 2D array of `BalloonModel` slots; `Place`, `Remove`, `IsEmpty`, `IsUnbalanced`, `OptimalNextEmptySlot`, `GetNeighbors`, `AllBalloonsStable` |
| `SlotGridChangedEvent` | UniRx `Subject` event fired on every `Place` or `Remove` |
| `SlotGridController` | Initialises grid dimensions and spawns initial balloon layout via injected factory |
| `SlotGridView` | MonoBehaviour subscribing to `SlotGrid.OnChanged` to redraw debug gizmos in the Editor |

## How it works

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by a balloon. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from above), and what the best empty slot is for a balloon to move into.

`AllBalloonsStable()` scans every occupied slot and returns true only when every balloon model's `IsStable` reactive property is true — used by `LevelUpPopUp` to defer appearing until the grid has settled, and by `ThrowerController` to gate firing.

## Interactions

- **BalloonSpawner** — calls `Place` for each new balloon
- **BalloonController** — calls `Remove` when a balloon is destroyed
- **BalloonBalancer** — reads slot occupancy to find gaps and optimal destinations
- **LevelUpPopUp** — polls `AllBalloonsStable()` before revealing the ceremony
- **ThrowerController** — polls `AllBalloonsStable()` to gate firing
