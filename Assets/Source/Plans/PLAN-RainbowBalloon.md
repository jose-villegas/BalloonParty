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

> **Phase task lists below were verified against the source 2026-07-07** (four implementation-read
> passes). ⚠️ marks a caveat/gap the source reading surfaced; **[in-editor]** marks work `dotnet
> build` can't verify (shaders, `.asset`/prefab edits, MPB visuals).

## Cross-phase corrections (fold these into every part)

- **The `IsRainbow` flag is the single foundation.** It is *simultaneously* Part A's score-override
  switch, Part B's wildcard marker, Part C's view-mode signal, and Part 5's Paint-conversion target.
  Build it **first** (Phase 0) — everything else reads it. It must be a `ReactiveProperty<bool>`
  (not a plain bool), because Paint flips it on an already-bound live balloon and the view must react.
- **Rainbow *is* `IHasColor`** (Part B's original "not `IHasColor`" premise is wrong once Part C makes
  it a `BalloonModel`, which implements `IPaintable : IHasColor`). Do **not** try to drop the
  interface or empty its colour. Instead gate colour-steal *out* via the `IsRainbow` flag at the guard
  site; the shield then fires naturally (see Phase 2).
- **`DamageContext.SourceColorId`** is the projectile's colour at pop time (set from
  `projectile.ColorName.Value` at `ProjectileHitResolver.cs:35`). The plan's earlier `context.ColorName`
  was a wrong field name — there is no `ColorName` on the context.
- **`BalloonPrefabEntry` + `BalloonModelConfig` gain two fields** (`_spillover` from Phase 2,
  `_itemActivationWeight` from Phase 1) via the same `ScoreValue`-style plumbing — do them together.
  ⚠️ **Serialized-default gotcha:** adding a field to the `[Serializable]` `BalloonPrefabEntry` may
  deserialize *existing* asset entries as `0`, silently making every balloon spillover-0 /
  never-item-host. Verify in-editor that existing entries read the intended default and re-save.

---

## Phase 0 — Foundation: the rainbow-mode flag (blocks 2/3/5)

**Task 0.1 — `IsRainbow` flag + read capability.**
- Files: `Balloon/Model/BalloonModel.cs`; new `Slots/Capabilities/IHasRainbowMode.cs`
  (`IReadOnlyReactiveProperty<bool> IsRainbow`), plus a writeable setter reachable by Paint (either a
  writeable capability or expose on `IPaintable`).
- How: `public ReactiveProperty<bool> IsRainbow { get; } = new(false)` on `BalloonModel`; expose
  read-only via `IHasRainbowMode`. Default `false` ⇒ Simple/Silver/Gold (same class) unaffected.
- Interacts with: Phase 2 (`ResolveScoreAttribution` branches on it), Phase 3 (view subscribes),
  Phase 5 (Paint sets it). ⚠️ Keep it reactive — a one-time `Bind`-time check can't see a mid-life flip.

---

## Phase 1 (Part D) — Per-type item-activation weight (independent; do anytime)

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

## Phase 2 (Parts A+B) — Scoring + wildcard streak/shield (depends on Phase 0)

**Why the scatter balloons aren't a precedent** (above) still holds: `RecordStreakMultiplier`
(`ScoreController.cs:118-127`) hard-breaks on *any* multi-attribution group and **ignores** the
per-attribution `breaksStreak` flag — so the fix must live there.

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

## Phase 3 (Parts C+E) — View mode + variant + programmatic bands (depends on Phase 0)

The colourable body is a `SpriteRenderer` on the **"Body"** child using Unity's built-in `Sprites-Default`
material; `SpriteColorableRenderer.SetColor` just writes `Renderer.color` (no MPB). The rainbow shader
reuses the same sprite (`_MainTex` is `[PerRendererData]`).

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
6. **[in-editor]** wire `BalloonRainbow.prefab` — it's currently a *bare* variant (`m_AddedComponents: []`,
   still carries `SimpleBalloonVariant` + plain material). Add `RainbowBalloonVariant`, wire the Body
   `SpriteRenderer` ref. (Runtime swap in Task 1 is needed regardless for Paint-conversion, so assigning
   the material in-prefab is optional.)

⚠️ **Instancing note:** the shader compiles `multi_compile_instancing`; what matters is the *material's*
"Enable GPU Instancing" checkbox vs the MPB. Confirm in-editor.

---

## Phase 4 (Part F) — Enum / factory / config wiring

1. **`BalloonType.Rainbow`** (`Balloon/Type/BalloonType.cs`) — append only. ✅ Safe: the *only* switch
   on `BalloonType` is `BalloonModelFactory.Create` (has a `default → throw`); no `[EnumIndexed]` drawer
   or `(int)`-indexed array (grep-confirmed), so serialized indices 0-5 stay valid.
