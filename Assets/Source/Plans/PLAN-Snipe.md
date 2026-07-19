@page plan_snipe Snipe — the armor-piercing tracer

# Snipe — an armor-piercing lance that spends itself cracking the tough line

> Snipe is the game's **anti-armor tool**. It arms the projectile with the engine's existing
> **piercing** state on demand (today only *earned* via a long cruise), adds a single, non-stacking
> **speed buff**, and plows straight through the soft board. Every tough/unbreakable balloon it
> cracks **bleeds its speed** (halving per hit, floored at base), and when the buff finally dies on a
> wall it **clears every hits>1 balloon on that last line**. A **rainbow** holder turns each armor
> crack into stored **charge**, then discharges a **colorable-only** rainbow bloom scaled by how much
> armor it ate — you can't paint armor, but shattering it powers the conversion of everything soft
> around it.

---

## 1. Where this sits today

`Snipe` already exists as an `ItemType` (`Assets/Source/Configuration/Items/ItemType.cs:11`) with a
**placeholder** handler: `SnipeItemHandler` (`Assets/Source/Item/Snipe/SnipeItemHandler.cs`) applies a
single multiplicative `ProjectileBuffId.Speed` buff — value pulled from the shared
`IBuffConfiguration.GetValue(Speed)` — that ends on the next `WallBounceEndCondition`. No piercing, no
decay, no line-clear, no rainbow branch, and **no `SnipeSettings`** of its own (it borrows the global
buff value). This plan replaces that placeholder with the full mechanic below.

The mechanic is **not new tech** — it rides rails that already exist:

| Rail | Where | What it already does |
|---|---|---|
| Piercing | `IProjectileModel.IsPiercing`, `ProjectileHitResolver.cs:41-63` | Plows through pops; a tough/unbreakable plow does `CruisePierceSpeedScale *= 0.5f`; the motion resolver floors the total at base and the next wall ends it |
| Speed floor | `ProjectileMotionResolver.cs:44,69` | `ComputeBuffedValue(Speed, …)` is the floor; `CruisePierceSpeedScale` scales the cruise multiplier above it |
| Buff lifecycle | `IProjectileBuffs`, `ProjectileBuffService`, `WallBounceEndCondition` | Applies a buff to the active projectile and drops it when its end-condition fires (a `ShieldLostMessage`) |
| Neighbor conversion | `ProjectileHitResolver.ConvertNeighborsToRainbow` (`:149`) | Converts `IPaintable` neighbors of a pop to rainbow — **skips non-paintable** toughs/unbreakables |
| Lights / disturbance | `SceneLightFieldService.RegisterLight`, `DisturbanceFieldService.Stamp` (`Shared/`) | Item-cast lights + shockwave stamps (Bomb/Laser/Paint precedent, see `Item/README.md`) |

So Snipe is largely a **second grant path** for piercing plus a signature payoff, not a new subsystem.

## 2. Base mechanic (non-rainbow)

**On activation** (the projectile pops the Snipe balloon, via `IBalloonItem.Activate`):

1. **Arm piercing.** Set the projectile's piercing state so it plows through balloons instead of
   one-shot-and-stop. This is the same `IsPiercing` state the cruise earns; Snipe grants it directly.
2. **Grant one Speed buff.** A single multiplicative `ProjectileBuffId.Speed` buff at
   `SnipeSettings.SpeedBuffMultiplier`. **Non-stacking** — a second Snipe *refreshes* the buff, it does
   not add. This is deliberately unlike the cruise/shield-lost boost, which is cumulative per banked
   shield (`CruiseSpeedPerShield`).

**In flight — two ways it ends:**

- **No tough contact.** It pierces and bounces, burning one shield per wall (`ShieldLostMessage`),
  until it runs out of shields and dies on the last-shield wall. *The game already terminates the shot
  here* — Snipe adds nothing to this path except the line-clear on that dying segment (§4).
- **Tough contact (hits > 1).** Each tough/unbreakable plow **bleeds speed** via the existing
  `CruisePierceSpeedScale *= ToughHitSpeedFalloff` (default `0.5`), floored at true base speed by the
  motion resolver. The buff then ends on **the next `ShieldLostMessage`** (wall hit) — which triggers
  the line-clear (§4).

The **speed-as-visual** through-line is intentional: fast at launch, visibly slowing with each armored
hit, then a hard discharge. The decay *is* the tell.

## 3. The "last line"

The line-clear fires on the **final travel segment the buff dies on** — reached one of two ways, both
already produced by the engine:

- the segment where **shields hit 0** (the game's own terminal segment), or
- the segment ending in the **next wall hit after a tough plow**.

Only hits>1 balloons **on that segment** are affected — a **directional line sweep**, never a global
tough-purge. "On the line" = within a capsule of half-width `SnipeSettings.LineClearHalfWidth` swept
along the terminating segment (query via the existing `BalloonOverlapQuery` / non-alloc physics used by
Bomb/Laser).

## 4. The payoff — line-clear

On the terminating wall hit, the sniper discharges and **one-shots every hits>1 balloon on that line**
(piercing damage, so unbreakables die too). Fire into a wall of tough balloons and the dying pass
cracks the whole armored row at once — the relief valve for an armored board.

## 5. Rainbow synergy — "charge-up discharge"

A rainbow holder keeps the entire base behavior (toughs are still **destroyed** — they are never
paintable, so they can never become rainbow) and adds a **charge → bloom** payoff. Toughs are the
*fuel*, not the target.

- **Charge.** Each tough/unbreakable plow increments a **count** (`ChargePerToughHit`, default `1`) —
  the same event that halves speed (`ProjectileHitResolver.cs:59`). Slowing down *is* the charge meter
  filling.
- **Discharge bloom.** On the terminating wall hit, in addition to the line-clear, convert **only
  colorable (`IPaintable`) balloons** within a radius centered on the impact point to rainbow. Radius
  scales with charge:
  `min(BloomBaseRadius + charge × BloomRadiusPerCharge, BloomRadiusCap)`.
  Toughs/unbreakables caught in the bloom are simply not converted (not paintable) — they were already
  cleared by the line-sweep if on the line.
- **In-flight conversion.** Like the rainbow-shield buff, the rainbow lance scores colour-agnostically
  and converts popped balloons' `IPaintable` neighbors as it pierces (reuse the existing
  `RainbowShield`-buff path in `ProjectileHitResolver`).

