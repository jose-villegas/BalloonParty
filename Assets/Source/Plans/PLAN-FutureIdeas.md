@page plan_future_ideas Future Ideas & Improvements

# Future Ideas & Improvements

> Collection of deferred features, improvement concepts, and speculative designs.
> Nothing here is scheduled — items graduate into dedicated plans when prioritised.

---

## 1 — VFX Improvements

### 1.1 Unbreakable Pop VFX — Falling Debris

When a Piercing item finally destroys an Unbreakable balloon, the pop should feel
weighty and earned. Instead of a standard particle burst, the Unbreakable shatters
into falling debris pieces that obey gravity.

**Art direction:** Heavy chunks of the Unbreakable's shell material (stone, metal,
knotted rubber — whichever direction the Unbreakable art takes) crack apart and
tumble downward. Pieces should vary in size, rotate as they fall, and fade out or
shrink before reaching the bottom of the screen. A brief flash/shockwave at the
moment of destruction sells the impact.

**VFX prefab:** `PSVFX_UnbreakablePop`
- Initial burst: 4–6 rigid-body-like sub-emitters with gravity, random angular
  velocity, and slight outward spread
- Each piece: sprite from the Unbreakable's material palette (reuse shader tint),
  starts at full opacity, fades over lifetime
- Dust cloud: soft additive particles at the origin, short-lived, sells the crack
- Optional screen-shake impulse via `CameraShakeMessage` (light, 0.1s)

**Deflect VFX:** `PSVFX_UnbreakableDeflect`
- Small spark/clank at the contact point — a single bright flash + 2–3 tiny sparks
- Communicates "this did nothing" without being dramatic

### 1.2 Soap Cluster Pop VFX

`PSVFX_SoapBubblePop` — iridescent soap-film ring + mist burst at the removed
bubble's world position (uses `IBalloonHitHandler.GetVfxWorldPosition` from
`PLAN-BubbleClusterHitFeedback`).

`PSVFX_SoapClusterBurst` — larger multi-ring final pop when the last bubble is
destroyed.

### 1.3 Deflector Bounce VFX

`PSVFX_DeflectorBounce` — small sparkle/flash at the contact point when a
projectile bounces off the Deflector.

### 1.4 Absorber Consume VFX

`PSVFX_AbsorberPop` — implosion / suction burst at contact point when the Absorber
consumes a projectile.

### 1.5 Gatekeeper Hit + Break VFX

- `PSVFX_GatekeeperHit` — hit dust/chip on non-killing hits
- `PSVFX_GatekeeperBreak` — larger debris/burst on final destruction

---

## 2 — Spawn Weights, Pity System & Streak Balancing

The procedural placement engine (Phase 8.3) and difficulty system (Phase 8.4) use
flat weights today. This section proposes dynamic weight adjustment to create better
pacing and satisfying scoring patterns.

### 2.1 Per-Level Balloon & Item Weights

`DifficultyProfile` (Phase 8.4) already defines `ActorWeightOverrides` per level.
Extend this to cover item spawn weights as well:

```
DifficultyProfile
├── BalloonWeights: BalloonWeightEntry[]    ← per-BalloonType weight multiplier
├── ItemWeights: ItemWeightEntry[]          ← per-ItemType weight multiplier
└── GridActorWeights: ActorWeightEntry[]    ← per-GridActorType weight multiplier (existing)
```

Each entry is a `(type, float weightMultiplier)` pair applied on top of the base
weight from `BalloonsConfiguration` / `ItemConfiguration`. This lets difficulty
profiles gradually introduce new types (weight 0 → 0.1 → 0.5) and phase out
early-game types (weight 1.0 → 0.3).

### 2.2 Pity System for Weight Randoms

Prevent long drought streaks where a desired type never appears. A pity counter
tracks consecutive spawn passes where a type was eligible but not selected. When
the counter exceeds a threshold, the type's effective weight is boosted until it
is selected, then the counter resets.

