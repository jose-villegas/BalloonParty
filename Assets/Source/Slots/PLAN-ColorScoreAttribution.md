# Color & Score Attribution Plan — `IHasScoreColor` Migration

> Tracks the design and implementation of the unified color/score attribution system.
> Extracted from `PLAN-ContentProduction.md` — this work cuts across actors and is
> independent of asset production timelines.

---

## Problem

`IHasColor` conflates **three** concerns today, all of which have different consumers
and different lifetimes:

| Concern | Consumer | Contract need |
|---|---|---|
| **Reactive tint identity** — view subscribes to `Color` to tint the sprite | `BalloonView`, `ProjectileView`, item VFX (Bomb/Laser/Shield) | Reactive read (`IReadOnlyReactiveProperty<string>`) |
| **Paintability** — Paint item can overwrite a balloon's color | `PaintItemHandler` | Writable reactive property (`IHasWriteableColor`) |
| **Score attribution** — which color bar receives points on destruction | `ScoreController` | Snapshot-at-destruction bitmask + distribution strategy |

For simple balloons all three are the same, so the conflation is invisible. Two upcoming
actors break the scoring assumption without touching the rendering or paint assumptions:

| Actor | Tint? | Paintable? | Score color |
|---|---|---|---|
| **Unbreakable** | No | No | Killer's color — unknown until hit time |
| **Bubble Cluster** | No | No | All palette colors — resolved at destruction |

## `IHasColor` is NOT being dropped

The earlier framing ("drop `IHasColor` entirely") was premature. Removing it would silently
break four live systems without replacing their capabilities:

- **`BalloonView.Bind()`** — subscribes reactively to `IHasColor.Color` via `BindColor`.
  `IHasScoreColor.ScoreColorMask` is a plain `int`, not reactive — view changes caused by
  Paint would not propagate without a separate reactive wrapper.
- **`IHasItemSlot : IHasColor`** — the item rendering service receives `itemSlot.Color`
  to tint item VFX and sprites. A colorless item host is incoherent.
- **`PaintItemHandler`** — reads the source balloon's color string and writes it to
  neighbors via `IHasWriteableColor`. This entire mechanic needs an explicit "paintable"
  contract that is distinct from score attribution.
- **`LightningItemHandler`** — finds same-colored balloons by comparing `Color.Value`.
  This is a targeting query against *visual/identity* color, not score color.

## Full removal analysis — could we unify everything into `IHasScoreColor`?

The "views should decide what to render from ScoreColorMask" approach is architecturally
appealing but **moderate-high complexity** to execute. Full inventory of required changes:

### What has to change
| Touch point | Change required |
|---|---|
| `IHasScoreColor` | `int ScoreColorMask` → `IReadOnlyReactiveProperty<int>` — breaking change to interface + all implementations |
| `IHasWriteableScoreColorMask` | Create new: `ReactiveProperty<int> ScoreColorMask` — write side for Paint |
| `GamePalette` | Add `GetColor(int mask)` — resolve the single set-bit to a `Color`; bit index == `_colors` array index must be confirmed |
| `ColorableRendererExtensions` | New `BindColor` overload for `IReadOnlyReactiveProperty<int>` |
| `BalloonView` | 3 casts + subscription site; hit-VFX section |
| `IHasItemSlot` | Inherits from `IHasColor` today → change to `IHasScoreColor`; `Color` property removed |
| `ItemDisplayService` | `Bind()` — `IReadOnlyReactiveProperty<string>` → `IReadOnlyReactiveProperty<int>`; remove `GamePalette` field and param |
| `IItemView` | `Activate(Color)` → `Activate(int colorMask)`; `SetColor(Color)` → `SetColor(int colorMask)` — views own mask interpretation |
| `ItemVisualView` | Inject `GamePalette`; resolve single-bit mask to `Color` internally |
| `PaintItemHandler` | Read: `Color.Value` string → extract name from mask bit. Write: `IHasWriteableColor` → `IHasWriteableScoreColorMask` |
| `LightningItemHandler` | String comparison → mask comparison (actually simpler) |
| `BombItemHandler`, `LaserItemHandler`, `ShieldItemHandler` | VFX color fetch: `IHasColor.Color.Value` → extract from mask |
| `ProjectileView` | `IHasColor` cast → `IHasScoreColor` cast; extract color from `ScoreColorMask.Value` |
| `BalloonModel` | Replace `ReactiveProperty<string> Color` with `ReactiveProperty<int> ScoreColorMask`; remove `IHasColor`/`IHasWriteableColor` |
| `IHasColor.cs`, `IHasWriteableColor.cs` | Delete |
| Tests (3 files) | `DurableActor`, `AbsorbingActor` stubs, `PaintItemHandlerTests` stubs, `ItemSlotTests` assertion — all implement/assert `IHasColor` |

