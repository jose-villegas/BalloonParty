# Nudge

Elastic push-out/return animations for actors — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `ActorHitMessage` (filtered by `IHasNudge`) and `NudgeMessage`; resolves per-actor and per-publisher overrides; dispatches nudge animations to any view implementing `INudgeable` |
| `INudgeable` | View-side contract — `Nudge(slotPosition, direction, distance, duration, onComplete)`. Decouples `NudgeService` from `BalloonView` specifically. |
| `NudgeType` | `[Flags]` enum — `Deflect`, `Neighbor`, `Shockwave`; controls which override entries apply |
| `NudgeOverride` | Serializable per-source struct: `AppliesTo` (flag mask), `Distance`, `Duration`, `Falloff` |
| `NudgeOverrideResolver` | Resolves distance, duration, and falloff through the 3-tier cascade (actor → publisher → config default). Injected into `NudgeService` |
| `NudgeMessage` | Pub/sub signal — carries target actor (null = shockwave), origin, source type, and optional publisher overrides. `Actor` is typed as `IHasNudge` so any nudgeable actor can be a target, not only balloons. |
| `Balloon/Controller/BalloonMotionTicker` | The motion driver behind `BalloonView.Nudge` — a central `ITickable` that walks a flat pooled entry list each frame and calls view setters, so a shockwave nudging dozens of balloons allocates nothing per balloon |
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

`BalloonView` owns stability during nudge. It holds a `_isNudging` flag. An actor is skipped if its model reports `IsStable = false` and `_isNudging` is not set — meaning some other system (spawning, balancing) caused the instability. If `_isNudging` is already set (mid-nudge interrupt), the nudge proceeds — `BalloonMotionTicker.StartNudge` silently replaces the view's running entry. On nudge start: `IsStable = false`, `_isNudging = true`. On nudge complete: `IsStable = true`, `_isNudging = false`.

Any actor whose view implements `INudgeable` can be nudged — the interface decouples `NudgeService` from any specific view type. `BalloonView` is the primary implementor today. Static grid actors with dedicated nudge animations can add `INudgeable` to their views when needed.

## Motion

`BalloonView.Nudge` does not tween — it forwards to `BalloonMotionTicker` (`Balloon/Controller/`), which owns the out-and-back motion state (ease-out to the offset, ease back to the slot) and applies positions through `IBalloonMotionView`. Because a ticker-driven lerp escapes `transform.DOKill()`, systems that take over a balloon's transform must cancel explicitly: `BalloonBalancer` calls `CancelNudge` before starting a balance move, and `BalloonView.OnDespawned` calls it before the view returns to the pool.

## Interactions

- **ActorHitMessage** — triggers automatic neighbor nudges for every hit on an `IHasNudge` actor
- **NudgeMessage** — triggers single-actor (Deflect) or shockwave nudges from controllers and item handlers
- **INudgeable / BalloonView.Nudge()** — starts the push-out → return motion on any implementing view (driven by `BalloonMotionTicker` for balloons)
- **SlotGrid** — resolves neighboring actors and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` passed to the model via `BalloonModelConfig` during spawning
