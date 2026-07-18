@page arch_message_flow Message Flow

# Message Flow

@image html message_flow.svg "MessagePipe Pub/Sub Flow"

## What this diagram shows

All MessagePipe pub/sub connections in the game — which systems publish which messages
and which systems subscribe to them. This is the primary decoupling mechanism:
publishers know nothing about subscribers; subscribers know nothing about publishers.

**Key flows:**
- `ProjectileHitResolver` → `IHitDispatcher.Dispatch` → `HitPipeline` runs the
  order-dependent stages explicitly (`ScoreController.OnActorHit`, then the owning
  `BalloonController` via `BalloonControllerRegistry.Route`) and only then publishes
  `ActorHitMessage` → (`NudgeService`, `ItemActivator`, `GridActorHitController`) —
  the broadcast serves order-independent observers only; it carries the pre-computed
  `HitOutcome` so every subscriber reads the same result without re-evaluating the hit.
  Never publish `ActorHitMessage` directly — route hits through `IHitDispatcher`
- `ScoreController` → `ScorePointsGroupMessage` → (`ScoreTrailService`,
  `LevelUpCinematic`) — one message per resolved color; carries the group's total
  points and the last point's cumulative score
- `BalloonSpawner` / `ProjectileView` → `BalanceBalloonsMessage` → `BalloonBalancer`
  — pure signal; no data needed
- `ScoreController` → `ScoreLevelUpMessage` → (`ColorProgressBar`, `LevelUpPopUp`,
  `ColorStreakTracker`) — announces the *confirmed* level-up (the cinematic itself
  triggers earlier, off `ScorePointsGroupMessage` when `WillLevelUp()` is true)

## Guidance

**Use MessagePipe when:**
- Two systems should not hold direct references to each other (e.g. `ProjectileView`
  should not know about `ScoreController`)
- One event has multiple independent consumers
- The consumer may not exist at publish time (e.g. UI panels that come and go)

**Do not use MessagePipe when:**
- A controller directly owns a service it always uses — just inject it
- The communication is one-to-one and both objects always exist together

**Message design rules:**
- Messages are structs, not classes — no heap allocation per publish
- Messages carry the **read-only interface** of involved models — never the writable one
- Include enough data in the message so subscribers don't need to inject the publisher
  to get context (e.g. `ActorHitMessage` carries `WorldPosition`, `ProjectileDirection`,
  `Outcome`, and the `DamageContext` so subscribers don't need to query the projectile)

**Finding all subscribers of a message:**
`grep -r "ISubscriber<MessageType>" Assets/Source` — every `[Inject] private ISubscriber<T>` field is a subscriber.

