@page plan_snipe Snipe — the armor-piercing tracer

# Snipe — an armor-piercing lance that spends itself cracking the tough line

> Snipe is the game's **anti-armor tool**. It arms the projectile with the engine's existing
> **piercing** state on demand (today only *earned* via a long cruise) and adds a single, non-stacking
> **speed buff**. Piercing is one shared effect reached two ways (cruise or Snipe): the shot pops soft
> balloons on contact but **plows through tough/unbreakable ones at full speed without popping them**,
> recording each. Once no new tough plow arrives for a short **quiet beat**, the run is over and the
> shot **discharges** — slowing once to base speed and **shattering all the recorded toughs at once**,
> VFX blooming over each strike. Punch through the armored line at speed, then crack the whole line on
> the way out. A **rainbow** holder turns each plowed tough into stored **charge**, then discharges a
> **colorable-only** rainbow bloom scaled by how much armor it ate — you can't paint armor, but
> shattering it powers the conversion of everything soft around it.

---

## 1. Where this sits today

`Snipe` is a shipped `ItemType` (`Assets/Source/Configuration/Items/ItemType.cs:11`). `SnipeItemHandler`
(`Assets/Source/Item/Snipe/SnipeItemHandler.cs`) arms `IsPiercing` and grants a single, non-stacking
multiplicative `ProjectileBuffId.Speed` buff (value from its own `SnipeSettings.SpeedBuffMultiplier`),
plus the shared `RainbowShield` buff on a rainbow-balloon host — both ended by `PierceEndedEndCondition`
once the pierce ends. The plow-then-shatter mechanic below (§2–§5) is fully built and shared with
cruise-earned piercing — a cruise pierce and a Snipe lance behave identically once armed:

| Rail | Where | What it does |
|---|---|---|
| Piercing plow | `IProjectileModel.IsPiercing`, `ProjectileHitResolver.Resolve` | A piercing hit on a hits>1 (tough/unbreakable) balloon records it into `ProjectileFlightState.PendingPierceHits` instead of popping it, at full speed — no per-hit slowdown |
| Discharge debounce | `ProjectileMotionResolver.TickPierceDischarge`, `IGameConfiguration.PierceDischargeDelay` | Each plow (re-)arms a countdown; once idle for `PierceDischargeDelay`, the pierce (and any riding buffs) end and the shot drops to base speed |
| Discharge shatter | `ProjectileHitResolver.DischargePending` (called from `ProjectileView` on discharge, or on shot death with toughs still pending) | Pops every recorded tough at its strike position; a rainbow-buffed plow also blooms a colour conversion (`BloomConvert`) |
| Buff lifecycle | `IProjectileBuffs`, `ProjectileBuffService`, `PierceEndedEndCondition` | Applies a buff to the active projectile and drops it once `IsPiercing` goes false |
| Neighbor conversion | `ProjectileHitResolver.ConvertNeighborsToRainbow` | Converts `IPaintable` neighbors of a pop to rainbow — **skips non-paintable** toughs/unbreakables |

Snipe is a **second grant path** onto this shared pierce/discharge mechanic plus its signature payoff,
not a separate subsystem.

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
- **Discharge debounce.** Each hits>1 plow (re-)arms a short countdown
  (`IGameConfiguration.PierceDischargeDelay`) instead of predicting ahead on the board — a run of toughs
  keeps re-arming it, so the shot stays fast and the discharge only fires once it's gone quiet on toughs
  for the full delay.

## 3. The discharge — shortly after the last tough

Each hits>1 plow (re-)arms a countdown (`IGameConfiguration.PierceDischargeDelay`, tunable for feel).
Once no new tough plow arrives before it elapses, the just-struck balloon was the **last** of the run and
the shot **discharges**:

1. **Slows once to true (un-buffed) base speed** — a single drop, not cumulative. It has punched
   clear of the armor and now coasts.
2. **Pops every recorded hits>1 balloon** (piercing kill — unbreakables included), each at its recorded
   hit position — the normal pop VFX plays there, so the shatter reads as a line, not a single point.
3. The pierce (`IsPiercing`) ends, and — on the Snipe path — the `Speed` buff (and, on a rainbow host,
   the `RainbowShield` buff) end with it.

The shot punches through the armored line *at speed*, then, once clear, slows and **shatters the whole
line at once**. The "shatter after the punch-through" beat — not a per-hit grind — is the payoff.

**Edge cases:**

- **No toughs ever hit** → nothing recorded, no discharge. The shot just pierces normals until it runs
  out of shields (or the cruise ends). Simpler than the old "line-clear on any death."
- **Shot ends (shields exhausted / despawn) with toughs still pending** (e.g. it died mid-armored-run
  before the countdown elapsed) → it **flushes** the pending pops then, so recorded toughs are never
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

**Not yet shipped.** The plan is to reuse the item light/disturbance seams (`SceneLightFieldService`,
`DisturbanceFieldService.Stamp`; see `Item/README.md` "Lights Cast by Items"):

| Beat | Light | Disturbance |
|---|---|---|
| In flight | Moving **capsule light** along the lance (laser-beam-light style); bright and tight | — |
| Each tough plow | Small **light spark** at the strike point | Short **inward** stamp — punching the armor thumps its neighbors |
| Discharge | **Point-light flash** scaled by charge; rainbow cycles the palette via `ColorCycle` | Outward **shockwave** stamped at the discharge point |

