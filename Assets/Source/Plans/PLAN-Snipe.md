@page plan_snipe Snipe — the armor-piercing tracer

# Snipe — an armor-piercing lance that spends itself cracking the tough line

> Snipe is the game's **anti-armor tool**. It arms the projectile with the engine's existing
> **piercing** state on demand (today only *earned* via a long cruise) and adds a single, non-stacking
> **speed buff**. Piercing is one shared effect reached two ways (cruise or Snipe): the shot pops soft
> balloons on contact but **plows through tough/unbreakable ones at full speed without popping them**,
> recording each. A trajectory **lookahead** detects the **last tough** in the run; shortly after, the
> shot **discharges** — slowing once to base speed and **shattering all the recorded toughs at once**,
> VFX blooming over each strike. Punch through the armored line at speed, then crack the whole line on
> the way out. A **rainbow** holder turns each plowed tough into stored **charge**, then discharges a
> **colorable-only** rainbow bloom scaled by how much armor it ate — you can't paint armor, but
> shattering it powers the conversion of everything soft around it.

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

## 2. The unified pierce-discharge mechanic (shared by cruise AND Snipe)

**Key reframe (decided 2026-07-19):** cruise-earned piercing and Snipe-granted piercing are the **same
effect reached two ways**. All the behavior below lives in the **shared pierce path**
(`ProjectileHitResolver` + `ProjectileMotionResolver` + a discharge step), so it applies to both — a
cruise pierce and a Snipe lance behave identically once armed.

**On activation** (Snipe path): arm `IsPiercing` + a single non-stacking `Speed` buff (Phase 2, shipped).
The cruise path arms `IsPiercing` after a long cruise, unchanged.

**In flight:**

- **Normal (1-hit) balloons pop on contact** (unchanged).
- **hits>1 balloons (tough/unbreakable) are plowed through at FULL speed and NOT popped on contact.**
  Each is **recorded** — the balloon + the world position it was struck (for the discharge VFX). The
  shot keeps full (buffed) speed through the whole armored run; there is **no per-hit slowdown**
  (this replaces Phase 2's `CruisePierceSpeedScale *= 0.5` per tough).
- **Prediction.** Each time a hits>1 balloon is struck, a trajectory lookahead (reuse `ShotSimulator` /
  `ShotBoardGather` / `PathTrace`) checks whether another hits>1 balloon lies ahead on the remaining
  path. While one does, the shot stays fast — still punching the armored run.

## 3. The discharge — shortly after the last tough

When the lookahead finds **no more hits>1 ahead**, the just-struck balloon was the **last** of the run.
A short beat later (`SnipeSettings.DischargeDelay`, tunable for feel) the shot **discharges**:

1. **Slows once to true (un-buffed) base speed** — a single drop, not cumulative. It has punched
   clear of the armor and now coasts.
2. **Pops every recorded hits>1 balloon** (piercing kill — unbreakables included), each with a **VFX
   placed over its recorded hit position**.
3. Optionally dips **time-scale** briefly (slow-mo) for weight — via `TimeScaleService`
   (`DischargeTimeScale` / `DischargeTimeScaleDuration`).
4. The pierce (`IsPiercing`) ends, and — on the Snipe path — the `Speed` buff ends with it.

The shot punches through the armored line *at speed*, then, once clear, slows and **shatters the whole
line at once**, VFX blooming over each armored balloon. The "shatter after the punch-through" beat —
not a per-hit grind — is the payoff.

**Edge cases:**

- **No toughs ever hit** → nothing recorded, no discharge. The shot just pierces normals until it runs
  out of shields (or the cruise ends). Simpler than the old "line-clear on any death."
- **Shot ends (shields exhausted / despawn) with toughs still pending** (e.g. it died mid-armored-run
  before the lookahead said "last") → it **flushes** the pending pops then, so recorded toughs are never
  silently dropped.

"Which toughs get popped" = exactly the ones the shot **struck** (the recorded list), so the VFX sits on
real hit positions. There is no wider spatial capsule sweep — `LineClearHalfWidth` from Phase 1 is
**obsolete** and is removed.

## 5. Rainbow synergy — "charge-up discharge"

A rainbow holder keeps the entire base behavior (toughs are still **destroyed** — they are never
paintable, so they can never become rainbow) and adds a **charge → bloom** payoff. Toughs are the
*fuel*, not the target.

- **Charge.** Charge = the **count of recorded (plowed) hits>1 balloons** in the run
  (`ChargePerToughHit` per plow, default `1`). The armored run *is* the charge meter.
- **Discharge bloom.** At the discharge (§3), in addition to popping the recorded toughs, convert
  **only colorable (`IPaintable`) balloons** within a radius centered on the discharge point to rainbow.
  Radius scales with charge:
  `min(BloomBaseRadius + charge × BloomRadiusPerCharge, BloomRadiusCap)`.
  The recorded toughs themselves pop (fuel); any tough/unbreakable caught in the bloom is simply not
  converted (not paintable).
- **In-flight conversion.** Like the rainbow-shield buff, the rainbow lance scores colour-agnostically
  and converts popped balloons' `IPaintable` neighbors as it pierces (reuse the existing
  `RainbowShield`-buff path in `ProjectileHitResolver`).

