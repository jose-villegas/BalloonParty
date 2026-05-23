# Nudge

Elastic push-out/return animations for actors — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `ActorHitMessage` (filtered by `IHasNudge`) and `NudgeMessage`; resolves per-actor and per-publisher overrides; dispatches nudge animations to any view implementing `INudgeable` |
| `INudgeable` | View-side contract — `Nudge(slotPosition, direction, distance, duration, onComplete)`. Decouples `NudgeService` from `BalloonView` specifically. |
| `NudgeType` | `[Flags]` enum — `Deflect`, `Neighbor`, `Shockwave`; controls which override entries apply |
| `NudgeOverride` | Serializable per-source struct: `AppliesTo` (flag mask), `Distance`, `Duration`, `Falloff` |
| `NudgeMessage` | Pub/sub signal — carries target actor (null = shockwave), origin, source type, and optional publisher overrides. `Actor` is typed as `IHasNudge` so any nudgeable actor can be a target, not only balloons. |
| `Editor/NudgeOverrideDrawer` | Extends `AutoFieldPropertyDrawer` (in `Source/Editor/`) — auto-draws `_distance` and `_duration`; pins `_appliesTo` above the auto section via `DrawPinnedFields` using `EnumFlagsField`; conditionally draws `_falloff` only when the Shockwave flag is set. Overrides `BuildFoldoutLabel` to append the current `NudgeType` value to each array element header (e.g. `Element 0  [Deflect]`) |

## Nudge types

| Type | Trigger | Target |
|---|---|---|
| `Neighbor` | Any `ActorHitMessage` where the hit actor implements `IHasNudge` | All 6 grid neighbors that also implement `IHasNudge` |
| `Deflect` | Projectile bounces off a tough or unbreakable balloon | The deflecting balloon itself via `NudgeMessage` |
| `Shockwave` | Item handler (e.g. Bomb) publishes `NudgeMessage(Source=Shockwave, Actor=null)` | All occupied grid slots, attenuated by distance |

## Override resolution

Distance, duration, and falloff are resolved in priority order for each affected actor:

1. **Per-actor override** — `NudgeOverride[]` from `IHasNudge.NudgeOverrides` on the actor's model, sourced from `BalloonPrefabEntry`
2. **Publisher override** — `NudgeOverride[]` carried in the `NudgeMessage` (set by item handlers)
3. **Global default** — `BalloonsConfiguration.NudgeDistance`, `NudgeDuration`, `NudgeFalloff`

Shockwave uses exponential distance falloff: `distance = baseDistance × exp(−falloff × d)`. A per-actor shockwave override skips the falloff entirely and uses its fixed `Distance`.

## Stability tracking

`BalloonView` owns stability during nudge. It holds a `_isNudging` flag. An actor is skipped if its model reports `IsStable = false` and `_isNudging` is not set — meaning some other system (spawning, balancing) caused the instability. If `_isNudging` is already set (mid-nudge interrupt), the nudge proceeds and kills the previous tween. On nudge start: `IsStable = false`, `_isNudging = true`. On nudge complete: `IsStable = true`, `_isNudging = false`.

Any actor whose view implements `INudgeable` can be nudged — the interface decouples `NudgeService` from any specific view type. `BalloonView` is the primary implementor today. Static grid actors with dedicated nudge animations can add `INudgeable` to their views when needed.

## Interactions

- **ActorHitMessage** — triggers automatic neighbor nudges for every hit on an `IHasNudge` actor
- **NudgeMessage** — triggers single-actor (Deflect) or shockwave nudges from controllers and item handlers
- **INudgeable / BalloonView.Nudge()** — executes the push-out → return DOTween sequence on any implementing view
- **SlotGrid** — resolves neighboring actors and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` assigned to the model by `BalloonController`
