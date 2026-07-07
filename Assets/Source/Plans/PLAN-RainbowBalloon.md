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
  balloon's shine sweep (no drop shadow — removed 2026-07-07, this shader never wants one). Up to four
  selectable colours (`_Color0.._Color3`) + `_BandCount`, meant to be driven at runtime from the
  level's allowed colours. `_StripeCount`/`_ScrollSpeed`/`_BandBlend`/`_BandAngle`/`_TimeOffset` tune
  it. `_MaskMin`/`_MaskMax` (a UV-space rectangle, tuned by eye in the Inspector) + `_MaskSoftness`
  exclude a region — e.g. the balloon's knot — from the band tint, so it isn't cut across
  inconsistently by the scroll; zero-size rect (default) disables the mask. `_ShineAngle` (its own
  turns-based angle, added 2026-07-07) independently tunes the shine sweep's direction, sharing the
  same rotation helper as `_BandAngle`. `_SpriteScale` (only ever a shadow-margin leftover) was removed
  once the shadow was. Also gained a **glitter layer** (added 2026-07-07) — a grid-hash sparkle field
  (`_GlitterDensity`/`_GlitterSize`/`_GlitterChance`/`_GlitterSpeed`/`_GlitterSharpness`/
  `_GlitterBrightness`, no texture) additive on top of the shine sweep, since a single smooth sweep
  didn't read as "glittery." Per-instance props via `MaterialPropertyBlock`; GPU instancing disabled.
- `Materials/Balloon/BalloonRainbow.mat` (points at the shader, seeded with the 4 palette colours).
- `Prefabs/Balloon/BalloonRainbow.prefab` — a variant of `Balloon.prefab` (inherits its full setup).
- ✅ **Confirmed rendering correctly in-editor** (2026-07-07, via a direct material preview) — the bands,
  colours, and shine all show up as designed.

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

> **Future idea (out of scope here, noted for later):** José wants Tough to eventually **not break the
> streak** either. The `IsPrimary` mechanism built for the rainbow (Phase 2) is directly reusable:
> `ToughBalloonModel.ResolveScoreAttribution` (`ToughBalloonModel.cs:32-45`) would mark one of its
> scattered attributions `isPrimary: true` (e.g. against `context.SourceColorId`, same as the rainbow)
> and set `breaksStreak: false` on all of them, and `RecordStreakMultiplier` would carry the streak
> through unchanged — no further core-scoring changes needed. Left undone for now because it's a
> distinct balance/feel decision, not a rainbow requirement; worth its own small task when picked up.

---

> **Phase task lists below were verified against the source 2026-07-07** (four implementation-read
> passes). ⚠️ marks a caveat/gap the source reading surfaced; **[in-editor]** marks work `dotnet
> build` can't verify (shaders, `.asset`/prefab edits, MPB visuals).

## ⚠️ Superseding refactor — sentinel colour, not a bool flag (2026-07-07)

The `IsRainbow` bool (and the `IHasRainbowMode`/`IHasWriteableRainbowMode` capability pair) **were
removed** after the first implementation landed. A rainbow balloon carrying a *concrete* spawn colour
leaked that colour into item/colour interactions when it logically has "all colours." The fix makes
the **colour itself** the single source of truth:

- `GamePalette.RainbowColorId` (a reserved id, `"__rainbow__"`, never a palette entry) marks rainbow
  identity. `IGamePalette.IsRainbow(colorId)` is the detection method; `GetColor(RainbowColorId)`
  returns `Color.white` as a neutral fallback for consumers that don't special-case it.
- `RainbowBalloonVariant.Initialize` skips base colour-picking and sets `Color.Value = RainbowColorId`.
- Every branch that read `IsRainbow.Value` now compares the colour (`ProjectileHitResolver`,
  `BalloonModel.ResolveScoreAttribution`, `BalloonView.ApplyColorMode`).
- **Paint collapsed to a single recolour path**: a rainbow holder's colour *is* the wildcard id, so
  recolouring targets to it converts them — `ConvertToRainbow`/`spreadsRainbow` were deleted.
