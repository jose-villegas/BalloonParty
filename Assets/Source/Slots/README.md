# Slots

The grid that holds all balloons in play.

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by a balloon. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from above), and what the best empty slot is for a balloon to move into.

`SlotGridChangedEvent` is fired on every `Place` or `Remove` call via a UniRx `Subject`. `SlotGridView` subscribes to this to redraw debug gizmos in the Editor.

`AllBalloonsStable()` scans every occupied slot and returns true only when every balloon model's `IsStable` reactive property is true — used by `LevelUpPopUp` to defer appearing until the grid has settled after a level-up.

Other systems talk to the grid to place or remove balloons, to query neighbours for power-up propagation, and to find spawn positions for new balloon lines.

## Interactions

- **BalloonSpawner** — calls `Place` for each new balloon
- **BalloonController** — calls `Remove` when a balloon is destroyed
- **BalloonBalancer** — reads slot occupancy to find gaps and optimal destinations
- **LevelUpPopUp** — polls `AllBalloonsStable()` before revealing the ceremony
- **ThrowerController** — polls `AllBalloonsStable()` to gate firing