**Design sketch:**

```csharp
internal class PityTracker
{
    // Key: type identifier (BalloonType, ItemType, etc.)
    // Value: consecutive eligible-but-not-selected count
    private readonly Dictionary<int, int> _missCount;

    float GetPityMultiplier(int typeId, int pityThreshold, float pityBoost)
    {
        if (!_missCount.TryGetValue(typeId, out var misses)) return 1f;
        if (misses < pityThreshold) return 1f;
        return 1f + pityBoost * (misses - pityThreshold + 1);
    }

    void OnSelected(int typeId) => _missCount[typeId] = 0;
    void OnSkipped(int typeId)  => _missCount[typeId] = _missCount.GetValueOrDefault(typeId) + 1;
}
```

**Config knobs** (on `DifficultyProfile` or a shared `SpawnBalancingSettings` SO):
- `PityThreshold` — misses before boost kicks in (e.g. 3)
- `PityBoostPerMiss` — additive weight multiplier per miss beyond threshold (e.g. 0.5)
- Per-type opt-out — some types (e.g. Absorber) should NOT pity-boost

### 2.3 Color Streak Balancing

The game is more satisfying when players occasionally encounter a high-density
cluster of same-color balloons — a "streak opportunity" that rewards aim and
planning. Pure random distribution rarely produces these clusters naturally.

**Mechanism — streak weight bias:**

When the spawner picks a color for a new balloon, it applies a streak bias that
temporarily increases the weight of a recently-used color. This creates natural
runs of 3–5 same-color balloons without making the distribution deterministic.

```
StreakBalancer
├── _currentStreakColor: ColorId
├── _streakLength: int
├── _streakCooldown: int           ← turns since last streak ended
│
├── GetColorWeights(baseWeights) → adjusted weights
│   ├── if streakCooldown < MinCooldown → return baseWeights (no bias)
│   ├── if streakLength < MaxStreakLength → boost _currentStreakColor weight
│   └── if streakLength >= MaxStreakLength → reset streak, start cooldown
│
└── OnBalloonSpawned(colorId)
    ├── if colorId == _currentStreakColor → _streakLength++
    └── else → start new streak candidate (probabilistic)
```

**Influence on balance pass — side bias:**

The streak system is not limited to spawn-time color picks. It can also steer the
`BalloonBalancer` to cluster same-color balloons spatially during the balance pass.
When the balancer settles balloons after a nudge or pop, it normally treats all
empty slots equally. With streak awareness, it can prefer dropping a balloon toward
the side where more of its color already sits — nudging same-color groups together
over successive turns without teleporting them.

```
StreakBalancer.GetBalanceBias(colorId, slotGrid) → float lateralBias
    ├── scan grid for existing balloons of colorId
    ├── compute weighted center-of-mass column for that color
    └── return bias toward that side (−1 = left, +1 = right, 0 = neutral)
```

`BalloonBalancer` applies the bias as a tie-breaker when multiple target slots are
equally valid: if two columns could receive a falling balloon, prefer the one closer
to the color's center of mass. The bias is subtle — it never overrides structural
constraints (support, traversability) and never moves a balloon farther than the
normal balance rules allow.

Over many turns this creates organic color regions that drift and merge, producing
streak opportunities the player can read and exploit. Combined with the spawn-time
color weight bias above, both the supply and the arrangement favour periodic
high-value streak windows.

**Considering bounces:** When a projectile bounces (Deflect outcome), the resulting
trajectory may cross additional balloons. The streak balancer should account for
spatial adjacency — placing same-color balloons in columns that a bounced projectile
is likely to traverse. This means the `SlotSelector` (Phase 8.3) needs a
color-aware placement mode:

- After placing a streak-colored balloon, bias adjacent slots toward the same color
- Weight columns reachable by a single bounce from common shot angles
- Cap spatial clustering to avoid making the grid trivially clearable

