# Nudge

Elastic push-out/return animations for actors — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `ActorHitMessage` (filtered by `IHasNudge`) and `NudgeMessage`; resolves per-actor and per-publisher overrides; dispatches nudge animations to any view implementing `INudgeable` |
| `INudgeable` | View-side contract — `Nudge(Vector3 direction, float distance, float duration)`. Decouples `NudgeService` from `BalloonView` specifically. |
| `NudgeType` | `[Flags]` enum — `Deflect`, `Neighbor`, `Shockwave`; controls which override entries apply |
| `NudgeOverride` | Serializable per-source struct: `AppliesTo` (flag mask), `Distance`, `Duration`, `Falloff` |
| `NudgeOverrideResolver` | Resolves distance, duration, and falloff through the 3-tier cascade (actor → publisher → config default). Injected into `NudgeService` |
| `NudgeMessage` | Pub/sub signal — carries target actor (null = shockwave), origin, source type, and optional publisher overrides. `Actor` is typed as `IHasNudge` so any nudgeable actor can be a target, not only balloons. |
| `Balloon/Controller/BalloonMotionTicker` | `ILateTickable` that manages impulse stacks: receives `AddImpulse()` calls, advances each impulse's elapsed time every late tick, reconciles external base writes, and applies `BasePosition + Σ impulses` to each active view. Zero steady-state GC (pooled state, released when impulses complete). |
| `Balloon/View/IBalloonMotionView` | Read/write `Position` passthrough to `transform.position`. Decouples the ticker from MonoBehaviour concerns. |
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

Shockwave uses exponential distance falloff: \f$\text{distance} = \text{baseDistance} \times \exp(-\text{falloff} \times d)\f$. A per-actor shockwave override skips the falloff entirely and uses its fixed `Distance`.

## The impulse model

A nudge is a **pure impulse** — an additive, transient offset applied on top of the balloon's base position. Each impulse has three parameters: a unit direction, a distance (peak amplitude), and a duration (how long it plays).

The per-frame offset is computed as:

\f[
\text{offset}(t) = \text{direction} \times \text{distance} \times \text{reach}(\text{progress})
\f]

where \f$\text{progress} = \text{elapsed} / \text{duration}\f$ and `reach()` is a symmetric ease curve:

- Outbound half (\f$0 \le \text{progress} \le 0.5\f$): \f$\text{reach} = \mathrm{EaseOutQuad}(\text{progress} \times 2)\f$ — accelerates away, decelerates toward the peak
- Return half (\f$0.5 < \text{progress} \le 1\f$): \f$\text{reach} = 1 - \mathrm{EaseOutQuad}\big((\text{progress} - 0.5) \times 2\big)\f$ — accelerates back, lands exactly at zero

This reach curve is identical to the nudge animation in the prior system — no visual retune needed. At \f$\text{progress} = 0\f$ and \f$\text{progress} = 1\f$, the offset is exactly zero; the impulse contributes nothing to position at both endpoints.

Multiple impulses on the same view **stack additively**:

\f[
\text{totalOffset}(\text{view}) = \sum_i \text{offset}_i \quad \text{for all active impulses on view}
\f]
\f[
\text{finalPosition} = \text{BasePosition} + \text{totalOffset}
\f]

A balloon hit twice in quick succession shows a bouncing effect: it nudges away for the first impact, gets hit again mid-return, bounces out again from wherever it is, and completes both impulses on independent timelines. Each impulse removal is automatic — when \f$\text{elapsed} \ge \text{duration}\f$, the impulse is discarded.

## Base reconciliation

The nudge ticker applies \f$\text{BasePosition} + \sum \text{impulses}\f$ every late tick. It cannot assume it knows when other systems move the balloon, so it detects external writes by remembering the final position it wrote last frame (`LastWritten`). At the start of each late tick:

```
if (view.Position ≉ LastWritten)   // another system wrote the transform this frame
    BasePosition = view.Position;  // adopt it as the new base
view.Position = BasePosition + totalOffset;
LastWritten = view.Position;
```

The comparison uses a small epsilon (\f$\text{sqrMagnitude} < 10^{-12}\f$) to guard against world↔local float rounding errors while accepting real position writes (DOTween moves \f$\ge\f$ µm/frame, pool teleports, manually-set positions).

This reconciliation enables seamless composition:

- A balloon on a balance DOPath tween (which writes `transform.position` every Update frame) receives a neighbor nudge. The ticker detects the DOPath's new position each late tick, adopts it as the base, and applies the nudge impulse on top. The balloon visibly bounces during the slide and lands exactly on the target slot when both motions complete.
- A balloon is teleported to a new position (e.g., board effect reparent). The ticker detects the teleport, adopts the new position, and any active impulses continue from that point without snapping or jumping.

When the last impulse completes, the ticker writes pure `BasePosition` once (releasing the view's pooled state back to the pool) and detaches from the view. Steady-state cost is zero — the ticker only manages active impulses.

## Ownership map

| System | Writes `transform.position` | When | Owns |
|---|---|---|---|
| BalloonFactory / pool | Direct set, spawn DOPath | Spawn teleport + spawn animation | Base position during spawn |
| BalloonBalancer / DOPath | DOPath tween | Balance slide to new slot | Base position during balance |
| BalloonMotionTicker | Base + nudge offsets | `ILateTickable`, only while impulses are active | Nudge impulses (transient offsets) |
| Everything else | — | never | — |

**Critical invariant**: Only one system writes at a time (by registration order). The factory/pool teleports the balloon and spawns with a tween; the balancer overrides with a balance DOPath; the ticker detects these writes and reads them as base updates. Nothing re-coordinated or paused — each system runs its own flow independently.

## Stability and motion state

`IsStable` now has a single, clear meaning: **the base position has no active motion** (no spawn tween, no balance DOPath). It is owned and managed exclusively by the spawn and balance systems.

Nudges do not touch `IsStable`. A nudge's offset rises and falls around the base, but the base is not in motion (from the nudge system's perspective), so the flag stays put. Behavioral implication: a balloon's idle animation (the slow bob) continues playing while a nudge impulse bounces it, since the animator's `IsStable` bool doesn't flip.

## Lifecycle

When a view despawns (returns to the pool), `BalloonView.OnDespawned()` calls:

```csharp
_motionTicker?.CancelAll(this);
```

This clears all pending impulses for the view and drops its pooled state **without a final write**. The caller (the pool/despawner) is about to teleport or recycle the view, so the ticker should not touch `transform.position` again.

For fine-grained impulse management, `AddImpulse()` also has a spillover cap: when a view already has 8 active impulses (max), a 9th impulse silently replaces the one closest to completion. This caps memory under shockwave spam without visible artifact — the fading impulse contributes almost nothing anyway.

## Interactions

- **ActorHitMessage** — triggers automatic neighbor nudges for every hit on an `IHasNudge` actor
- **NudgeMessage** — triggers single-actor (Deflect) or shockwave nudges from controllers and item handlers
- **INudgeable / BalloonView.Nudge()** — forwards to `BalloonMotionTicker.AddImpulse()` to start the push-out → return motion on any implementing view
- **SlotGrid** — resolves neighboring actors and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` passed to the model via `BalloonModelConfig` during spawning

---

**History**: The prior guard-based nudge–balance coordination was replaced by this layered-motion design. See @ref plan_nudge_balance_interaction for the debugging post-mortem and architectural context.
