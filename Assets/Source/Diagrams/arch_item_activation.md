@page arch_item_activation Item Activation Pipeline

# Item Activation Pipeline

@image html item_activation.svg "Item Activation Pipeline"

## What this diagram shows

The handoff chain from projectile contact to item effect, and how `BalloonController`
coordinates the timing so the item balloon is not returned to the pool mid-activation.

**Sequence:**
1. `ProjectileView.OnTriggerEnter2D` calls `EvaluateHit(new DamageContext(1))`, embeds
   the outcome (`Pop`) in `ActorHitMessage`, and publishes it.
2. **`BalloonController`** receives the message, calls `_view.Hide()` (disables collider
   and renderers), and waits asynchronously for `ItemActivatedMessage` before returning
   the balloon to the pool. This blocks pool return for the duration of the effect.
3. **`ItemActivator`** receives the same `ActorHitMessage`, yields one frame
   (`UniTask.Yield`) to let all synchronous subscribers finish, then calls
   `IBalloonItem.Setup(balloon, worldPos)` + `await Activate()` on the matching handler.
4. The handler runs its effect — which may be multi-frame (e.g. lightning chain) — and
   publishes `ActorHitMessage` for each secondary balloon it affects.
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
4. Add `ItemSettings` entry to `ItemConfiguration` — controls frequency, weight, cap,
   damage, and any type-specific params
5. Add a visual: create an `ItemVisualView` prefab; `ItemDisplayService` handles display

**Why the one-frame yield in `ItemActivator`:**
`ActorHitMessage` subscribers run synchronously in publish order. `ScoreController`
(which processes score) and `BalloonController` (which hides the balloon) must finish
before activation starts — the yield ensures that ordering without explicit dependency
wiring.

**`ItemSettings.Damage` vs projectile damage:**
The projectile always hits with `Damage = 1`. Item handlers pass `settings.Damage`
into `ActorHitMessage` for secondary hits — so a Bomb can one-shot tough balloons
with `Damage = 2` without any special-casing in `BalloonController`.