### 2.4 Full Grid Clear-Out Calculation

Before finalising a spawn pass, run a simulation to verify the grid is theoretically
clearable — that the player has a path to pop every balloon given optimal play. If
the grid is provably unclearable (e.g. Unbreakables block all paths to a cluster with
no Piercing items available), the spawner should adjust.

**Scope:**
- Lightweight: check that every non-permanent balloon is reachable by at least one
  projectile trajectory (ray-cast from launcher through grid, considering bounces)
- Medium: verify that the color distribution allows full clear given the available
  projectile colors (no orphaned single-color balloon behind an Absorber)
- Heavy: full Monte Carlo simulation of N random play-throughs to estimate clear
  probability (expensive — run only in editor / debug builds for level validation)

**Integration point:** `GridSpawner.ValidateGrid()` — called after placement, before
the spawn pass commits. If validation fails, re-roll problematic slots (up to N
retries) or log a warning for manual review.

---

## 3 — Custom Level Editor

Insert hand-designed levels between procedural ones. Useful for story beats,
tutorials, boss encounters, and curated difficulty spikes.

### 3.1 Level Sequence Model

```
LevelSequence
├── entries: LevelEntry[]
│   ├── ProceduralLevelEntry  → GridSpawner generates the grid
│   └── CustomLevelEntry      → loads from a LevelDefinition asset
└── GetEntry(levelIndex) → LevelEntry
```

`LevelSequence` is a ScriptableObject that defines the ordered list of levels.
Procedural entries use `DifficultyProfile`; custom entries reference a
`LevelDefinition` asset.

### 3.2 `LevelDefinition` ScriptableObject

```
LevelDefinition
├── GridLayout: SlotEntry[columns, rows]
│   └── SlotEntry: { ActorType, BalloonType?, ColorId?, ItemType?, HitsToPop? }
├── AvailableProjectileColors: ColorId[]
├── ObjectiveOverride: LevelObjective?    ← optional; null = use default scoring
├── TutorialOverride: TutorialSequence?   ← optional; triggers tutorial on entry
└── Metadata: { displayName, description, author }
```

### 3.3 Custom Level Editor Window (Unity Editor)

A Unity Editor window (`Window > BalloonParty > Level Editor`) for visual grid
authoring:

- Grid of slot buttons matching the game's column/row dimensions
- Click a slot → pick actor type, balloon type, color, item from dropdowns
- Drag to paint multiple slots with the same config
- Preview button: enters Play mode with the designed level loaded
- Save/Load: serialises to `LevelDefinition` SO assets in
  `Assets/ScriptableObjects/Levels/`

**Implementation:** `EditorWindow` subclass in `BalloonParty.Editor`. Uses
`SerializedObject` for undo support. Grid rendering via `GUILayout` or custom
`EditorGUI` drawing.

### 3.4 Level Sequence Integration

`DifficultyService` (Phase 8.4) gains a `LevelSequence` reference. On level
transition:
1. Read `LevelSequence.GetEntry(currentLevel)`
2. If `ProceduralLevelEntry` → `GridSpawner` runs as today
3. If `CustomLevelEntry` → `CustomLevelLoader` populates the grid from the
   `LevelDefinition` asset, bypassing `GridSpawner`

`CustomLevelLoader` implements `IGridSpawner` with `SpawnStage.DynamicActors`.
The coordinator runs it instead of `GridSpawner` when the current level is custom.

---

## 4 — Tutorial System & Pacing

Introduce new actors and mechanics gradually through guided tutorial sequences
rather than dropping everything on the player at once.

### 4.1 Tutorial Trigger System

```
TutorialTrigger
├── TriggerCondition: enum { OnLevelReached, OnFirstEncounter, OnItemAcquired, Manual }
├── TutorialSequence: TutorialSequence  ← reference to the tutorial content
├── RequiredActorType: ActorType?       ← for OnFirstEncounter
├── RequiredLevel: int?                 ← for OnLevelReached
└── HasBeenShown: bool                  ← persisted; never re-shows after completion
```

