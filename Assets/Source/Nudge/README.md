# Nudge

Elastic push-out/return animations for balloons — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `BalloonHitMessage` and `BalloonNudgeMessage`; resolves per-balloon and per-publisher overrides; dispatches nudge animations to `BalloonView` |
| `NudgeType` | `[Flags]` enum — `Deflect`, `Neighbor`, `Shockwave`; controls which override entries apply |
| `NudgeOverride` | Serializable per-source struct: `AppliesTo` (flag mask), `Distance`, `Duration`, `Falloff` |
| `BalloonNudgeMessage` | Pub/sub signal — carries target balloon (null = shockwave), origin, source type, and optional publisher overrides |
| `Editor/NudgeOverrideDrawer` | Custom property drawer for `NudgeOverride[]` arrays in the Inspector |

## Nudge types

| Type | Trigger | Target |
|---|---|---|
| `Neighbor` | Any `BalloonHitMessage` — automatic | All 6 grid neighbors of the hit slot |
| `Deflect` | Projectile bounces off a tough or unbreakable balloon | The deflecting balloon itself |
| `Shockwave` | Item handler (e.g. Bomb) publishes `BalloonNudgeMessage(Source=Shockwave, Balloon=null)` | All occupied grid slots, attenuated by distance |

## Override resolution

Distance, duration, and falloff are resolved in priority order for each affected balloon:

1. **Per-balloon override** — `NudgeOverride[]` on the balloon's model (`IBalloonModel.NudgeOverrides`), sourced from `BalloonPrefabEntry`
2. **Publisher override** — `NudgeOverride[]` carried in the `BalloonNudgeMessage` (set by item handlers)
3. **Global default** — `BalloonsConfiguration.NudgeDistance`, `NudgeDuration`, `NudgeFalloff`

Shockwave uses exponential distance falloff: `distance = baseDistance × exp(−falloff × d)`. A per-balloon shockwave override skips the falloff entirely and uses its fixed `Distance`.

## Stability tracking

`NudgeService` maintains `_nudging: HashSet<IBalloonModel>`. A balloon can be nudged if it is stable or already mid-nudge (allowing nudge-to-nudge interrupts). Balloons that are unstable for other reasons (spawning, balancing) are skipped to avoid conflicts. On nudge start `IsStable = false` and the model is added to `_nudging`; on nudge complete `IsStable = true` and it is removed.

## Interactions

- **BalloonHitMessage** — triggers automatic neighbor nudges for every hit
- **BalloonNudgeMessage** — triggers single-balloon (Deflect) or shockwave nudges from controllers and item handlers
- **BalloonView.Nudge()** — executes the push-out → return DOTween sequence on the view
- **SlotGrid** — resolves neighboring models and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` assigned to the model by `BalloonController`