- **Rainbow item synergies specced/built**: Lightning on a rainbow holder chains through *every*
  colour (drops the same-colour filter); Bomb on a rainbow holder piercing-kills all colours within
  `Radius` and converts the ring beyond it to rainbow mid-effect — `RainbowConversionRange` (outer-ring
  width, 0 disables) applied at half the effect duration, plus a visual-only `RainbowEffectScale` on the
  effect transform. Laser's rainbow behaviour is open.
- ⚠️ Still undefined-for-now (documented, deferred): the *scoring attribution* of a rainbow-held
  Bomb/Laser/Lightning credits the sentinel colour, which drops out of concrete-colour scoring.
  Targeting/blast behaviour is defined; only the point payout on those paths is unspecced.

The rest of this plan below describes the original flag-based build for history; read the sentinel
model as authoritative where they conflict.

## Cross-phase corrections (fold these into every part)

- **Rainbow identity is the reserved colour id** (was: the `IsRainbow` flag). It is *simultaneously*
  Part A's score-override switch, Part B's wildcard marker, Part C's view-mode signal, and Part 5's
  Paint-conversion target — all read off `Color.Value == GamePalette.RainbowColorId`. Because `Color`
  is a `ReactiveProperty<string>`, a mid-life Paint convert re-derives the view from one subscription.
- **Rainbow *is* `IHasColor`** (Part B's original "not `IHasColor`" premise is wrong once Part C makes
  it a `BalloonModel`, which implements `IPaintable : IHasColor`). Do **not** try to drop the
  interface or empty its colour. Instead gate colour-steal *out* via the sentinel-colour check at the
  guard site; the shield then fires naturally (see Phase 2).
- **`DamageContext.SourceColorId`** is the projectile's colour at pop time (set from
  `projectile.ColorName.Value` at `ProjectileHitResolver.cs:35`). The plan's earlier `context.ColorName`
  was a wrong field name — there is no `ColorName` on the context.
- **`BalloonPrefabEntry` + `BalloonModelConfig` gain two fields** (`_spillover` from Phase 2,
  `_itemActivationWeight` from Phase 1) via the same `ScoreValue`-style plumbing — do them together.
  ⚠️ **Serialized-default gotcha:** adding a field to the `[Serializable]` `BalloonPrefabEntry` may
  deserialize *existing* asset entries as `0`, silently making every balloon spillover-0 /
  never-item-host. Verify in-editor that existing entries read the intended default and re-save.

---

## Design alternative considered — pool replacement (rejected 2026-07-07)

Evaluated making rainbow a **distinct pooled `BalloonType.Rainbow` + `RainbowBalloonModel`** (spawned
as a normal type; Paint converts by despawn+respawn) instead of a mode/state on `BalloonModel`.
Source-verified both halves; **kept the mode-flag.** Why:

- **The spawn-side win collapses under item-capability.** A distinct model's apparent wins (colour-steal
  skips for free, no runtime material swap, no `SetColor`-suppression) all require it to **not** be
  `IHasColor`. But `IHasWriteableItemSlot : IHasItemSlot : IHasColor` (item visuals tint to the host
  colour, and `BalloonView.Bind` reads `itemSlot.Color`), so an **item-capable** rainbow *must* be
  `IHasColor` — collapsing every win, forcing the model to re-implement most of `BalloonModel`, and
  additionally **breaking the shield guard** (`balloon is IHasColor` now fails → needs its own relax).
  `IsPrimary` + the `RecordStreakMultiplier` branch are required identically in both approaches.
- **The Paint side is much harder.** `PaintItemHandler` is a leaf (`IPaintable` only); replacement needs
  three new cross-layer deps (`BalloonFactory`/`BalloonControllerRegistry`/`IBalloonsConfiguration`),
  a silent-remove API and a no-animation-spawn path (neither exists — both must be written), and it
  **loses hosted items, durability, in-flight motion, and can dangle a projectile's `LastHitBalloon`**;
  it also corrupts `_activeCounts` caps and risks `SlotGrid.Place`-throws-on-occupied across an async
  multi-target splash. The flag flip preserves the same model reference and all of it for one `.Value = true`.