Fantasy: *you can't paint armor, but shattering it releases the energy that paints everything soft
around it.* Crack a 4-wide tough wall → charge 4 → the discharge blooms a rainbow field across the soft
balloons behind it. Zero toughs cracked → a tiny bloom. The payoff is proportional to the armor eaten.

## 6. Feel — lights & disturbance

Reuse the item light/disturbance seams (`SceneLightFieldService`, `DisturbanceFieldService.Stamp`; see
`Item/README.md` "Lights Cast by Items"):

| Beat | Light | Disturbance / time |
|---|---|---|
| In flight | Moving **capsule light** along the lance (laser-beam-light style); bright and tight | — |
| Each tough plow | Small **light spark** at the strike point | Short **inward** stamp — punching the armor thumps its neighbors |
| Discharge | **Point-light flash** scaled by charge; rainbow cycles the palette via `ColorCycle`. Plus the per-tough **pop VFX** placed over each recorded strike position | Outward **shockwave** stamped at the discharge point; optional brief **slow-mo** time-scale dip (`TimeScaleService`) for weight |

## 7. Config — `SnipeSettings`

New `[Serializable] SnipeSettings` on `ItemSettings` (mirroring `BombSettings`/`LaserSettings`;
`Assets/Source/Configuration/Items/ItemSettings.cs`). `ItemSettingsDrawer` already hides `Damage` for
Snipe (non-damaging item) — extend it to surface these.

| Field | Purpose | Starting value |
|---|---|---|
| `SpeedBuffMultiplier` | Initial speed buff (multiplicative, non-stacking) | ~1.6 |
| `DischargeDelay` | Beat between "last tough struck" and the discharge (slow + pops) | tune (~0.05–0.15s) |
| `DischargeTimeScale` | Slow-mo time-scale during the discharge (`1` = off) | tune |
| `DischargeTimeScaleDuration` | How long the slow-mo dip lasts | tune |
| `ChargePerToughHit` | Rainbow charge per recorded tough | 1 |
| `BloomBaseRadius` | Rainbow bloom radius at charge 0 | tune |
| `BloomRadiusPerCharge` | Bloom radius growth per charge | tune |
| `BloomRadiusCap` | Hard cap so the bloom never eats the whole board | tune |
| `ColorCycles` | Rainbow iridescence cycles over flight | mirror other rainbow items |
| Light params (`tracer`/`discharge`/`fallbackSeconds`) | Flight + spark + discharge lights | mirror Laser/Bomb |

