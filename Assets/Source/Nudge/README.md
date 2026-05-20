# Nudge

Elastic push-out/return animations for actors — triggered by projectile hits, tough-balloon deflections, and item shockwaves.

## Folder structure

| File | What it owns |
|---|---|
| `NudgeService` | `IStartable` — subscribes to `ActorHitMessage` (filtered by `IHasNudge`) and `BalloonNudgeMessage`; resolves per-actor and per-publisher overrides; dispatches nudge animations to `BalloonView` |
| `NudgeType` | `[Flags]` enum — `Deflect`, `Neighbor`, `Shockwave`; controls which override entries apply |
| `NudgeOverride` | Serializable per-source struct: `AppliesTo` (flag mask), `Distance`, `Duration`, `Falloff` |
| `BalloonNudgeMessage` | Pub/sub signal — carries target balloon (null = shockwave), origin, source type, and optional publisher overrides |
| `Editor/NudgeOverrideDrawer` | Extends `AutoFieldPropertyDrawer` (in `Source/Editor/`) — auto-draws `_distance` and `_duration`; pins `_appliesTo` above the auto section via `DrawPinnedFields` using `EnumFlagsField`; conditionally draws `_falloff` only when the Shockwave flag is set. Overrides `BuildFoldoutLabel` to append the current `NudgeType` value to each array element header (e.g. `Element 0  [Deflect]`) |

## Nudge types

| Type | Trigger | Target |
|---|---|---|
| `Neighbor` | Any `ActorHitMessage` where the hit actor implements `IHasNudge` | All 6 grid neighbors that also implement `IHasNudge` |
| `Deflect` | Projectile bounces off a tough or unbreakable balloon | The deflecting balloon itself via `BalloonNudgeMessage` |
| `Shockwave` | Item handler (e.g. Bomb) publishes `BalloonNudgeMessage(Source=Shockwave, Balloon=null)` | All occupied grid slots, attenuated by distance |

## Override resolution

Distance, duration, and falloff are resolved in priority order for each affected actor:

1. **Per-actor override** — `NudgeOverride[]` from `IHasNudge.NudgeOverrides` on the actor's model, sourced from `BalloonPrefabEntry`
2. **Publisher override** — `NudgeOverride[]` carried in the `BalloonNudgeMessage` (set by item handlers)
3. **Global default** — `BalloonsConfiguration.NudgeDistance`, `NudgeDuration`, `NudgeFalloff`

Shockwave uses exponential distance falloff: `distance = baseDistance × exp(−falloff × d)`. A per-actor shockwave override skips the falloff entirely and uses its fixed `Distance`.

## Stability tracking

`NudgeService` maintains `_nudging: HashSet<IWriteableSlotActor>`. An actor can be nudged if it is stable or already mid-nudge (allowing nudge-to-nudge interrupts). Actors that are unstable for other reasons (spawning, balancing) are skipped to avoid conflicts. On nudge start `IsStable = false` and the actor is added to `_nudging`; on nudge complete `IsStable = true` and it is removed.

Only actors whose view is a `BalloonView` are nudged — nudging is balloon-specific until a second actor type with its own nudge animation is introduced.

## Interactions

- **ActorHitMessage** — triggers automatic neighbor nudges for every hit on an `IHasNudge` actor
- **BalloonNudgeMessage** — triggers single-balloon (Deflect) or shockwave nudges from controllers and item handlers
- **BalloonView.Nudge()** — executes the push-out → return DOTween sequence on the view
- **SlotGrid** — resolves neighboring actors and slot world positions
- **BalloonsConfiguration** — global nudge defaults (`NudgeDistance`, `NudgeDuration`, `NudgeFalloff`)
- **BalloonPrefabEntry** — per-type `NudgeOverride[]` assigned to the model by `BalloonController`