- **Only upside** — a real `Rainbow` `TypeName` gives correct cap-counting for *spawned* rainbows. Minor,
  and the flag's cap-bypass is a *conversion-only* issue already accepted below.

Pool replacement would only be simpler in a reduced scope: rainbow **not** item-capable **and** no Paint
conversion. Both are locked in, so the mode-flag is the better fit for the full feature.

---

## Phase 0 — Foundation: the rainbow-mode flag (blocks 2/3/5) — ✅ DONE, then ♻️ SUPERSEDED 2026-07-07

> ♻️ **Superseded** by the sentinel-colour refactor above — the flag and both capability interfaces
> were deleted. Retained below for history.

**Task 0.1 — `IsRainbow` flag + read capability.** ✅ Done: `IHasRainbowMode`/`IHasWriteableRainbowMode`
added to `Slots/Capabilities/` (mirroring the `IHasColor`/`IPaintable` read/write pair); `BalloonModel`
implements `IHasWriteableRainbowMode` with `ReactiveProperty<bool> IsRainbow { get; } = new(false)` +
the explicit `IHasRainbowMode.IsRainbow` read-only forward. All 5 assemblies build clean.
- Files: `Balloon/Model/BalloonModel.cs`; new `Slots/Capabilities/IHasRainbowMode.cs`
  (`IReadOnlyReactiveProperty<bool> IsRainbow`), plus a writeable setter reachable by Paint (either a
  writeable capability or expose on `IPaintable`).
- How: `public ReactiveProperty<bool> IsRainbow { get; } = new(false)` on `BalloonModel`; expose
  read-only via `IHasRainbowMode`. Default `false` ⇒ Simple/Silver/Gold (same class) unaffected.
- Interacts with: Phase 2 (`ResolveScoreAttribution` branches on it), Phase 3 (view subscribes),
  Phase 5 (Paint sets it). ⚠️ Keep it reactive — a one-time `Bind`-time check can't see a mid-life flip.

---

## Phase 1 (Part D) — Per-type item-activation weight — ✅ DONE 2026-07-07 (incl. [in-editor])

✅ Tasks 1-5 done: `_itemActivationWeight` + `_spillover` on `BalloonPrefabEntry`, threaded through
`BalloonModelConfig` into `BalloonModel`; `IHasItemSlot.ItemActivationWeight`; `ItemAssigner` excludes
zero-weight hosts and uses the new `PickWeightedIndex` (cumulative-weight scan, mirrors `SampleCount`'s
testable-static shape — `WeightedPickExtensions.PickRandom` didn't fit, per the plan's caveat). Tests
added: zero-weight never eligible, `PickWeightedIndex` unit tests (all-zero → -1, even split picks by
roll, zero-then-nonzero always picks nonzero). All 5 assemblies build clean; full `style_audit.py`
clean (one advisory `repeated-accessor` WARN on `BalloonModelConfig.From` reading 6 fields off `entry`
— intentional: the struct is deliberately decoupled from `BalloonPrefabEntry`/SO for test construction).
✅ **[in-editor] done:** `BalloonsConfiguration.asset` weights set — Simple 1, Silver 0.3, Gold 0.15,
Tough/Cluster/Unbreakable 0 (items only ever go on colourable balloons). Rainbow's own weight is set
when its catalog entry is added (Phase 4).

Items are assigned **uniformly** today (`ItemAssigner.AssignItems`, `Random.Range(0, eligibleBuffer.Count)`
at `:103`), so Simple/Silver/Gold host at the same rate. (Confirmed item-capable: both map to
`BalloonModel`; item visuals bind for any `IHasItemSlot` in `BalloonView`.)