Fantasy: *you can't paint armor, but shattering it releases the energy that paints everything soft
around it.* Crack a 4-wide tough wall → charge 4 → the discharge blooms a rainbow field across the soft
balloons behind it. Zero toughs cracked → a tiny bloom. The payoff is proportional to the armor eaten.

## 6. Feel — lights & disturbance

Reuse the item light/disturbance seams (`SceneLightFieldService`, `DisturbanceFieldService.Stamp`; see
`Item/README.md` "Lights Cast by Items"):

| Beat | Light | Disturbance |
|---|---|---|
| In flight | Moving **capsule light** along the lance (laser-beam-light style); intensity tracks current speed so decay reads in the lighting | — |
| Each tough plow | Small **light spark** at impact | Short **inward** stamp — cracking armor thumps the soft neighbors and telegraphs "charge +1" |
| Discharge | **Point-light flash** scaled by charge; rainbow cycles the palette via `ColorCycle` | Outward **shockwave** stamped at the impact point, radius matched to the bloom (rainbow: carries the conversion ripple outward) |

## 7. Config — `SnipeSettings`

New `[Serializable] SnipeSettings` on `ItemSettings` (mirroring `BombSettings`/`LaserSettings`;
`Assets/Source/Configuration/Items/ItemSettings.cs`). `ItemSettingsDrawer` already hides `Damage` for
Snipe (non-damaging item) — extend it to surface these.

| Field | Purpose | Starting value |
|---|---|---|
| `SpeedBuffMultiplier` | Initial speed buff (multiplicative, non-stacking) | ~1.6 |
| `ToughHitSpeedFalloff` | Per-tough-plow speed scale (feeds `CruisePierceSpeedScale`) | 0.5 |
| `LineClearHalfWidth` | Capsule reach for "on the line" | tune in editor |
| `ChargePerToughHit` | Rainbow charge per tough plow | 1 |
| `BloomBaseRadius` | Rainbow bloom radius at charge 0 | tune |
| `BloomRadiusPerCharge` | Bloom radius growth per charge | tune |
| `BloomRadiusCap` | Hard cap so the bloom never eats the whole board | tune |
| `NeighborConversionRange` / `ColorCycles` | Rainbow in-flight neighbor conversion + iridescence | mirror other rainbow items |
| Light params (`radius`/`intensity`/`fallbackSeconds`) | Flight + spark + discharge lights | mirror Laser/Bomb |

`SpeedBuffMultiplier` supersedes the current reliance on `IBuffConfiguration.GetValue(Speed)` for
Snipe, so the value is authored per-item rather than shared with other Speed grants.

> **Perf note (carry into phases 2 & 5):** the flight/light runtime reads `SnipeSettings` fields
> per-frame during the lance's flight (tracer light intensity tracking speed, colour-cycle lerp,
> etc.). Snapshot the needed values into locals / the flight state at activation — do **not** walk the
> `IItemConfiguration.Snipe.X` interface chain every frame.

## 8. State ownership

Handlers are singletons and activations overlap (`Item/README.md`), so **no per-activation state on the
handler**. Piercing already lives on the projectile (`IsPiercing`); the tough-plow speed scale lives on
`ProjectileFlightState` (`CruisePierceSpeedScale`). Add the rainbow **charge counter** to the same
per-projectile flight state (e.g. `SnipeCharge`, reset on `ProjectileLoadedMessage` like the other
flight bookkeeping), never to the handler. The Snipe buff instance carries its `WallBounceEndCondition`
exactly as today.

## 9. Build order

1. `SnipeSettings` + `ItemSettings` wiring + `ItemSettingsDrawer` surface (compile-checkable).
2. Grant path: arm `IsPiercing` + non-stacking Speed buff in `SnipeItemHandler`; verify decay-on-tough
   already works via the existing pierce rails.
3. Line-clear on the terminating segment (capsule query + piercing dispatch).
4. Rainbow: charge counter on flight state; discharge bloom (colorable-only radius convert); in-flight
   neighbor conversion via the `RainbowShield` path.
5. Lights + disturbance beats (§6).
6. Playtest in-editor — decay/feel, line-clear framing, bloom scaling, and the light/disturbance beats
   all need a runtime pass (`dotnet build` cannot validate behavior or shaders).

## 10. Open tuning (deferred to playtest)

- `SpeedBuffMultiplier` vs. `ToughHitSpeedFalloff` — how fast the lance should feel and how quickly a
  tough wall drains it.
- `BloomRadiusPerCharge` / `BloomRadiusCap` — how explosive a big-charge rainbow discharge reads without
  trivializing the board.
- Whether the in-flight capsule light needs a lower floor so a fully-decayed lance still reads as lit.