**Total: ~16 files, all must land atomically (won't compile mid-migration).**

### Emergent multi-color mechanics

Unifying everything under a bitmask doesn't just simplify the model — it unlocks
**multi-color state as a first-class concept** that the current string system cannot
express at all. Two items are directly affected, and the implications are significant
enough to resolve as explicit design decisions before the migration is implemented.

---

#### Paint + multi-color mask

**Current behavior (string):** Paint reads one color string from the source balloon and
writes that same string to each neighbor. Always produces a single-color result.

**With bitmask:** Paint copies `ScoreColorMask` verbatim from the source to each
neighbor. If the source carries multiple bits (e.g. a Rainbow balloon), neighbors become
multi-color too. A Rainbow Paint item would propagate rainbow state across the grid —
a completely novel mechanic available for free.

**Design decision — Paint copy mode: ✅ `CopyMask`**

Paint **replaces** the target's `ScoreColorMask` with the source's mask — it does not
union them. A Red balloon painting a Green balloon produces Red, not Red+Green. A
Rainbow balloon painting a Green balloon produces Rainbow, not Rainbow (which would be
the same result either way, but the semantic is still replace).

```csharp
// PaintItemHandler — write side (Phase 2)
target.ScoreColorMask.Value = sourceMask;   // assign, not |= 
```

This preserves the current string-based behavior where `Color.Value = paintColor`
overwrites the previous color. Rainbow balloon Paint spreads rainbow state — not because
bits accumulate, but because the full rainbow mask is stamped onto each neighbor.
Jackpot path exists (see Phase 0 balance note) — managed through spawn weight and item
rarity, not restricted at the code level.

---

#### Lightning + multi-color mask

**Current behavior (string):** `LightningItemHandler` collects targets where
`modelColor.Color.Value == sourceColor.Color.Value` — strict equality. Lightning chains
to exactly the same-color group.

**With bitmask:** Two semantics were considered:

| Comparison | Effect |
|---|---|
| `targetMask == sourceMask` | Strict equality — Rainbow only chains to Rainbow. |
| `(targetMask & sourceMask) != 0` | **✅ Chosen.** Any shared bit qualifies. Rainbow chains to everything. |

**Design decision — Lightning match mode: ✅ `Overlap` (`(targetMask & sourceMask) != 0`)**

Any shared bit qualifies. A Red balloon chains to Rainbow balloons. A Rainbow balloon
chains to the entire grid. Deliberate high power ceiling — managed through rarity.

---

#### New actor types this enables

Multi-color bitmask state also makes new balloon archetypes trivially expressible
without new model fields:

| Archetype | Mask | Paint behavior | Lightning behavior | Score |
|---|---|---|---|---|
| **Rainbow** | All palette bits | Copies all bits (if `CopyMask`) | Chains to matching mask only (strict) | `AllColors` — every bar scores |
| **Dual-color** | 2 bits | Copies both bits | Matches only other same-pair balloons | `AllColors` on those 2 bits |
| **Shifting** | 1 bit, changes each turn | Paint spreads current bit | Chains to current-matching group | `RandomPick` on current bit |

Because `IItemView.Activate(int colorMask)` is also mask-aware, **item visuals on hosted
balloons automatically reflect multi-color state** — a Rainbow balloon with a Bomb item
would show a rainbow-tinted Bomb without any special casing. A `RainbowItemVisualView`
subclass could apply a hue-shift animation by iterating the set bits; no changes to
`ItemDisplayService` or the item system are needed.

These are design-space observations, not committed features — noting them here so the
interface decision is made with awareness of what it enables.

---

### The `ItemDisplayService` and `IItemView` pipeline

Currently `ItemDisplayService.Bind()` takes `IReadOnlyReactiveProperty<string> colorName`,
resolves it to a `Color` via `GamePalette`, and passes that resolved `Color` to
`IItemView.Activate(Color)` and `IItemView.SetColor(Color)`. The palette lookup happens
in the service — item views are color-aware but not mask-aware.

**Committed direction:** propagate the mask all the way down. The service passes the raw
mask; each view handler decides how to interpret it.

```
IHasScoreColor.ScoreColorMask (int, reactive)
  └─ ItemDisplayService.Bind(IReadOnlyReactiveProperty<int> colorMask)
       └─ IItemView.Activate(int colorMask)   ← view resolves mask → visuals
       └─ IItemView.SetColor(int colorMask)   ← recolor on mask change
```

**Consequences:**

- `ItemDisplayService` no longer needs `GamePalette` — remove it from `Bind()` and
  the service's field. The service becomes a pure lifecycle bridge.
- `IItemView` implementations gain full control over multi-color rendering:
  - `ItemVisualView` (default, single-color) — extracts the single set bit from the
    mask, looks up the `Color` via an injected `GamePalette`, and calls `sr.color`.
    Single-bit mask = same behavior as today.
  - A future `RainbowItemVisualView` — iterates all set bits, applies a gradient or
    animated hue-shift across all palette colors. Zero new infrastructure required.
- `ItemVisualView` needs `GamePalette` injected. It already lives inside `ItemViewScope`
  (VContainer), so `[Inject]` is the only addition.

**Additional touch points this adds to the migration:**

| Touch point | Change required |
|---|---|
| `IItemView` | `Activate(Color)` → `Activate(int colorMask)`; `SetColor(Color)` → `SetColor(int colorMask)` |
| `ItemVisualView` | Inject `GamePalette`; resolve mask to `Color` internally |
| `ItemDisplayService` | `Bind()` — `IReadOnlyReactiveProperty<string>` → `IReadOnlyReactiveProperty<int>`; remove `GamePalette` field and param |
| `ItemViewScope` | Remove `GamePalette` registration from `Bind()` call-site if it was the only consumer |

### When it's worth doing

This migration delivers real long-term simplicity — one reactive color property per
model instead of two (`Color` string + `ScoreColorMask` int). The conceptual model
becomes: **"the mask IS the color identity; everything derives from it"**. It also
unlocks multi-color mechanics (Rainbow balloon, dual-color, shifting) as a free
consequence of the data model — see **Emergent multi-color mechanics** above.

It's worth doing only as a **dedicated refactor sprint** (not inline with feature work)
because all 16 touch points must compile together. All Phase 0 decisions are resolved.
Remaining gate: Phase 1 must be complete before starting.

## Current resolution — interim state (three interfaces)

Until the full `IHasColor` removal sprint is executed, the codebase uses three interfaces:

```
IHasColor          → reactive tint identity (read). Views and items consume this.
IPaintable         → rename of IHasWriteableColor. Opt-in "Paint can recolor me" capability.
IHasScoreColor     → score attribution only. ScoreController consumes ONLY this.
```

This is a stable intermediate step — `IHasScoreColor` is additive, `IPaintable` is a
rename, `IHasColor` is untouched. No behavior changes.

### `IHasColor` (unchanged, survives)

Keeps its `IReadOnlyReactiveProperty<string> Color { get; }` shape. All view and
item-handler reads stay on this interface. The score system no longer reads it.

### `IPaintable` (rename of `IHasWriteableColor`)

```csharp
// Replaces IHasWriteableColor — explicit "Paint item can recolor me" capability.
public interface IPaintable : IHasColor
{
    new ReactiveProperty<string> Color { get; }
}
```

Renaming is the only change. The Paint item handler already targets `IHasWriteableColor`
by name — updating that single cast is the full migration. Unbreakable and grid actors
that should **not** be paintable simply do not implement `IPaintable`.

### `IHasScoreColor` (new, additive — does not replace `IHasColor`)

Used **only** by `ScoreController`. The double-cast `IHasColor and IHasScore` in
`ScoreController.OnActorHit` migrates to a single `IHasScoreColor` cast. No view
or item handler touches this interface.

### Mapping per actor

| Actor | `IHasColor` | `IPaintable` | `IHasScoreColor` |
|---|---|---|---|
| **Simple** | ✅ (colors from spawn) | ✅ (Paint can recolor) | ✅ `RandomPick` |
| **Tough** | ❌ (no tint) | ❌ (immutable) | ✅ `Inherited` — killer earns it |
| **Unbreakable** | ❌ (no tint) | ❌ (immutable) | ✅ `Inherited` |
| **Bubble Cluster** | ❌ (procedural shader) | ❌ (immutable) | ✅ `RandomUntilDepleted` *(Phase 9)* |
| **Grid actors** | ❌ (no tint) | ❌ (immutable) | ❌ (no score) |

---

## `IHasScoreColor` interface shape

```csharp
// Slots/Capabilities/IHasScoreColor.cs
public interface IHasScoreColor
{
    /// The palette color bitmask this actor carries for scoring purposes.
    /// Zero = colorless (Inherited actors resolve at hit time instead).
    /// INTERIM: views read IHasColor.Color for tinting (Phase 1).
    /// POST-MIGRATION: ScoreColorMask becomes reactive and IS the color identity (Phase 2).
    [PaletteColorMask] int ScoreColorMask { get; }  // → IReadOnlyReactiveProperty<int> in Phase 2

    /// How points are distributed across the colors in ScoreColorMask when this
    /// actor is destroyed. Exposed so inspectors and debug overlays can read the
    /// strategy without triggering resolution.
    ScoreDistributionMode ScoreDistribution { get; }

    /// Called once by ScoreController at the moment this actor is destroyed.
    /// Implementations append (colorId, points) pairs to `results`.
    /// Callers own the list — pass a pooled or pre-allocated instance.
    void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results);
}

// Value type — one entry per color bar that should receive points.
public readonly struct ScoreAttribution
{
    public readonly string ColorId;
    public readonly int    Points;
}
```

Two members, two consumers (note: `ScoreColorMask` is **not** read by views — see `IHasColor`):
- **`ScoreDistribution`** — inspectors, debug overlays, future filter queries. Describes
  strategy without executing it.
- **`ResolveScoreAttribution`** — `ScoreController` only, called once at destruction.

---

## `ScoreDistributionMode` enum

```csharp
public enum ScoreDistributionMode
{
    /// Every flagged color receives ScoreValue in full.
    AllColors,

    /// One color chosen at random from the flagged set receives ScoreValue in full.
    RandomPick,

    /// ScoreValue points dealt one-at-a-time to random colors until exhausted.
    RandomUntilDepleted,

    /// Mask ignored. ResolveScoreAttribution reads context.SourceColor directly.
    Inherited,
}
```

Config entries expose `ScoreColorMask` and `ScoreDistribution` as designer-facing
fields. Each model implementation delegates both properties to its injected config
entry and provides a `ResolveScoreAttribution` body that matches the declared mode.

---

## Spawn-time vs score-time resolution

The key design axis is **when** `ResolveScoreAttribution` prepares its answer:

**Spawn-time resolution — normal balloons**

A `BalloonPrefabEntry` carries `ScoreColorMask = all palette bits` and
`ScoreDistribution = RandomPick`. At the moment a balloon is spawned, the spawner
evaluates the distribution once — picks one color at random — and caches the resulting
`ScoreAttribution(colorId, scoreValue)` on the model instance. `ResolveScoreAttribution`
simply appends that cached entry; O(1) and allocation-free at score time.

The view reads the cached color from the model to apply a tint. The spawn-time
resolution is what produces the deterministic red/blue/green identity players recognise.

**Score-time resolution — colorless actors**

`BubbleClusterModel`, `UnbreakableBalloonModel`, and grid actors compute their result
inside `ResolveScoreAttribution` when called. No state is cached at spawn.

- `BubbleClusterModel`: iterates `ScoreColorMask` bits, distributes `ScoreValue` one
  point at a time to randomly selected colors, appends one `ScoreAttribution` per point.
- `UnbreakableBalloonModel`: reads `context.SourceColor`, appends a single
  `ScoreAttribution(context.SourceColor, scoreValue)`. Model is fully stateless.

```
Config entry               Spawn                     ResolveScoreAttribution call
─────────────────────      ──────────────────────    ─────────────────────────────
all bits + RandomPick  ──▶ cache (Red, N)        ──▶ append cached entry → [Red×N]
all bits + RandDepleted──▶ nothing cached         ──▶ scatter at call time → [R×1, B×1, …]
0 + Inherited          ──▶ nothing cached         ──▶ read context → [SourceColor×N]
```

---

## Recommended config defaults per actor

| Actor | Config mask | Config mode | Resolution | Rationale |
|---|---|---|---|---|
| **Simple** | all palette bits | `RandomPick` | Spawn | One color cached at spawn; view tints; scores in full |
| **Tough** | `0` | `Inherited` | Score time | Stateless; killer earns the score in their own color |
| **Unbreakable** | `0` | `Inherited` | Score time | Reads `context.SourceColor` at call; model stateless |
| **Bubble Cluster** | all palette bits | `RandomUntilDepleted` | Score time | *(Phase 9)* scatter across all bars at call time |

> **Bubble Cluster tuning (Phase 9):** If scatter feels too diffuse, change SO entry to
> `RandomPick` — one color wins the full `ScoreValue`, no code change needed. Escalate
> to `AllColors` only for a deliberate jackpot, and reduce `ScoreValue` to compensate.

---

## Task tracking

Work is split into four phases. Phases 0 and 1 unblock each other and can overlap.
Phase 2 is an atomic sprint — nothing compiles mid-way. Phase 3 is deferred to Phase 9.

---

### Phase 0 — Design decisions ✅ All resolved

- [x] **Tough scoring strategy** → **`Inherited`**
      No color at spawn. Whoever delivers the killing blow earns the points in their own
      color. Model stays stateless for color. No sprite changes needed.

- [x] **Paint copy mode** → **`CopyMask`**
      Paint copies the full `ScoreColorMask` verbatim to each neighbor. A Rainbow balloon's
      Paint spreads rainbow state to all 6 neighbors. ⚠️ Creates a jackpot path with
      Lightning Overlap (see balance note below) — gate Rainbow balloon behind high
      difficulty/rarity to control exposure.

- [x] **Lightning match mode** → **`Overlap`** (`(targetMask & sourceMask) != 0`)
      Any shared bit qualifies as a match. A Red balloon chains to Rainbow balloons (they
      share the red bit). A Rainbow balloon chains to every balloon on the grid.
      ⚠️ Deliberately high power ceiling — Rainbow Lightning is a board-clear. Tune
      Rainbow balloon weight and Lightning item rarity accordingly.

- [x] **`GamePalette` bit-index convention** → **Array order confirmed**
      `bit i == _colors[i]` — verified in `PaletteColorMaskDrawer` (lines 100, 138).
      `GamePalette.GetColor(int mask)` implementation: find lowest set bit index via
      `BitOperations.TrailingZeroCount(mask)`, return `_colors[index].Color`.
      No `PaletteEntry` changes needed.

> **Balance note — CopyMask + Overlap combo:** A Rainbow balloon with Paint turns 6
> neighbors rainbow; any subsequent Lightning then chains to the entire grid. This is a
> deliberate high-power jackpot path, not an accident. Manage it through spawn weight
> and item rarity, not by restricting the mechanic.

---

### Phase 1 — Interim groundwork *(do now, independent of Phase 2)*

Safe incremental work. Each item compiles and ships independently. Does not require
the full sprint.

**Score wiring for remaining actors:**
- [ ] `ToughBalloonModel` — implement `IHasScoreColor` once Phase 0 decision is made
- [ ] `UnbreakableBalloonModel` — implement `IHasScoreColor` with `Inherited` mode;
      `ResolveScoreAttribution` appends `(context.SourceColor, scoreValue)`
- [ ] `GridActorPrefabEntry` — add `ScoreColorMask` + `ScoreDistribution` fields
      (mirrors `BalloonPrefabEntry`; needed before grid actors can attribute score)

**`IPaintable` rename (zero behavior change):**
- [ ] Rename `IHasWriteableColor` → `IPaintable` in `Slots/Capabilities/`
- [ ] `BalloonModel` — change `IHasWriteableColor` → `IPaintable` in declaration
- [ ] `PaintItemHandler` — change cast target from `IHasWriteableColor` → `IPaintable`
- [ ] Update `IHasItemSlot` doc comment (`IHasColor` reference still correct for now)

**Guard rails:**
- [ ] Confirm `UnbreakableBalloonModel` does NOT implement `IHasColor` or `IPaintable`
- [ ] Confirm grid actor models (`PuffObstacleModel`, `BushObstacleModel`, etc.) do NOT
      implement `IHasColor` or `IPaintable`

---

### Phase 2 — Full migration sprint *(atomic — all changes land in one PR)*

**Prerequisites:** Phase 0 decisions all recorded. Phase 1 complete.
All items below must compile together. Do not ship partial.

#### 2a — Core interfaces

- [ ] `IHasScoreColor` — change `int ScoreColorMask` to
      `IReadOnlyReactiveProperty<int> ScoreColorMask` (note: `ScoreDistributionMode` and
      `ResolveScoreAttribution` are unchanged)
- [ ] Create `IHasWriteableScoreColorMask` in `Slots/Capabilities/` —
      `ReactiveProperty<int> ScoreColorMask { get; }` extending `IHasScoreColor`
- [ ] Delete `IHasColor.cs`
- [ ] Delete `IHasWriteableColor.cs` (now superseded by `IPaintable` from Phase 1)

#### 2b — Palette utility

- [ ] `GamePalette` — add `Color GetColor(int mask)` resolving single set-bit to `Color`
      (needed by `ItemVisualView`, `ColorableRendererExtensions`, item handlers)
- [ ] `GamePalette` — add `string GetColorName(int mask)` resolving single set-bit to
      color name string (needed by score attribution and debug tooling)

#### 2c — Model layer

- [ ] `BalloonModel` — replace `ReactiveProperty<string> Color` with
      `ReactiveProperty<int> ScoreColorMask`; implement `IHasWriteableScoreColorMask`;
      remove `IHasColor` and `IPaintable` explicit implementations
- [ ] `BalloonSpawner` / `ColorableBalloonVariant` — write spawn-time resolved single-bit
      mask to `IHasWriteableScoreColorMask` instead of `IHasWriteableColor.Color`
- [ ] `ToughBalloonModel` — update to use `IHasWriteableScoreColorMask` if spawn-time
      `RandomPick` was chosen in Phase 0; no model change needed for `Inherited`

#### 2d — Rendering pipeline

- [ ] `ColorableRendererExtensions` — add `BindColor(IReadOnlyReactiveProperty<int> mask,
      Func<int, Color> resolve)` overload
- [ ] `BalloonView` — replace `IHasColor` cast with `IHasScoreColor` cast; update
      `BindColor` call to use mask overload; update hit-VFX color section
- [ ] `ProjectileView` — replace `IHasColor` cast with `IHasScoreColor`; extract `Color`
      from `ScoreColorMask.Value` for trail color

#### 2e — Item display pipeline

- [ ] `IItemView` — change `Activate(Color)` → `Activate(int colorMask)`;
      change `SetColor(Color)` → `SetColor(int colorMask)`
- [ ] `ItemVisualView` — add `[Inject] private GamePalette _palette`; resolve mask to
      `Color` internally in `Activate` and `SetColor`
- [ ] `ItemDisplayService` — change `Bind()` param `IReadOnlyReactiveProperty<string> colorName`
      → `IReadOnlyReactiveProperty<int> colorMask`; remove `GamePalette` field and
      constructor param; propagate mask to `IItemView.Activate` / `SetColor`
- [ ] `IHasItemSlot` — remove `: IHasColor` base; add `IReadOnlyReactiveProperty<int> ScoreColorMask`
      (inherited from `IHasScoreColor`, or explicit — confirm which actors need item slots)
- [ ] `BalloonView` item slot section — pass `itemSlot.ScoreColorMask` to `_itemService.Bind()`

#### 2f — Item handlers

- [ ] `PaintItemHandler` — read source mask from `IHasScoreColor.ScoreColorMask.Value`;
      apply Paint copy mode decided in Phase 0; write via `IHasWriteableScoreColorMask`
- [ ] `LightningItemHandler` — replace `IHasColor` cast + string comparison with
      `IHasScoreColor` cast + mask comparison using mode decided in Phase 0
- [ ] `BombItemHandler` — replace `IHasColor` cast with `IHasScoreColor`;
      fetch `Color` via `_palette.GetColor(mask)`
- [ ] `LaserItemHandler` — same as Bomb
- [ ] `ShieldItemHandler` — same as Bomb

#### 2g — Tests

- [ ] `ScoreControllerTests` — update `DurableActor` and `AbsorbingActor` stubs:
      remove `IHasColor`, implement `IHasScoreColor` with reactive `ScoreColorMask`
- [ ] `PaintItemHandlerTests` — update stub balloons; replace `Color.Value` string
      assertions with `ScoreColorMask.Value` bitmask assertions
- [ ] `ItemSlotTests` — update `BalloonModel_IHasItemSlot_AlsoImplementsIHasColor`
      assertion to check `IHasScoreColor` instead

---

### Phase 3 — Deferred to Phase 9

- [ ] `BubbleClusterModel` — implement `IHasScoreColor` with `RandomUntilDepleted`;
      `ResolveScoreAttribution` scatters `ScoreValue` across all palette bits one point
      at a time
- [ ] `PSVFX_SoapBubblePop` + `PSVFX_SoapClusterBurst` — pop VFX for Soap Cluster
- [ ] Soap Cluster multi-color scoring tuning — confirm `RandomUntilDepleted` vs
      `RandomPick` in playtesting; adjust SO entry only (no code change)


