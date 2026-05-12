# Configuration

All game data lives in the `GameConfiguration` ScriptableObject and is accessed exclusively through the `IGameConfiguration` interface (defined in `Shared/`).

## Contents

| File | What it provides |
|---|---|
| `GameConfiguration` | `ScriptableObject` implementing `IGameConfiguration` — the single serialized asset holding every tunable value in the game |
| `IGameConfiguration` *(in Shared/)* | Read-only interface that all systems inject; decouples consumers from the concrete SO |
| `BalloonColorConfiguration` | Serializable pair of color name + `Color`, used by the balloon color array |
| `IBalloonColorConfiguration` | Read-only interface for balloon color entries |
| `ItemType` | Enum listing all item types: `None`, `Shield`, `Bomb`, `Laser`, `Lightning` |
| `ItemSettings` | Per-item tuning data: check frequency, weight, max count, nudge values |
| `ItemConfiguration` | Serializable list of `ItemSettings` with indexer by `ItemType` |
| `GameDisplayConfiguration` | Aspect-ratio → orthographic-size lookup for camera sizing |
| `DisplayOption` | Single aspect-ratio / orthographic-size pair used by `GameDisplayConfiguration` |

## Design Rules

- **Never hardcode** values that exist in a configuration asset — always inject the relevant configuration object and read through it.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems.
- Each configuration asset is registered once as a singleton in `GameLifetimeScope` and injected wherever needed.
- New configuration fields are added to the appropriate asset type and its interface (if one exists). When a domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.