`TutorialManager : IStartable` — holds `TutorialTrigger[]` (from a
`TutorialConfiguration` SO). On each level start or actor encounter, checks
triggers and fires the first unshown match.

### 4.2 Tutorial Sequence Definition

```
TutorialSequence (ScriptableObject)
├── Steps: TutorialStep[]
│   ├── DialogStep     → text + optional character portrait
│   ├── HighlightStep  → highlights a grid region or UI element
│   ├── ActionStep     → waits for the player to perform an action (e.g. "tap to shoot")
│   └── PauseStep      → freezes gameplay for N seconds or until tap
├── PausesGameplay: bool   ← if true, sets Time.timeScale = 0 during the sequence
└── CompletionReward: int? ← optional bonus score on completion
```

### 4.3 Pacing — Suggested Actor Introduction Order

Tutorials fire on first encounter with each actor type. The difficulty curve
(Phase 8.4) controls when actors first appear, so tutorials are implicitly paced:

```
Level 1:    Tutorial — "Welcome" + basic shooting
Level 2–3:  Simple balloons only; no tutorial interruptions
Level 4:    First Tough balloon → Tutorial: "Tough balloons deflect — hit them twice"
Level 5–6:  Tough + Simple mix
Level 7:    First Soap Cluster → Tutorial: "Soap Clusters lose bubbles on each hit"
Level 8:    First Puff → Tutorial: "Puffs are decoration — balloons pass through them"
Level 9:    First Bush → Tutorial: "Bushes block paths — shoot around them"
Level 10:   First Deflector → Tutorial: "Deflectors bounce your shot — use the angle"
Level 12:   First Gatekeeper → Tutorial: "Gatekeepers break after 3 hits"
Level 14:   First Unbreakable → Tutorial: "Unbreakables can only be destroyed with items"
Level 16:   First Absorber → Tutorial: "Absorbers eat your shot — avoid them!"
```

These are starting suggestions — real pacing comes from playtesting.

### 4.4 Tutorial UI

- **Dialog overlay:** semi-transparent panel at bottom of screen; character portrait
  on left, text on right; tap to advance
- **Highlight mask:** full-screen dark overlay with a cutout around the highlighted
  element; animated pulse on the cutout edge
- **Action prompt:** arrow or hand icon pointing at the expected interaction point;
  fades once the player performs the action

**Implementation:** `TutorialView : MonoBehaviour` on a dedicated Canvas
(sort order above gameplay, below pause menu). Subscribes to `TutorialStepMessage`
via MessagePipe. Each step type has a corresponding view handler.

### 4.5 Persistence

`TutorialProgressData` — a simple `HashSet<string>` of completed tutorial IDs,
serialised to `PlayerPrefs` or a JSON file. `TutorialManager` checks this set
before firing any trigger.

---

## 5 — Relocated Future Ideas

Ideas previously embedded in other plan files, moved here for consolidation.

### 5.1 Soap Cluster Merge *(from PLAN-ContentProduction + PLAN-GridActorExpansion)*

Adjacent Soap Cluster balloons merge when nudged together or when proximity drops
below a threshold. The merged cluster's `HitsRemaining` = sum of both (capped at 5);
`_BubbleCount` transitions to the new count via a merge animation (two soap-film
rings flowing into each other). One `IBalloonModel` survives; the other is returned
to the pool.

Scoring angle: merge triggers a bonus payout (`mergedHits × baseScore`), creating a
risk/reward dynamic — letting clusters grow is tempting but reduces future individual
scoring opportunities.

Needs: neighbor query post-nudge, `ClusterMergeMessage`, merge VFX, and a
`BubblePopController` that knows which bubble index was added/removed for the
transition animation.

