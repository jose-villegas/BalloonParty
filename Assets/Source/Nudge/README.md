# Nudge

Elastic push-out/return animations for actors — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `ActorHitMessage` (filtered by `IHasNudge`) and `NudgeMessage`; resolves per-actor and per-publisher overrides; dispatches nudge animations to any view implementing `INudgeable` |
| `INudgeable` | View-side contract — `Nudge(slotPosition, direction, distance, duration, source, onComplete)`. Decouples `NudgeService` from `BalloonView` specifically. |
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

`BalloonView` owns stability during nudge via two guards evaluated in order:

1. **Stability guard** — skips the nudge if `IsStable = false` and `_isNudging = false`. This means some other system (spawning, balancing) caused the instability and the nudge ticker should not compete.
2. **Tracker-playing guard** — skips the nudge if the balance `TweenTracker` is actively playing a DOPath. The ticker-driven lerp and DOTween would fight over `transform.position`.

Both guards are bypassed for `NudgeType.Deflect` — a direct impact always plays.

If `_isNudging` is already set (mid-nudge replacement), the stability guard passes and `BalloonMotionTicker.StartNudge` silently replaces the running entry. On nudge start: `IsStable = false`, `_isNudging = true`. On natural completion: `IsStable = true` (unless a deferred balance or paused tracker takes over), `_isNudging = false`. On cancellation (by `BalloonBalancer`): only `_isNudging = false` — `IsStable` stays false until the balance tween finishes.

Any actor whose view implements `INudgeable` can be nudged — the interface decouples `NudgeService` from any specific view type. `BalloonView` is the primary implementor today. Static grid actors with dedicated nudge animations can add `INudgeable` to their views when needed.

## Motion

`BalloonView.Nudge` does not tween — it forwards to `BalloonMotionTicker` (`Balloon/Controller/`), which owns the out-and-back motion state and applies positions through `IBalloonMotionView`. Because a ticker-driven lerp escapes `transform.DOKill()`, systems that take over a balloon's transform must cancel explicitly: `BalloonBalancer` calls `CancelNudge` before starting a balance move, and `BalloonView.OnDespawned` calls it before the view returns to the pool.

### Blend formula

Each nudge entry tracks two positions: `StartPosition` (where the balloon was visually when the nudge began) and `SlotPosition` (the correct grid position it should return to). The per-frame position is:

```
basePos  = Lerp(startPosition, slotPosition, progress)
position = basePos + offset × reach(progress)
```

Where `reach` rises from 0 → 1 (outbound half, EaseOutQuad) then falls from 1 → 0 (return half). At `progress = 0` the balloon is at its visual position (no first-frame snap); at `progress = 1` it lands exactly on the grid slot (correcting any accumulated drift).

### Return-position inheritance

When a nudge is replaced mid-flight (a new neighbor nudge arrives before the current one finishes), `StartNudge` inherits the cancelled entry's `SlotPosition` as the new return target. Without this, each replacement would use the mid-outbound visual position as its center, accumulating a small drift with every replacement. Deferred balance callbacks are also inherited (`onComplete ??= previousComplete`) so balance moves are never lost.

## Nudge ↔ balance interaction

The balance system (`BalloonBalancer`) and the nudge ticker both drive `transform.position`. Their interaction is managed through priority: **balance always wins**.

When `AnimatePaths` encounters a balloon:

| Balloon state | Balancer action |
|---|---|
| `IsNudging = true` | **Defers** — replaces the nudge's `onComplete` callback so the balance tween starts after the nudge finishes. The nudge plays out fully. |
| `IsNudging = false` | **Cancels** any residual ticker entry, resets `_isNudging` via `OnNudgeCancelled()`, then starts the DOPath balance tween immediately. |

After cancellation, the balloon is `IsStable = false` and `_isNudging = false` until the balance tween completes. During this window, neighbor nudges are rejected by the stability guard. This is intentional — the nudge ticker and DOPath would fight over the transform if both ran simultaneously.

### Deflect during balance

A `Deflect` nudge bypasses both guards. If the balance tracker is playing, `BalloonView` pauses it and nudges from the current visual position. On `CompleteNudge`: if a deferred balance callback is pending, the paused tracker is killed (the deferred balance creates a fresh path from the post-nudge position); otherwise the paused tracker resumes. A second deflect arriving while the first is still playing kills the paused tracker outright.

## Architecture notes

The current nudge–balance interaction is the result of iterative bug-fixing; several patterns emerged that a future refactor could formalize:

- **No canonical home position** — the ticker infers the return-to position from the grid slot. A dedicated `HomePosition` field on the model (updated by balance on completion) would remove the grid-lookup indirection and make drift impossible by construction.
- **Implicit state machine** — motion state is encoded across `_isNudging`, `IsStable`, and `trackerPlaying` (8 combinations, most invalid). A formal `MotionState` enum (`Idle`, `Nudging`, `Balancing`, `Spawning`) with explicit transitions would make guards declarative.
- **Layered motion** — nudges and balance cannot compose. The current architecture forces "one owner at a time" for `transform.position`. If nudges were expressed as additive offsets on top of a base position (owned by balance or idle), both could run simultaneously and the stability guards would become unnecessary.

## Interactions

- **ActorHitMessage** — triggers automatic neighbor nudges for every hit on an `IHasNudge` actor
- **NudgeMessage** — triggers single-actor (Deflect) or shockwave nudges from controllers and item handlers
- **INudgeable / BalloonView.Nudge()** — starts the push-out → return motion on any implementing view (driven by `BalloonMotionTicker` for balloons)
- **SlotGrid** — resolves neighboring actors and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` passed to the model via `BalloonModelConfig` during spawning
