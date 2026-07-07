@page plan_rainbow_balloon Rainbow Balloon

# Rainbow Balloon

> A new "rainbow star" balloon: a **wildcard** that is colour-agnostic to pop, scores into
> **every currently-allowed colour** at once, and — unlike the scatter balloons (Tough,
> BubbleCluster) — **carries the streak** instead of breaking it. Its render layer (the
> banded shader) is already built; this plan is the gameplay integration plus one shared
> new lever (per-type item-activation weight).

---

## Orientation — start here

The core loop: popping is **not** colour-gated (`ProjectileHitResolver.Resolve` pops on
durability via `EvaluateHit`, not colour). Colour drives three things on a pop — **colour-steal**
(projectile absorbs the popped balloon's colour), **streak/shields** (`ColorStreakTracker`), and
**scoring** (per-colour `ScoreAttribution`). The rainbow is a deliberate carve-out on all three.

**Already done (render layer):**
- `Shaders/BalloonParty/Balloon/RainbowBalloon.shader` — scrolling diagonal colour bands over the
  balloon's shine + drop shadow. Up to four selectable colours (`_Color0.._Color3`) + `_BandCount`,
  meant to be driven at runtime from the level's allowed colours. `_StripeCount`/`_ScrollSpeed`/
  `_BandBlend`/`_BandAngle`/`_TimeOffset` tune it. Per-instance props via `MaterialPropertyBlock`;
  GPU instancing disabled.
- `Materials/Balloon/BalloonRainbow.mat` (points at the shader, seeded with the 4 palette colours).
- `Prefabs/Balloon/BalloonRainbow.prefab` — a variant of `Balloon.prefab` (inherits its full setup).
- ⚠️ The shader is **unverified** — `dotnet build` never compiles shaders. Needs an in-editor pass.

**Design decisions (locked with the designer):**
- Mechanic = **A + C**: universal wildcard (A) that contributes to **all** allowed colours (C).
- **Item-capable** (uses `BalloonModel`'s item slot), but items should be **rare** on it — see the
  new item-activation-weight lever below.
- **Streak insurance**: a rainbow pop does not break the streak and **grants a shield** (streak ≥ 2).
- **Scoring**: the **current streak colour** gets its full, streak-multiplied reward; every **other**
  allowed colour gets `scoreValue × spillover`, where **spillover is a config tuning knob** (`< 1`,
  no baked default — authored in the editor).
- **Paint → rainbow**: the Paint item **converts** splashed balloons into rainbow instead of
  recolouring them (a new system interaction — the hard part).

---

## Why the scatter balloons are not a precedent

Tough and BubbleCluster also emit multi-colour score, but they **break the streak on purpose**:
every `ScoreAttribution` they add passes `breaksStreak: true` (→ `ColorStreakTracker.Record` calls
`Reset()`), and being multi-attribution they also hit the `Record(null, true)` group-break path in
`ScoreController.RecordStreakMultiplier`. Neither is `IHasColor`, so a scatter pop also never steals
the projectile's colour nor grants a shield. The rainbow wants the **opposite** on the streak, so it
needs explicit handling — it does not fall out of the existing model.

**Critical:** for a multi-attribution group, `RecordStreakMultiplier` takes the `Record(null, true)`
branch and **ignores the per-attribution `breaksStreak` flag entirely**. So setting
`breaksStreak: false` on the rainbow's attributions does nothing on its own — the surgical fix has to
be in `RecordStreakMultiplier`.

---

## Part A — Scoring: full-to-current, spillover-to-others, streak carries

`DamageContext` already carries the projectile's current colour (`ProjectileHitResolver` passes
`projectile.ColorName.Value` into `EvaluateHit`), so the rainbow knows the streak colour at
attribution time.

1. **`ScoreAttribution`** (`Slots/Capabilities/`, a 3-field readonly struct today) gains a
   `bool IsPrimary` (default `false`) marking the streak-anchor colour of a wildcard group.
2. **Rainbow score attribution** emits one entry per currently-allowed colour:
   - `context.ColorName` → `(colour, scoreValue, breaksStreak: false, isPrimary: true)`;
   - every other allowed colour → `(colour, round(scoreValue × spillover), breaksStreak: false)`.
   - Edge case: if `context.ColorName` isn't in the allowed set (rare), fall back to marking the
     first emitted colour primary so the group still anchors a streak.
3. **`ScoreController.RecordStreakMultiplier`**: if a multi-attribution group has exactly one
   `IsPrimary` entry → `Record(primary.ColorId, primary.BreaksStreak)` (continues/grows the streak,
   returns multiplier `M`) instead of the `null`/break path. Groups with no primary keep breaking —
   unchanged, so Tough/Cluster are untouched.
4. Net, with the existing uniform group multiplier: current colour = `scoreValue × M`, each other =
   `round(scoreValue × spillover) × M`. Current wins; others are attenuated by `spillover`.
   - Open sub-decision (defer to feel): whether `M` should boost the spillover colours too (simplest,
     current behaviour) or only the current colour (stronger separation, needs a per-attribution
     multiplier). Ship simplest first.

## Part B — Wildcard identity: colour-steal, shield, IHasColor

- The rainbow is **not `IHasColor`** (like the scatter balloons). Consequence: colour-steal is
  skipped (`ProjectileHitResolver` line ~47 is gated on `balloon is IHasColor`), so the projectile
  **keeps its colour** — which is what preserves the streak colour.
- **Shield relax**: the shield grant (`ProjectileHitResolver` line ~59) is gated on the same
  `balloon is IHasColor`. Relax it so a wildcard/rainbow pop also qualifies; the
  `streak ≥ 2 && LastColor == projectile.ColorName` condition still holds because Part A recorded the
  streak against the projectile's colour. Introduce a narrow signal for "this pop was a wildcard"
  (e.g. a capability marker on the model) rather than broadening the colour guard loosely.

## Part C — Rainbow as a *mode*, shared by spawn and Paint-conversion

The Paint item today writes `IPaintable.Color.Value`; converting a live, pooled balloon to a
different *type* is new. Chosen approach: **rainbow is a behavioural mode of a colourable balloon**,
not a separate model class.

- Add a rainbow-mode capability/flag to `BalloonModel` (the only `IPaintable`). When set it:
  overrides score attribution (Part A), reports itself as a wildcard for the streak/shield path
  (Part B), and signals the view to swap the SpriteRenderer material to `BalloonRainbow.mat` and
  start pushing the band `MaterialPropertyBlock` (Part E).
- **Spawned `BalloonType.Rainbow`** = a colourable balloon that starts in rainbow mode (set by its
  variant at `Initialize`).
- **Paint → rainbow** = flip the flag on the already-spawned `BalloonModel` the splash touches. No
  pool churn; spawned and painted rainbows share one implementation.
- Factor the mode behaviour behind a small strategy if the `BalloonModel` branch grows.
- Interaction to define during build: what a Paint splash whose source is *itself* a rainbow does
  (recommend: still converts neighbours, no infinite recolour since target is already rainbow).

## Part D — Per-type item-activation weight (shared lever)

Independent of the rainbow, but needed by it: items are assigned uniformly among eligible balloons
today (`ItemAssigner.AssignItems` uses `Random.Range(0, eligibleBuffer.Count)`), so Simple/Silver/Gold
all host items at the same rate. (Confirmed: Silver & Gold are item-capable — both map to
`BalloonModel`, and item visuals bind at runtime for any `IHasItemSlot` in `BalloonView`.)

- Add `_itemActivationWeight` (float, default `1`) to `BalloonPrefabEntry`: *relative chance this
  balloon is chosen to host an item when items are granted; 0 = never.*
- Carry it onto the spawned model via `BalloonModelConfig.From(entry)` (per-instance, so no ambiguity
  when multiple prefab entries share a type), exposed on the item-slot capability.
- `ItemAssigner`: filter weight-0 balloons out of `CollectEligibleSlots`, and replace the uniform
  host pick with a **weighted pick** reusing `Shared/Extensions/WeightedPickExtensions`.
- Tuning outcome: Rainbow gets a low-but-nonzero weight (rare item host); Silver/Gold can be dialed
  down from 1 if desired; plain Simple stays 1.

## Part E — Programmatic band colours from the allowed set

- New `RainbowBalloonVariant` (mirrors `ColorableBalloonVariant`/`ToughBalloonVariant`): on init and
  on `ScoreLevelUpMessage`, read `IActiveLevelParameters.Current.AllowedColors`, resolve each via
  `IGamePalette.GetColor`, and push `_Color0..N` + `_BandCount` (+ a random `_TimeOffset`) through a
  `MaterialPropertyBlock`. `BalloonFactory` already threads the allowed-colour mask to the variant.
- Result: the balloon literally **grows a band as colours unlock** (2→3→4) — a diegetic
  tutorialisation cue.

## Part F — Type/factory/config wiring

- `BalloonType` enum gains `Rainbow` (append-only — serialized indices must not shift).
- `BalloonModelFactory` adds a `Rainbow` case (a `BalloonModel` started in rainbow mode).
- `BalloonsConfiguration.asset` gains a Rainbow entry (prefab, weight, maxCount, scoreValue,
  low `_itemActivationWeight`), gated into level ranges via the existing `BalloonTypeWeight[]` —
  likely only once ≥ 2 colours are allowed (a wildcard is meaningless at 1 colour), which also paces
  its introduction.

---

## Phasing

1. **D — item-activation weight** (self-contained, testable, unblocks rare-item tuning). Reuse
   `WeightedPickExtensions`; add `ItemAssigner` coverage for the weighted/zero-weight paths.
2. **A + B — scoring + wildcard streak/shield** (`ScoreAttribution.IsPrimary`,
   `RecordStreakMultiplier`, shield relax). EditMode-testable with substituted seams — this is the
   correctness-critical core; guard Tough/Cluster don't regress (they must still break).
3. **C + E — rainbow mode + variant + band push** (spawned rainbows first).
4. **F — enum/factory/config wiring**; spawn it, tune weights/ranges.
5. **Paint → rainbow conversion** (the new interaction) last, once the mode exists.

## Testability & verification

- EditMode covers Parts A/B/D (pure logic, substituted seams): streak carries on a primary group,
  Tough/Cluster still break, spillover maths, weighted host selection, zero-weight exclusion.
- **In-editor only** (flag it): the shader, the band `MaterialPropertyBlock` push, the material swap
  on Paint-conversion, and overall feel. `dotnet build` compiles none of the visual layer.

## Open decisions (deferred to build/feel)

- Spillover value — **tuning knob**, authored in config (no baked default).
- Rainbow base `scoreValue` — a higher value (≈4–5) makes spillover rounding smooth and the pop feel
  chunky.
- Whether the streak multiplier `M` boosts spillover colours (simplest) or only the current colour.
- Paint-source-is-rainbow behaviour.