2. **Factory case** (`Balloon/Model/BalloonModelFactory.cs:19`) — `BalloonType.Rainbow => new BalloonModel(config)`
   (a plain `BalloonModel`; rainbow-ness comes from the variant flipping `IsRainbow` at `Initialize`).
3. **[in-editor] Catalog entry** in `BalloonsConfiguration.asset` — `BalloonPrefabEntry` → prefab
   `BalloonRainbow.prefab`, `_balloonType: 6`, weight/maxCount, `_scoreValue ≈ 4-5` (smooths spillover
   rounding + chunky pop), low `_itemActivationWeight` (needs Phase 1).
4. **[in-editor] Range gate** in `LevelPacingConfiguration.asset` — add a `BalloonTypeWeight{_type:6}`
   **only** to ranges whose `_allowedColorsMask` has ≥2 bits (membership *is* the gate, per
   `LevelDifficultyResolver.BuildBalloonPickList`). The level 0-1 range (mask `1`) must not get it —
   a wildcard is meaningless at one colour, and this paces its introduction. No automatic
   "≥2 colours ⇒ eligible" rule exists; it's expressed purely by which ranges include the entry.

---

## Phase 5 — Paint → rainbow conversion (the new interaction; last)

`PaintItemHandler.Recolor` (`:174-185`) currently sets `target.Color.Value = paintColor` over
`IPaintable` splash targets (`CollectPaintTargets` `:122-155`). Convert = **flip the reactive
`IsRainbow` flag** on the target model instead of (or in addition to) recolouring; the view's Phase-3
subscription then does the material swap — the handler never touches the view (clean MVC).

1. **Reach the flag from the handler** — the handler enumerates `_grid.At(slot) as IPaintable`, so the
   rainbow-mode setter must be reachable through that cast: either add it to `IPaintable` or also cast
   to a writeable rainbow capability. *Why:* ⚠️ the handler has **no** `BalloonView` reference; the
   reactive flag is the only clean path to the view.
2. **Convert in `Recolor`** — set `IsRainbow = true` (guard with `.DistinctUntilChanged()` / a
   current-value check to avoid redundant re-swaps). Score/streak behaviour is resolved lazily at pop
   time from `IsRainbow.Value`, so a mid-life conversion scores as rainbow when it later pops — no
   stale state.
3. **Decisions to nail** — ⚠️ `CollectPaintTargets` skips targets whose colour already equals the paint
   colour (`:139`); decide whether *conversion* should bypass that skip. ⚠️ Paint source already
   rainbow: still a valid `IHasColor` source (keeps a base colour), so it works; converting an
   already-rainbow target is a no-op flip (guard it).

⚠️ **Converted rainbows bypass spawn-time caps and item-weight** — `BalloonModelBase.TypeName` is set
once in the ctor and is immutable (`:42`), so flipping `IsRainbow` does **not** change `TypeName`. A
converted balloon keeps its original type for `MaxCount`/`BalloonTypeWeight` cap-counting and its
original entry's `_itemActivationWeight`. Rare-by-nature (an item effect), but make it an explicit
decision, not an accident.

---

## Phasing (revised)

0. **Foundation** — the `IsRainbow` reactive flag + capability (Phase 0). Tiny; unblocks everything.
1. **Item-activation weight** (Phase 1 / Part D) — independent; shares the `BalloonPrefabEntry`/
   `BalloonModelConfig` plumbing with the spillover knob, so land them together.
2. **Scoring + wildcard streak/shield** (Phase 2 / Parts A+B) — correctness-critical core; guard that
   Tough/Cluster still break. Fully EditMode-testable.
3. **View mode + variant + bands** (Phase 3 / Parts C+E) — spawned rainbows first.
4. **Enum/factory/config/prefab wiring** (Phase 4 / Part F) — spawn it, tune weights/ranges.
5. **Paint → rainbow conversion** (Phase 5) — last, once the mode + view swap exist.

## Testability & verification

- **EditMode (no Unity):** Phases 0-2 and the C# half of 5 — streak carries on a primary group,
  Tough/Cluster still break, spillover maths, weighted host selection + zero-weight exclusion, wildcard
  shield + no-steal. `dotnet build BalloonParty.Runtime.csproj` + the EditMode asmdef cover these.
- **[in-editor] only:** the shader compile, the band MPB push + material swap, the prefab + two `.asset`
  edits, the level-up re-push timing, and overall feel. Flag every such task.

## Open decisions (deferred to build/feel)

- Spillover value — **tuning knob**, authored in config (no baked default).
- Rainbow base `scoreValue` — ≈4-5 (smooths spillover rounding, chunky pop).
- Whether streak `M` boosts spillover colours (simplest, shipping) or only the current colour.
- Paint: bypass the same-colour skip for conversion? Convert same-colour non-rainbow targets?
- Converted rainbows bypassing spawn caps / item-weight — accept, or track separately?
