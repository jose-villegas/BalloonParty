@page arch_item_activation Item Activation Pipeline

# Item Activation Pipeline

@image html item_activation.svg "Item Activation Pipeline"

## What this diagram shows

The handoff chain from projectile contact to item effect, and how `BalloonController`
coordinates the timing so the item balloon is not returned to the pool mid-activation.

**Sequence:**
1. `ProjectileHitResolver` calls `EvaluateHit(new DamageContext(1))`, embeds the outcome
   (`Pop`) in `ActorHitMessage`, and dispatches it through `IHitDispatcher` (`HitPipeline`).
2. **`BalloonController`** — invoked as the pipeline's balloon-owner stage via
   `BalloonControllerRegistry.Route` — calls `_view.Hide()` (disables collider and
   renderers), then subscribes for `ItemActivatedMessage` instead of returning the
   balloon to the pool. This blocks pool return for the duration of the effect.
3. **`ItemActivator`** receives the trailing `ActorHitMessage` broadcast, yields one
   frame (`UniTask.Yield`) so everything triggered by the hit settles first, then calls
   `await IBalloonItem.Activate(balloon, worldPos)` on the matching handler.
4. The handler runs its effect — which may be multi-frame (e.g. lightning chain) — and
   dispatches an `ActorHitMessage` through `IHitDispatcher` for each secondary balloon
   it affects.
5. `ItemActivator` publishes `ItemActivatedMessage` — `BalloonController` unblocks and
   returns the item balloon to pool.

**Context-independence:** `Item/` has no dependency on `Balloon/`. `IBalloonItem` is
the balloon system's adapter into the item system — not the item system's knowledge of
balloons. Items can be hosted on any object that provides the `IBalloonItem` interface.

## Guidance

**Adding a new item type:**
1. Add the enum value to `ItemType`
2. Implement `IBalloonItem` in a new handler class (e.g. `Item/MyItem/MyItemHandler.cs`)
3. Register the handler in `GameLifetimeScope` — `ItemActivator` discovers all
   `IBalloonItem` implementations via collection injection
4. Add an `ItemSettings` entry to `ItemConfiguration` — controls assignment weight,
   on-board cap, damage/flags, and any type-specific params
5. Add a visual: create an `ItemVisualView` prefab; `ItemDisplayService` handles display

**Why the one-frame yield in `ItemActivator`:**
Scoring and the owning balloon's reaction already ran as explicit `HitPipeline` stages,
but the trailing broadcast's other subscribers run synchronously in subscription order,
and a handler that dispatches secondary hits immediately would re-enter the pipeline
from inside the original dispatch. Deferring activation by one frame lets the whole
hit — including chained item hits, which each defer themselves the same way — settle
before the effect starts.

**`ItemSettings.Damage` vs projectile damage:**
The projectile always hits with `Damage = 1`. Item handlers pass `settings.Damage`
into `ActorHitMessage` for secondary hits — so a Bomb can one-shot tough balloons
with `Damage = 2` without any special-casing in `BalloonController`.