### 5.2 Unbreakable Roam *(from PLAN-ContentProduction)*

Before the normal balance pass runs, each Unbreakable balloon picks a random empty
slot on the grid and teleports (or animates) to it. Then the standard balance
algorithm resumes as normal, settling everything else around the Unbreakable's new
position.

This prevents Unbreakables from always drifting to the top of the grid (since they're
never popped, they accumulate at the highest rows after repeated balance passes). The
random repositioning reinforces the "autonomous robot" personality — it feels alive
and unpredictable, occupying different columns each turn.

Implementation sketch:
- New interface `IPreBalanceRelocatable` — marker on models that opt into pre-balance
  random relocation (only `UnbreakableBalloonModel` initially)
- `BalloonBalancer.Balance()` gets a pre-pass: iterate all grid slots, collect actors
  implementing `IPreBalanceRelocatable`, for each one pick a random slot from
  `SlotGrid.AllEmptySlots()`, remove from current slot, place at new slot, and record
  the move as a path segment for animation
- The relocation animation plays first (or as part of the same batch); then the normal
  balance loop runs and may move other actors that became unbalanced by the relocation
- Config knob on `BalloonsConfiguration` entry: `bool RelocatesOnBalance` — so the
  behaviour can be toggled per balloon type without code changes

### 5.3 New Balloon Archetypes *(from PLAN-GridActorExpansion)*

- **Chain** — pops adjacent same-color balloons when destroyed; needs neighbor query
  at pop time
- **Ghost** — `PassThrough` always (projectile travels through), pops after N passes

### 5.4 New Grid Actor Archetypes *(from PLAN-GridActorExpansion)*

- **Recolorer** — static; changes adjacent balloon colors each turn (undermines paint
  strategy)
- **Mover** — dynamic; shuffles adjacent balloons to an adjacent empty slot each turn
- **Spawner** — static; places a new balloon into an adjacent empty slot each turn
  (fills gaps)
- **ShieldTower** — static; grants periodic shields to adjacent balloons

### 5.5 `IPassThrough` Behaviour Extensions *(from PLAN-GridActorExpansion)*

**Density / passage resistance** — a Puff-like actor could expose a `float Density`
(0–1) that the animation system uses to modulate travel speed. A thin mist barely
slows the path; a dense cloud visibly delays it:
```csharp
public interface IPassThrough
{
    float Density { get; }  // 0 = no resistance, 1 = maximum slow
}
```
The spawn animation driver reads `Density` and scales the DOTween duration multiplier
for the segment that crosses the slot.

**Pass-through triggers** — when a balloon's spawn animation crosses a traversable
slot, the occupant can react:
```csharp
public interface IOnPassThrough
{
    void OnActorPassedThrough(ISlotActor passing);
}
```
Example uses: a **Recolorer** cloud that tints any balloon whose path arcs through
it; a **PowerUp** cloud that assigns an item; a **Curse** cloud that reduces
`HitsRemaining` by 1 on pass.

Both extensions are additive — `IPassThrough` stays a marker today and gains members
(or a companion interface) only when a concrete actor type demands it.

---

## 6 — Puff Cloud Polish & Performance

Items deferred from the completed Puff Cloud Simulation implementation.

### 6.1 Visual Polish

- **Shadow pass** — `_SHADOW_ON` toggle already exists in the shader but is tuning-incomplete; needs art-direction pass for shadow offset, softness, and fill width per scene context
- **Cloud fade-in on Puff spawn** — when a new Puff slot is placed, the cloud density should start at 0 and reform to 1.0 over ~0.5s, reusing the existing diffusion reform mechanic instead of appearing instantly
- **Cloud dissolve on Puff removal** — when a Puff slot is removed, the cloud should drain to 0 over ~0.5s (invert the reform trend for that cluster region) rather than vanishing immediately; mirrors the fade-in and feels organic

### 6.2 Performance & Device Scaling