`SnipeSettings` already carries `TracerLightHalfWidth`/`TracerLightIntensity` (flight) and
`DischargeLightIntensity`/`LightFallbackSeconds` (discharge) for this, but nothing reads them yet. The
discharge slow-mo time-scale dip originally planned for weight was dropped in favor of the debounce
approach (§3) and isn't part of this beat.

## 7. Config — `SnipeSettings`

`[Serializable] SnipeSettings` on `ItemSettings` (mirroring `BombSettings`/`LaserSettings`;
`Assets/Source/Configuration/Items/ItemSettings.cs`). `ItemSettingsDrawer` hides `Damage` for Snipe
(non-damaging item).

| Field | Purpose | Shipped value |
|---|---|---|
| `SpeedBuffMultiplier` | Initial speed buff (multiplicative, non-stacking) | 1.6 |
| `ChargePerToughHit` | Rainbow charge per recorded tough | 1 |
| `BloomBaseRadius` | Rainbow bloom radius at charge 0 | 1 |
| `BloomRadiusPerCharge` | Bloom radius growth per charge | 0.5 |
| `BloomRadiusCap` | Hard cap so the bloom never eats the whole board | 4 |
| `ColorCycles` | Rainbow iridescence cycles over flight | 2 |
| `TracerLightHalfWidth` / `TracerLightIntensity` / `DischargeLightIntensity` / `LightFallbackSeconds` | Declared for §6, not yet wired | — |

`SpeedBuffMultiplier` supersedes the old reliance on `IBuffConfiguration.GetValue(Speed)` for Snipe.
**Removed by the redesign:** `ToughHitSpeedFalloff` and `LineClearHalfWidth` (no per-tough decay, no
spatial sweep). The discharge delay is **not** a Snipe-specific field — it's the shared
`IGameConfiguration.PierceDischargeDelay`, since cruise-earned piercing discharges the same way.

> **Perf note:** the flight/light runtime would read `SnipeSettings` fields per-frame once §6 lands.
> Snapshot needed values at activation — do **not** walk the `IItemConfiguration.Snipe.X` chain each
> frame.

## 8. State ownership

Handlers are singletons and activations overlap (`Item/README.md`), so **no per-activation state on the
handler**. Piercing lives on the projectile (`IsPiercing`); per-shot pierce bookkeeping lives on
`ProjectileFlightState`: the **recorded pending toughs** (`PendingPierceHits` — balloon + strike
position; its count doubles as the rainbow charge), the discharge debounce (`DischargeArmed` /
`DischargeCountdown`), and whether the pierce was rainbow-buffed when it was armed (`PierceWasRainbow`,
captured at plow time since the discharge drops the buff before it resolves) — all reset per shot (a
fresh `ProjectileModel`/`Flight` is built per shot in `ThrowerController`, so no stale carry-over). The
Snipe `Speed` (and rainbow-host `RainbowShield`) buff is ended by the discharge — its end-condition keys
off `IsPiercing` going false (`PierceEndedEndCondition`, Phase 2).

## 9. Build order

1. **[SHIPPED]** `SnipeSettings` + wiring + drawer (Phase 1).
2. **[SHIPPED]** Grant path: arm `IsPiercing` + non-stacking Speed buff; decoupled pierce onto
   `IsPiercing` + true-base floor (Phase 2). *Superseded in part by the redesign — see 3.*
3. **[SHIPPED]** The discharge rework (shared cruise+Snipe pierce):
   - `ProjectileHitResolver`: a piercing hit on a hits>1 balloon **records** it (balloon + strike
     position) and passes through **without popping**; normal balloons pop as before. The per-tough
     `CruisePierceSpeedScale *= 0.5` decay, `ToughHitSpeedFalloff`, and `LineClearHalfWidth` are gone.
   - No trajectory lookahead was needed: `ProjectileMotionResolver.TickPierceDischarge` debounces off
     `IGameConfiguration.PierceDischargeDelay` instead — each plow re-arms the countdown, so a run of
     toughs holds it open and it only fires once idle for the full delay.
   - Discharge (`ProjectileHitResolver.DischargePending`, driven from `ProjectileView` on countdown
     elapse or shot-end flush): slows once to true base, dispatches piercing pops for the recorded
     toughs, ends the pierce.
   - EditMode tests cover: tough plow records-not-pops, discharge debounce re-arming, discharge pops
     the recorded set, and the shields-exhausted flush (`ProjectileHitResolverTests`,
     `ProjectileMotionResolverTests`).
4. **[SHIPPED]** Rainbow: charge = recorded-tough count; discharge bloom (colorable-only radius
   convert, `BloomConvert`); in-flight neighbor conversion via the `RainbowShield` path.
5. **Not yet shipped.** Feel: lights + disturbance beats (§6). The discharge slow-mo dip planned here
   was dropped in favor of the debounce approach and is no longer part of the plan.
6. Playtest in-editor — the punch-through-then-shatter beat, discharge feel, and bloom scaling have
   been playtested (Phases 1–4); the deferred lights/disturbance beats (Phase 5) still need a pass once
   built (`dotnet build` can't validate behavior).

## 10. Open tuning (deferred to playtest)

- `SpeedBuffMultiplier` — how fast the lance reads.
- `IGameConfiguration.PierceDischargeDelay` — how long the beat between punch-through and shatter
  should be (shared with cruise, so tuning it affects both paths).
- `BloomRadiusPerCharge` / `BloomRadiusCap` — how explosive a big-charge rainbow discharge reads without
  trivializing the board.