1. **`_itemActivationWeight` on `BalloonPrefabEntry`** (`Configuration/Balloons/BalloonPrefabEntry.cs`)
   — `[SerializeField] private float _itemActivationWeight = 1f` + getter. *Why:* per-type authoring
   lever. ⚠️ serialized-default gotcha (above).
2. **Thread through `BalloonModelConfig`** (`Balloon/Model/BalloonModelConfig.cs`) — add field +
   trailing optional ctor param (`= 1f`, keeps all call sites compiling) + `entry.ItemActivationWeight`
   in `.From`. *Why:* per-instance carrier (no per-type lookup in `ItemAssigner`).
3. **Expose on `IHasItemSlot`** (`Slots/Capabilities/IHasItemSlot.cs` — read side) + implement on
   `BalloonModel`. *Why:* `ItemAssigner` only sees the model as `IHasWriteableItemSlot`/`IHasItemSlot`.
4. **Filter + weighted pick in `ItemAssigner`** (`Item/ItemAssigner.cs`) — add `&& slot.ItemActivationWeight > 0f`
   to `CollectEligibleSlots` (`:158`); replace the uniform `Random.Range` host pick with a weighted
   **index** draw. ⚠️ **`WeightedPickExtensions.PickRandom` cannot be reused directly** — it requires
   `T : IWeightedEntry` + a `PoolKey`/`activeCounts` cap protocol and returns the *entry*, but the
   multi-grant loop needs the *index* for its `RemoveAt(indexOf)` (`:105`). Write a small inline
   cumulative-weight scan mirroring `WeightedPickExtensions.cs:36-48` (guard `total <= 0`), ideally as
   `internal static int PickWeightedIndex(...)` taking a roll — mirroring `SampleCount` so tests can
   call it directly.
5. **Tests + asset** — `ItemAssignerTests`: zero-weight never assigned; weight-1 beats weight-0 at
   `count=1`; unit-test the extracted helper with a seeded roll. **[in-editor]** set real weights in
   `BalloonsConfiguration.asset` (Rainbow low, Simple 1).

⚠️ **Converted rainbows keep their spawn entry's item-weight** (see Phase 5) — this lever only tunes
*spawned* rainbows.

---

## Phase 2 (Parts A+B) — Scoring + wildcard streak/shield — ✅ DONE 2026-07-07

**Why the scatter balloons aren't a precedent** (above) still holds: `RecordStreakMultiplier`
(`ScoreController.cs:118-127`) hard-breaks on *any* multi-attribution group and **ignores** the
per-attribution `breaksStreak` flag — so the fix must live there.

✅ Tasks 1-6 all done, split into 3 commits (streak carve-out → model scoring → resolver gate).
One correction found while implementing: `BalloonModelFactory` now threads palette+allowedColors to
**every** `BalloonModel` branch (Simple/Silver/Gold too, not just Tough/Cluster) — a plain balloon can
be converted to rainbow by Paint later (Phase 5) and needs its own colour pool at that point; it's
never re-threaded after spawn. Full traced-by-hand verification (no Unity Test Runner available here)
confirmed the streak-growth and spillover math end-to-end. All 5 assemblies build clean; full
`style_audit.py` clean (same pre-existing advisory `repeated-accessor` WARN as Phase 1, unchanged).

1. **`IsPrimary` on `ScoreAttribution`** (`Slots/Capabilities/ScoreAttribution.cs`) — add `bool IsPrimary`
   (trailing ctor param, default `false`). Tough/Cluster/plain-`BalloonModel` never set it → their
   groups stay "no primary" and keep breaking.
2. **Branch `RecordStreakMultiplier`** (`ScoreController.cs:118`) — if a group has exactly one
   `IsPrimary`, `return _streakTracker.Record(primary.ColorId, primary.BreaksStreak)` (grows the streak,
   returns `M`) instead of `Record(null, true)`. 0 or >1 primaries → keep the break path (regression-safe).
