# Configuration

All game data lives in the `GameConfiguration` ScriptableObject and is accessed exclusively through the `IGameConfiguration` interface (defined in `Shared/`).

## Contents

| File | What it provides |
|---|---|
| `GameConfiguration` | `ScriptableObject` implementing `IGameConfiguration` — the single serialized asset holding every tunable value in the game |
| `IGameConfiguration` *(in Shared/)* | Read-only interface that all systems inject; decouples consumers from the concrete SO |
| `BalloonColorConfiguration` | Serializable pair of color name + `Color`, used by the balloon color array |
| `IBalloonColorConfiguration` | Read-only interface for balloon color entries |
| `BalloonPowerUp` | Enum listing all power-up types: `None`, `Shield`, `Bomb`, `Laser`, `Lightning` |
| `PowerUpSettings` | Per-power-up tuning data: check frequency, weight, max count, nudge values |
| `PowerUpConfiguration` | Serializable list of `PowerUpSettings` with indexer by `BalloonPowerUp` type |
| `GameDisplayConfiguration` | Aspect-ratio → orthographic-size lookup for camera sizing |
| `DisplayOption` | Single aspect-ratio / orthographic-size pair used by `GameDisplayConfiguration` |

## Design Rules

- **Never hardcode** values that exist in `GameConfiguration` — always read through `IGameConfiguration`.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems.
- `IGameConfiguration` is registered once as a singleton in `GameLifetimeScope` and injected wherever needed.
- New configuration fields are added to both `IGameConfiguration` (interface) and `GameConfiguration` (implementation + serialized field).

