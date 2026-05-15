# Slots

The grid that holds all balloons in play.

## Contents

| File | What it does |
|---|---|
| `SlotGrid` | Core data structure — parallel 2D arrays of `IWriteableBalloonModel` and `BalloonView`; `Place`, `Remove`, `At`, `ViewAt`, `IsEmpty`, `IsUnbalanced`, `OptimalNextEmptySlot`, `BottomEmptySlotPerColumn`, `HexNeighborIndices` (static), `GetNeighbors`, `IndexToWorldPosition`, `OnChanged` |
| `SlotGridChangedEvent` | Struct fired on every `Place` or `Remove` — carries the affected index and change type |
| `SlotGridView` | MonoBehaviour — draws gizmo spheres in `OnDrawGizmos` to visualise occupied/empty slots in the Editor |

## How it works

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by a balloon model and its view. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from below), and what the best empty slot is for a balloon to move into.

`OptimalNextEmptySlot` uses a recursive weight algorithm to decide between two candidate slots (directly above and diagonally above). The weight of a candidate = count of occupied slots in the tree above it. Higher weight = more support = preferred. On tie, the diagonal candidate wins (via `>=` comparison). This biases balloons toward the side of the grid with more balloons, creating natural clustering.

`Place` guards against double-occupation — if a slot is already occupied, the placement is rejected with an error log. This prevents silent overwrites where the first balloon would become orphaned from the grid but visually remain.

`BottomEmptySlotPerColumn` returns the lowest empty row index per column — used by `BalloonSpawner` when finding spawn targets for a new line.

## Interactions

- **BalloonSpawner** — calls `Place` for each new balloon
- **BalloonController** — calls `Remove` when a balloon is popped
- **BalloonBalancer** — reads occupancy to find gaps; calls `Remove` + `Place` to relocate balloons; uses `ViewAt` to reach views for animation
- **NudgeService** — uses `GetNeighbors` and `IndexToWorldPosition` to direct nudge animations
- **PaintItemHandler** — uses `HexNeighborIndices` and `IndexToWorldPosition` to compute all 6 neighbor flight targets; uses `IsEmpty` and `At` to resolve paintable balloons
- **IGameConfiguration** — provides `SlotsSize`, `SlotSeparation`, `SlotsOffset` for grid construction and position calculations
