@page plan_spider_web Spider Web Obstacle — Ideation Plan

# Spider Web Obstacle — Ideation Plan

> Ideation and design exploration for the **Spider Web** obstacle — a traversable,
> degradable grid actor that disappears after N projectile hits.

---

## Context

The current structural actor vocabulary has a gap: no actor is both **traversable** and
**destructible**. Puff is traversable but permanent; Gatekeeper is destructible but
blocks paths. Spider Web fills this gap.

| Archetype | Traversable | Hitable | Durable | Outcome |
|---|---|---|---|---|
| **Puff** | ✅ | ❌ | ❌ | — |
| **Bush** | ❌ | ❌ | ❌ | — |
| **Spider Web** | ✅ | ✅ | ✅ N hits | `PassThrough` |
| **Deflector** | ❌ | ✅ | ❌ | `Deflect` |
| **Absorber** | ❌ | ✅ | ❌ | `Absorb` |
| **Gatekeeper** | ❌ | ✅ | ✅ N hits | `Deflect` → `Pop` |

---

## What it is

Traversable, degradable structural actor. Occupies a grid slot; spawn paths can arc
through it (`IPassThrough`). Unlike Puff (permanent and non-interactive), Spider Web
reacts to projectile hits: each projectile that passes through tears the web, reducing
`HitsRemaining` by 1. When durability reaches zero, the web disappears and the slot
becomes empty.

Spider Web is the first actor that is simultaneously **traversable AND hitable** — the
projectile passes through (like Puff) but the web takes damage from the traversal
(like Gatekeeper, but with `PassThrough` outcome instead of `Deflect`).

---

## Gameplay role

- **Soft clutter** — webs partially obscure the grid, making it harder to read balloon
  colours and positions behind them. Clearing webs improves visibility.
- **Passive obstacle** — webs never block paths, but their visual noise creates cognitive
  load. Players are rewarded for clearing them even though there's no score.
- **Difficulty knob** — the procedural placer can increase web density to raise perceived
  difficulty without adding lethal obstacles (Absorbers) or hard blocks (Bushes).
- **Ephemeral terrain** — unlike permanent actors, webs make the grid feel alive and
  changing. Over the course of a game, paths that were cluttered become clear.

---

## Model design

```
SpiderWebObstacleModel : IWriteableSlotActor, IPassThrough, IHasDurability
```

- `IPassThrough` — projectiles pass through; spawn paths traverse freely
- `IHasDurability` (extends `IHitable`) — `HitsRemaining` decremented on each hit
- `EvaluateHit` returns `HitOutcome.PassThrough` always (projectile continues)
- When `HitsRemaining` reaches 0, `GridActorHitController` removes it from the grid

This follows the same removal pattern as `GatekeeperActorModel` — no new controller
infrastructure needed.

```csharp
internal class SpiderWebObstacleModel : IWriteableSlotActor, IPassThrough, IHasDurability
{
    public ReactiveProperty<int> HitsRemaining { get; }

    IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

    public Vector2Int SlotIndex { get; private set; }

    Vector2Int IWriteableSlotActor.SlotIndex
    {
        get => SlotIndex;
        set => SlotIndex = value;
    }

    public SlotActorKind Kind => SlotActorKind.Static;

    internal SpiderWebObstacleModel(int hitsToPop)
    {
        HitsRemaining = new ReactiveProperty<int>(hitsToPop);
    }

    public HitOutcome EvaluateHit(DamageContext context)
    {
        HitsRemaining.Value = System.Math.Max(0, HitsRemaining.Value - context.Damage);
        return HitOutcome.PassThrough;
    }
}
```

**Key difference from Gatekeeper:** Gatekeeper returns `Deflect` until destroyed then
`Pop`. Spider Web always returns `PassThrough` — the projectile is never stopped.

---

## Art direction — Ideas

### Option A — Classic Cobweb

Radial spider web pattern stretched across the slot. Semi-transparent white/silver
strands. Tears appear as strands break on each hit.

**Pros:** Instantly recognisable; strong "fragile" signal; natural degradation visual.
**Cons:** Radial symmetry might clash with hex grid alignment.

### Option B — Messy Garden Web

Irregular, messy web — scattered strands in various angles. Organic and park-appropriate.

**Pros:** Fits park theme; irregular shape masks alignment issues.
**Cons:** Less iconic; might not read clearly at small scale.

### Option C — Procedural Shader Web

Shader-generated strands using polar UV mapping. `HitsRemaining` drives strand density.

**Pros:** Smooth degradation; consistent with procedural actors; no sprite production.
**Cons:** Shader complexity; thin strands may alias at game scale.

### Option D — Silk Curtain

Vertical sheet of silk threads hanging from the top of the slot. Parting/tearing on hit.

**Pros:** Unique visual; clear "pass through" signal.
**Cons:** Less recognisable as "spider web".

---

## Visual degradation

| HitsRemaining | Visual state |
|---|---|
| 3 (full) | Complete web — all strands intact, full opacity |
| 2 | Partial tear — outer strands missing, slight opacity reduction |
| 1 | Heavily torn — only core strands remain, low opacity, frayed edges |
| 0 | Destroyed — removed from grid |

View subscribes to `IHasDurability.HitsRemaining` and swaps sprites or adjusts shader
parameters.

---

## Required code changes

1. **`GridActorType.cs`** — add `SpiderWeb = 5`
2. **`SpiderWebObstacleModel.cs`** — new model in `Slots/Actor/Archetype/`
3. **Spawner** — factory case: `GridActorType.SpiderWeb => new SpiderWebObstacleModel(entry.HitsToPop)`
4. **Prefab** — `Assets/Prefabs/Grid/SpiderWeb.prefab`
5. **Animator** — `Assets/Animation/Grid/SpiderWeb.controller`
6. **Config entry** — add to `GridActorConfiguration` SO

---

## Tests

New fixture **`SpiderWebObstacleTests`** (`Tests/EditMode/Slots/`):
```
SpiderWebObstacle_KindIsStatic
SpiderWebObstacle_IsIPassThrough
SpiderWebObstacle_IsIHasDurability
SpiderWebObstacle_EvaluateHit_ReturnsPassThrough
SpiderWebObstacle_EvaluateHit_DecrementsHitsRemaining
SpiderWebObstacle_EvaluateHit_AtZeroHits_StillReturnsPassThrough
SpiderWebObstacle_GridActorHitController_RemovesAtZeroHits
```

---

## Open questions

1. **Hit VFX** — Should each hit play a strand-snap particle effect, or keep it
   sprite-swap only?

2. **Puff overlap** — Can a Spider Web sit in the same column as a Puff? Both are
   traversable, but stacking may create visual noise.

3. **Sound** — Unique SFX per hit, or reuse the generic hit sound?

4. **Items** — Should Spider Web implement `IHasItemSlot`? Probably not, but a web
   "catching" an item could be a future mechanic.

5. **Score** — No score for clearing a web (clearing is its own reward). Confirm this
   design choice before implementation.