- **Disturbance field resolution scaling** — scale `TexelsPerUnit` down based on `QualitySettings.GetQualityLevel()` (e.g. half resolution on `Low`, full on `High`); avoids a fixed RT size that may be too large for low-end devices
- **Diffusion tick rate throttle** — skip diffusion ticks on low-end when the frame budget is tight; `DiffusionTickInterval` is already configurable, but an adaptive version would watch `Time.deltaTime` against a budget threshold and extend the interval dynamically
- **Target: < 0.5 ms total** for all active cloud blits + diffusion tick on target mobile devices; profile with Unity's GPU profiler before shipping

### 6.3 Edge Cases

- **Cloud at grid boundary** — quad placement at extreme column/row positions can push cloud geometry outside the camera frustum; clamp the world-space quad to the field bounds
- **All Puffs removed in one frame** — if a balance pass removes support from every slot in a cluster simultaneously, `PuffClusterRegistry` publishes `Removed` and the view is returned to pool; verify the registry handles this atomically (no intermediate `Resized` event with zero slots)

---

## 7 — Vertical Cloud Drift

> Captured for when the feature is prioritised. Pairs well with the difficulty scaling system (Phase 8.4).

As the game progresses, Puff clouds could drift vertically through the grid — slowly migrating upward row by row. This makes the sky feel alive and gives the grid a sense of weather that evolves over a session.

**Concept:**
- On a configurable interval (every N turns, or tied to `DifficultyService` level transitions), each Puff slot relocates one row upward (or downward)
- `PuffClusterRegistry` handles the grid-level move: `SlotGrid.Remove` old slot, `SlotGrid.Place` at new slot — same pattern as `BalloonBalancer` relocations
- `PuffCloudView` animates the quad position lerp; density RT is preserved across the move so disturbance state carries over
- Puffs that drift off the top are removed; new Puffs can spawn at the bottom to replace them, creating a conveyor-belt effect

**Why it works with the current architecture:**
- `PuffClusterRegistry` recomputes adjacency on every `SlotGridChangedEvent` — a Puff moving one row naturally triggers cluster merge/split without extra code
- The density RT resize/copy logic handles cluster shape changes
- `IPassThrough` traversability is per-slot, not per-cloud, so moving a Puff correctly updates path computation

**Gameplay angle:**
- Clouds drifting through the grid change which columns have structural support (Puffs occupy slots) and which are visually distinct
- Faster drift at higher levels creates a more dynamic, harder-to-read grid — a natural difficulty knob
- Pairs well with `IOnPassThrough` triggers (see section 5.5) — a drifting cloud that tints or buffs balloons it passes through would create emergent gameplay

---

## Open Questions

1. **Pity system scope** — should pity tracking be global (across all spawn types) or
   per-category (balloons, items, grid actors independently)?

2. **Streak length tuning** — what's the ideal streak length before it feels unfair?
   Needs playtesting data. Start with max 4–5 same-color in a row.

3. **Custom level format versioning** — `LevelDefinition` will evolve as new actor
   types are added. Need a version field or migration strategy for older level assets.

4. **Tutorial skip** — should tutorials be skippable? Recommended: yes, with a
   "skip all tutorials" option in settings that sets all triggers to shown.

5. **Grid clearability — performance budget** — the lightweight ray-cast check is
   cheap; the Monte Carlo simulation is not. Define acceptable frame budget or
   confirm editor-only usage.

6. **Cloud fade timing** — what duration feels natural for the Puff cloud fade-in
   (on spawn) and dissolve (on removal)? Suggested starting point: 0.3–0.5 s,
   driven by the existing diffusion reform rate. Needs playtesting.

7. **Vertical drift interval** — how many turns between drift steps? Too fast feels
   chaotic; too slow is imperceptible. Suggested range: every 3–6 turns at base
   difficulty, scaling to every 1–2 turns at max.