`SpeedBuffMultiplier` supersedes the old reliance on `IBuffConfiguration.GetValue(Speed)` for Snipe.
**Removed by the redesign:** `ToughHitSpeedFalloff` and `LineClearHalfWidth` (no per-tough decay, no
spatial sweep) — Phase 3 drops both fields + their drawer rows.

> **Perf note:** the flight/light runtime reads `SnipeSettings` fields per-frame during flight.
> Snapshot needed values at activation — do **not** walk the `IItemConfiguration.Snipe.X` chain each
> frame. The per-tough prediction lookahead runs only on a hits>1 contact (rare), not per-frame.

## 8. State ownership

Handlers are singletons and activations overlap (`Item/README.md`), so **no per-activation state on the
handler**. Piercing lives on the projectile (`IsPiercing`); per-shot pierce bookkeeping lives on
`ProjectileFlightState`. The redesign adds there: the **recorded pending toughs** (balloon + strike
position), a **pending-discharge timer** (the `DischargeDelay` countdown once the last tough is
detected), and the rainbow **charge count** — all reset per shot (a fresh `ProjectileModel`/`Flight` is
built per shot in `ThrowerController`, so no stale carry-over). The prediction lookahead is a stateless
query against the live board (`SlotGrid`) + walls. The Snipe `Speed` buff is ended by the discharge (its
end-condition already keys off `IsPiercing` going false — `PierceEndedEndCondition`, Phase 2).

## 9. Build order

1. **[SHIPPED]** `SnipeSettings` + wiring + drawer (Phase 1).
2. **[SHIPPED]** Grant path: arm `IsPiercing` + non-stacking Speed buff; decoupled pierce onto
   `IsPiercing` + true-base floor (Phase 2). *Superseded in part by the redesign — see 3a.*
3. **The discharge rework** (this phase, shared cruise+Snipe pierce):
   - **3a.** `ProjectileHitResolver`: a piercing hit on a hits>1 balloon **records** it (balloon +
     strike position) and passes through **without popping**; normal balloons pop as before. Remove
     the per-tough `CruisePierceSpeedScale *= 0.5`. Drop `ToughHitSpeedFalloff` + `LineClearHalfWidth`
     from config + drawer. Add the pending-tough list + charge count to `ProjectileFlightState`.
   - **3b.** Prediction: on a hits>1 contact, a trajectory lookahead (`ShotSimulator`/`ShotBoardGather`/
     `PathTrace`) reports whether another hits>1 lies ahead; if not, start the `DischargeDelay` timer.
   - **3c.** Discharge (driven from the flight tick / motion resolver): on timer elapse **or** on
     shot-end flush — slow once to true base, dispatch piercing pops for the recorded toughs, end the
     pierce. (VFX + slow-mo are Phase 5.)
   - EditMode tests for: tough plow records-not-pops, prediction "last tough" detection, discharge
     pops the recorded set, and the shields-exhausted flush.
4. Rainbow: charge = recorded-tough count; discharge bloom (colorable-only radius convert); in-flight
   neighbor conversion via the `RainbowShield` path.
5. Feel: lights + disturbance beats (§6) + the per-tough pop VFX over strike positions + the discharge
   slow-mo dip.
6. Playtest in-editor — the punch-through-then-shatter beat, prediction correctness, discharge feel,
   bloom scaling, and all VFX/light beats need a runtime pass (`dotnet build` can't validate behavior).

## 10. Open tuning (deferred to playtest)

- `SpeedBuffMultiplier` and `DischargeDelay` — how fast the lance reads and how long the beat between
  punch-through and shatter should be.
- `DischargeTimeScale` / `DischargeTimeScaleDuration` — how heavy the slow-mo should feel (or off).
- `BloomRadiusPerCharge` / `BloomRadiusCap` — how explosive a big-charge rainbow discharge reads without
  trivializing the board.
- Prediction horizon — how far ahead the lookahead scans for the next tough (whole remaining path vs. a
  bounded window), and how that feels when toughs are spread across bounces.