3. **Rainbow `ResolveScoreAttribution`** (on `BalloonModel`, branched by `IsRainbow.Value`; Phase 3
   supplies the allowed-colour list threaded like Tough/Cluster via `BalloonModelFactory.Create(entry,
   palette, allowedColors)`): emit primary `(SourceColorId, ScoreValue, breaksStreak:false, isPrimary:true)`
   + each other allowed colour `(colour, round(ScoreValue × spillover), breaksStreak:false)`.
   ⚠️ if `SourceColorId` isn't in the allowed set, mark the *first* emitted colour primary — else the
   group has no primary and Task 2 makes it **break** (silent regression). Skip zero-point spillover
   entries (they'd be filtered at `ScoreController.cs:137` anyway).
4. **Gate colour-steal + grant shield** (`Projectile/Controller/ProjectileHitResolver.cs:46-65`) —
   add a wildcard check (read `IsRainbow`) that **skips colour-steal** (`:47`) so the projectile keeps
   its colour (preserving the streak colour). The shield grant (`:59`) then fires *naturally*: rainbow
   *is* `IHasColor`, and `LastColor == projectile.ColorName.Value && streak ≥ 2` holds because Task 2
   recorded against the projectile's colour and no steal overwrote it. ⚠️ Load-bearing: this only works
   because the score stage runs synchronously *before* the shield check (`:55` dispatch, `:58` comment).
5. **Spillover knob** — `_spillover` on `BalloonPrefabEntry` → `BalloonModelConfig` (same plumbing as
   Phase 1; `[Range(0,1)]`, no baked default). ⚠️ *not* on `BalloonsConfiguration` (that's board-wide).
6. **Tests** (`ScoreControllerTests`, `ProjectileHitResolverTests`, rainbow-model test) — primary gets
   `ScoreValue×M`, others `round(ScoreValue×spillover)×M`, streak *grows* across consecutive rainbow
   pops; Tough/Cluster group still resets (regression); wildcard pop grants a shield and does **not**
   steal colour. Seams are fully substituted already (`ILevelProgress`, real `ColorStreakTracker`).

⚠️ **Uniform `M` also multiplies spillover** (`ResolveAttributions` applies one group multiplier to all
attributions) — so the primary/spillover *ratio* is constant across streak levels. This is the "ship
simplest" choice; "M boosts only the current colour" would need a per-attribution multiplier
(`ResolveAttributions` signature change) — deferred feel decision.
⚠️ **`ClaimProgress` caps per colour independently** (no cross-colour interference). Edge case: if the
primary colour is already at threshold, it grants 0 points but the streak still records (streak is
about the pop, not the points) — verify that's desired.

---

## Phase 3 (Parts C+E) — View mode + variant + programmatic bands — ✅ DONE 2026-07-07

The colourable body is a `SpriteRenderer` on the **"Body"** child using Unity's built-in `Sprites-Default`
material; `SpriteColorableRenderer.SetColor` just writes `Renderer.color` (no MPB). The rainbow shader
reuses the same sprite (`_MainTex` is `[PerRendererData]`).

✅ Tasks 1-6 done. Deviation from the sketch: rather than `BalloonView` hardcoding `BalloonRainbow.mat`,
it reads `IBalloonsConfiguration.RainbowMaterial` (a new config-authored field, mirroring
`DefaultPopVfxPrefab`) — avoids wiring a rainbow material onto every individual balloon prefab. Task 4's
subscriber-ordering risk was resolved by deferring the re-push one `UniTask.Yield()` frame. All 5
assemblies build clean; full `style_audit.py` clean. ✅ **Confirmed rendering correctly in-editor**
(bands/glitter/shine all show up as designed via a direct material preview) — followed by two shader
iterations on the same session (dropped the drop-shadow path entirely; added a tunable `_ShineAngle`
and a scattered-twinkle glitter layer). The Task-6 wiring is done and the mask rect ended up genuinely
used (tuned to a non-zero rectangle in the material), not just left inert.

⚠️ **Runtime bug found + fixed post-playtest:** `RainbowBalloonVariant` declared its own
`[Inject] private IGamePalette _palette` alongside `ColorableBalloonVariant`'s existing one, on the
(wrong) assumption that same-named private fields in a base/derived pair are harmless since they're
separate storage. VContainer's injector throws `VContainerException: Duplicate injection found for
field: ... _palette` — it doesn't tolerate two identically-named injectable fields across the
hierarchy, even though C# itself does. Fixed by widening the base field to
`[Inject] protected IGamePalette Palette` (PascalCase, no underscore — the one existing protected-field
precedent in this codebase is `EffectView.OnComplete`, same convention) and having the derived class
reuse it instead of shadowing. **Lesson for future variants:** don't re-declare an `[Inject]` field a
base class already has: dependency injection duplication across a MonoBehaviour hierarchy is a real
error, not a benign redundancy.

1. **View reacts to `IsRainbow`** (`Balloon/View/BalloonView.cs` `Bind` `:100`) — subscribe the flag
   (`.AddTo(_bindDisposables)`); on true, swap the Body renderer's **`sharedMaterial`** to
   `BalloonRainbow.mat` and **suppress** the normal `_colorableRenderers.BindColor` (`:102`).
   ⚠️ **The normal tint fights the bands** — the shader multiplies `SpriteRenderer.color` (`IN.color.rgb`)
   into the band colour, so `SetColor` must be torn down and the colour reset to white in rainbow mode.
   ⚠️ Use `.sharedMaterial`, **not** `.material` (`.material` clones per-instance → leaks across pool
   cycles). Cache the original `sharedMaterial` at `Awake`.
2. **`RainbowBalloonVariant`** (new `Balloon/Type/RainbowBalloonVariant.cs`, `IBalloonVariant` +
   `IBalloonViewBinding`, mirroring `ToughBalloonVariant`) — `Initialize` sets `IsRainbow = true` and
   picks a base colour (rainbow is still a colourable balloon; the base colour is the Paint-source tint
   + fallback, the view ignores it visually). Push `_Color0..N`/`_BandCount`/random `_TimeOffset` via a
   cached `MaterialPropertyBlock` (cached `Shader.PropertyToID`s). Inject `IGamePalette`.
3. **Band colours from the allowed set** — resolve via `GamePalette.ColorNamesForMask(mask)` +
   `GetColor`. `BalloonFactory.cs:63-64` already threads `AllowedColorsMask` into `Initialize`, and the
   allowed set **is** available then.
4. **Re-push on level-up** — subscribe `ISubscriber<ScoreLevelUpMessage>` in `Bind`
   (`.AddTo(disposables)`); on message, **re-read `IActiveLevelParameters.Current.AllowedColorsMask`**
   and re-push. ⚠️ **Do NOT use `ScoreLevelUpMessage.CompletedColors`** — that's the *completed* set, not
   the new allowed set. ⚠️ **Subscriber-ordering risk:** `LevelDifficultyResolver` also subscribes to
   this message to re-resolve `Current`; MessagePipe order is unenforced, so the variant may read a
   stale `Current`. Verify in-editor (or defer the re-push a frame).
5. **Pooling revert** (`BalloonView.OnDespawned` `:83`) — restore the cached original `sharedMaterial`
   so a recycled instance spawned as a plain Simple doesn't keep the rainbow material. Subscriptions go
   through `_bindDisposables` (cleared on despawn) — **not** `AddTo(this)`.
6. **[in-editor] ✅ done**
   - `BalloonsConfiguration.asset` — `_rainbowMaterial` set to `BalloonRainbow.mat`.
   - `Balloon.prefab` (the base — `Balloon5`/`Balloon10`/`BalloonRainbow` are its variants, so wiring
     once here covers all four) — `BalloonView._bodyRenderer` wired to the Body `SpriteRenderer`.
   - `BalloonRainbow.prefab` — `SimpleBalloonVariant` replaced with `RainbowBalloonVariant`, wired to
     the same Body `SpriteRenderer`. Its separate "Knot" child (its own `SpriteRenderer`, not part of
     the Body sprite — the shader's UV-rect mask doesn't apply to it) was deactivated.

⚠️ **Instancing note:** the shader compiles `multi_compile_instancing`; what matters is the *material's*
"Enable GPU Instancing" checkbox vs the MPB. Confirm in-editor.

---

## Phase 4 (Part F) — Enum / factory / config wiring — ✅ DONE 2026-07-07 (one manual step left)

1. ✅ **`BalloonType.Rainbow`** (`Balloon/Type/BalloonType.cs`) — appended. Confirmed safe: the *only*
   switch on `BalloonType` is `BalloonModelFactory.Create` (has a `default → throw`); no `[EnumIndexed]`
   drawer or `(int)`-indexed array, so serialized indices 0-5 stayed valid.
2. ✅ **Factory case** (`Balloon/Model/BalloonModelFactory.cs`) — `BalloonType.Rainbow => new
   BalloonModel(config, palette, allowedColors)`. **Correction to this task's original sketch**
   (`new BalloonModel(config)` alone): Phase 2 already had to thread `palette`/`allowedColors` into
   every `BalloonModel` branch (Simple/Silver/Gold too) so `ResolveRainbowAttribution`'s colour pool
   isn't empty. Rainbow needs the exact same treatment, or a spawned one would silently score nothing.
3. ✅ **Catalog entry** in `BalloonsConfiguration.asset` — `BalloonPrefabEntry` added: `_balloonType: 6`,
   `_weight: 0.15`, `_maxCount: 1`, `_scoreValue: 4`, `_itemActivationWeight: 0.1`, `_spillover: 0`
   (a tuning knob — left at 0 pending a deliberate choice, per the "no baked default" decision).
   ⚠️ **One manual step remains:** the entry's `_prefab` slot is `{fileID: 0}` (empty) — the sibling
   entries reference their prefabs through a nested-prefab-variant fileID hash that isn't derivable
   from any literal ID in the repo (confirmed: `9117278215908477035` for Balloon5/Balloon10 doesn't
   appear as a raw object header in either the base `Balloon.prefab` or the variants' own files).
   Hand-guessing it risks a silently broken reference with no compiler check to catch it. **Drag
   `BalloonRainbow.prefab` into the Prefab field of the new entry in the Inspector.**
4. ✅ **Range gate** in `LevelPacingConfiguration.asset` — `BalloonTypeWeight{_type:6, _weight:1,
   _maxCountOverride:0}` added to every range whose `_allowedColorsMask` has ≥2 bits: levels 2, 3, 4,
   and the 5+ tail (all mask `3`). The 0-1 range (mask `1`) was deliberately left out — a wildcard is
   meaningless at one colour. ⚠️ **Caveat for next time:** a `replace_all` edit on this file initially
   also matched the 0-1 range, because the trailing anchor text (`_itemWeights:`) was a substring
   prefix of that range's `_itemWeights: []` — plain substring matching doesn't care what follows the
   match. Caught by re-grepping and fixed; worth choosing anchors that can't prefix-match a sibling
   line when hand-editing repeated YAML blocks.

---

## Phase 5 — Paint → rainbow conversion — ✅ DONE 2026-07-07 (revised — holder-gated)

**Key correction (2026-07-07):** rainbow-spreading is a property of a rainbow **paint-holder**, not of
the Paint item. The first pass had Paint *always* convert targets to rainbow — wrong. Now Paint
branches on the holding balloon: a normal-coloured holder **recolours** targets to its colour (the
original Paint behaviour, restored); only a **rainbow** holder **converts** targets to rainbow. The
gate is `balloon is IHasRainbowMode { IsRainbow: true }`, decided once in `Activate` and threaded as a
`spreadsRainbow` bool into collection + application.

1. ✅ **Reached the flag from the handler** — no interface change needed: `target is
   IHasWriteableRainbowMode rainbowMode` casts the *same* `IPaintable` reference directly (both live on
   the one `BalloonModel` instance), so `IPaintable` itself was never touched.
2. ✅ **Both `Recolor` and `ConvertToRainbow` exist**; `PaintBlob` picks by `spreadsRainbow`. Convert
   sets `IsRainbow.Value = true` — no explicit `DistinctUntilChanged` guard needed, `ReactiveProperty<T>`
   already skips re-emitting an unchanged value, so an already-rainbow target is a safe no-op. Score/
   streak resolve lazily at pop time from `IsRainbow.Value`, so a mid-life conversion scores as rainbow
   when it later pops.
3. ✅ **Decisions resolved:**
   - **Same-colour skip is now mode-dependent** in `CollectPaintTargets` — kept for a recolour (avoids
     a pointless recolour), dropped for a rainbow spread (which applies regardless of current colour,
     so keeping it would wrongly exclude same-coloured targets). `ResolvePaintTarget` was extracted to
     hold this per-slot eligibility (and keep the method under the cognitive-complexity threshold).
   - **Already-rainbow target** — no-op via the `ReactiveProperty` behaviour above, no manual guard.
   - **Rainbow-as-Paint-source** — this *is* the trigger for spreading rainbow; it's a valid
     `IHasColor` source (keeps a base colour, used for the splash VFX tint).

⚠️ **Converted rainbows bypass spawn-time caps and item-weight** — `BalloonModelBase.TypeName` is set
once in the ctor and is immutable (`:42`), so flipping `IsRainbow` does **not** change `TypeName`. A
converted balloon keeps its original type for `MaxCount`/`BalloonTypeWeight` cap-counting and its
original entry's `_itemActivationWeight`. Rare-by-nature (an item effect), but make it an explicit
decision, not an accident.

---

## Phasing (revised) — ✅ ALL PHASES DONE 2026-07-07

0. ✅ **Foundation** — the `IsRainbow` reactive flag + capability (Phase 0).
1. ✅ **Item-activation weight** (Phase 1 / Part D), incl. in-editor weights.
2. ✅ **Scoring + wildcard streak/shield** (Phase 2 / Parts A+B).
3. ✅ **View mode + variant + bands** (Phase 3 / Parts C+E), incl. in-editor wiring + shader iteration
   (shadow removed, `_ShineAngle`/glitter added, `_SpriteScale` removed).
4. ✅ **Enum/factory/config/prefab wiring** (Phase 4 / Part F) — one manual prefab-reference drag done
   by the designer (see Phase 4's note on why I couldn't hand-author it); range gate currently scoped
   to level 2 only (narrower than the original ≥2-colours-everywhere plan — a deliberate, designer-made
   choice, not reverted).
5. ✅ **Paint → rainbow conversion** (Phase 5).

Also fixed post-Phase-3, found via playtest: a `VContainerException: Duplicate injection found`
(`RainbowBalloonVariant` re-declared a same-named `[Inject] IGamePalette` field the base class already
had) — see Phase 3's note for the fix and the lesson.

## Testability & verification

- **EditMode (no Unity):** Phases 0-2, Phase 5's C# half — streak carries on a primary group,
  Tough/Cluster still break, spillover maths, weighted host selection + zero-weight exclusion, wildcard
  shield + no-steal, Paint converts (incl. same-colour + already-rainbow no-op). All covered.
- **Confirmed in-editor:** the shader (bands/glitter/shine render correctly via material preview).
- **Still unverified in a live playtest:** the full spawn → pop → score → Paint-convert loop end to
  end (all the pieces are individually verified, but not exercised together in a run).

## Open decisions (deferred to build/feel — none blocking)

- Spillover value — **tuning knob**, authored in config (no baked default; currently 0 on the shipped
  catalog entry, pending a deliberate choice).
- Rainbow base `scoreValue` — currently 4, per the original "smooths spillover rounding" suggestion.
- Whether streak `M` boosts spillover colours (simplest, shipping) or only the current colour.
- Converted rainbows bypassing spawn caps / item-weight — accepted as documented, not tracked further.
