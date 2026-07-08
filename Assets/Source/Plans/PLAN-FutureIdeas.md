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

**Conditional pass-through — phase-gated + maturity timer** — instead of Puffs being
traversable at all times, gate traversability on two conditions:

1. **Phase-gated:** Puffs allow pass-through only during the **balance phase** (and
   post-spawn balance). During the hit phase and spawn animation, they block
   traversal. This means freshly-spawned balloons arc *around* Puffs while
   existing balloons consolidate *through* them — the player can read which paths
   the balancer will use because they coincide with the clouds.
2. **Maturity timer:** Each Puff starts in a **solid** state and becomes
   **permeable** after a configurable duration (e.g. 2–4 turns or N seconds of
   game time). Before maturing, the Puff blocks all traversal regardless of
   phase. After maturing, the phase-gated rule above applies.

Model sketch:
```csharp
public interface IPassThrough
{
    bool IsCurrentlyTraversable { get; }
}
```

`PuffObstacleModel` would track maturity internally:
```csharp
internal class PuffObstacleModel : IWriteableSlotActor, IPassThrough
{
    private int _turnsAlive;
    private readonly int _maturityThreshold;  // from config

    public bool IsMature => _turnsAlive >= _maturityThreshold;

    // Phase-gated: only traversable when mature AND during balance
    public bool IsCurrentlyTraversable => IsMature && _inBalancePhase;

    internal void OnTurnEnd() => _turnsAlive++;
}
```

`SlotGrid.IsTraversable` would call `IsCurrentlyTraversable` instead of just
checking for the marker interface. The balance phase flag could be set via a
shared `TurnPhaseTracker` service or a simple bool toggled by `BalloonBalancer`
before/after its pass.

Visual feedback: immature Puffs render denser/more opaque (shader `_Density`
parameter driven from `IsMature`), so the player can see they're currently
solid. On maturity transition, the cloud lightens over ~0.5 s via the existing
diffusion reform rate.

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

## 8 — Timed Release: Balloon Pass-Through Delay

> Balloons that land beneath a Puff cloud rest visibly on the support for a
> configurable beat, then float up through the traversable slots on the next
> balance pass. The delay makes the interaction readable — "the balloon is resting
> under the cloud" — before it naturally pushes through.

### 8.1 Current Behaviour

1. Pop happens → `BalanceBalloonsMessage` fires → `BalloonBalancer.Balance()` runs
   (next frame)
2. Balancer scans for `IsUnbalanced` → moves balloons toward row 0
3. Puff slots block movement because `IsEmpty` returns false → balloon stops below
   the cloud

### 8.2 Proposed Flow

1. Pop happens → balance runs as before
2. Balloon finds a Puff above it → can't move through (Puff is occupied) → **stays
   put for now**
3. After N seconds (configurable delay), the balloon is marked as *ready to pass
   through*
4. On the next balance pass, the balancer checks `IsTraversable` for `IPassThrough`
   slots and allows routing through them — but **only for balloons whose delay has
   elapsed**
5. Balloon animates **through** the cloud, stamping the disturbance field as it goes
   (the cloud reacts to the balloon passing through it)

### 8.3 Model Sketch

Each balloon model tracks how long it has been waiting beneath a blocking
`IPassThrough` slot:

```csharp
internal interface ITimedRelease
{
    bool IsReadyToPassThrough { get; }
    void BeginWait();
    void ResetWait();
}
```

`BalloonBalancer` sets `BeginWait()` when a balloon is blocked by a traversable slot
and calls `ResetWait()` when the balloon moves freely. On subsequent balance passes,
balloons with `IsReadyToPassThrough == true` are allowed to route through
`IPassThrough` slots.

**Config knob** (on `IBalloonsConfiguration` or a dedicated `IPassThroughSettings`):
- `float PassThroughDelay` — seconds a balloon must wait before it can traverse
  (e.g. 1.0–2.0 s)

### 8.4 Balancer Changes

- `OptimalNextEmptySlot` gains a traversability check: when evaluating upward paths,
  if the next slot implements `IPassThrough` and the moving balloon's
  `IsReadyToPassThrough` is true, treat the slot as passable
- Path computation may need to chain through multiple consecutive Puff slots (a
  balloon floating up through a tall cloud)
- `IsUnbalanced` must account for balloons that are *temporarily* stable (waiting
  under a cloud) vs. *permanently* stable

### 8.5 Visual Feedback

- **Waiting state:** subtle idle bob or squash animation on the resting balloon so
  the player reads it as "about to move"
- **Transit animation:** balloon floats upward through the cloud; each frame the
  balloon's world position stamps the disturbance field, creating a visible parting
  effect in the cloud density
- **Cloud reaction:** the disturbance dissipates via the existing diffusion reform
  rate — the cloud naturally closes behind the balloon

### 8.6 Relationship to Existing Pass-Through Ideas

This complements the **phase-gated + maturity timer** concept in section 5.5:
- **Maturity timer** (§ 5.5) gates when the *Puff itself* becomes permeable
- **Timed release** (this section) gates when the *balloon* is ready to pass through

Both can coexist: a Puff must first mature before any balloon can traverse it, and
even then the balloon must wait its own delay before floating through. The two timers
create a layered pacing system — the cloud opens up, then the balloon pushes through.

### 8.7 Open Questions

1. **Delay scaling** — should `PassThroughDelay` decrease at higher difficulty
   (faster pace) or increase (harder grid)?
2. **Per-balloon-type override** — should some balloon types (e.g. Tough) have a
   longer delay, reinforcing their heavy/sluggish personality?
3. **Multiple balloons queued** — if several balloons are waiting under the same
   cloud, do they all release simultaneously or stagger? Stagger feels more
   readable but adds complexity to the balancer.
4. **Interaction with Unbreakable Roam** (§ 5.2) — an Unbreakable that roams into a
   slot below a Puff should respect the same delay, or should roam bypass it?

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

8. **Puff maturity threshold** — how many turns before a Puff becomes permeable?
   Too short and the blocking phase is imperceptible; too long and it feels like a
   permanent wall. Suggested range: 2–4 turns. Should this scale with difficulty?

---

## 9 — Quality Settings System

> A runtime quality tier system that adapts rendering fidelity, VFX density, and
> simulation resolution to the target device. The game currently ships at one fixed
> quality level with no device-adaptive scaling.

### 9.1 Architecture

```
IQualityProfile (interface)
├── QualityTier Tier { get; }            // Low, Medium, High
├── ShaderQuality Shaders { get; }
├── VfxQuality Vfx { get; }
├── DisturbanceQuality Disturbance { get; }
├── AnimationQuality Animation { get; }
└── PoolQuality Pool { get; }

QualityProfileSettings : ScriptableObject, IQualityProfile
└── One SO per tier in Assets/ScriptableObjects/Quality/

QualityService : IStartable
├── Detects device capability at startup
├── Selects a QualityProfile
├── Exposes IQualityProfile for injection
└── Applies global settings (shader keywords, RT resolution, etc.)
```

`QualityService` runs once at startup. Consumers inject `IQualityProfile` (read-only)
and branch on tier or read specific values. No per-frame cost — all decisions are made
at init time.

### 9.2 Parameter Inventory

Every tuneable parameter in the current codebase that is a candidate for quality
binding, grouped by system.

#### Shader Keywords (GPU cost)

| Shader | Keyword | High | Low | Impact |
|---|---|---|---|---|
| PuffCloud | `_SHADOW_ON` | ON | OFF | Removes shadow pass — saves 3 Simplex calls/frag |
| PuffCloud | `_DENSITY_ON` | ON | ON | Disturbance reaction — cheap (1 tex2D), keep on all tiers |
| Bush (planned) | `_SHADOW_ON` | ON | OFF | Same as Puff |
| Bush (planned) | `_LIGHTING_ON` | ON | OFF | Removes lighting gradient — saves 4 Simplex calls/frag |
| Bush (planned) | `_DISTURBANCE_ON` | ON | ON | Cheap — keep on all tiers |
| Bush (planned) | `_CENTER_SHADOW_ON` | OFF | OFF | Optional polish — off by default all tiers |
| SoapBubbleCluster | `_SHADOW_ON` | ON | OFF | Shadow under soap cluster |
| PaintBlob | `_SHADOW_ON` | ON | OFF | Shadow under paint splat |

**Implementation:** `QualityService.Start()` iterates all affected materials and calls
`material.EnableKeyword()` / `material.DisableKeyword()`. Alternatively, views read
`IQualityProfile.Shaders` in their setup and toggle locally.

#### Disturbance Field (GPU + memory)

| Parameter | Setting | High | Medium | Low | Source |
|---|---|---|---|---|---|
| `TexelsPerUnit` | RT resolution | 8 | 6 | 4 | `IDisturbanceFieldSettings` |
| `DiffusionTickInterval` | Blit frequency | 0.05s | 0.066s | 0.10s | `IDisturbanceFieldSettings` |
| `MaxLerpStamps` | Concurrent stamps | 32 | 16 | 8 | `IDisturbanceFieldSettings` |

**Impact:** Halving `TexelsPerUnit` (8→4) reduces the RT from ~48×72 to ~24×36
texels — 4× fewer pixels in the diffusion blit. `DiffusionTickInterval` at 0.10s runs
the blit 10×/sec instead of 20×/sec — halves GPU blit work.

**Implementation:** `DisturbanceFieldService` already reads these from
`IDisturbanceFieldSettings`. Option A: have `QualityService` modify the SO values at
startup (mutates shared asset — fragile). Option B: create a `QualityAwareDisturbanceSettings`
wrapper that reads the base SO and overrides values based on tier. Option B is cleaner.

#### Unbreakable Balloon GrabPass (GPU — single highest-cost operation)

The `UnbreakableBalloon.shader` uses a **named `GrabPass { "_GrabTexture" }`** to
capture the framebuffer for realtime convex-mirror reflections. Key facts:

- **One grab per frame** — named GrabPass is shared by all Unbreakable instances, so
  count doesn't matter. But even one grab is expensive.
- **Cost: ~0.3–1.0 ms on mobile** — forces a tile flush + full-screen copy on TBDR
  GPUs (all mobile). This is likely the single most expensive rendering operation in
  the game.
- **Each Unbreakable = 4 quadrant SpriteRenderers** + inner renderers, all sharing
  `_SphereCenter` via MPB from `UnbreakableBalloonVariant.Update()`.
- **Unbreakables don't reflect each other** — the named GrabPass fires once before any
  Unbreakable draws, so `_GrabTexture` contains the scene *without* any Unbreakables.
  All instances read the same pre-Unbreakable snapshot.

**Quality tier options:**

| Setting | High | Medium | Low |
|---|---|---|---|
| GrabPass frequency | Every frame | Every 2nd frame | Disabled |
| Reflection content | Includes other Unbreakables (1-frame stale) | Same | Static metallic |
| Reflection fallback | N/A | Reuse last `_reflectionRT` | Flat metallic gradient |

**Implementation — `CommandBuffer`-based capture (`ReflectionCaptureService`):**

Replace the in-shader `GrabPass` with a C#-managed `CommandBuffer` blit into a
persistent `RenderTexture`. This gives full control over capture timing and frequency,
and as a bonus **fixes the self-reflection limitation**.

```
Current (GrabPass):
  1. Background, balloons, grid actors render
  2. GrabPass fires → captures framebuffer (no Unbreakables in it)
  3. Unbreakable A renders (reads _GrabTexture — sees scene only)
  4. Unbreakable B renders (reads _GrabTexture — sees scene only, NOT A)

Proposed (CommandBuffer):
  1. Everything renders — including Unbreakables (using previous frame's _reflectionRT)
  2. CommandBuffer blit → _reflectionRT (now contains scene + all Unbreakables)
  3. Shader.SetGlobalTexture("_GrabTexture", _reflectionRT)

  Next frame:
  1. Everything renders — Unbreakables read _reflectionRT from last frame
     → each Unbreakable now sees OTHER Unbreakables in its reflection (1 frame stale)
  2. CommandBuffer blit → _reflectionRT (refreshed)
```

At 60 fps the 1-frame staleness is invisible. Racing games use the same trick for
car-to-car reflections.

**Key architectural steps:**

1. **Remove** `GrabPass { "_GrabTexture" }` from `UnbreakableBalloon.shader`
2. **Add** `#pragma shader_feature _REFLECTION_ON` — gates the entire reflection
   code path (sample + distortion). When off, the shader skips to pure metallic.
3. **Create `ReflectionCaptureService : IStartable, IDisposable`** — owns a persistent
   `RenderTexture _reflectionRT`, attaches a `CommandBuffer` to the camera at
   `CameraEvent.BeforeForwardAlpha` (or appropriate event so all Unbreakables have
   rendered before the next frame's capture reads it)
4. **Render order:** Unbreakables must draw in a sorting layer/order that places them
   *after* all other Transparent objects, so the capture (which happens after them)
   includes their pixels. Their current sorting order likely already does this since
   they're balloons in the grid, but verify.
5. **Every-other-frame:** `ReflectionCaptureService` checks `Time.frameCount % 2`
   (or a configurable skip count from `IQualityProfile`) and only blits on capture
   frames. On skip frames, `_reflectionRT` retains the previous content.
6. **Set globally:** `Shader.SetGlobalTexture("_GrabTexture", _reflectionRT)` — the
   shader reads the same sampler name, no shader code changes needed for the sample.

**Disabled reflection (Low tier):**
`ReflectionCaptureService` does not create the RT or attach the CommandBuffer.
Material has `_REFLECTION_ON` disabled. Shader renders pure metallic gradient +
specular — still reads as chrome, just no environment reflection. Zero GPU cost from
capture.

**Visual improvement — self-reflection:**
This is not just a performance optimization. The CommandBuffer approach fixes a
visible limitation: multiple Unbreakables on screen will now reflect each other
(1 frame stale). The current GrabPass approach makes each Unbreakable look like it
exists in isolation — the scene behind it is reflected but other chrome balloons are
invisible. With the proposed approach, a cluster of Unbreakables will show subtle
chrome reflections of their neighbors, selling the metallic material much better.

**Estimated savings:**
- Every-other-frame: **~0.15–0.5 ms** per skipped frame
- Disabled: **~0.3–1.0 ms** every frame — the biggest single GPU win available

**Estimated visual gain:**
- Unbreakable-to-Unbreakable reflections — noticeable when 2+ are adjacent

#### Particle VFX (GPU fill rate + CPU simulation)

| Parameter | High | Low | Impact |
|---|---|---|---|
| Pop VFX particle count | Default (burst count from prefab) | 50% of default | Fewer particles per pop |
| Pop VFX lifetime | Default | 70% of default | Shorter particles = less overdraw |
| Trail VFX emission rate | Default | 50% | Less trail density |
| Simultaneous VFX cap | No cap | Max 4 active particle systems | Evict oldest when full |

**Current VFX prefabs (10):**
- Balloon: `PSVFX_BalloonPop`, `PSVFX_ToughBalloonPop`, `PSVFX_SoapClusterPop`
- Items: `PSVFX_BombRange`, `PSVFX_PaintSplash`, `PSVFX_PaintFlyDrip`, `PSVFX_ShieldGainPU`
- Shield: `PSVFX_ShieldGain`, `PSVFX_ShieldBounce`, `PSVFX_ShieldLose`

**Implementation:** VFX pool channels already exist. A `QualityAwareParticleScaler`
could modify `ParticleSystem.main.maxParticles` and `startLifetime` on spawn. Or:
create Low/High prefab variants and swap via `IQualityProfile` — more work but
art-directable.

#### Projectile Trail (GPU fill rate)

| Parameter | High | Low | Impact |
|---|---|---|---|
| `TrailRenderer` time | Default | 50% | Shorter trail = less overdraw |
| `TrailRenderer` width curve | Default | Narrower | Less fill |

**Files:** `ProjectileTrail.cs`, `FlyingTrail.cs`, `LevelUpTrailEffect.cs`

**Implementation:** Read `IQualityProfile` in trail setup and scale
`_trailRenderer.time` and width.

#### Procedural Shader Octave Count

| Shader | Parameter | High | Low |
|---|---|---|---|
| PuffCloud | Noise octaves | 3 | 2 |
| Bush (planned) | LeafNoise octaves | 3 | 2 |

**Implementation option A — runtime uniforms:** Add `_OctaveCount` int property to
the shader and branch in the noise function:
```hlsl
if (_OctaveCount >= 3) n += SimplexNoise2D(pFine) * 0.20;
```
Branching on a uniform is well-predicted on mobile GPUs — minimal cost.

**Implementation option B — shader variants:** `#pragma multi_compile OCTAVES_2 OCTAVES_3`.
Compiles two variants, no runtime branching, but increases shader memory.

**Recommendation:** option A for simplicity. The branch is uniform (same for all
fragments in a draw call), so the GPU doesn't pay warp-divergence cost.

#### Animation Quality

| Parameter | High | Low | Impact |
|---|---|---|---|
| Balance animation speed | 1.0× | 1.5× (faster settle) | Less time animating = fewer DOTween ticks |
| Nudge animation damping | Full physics | Faster settle, less bounce | Less jitter computation |
| Score trail curve resolution | Default | Fewer waypoints | Less Bezier evaluation |

These are minor CPU savings — only relevant if profiling shows DOTween overhead.
Low priority.

#### Pool Sizes

| Pool | High | Low | Impact |
|---|---|---|---|
| Balloon pool initial capacity | Current | Same | No change — pool grows on demand |
| Particle pool max reuse | No cap | Cap at 8 | Limits peak VFX memory |
| Score trail pool | Current | Smaller | Less memory |

Pool sizes mainly affect memory, not frame time. Low priority for quality tiers.

### 9.3 Suggested Tier Thresholds

| Tier | Target devices | Selection heuristic |
|---|---|---|
| **High** | iPhone 11+, flagship Android (2020+) | `SystemInfo.graphicsMemorySize >= 3072` AND `SystemInfo.processorCount >= 6` |
| **Medium** | iPhone 8–10, mid-range Android | Default fallback |
| **Low** | iPhone 7, budget Android (< 3 GB RAM) | `SystemInfo.graphicsMemorySize < 2048` OR `SystemInfo.processorFrequency < 1800` |

Alternative: use `Application.targetFrameRate` and monitor `Time.deltaTime` over the
first 5 seconds — if the device can't hold 60 fps during the intro, drop to Low.

### 9.4 Implementation Priorities

| Priority | System | Savings | Effort |
|---|---|---|---|
| **P0** | GrabPass frequency / disable (`_REFLECTION_ON`) | ~0.3–1.0 ms — single biggest GPU win | Medium — CommandBuffer replacement |
| **P0** | Shader keywords (`_SHADOW_ON`, `_LIGHTING_ON`) | ~30–50% GPU per cluster shader | Low — toggle keywords on material |
| **P1** | Disturbance RT resolution (`TexelsPerUnit`) | ~75% blit cost reduction (8→4) | Low — wrapper around settings |
| **P1** | Disturbance tick interval | ~50% blit frequency | Low — same wrapper |
| **P2** | Particle count/lifetime scaling | Reduces fill rate spikes on pop | Medium — scaler on pool spawn |
| **P2** | Noise octave count (uniform branch) | ~15% per cluster shader | Low — add uniform + branch |
| **P3** | Trail length/width scaling | Minor fill reduction | Low — read profile in trail setup |
| **P3** | Animation speed scaling | Minor CPU | Low — multiply durations |
| **P4** | Pool size caps | Memory only | Low |

### 9.5 Open Questions

1. **When to detect tier** — at app startup (once) or dynamically (adaptive)? Adaptive
   is more complex but handles thermal throttling. Start with startup detection; add
   adaptive downgrade later if needed.

2. **Per-shader vs. global keywords** — should `_SHADOW_ON` be toggled globally (all
   shadows off on Low) or per-shader (Puff keeps shadows, Bush doesn't)? Global is
   simpler; per-shader gives more art control.

3. **User-facing settings** — should the player be able to choose quality tier
   manually (Settings menu)? Recommended: yes, with an "Auto" default that uses
   device detection.

4. **Disturbance field disable** — on extremely low-end, should the entire disturbance
   system be disabled (no RT, no blits, no stamps)? This saves the most GPU but
   removes all cloud/bush reactions. Needs a `_DENSITY_ON` / `_DISTURBANCE_ON`
   keyword disable path that renders clusters without any reaction.

5. **Testing matrix** — which devices define the Low/Medium/High boundaries?
   Need a concrete test device list before shipping quality tiers.

---

## 10 — Baked Noise Texture for Puff Clouds

> Replace runtime Simplex noise evaluation in the PuffCloud shader with a
> pre-baked tileable noise texture lookup.

**Feasibility: ✅ Yes — High Confidence.**
2D Simplex noise is deterministic and stateless. A single octave can be
pre-sampled onto a tileable (wrapping) 512×512 R8 texture and fetched
with a single `tex2D` instead of ~30 ALU ops per evaluation.

**Current cost:** 15–24 Simplex evaluations per fragment (3 octaves ×
main cloud + 4 lighting samples + shadow + density) = **450–720 ALU ops**
just for noise. With baked texture: same number of `tex2D` fetches with
high cache coherence — ~90% ALU reduction.

**Implementation outline:**
1. Editor tool: `NoiseTextureBaker` generates 512×512 R8 tileable Simplex
   texture (4D torus mapping for seamless wrap, or large 2D + Repeat)
2. Shader: replace `SimplexNoise2D(p)` → `tex2D(_NoiseTex, p).r * 2.0 - 1.0`;
   keep three-octave structure, each octave samples at different UV scale
3. Wire `_NoiseTex` on the PuffCloud material; verify visual parity
4. Also benefits `DisturbanceStamp.shader` (same noise include)

**Texture:** 512×512 R8 = 256 KB, Repeat wrap, Bilinear, no mipmaps.

**Priority:** Medium — biggest GPU bottleneck on mobile. Do after bush
Phase 4 (rattle). Pairs with section 11 below for a unified preload pass.

---

## 11 — Runtime Bush Baking at Preload

> Move bush leaf atlas and branch map generation from editor-only export
> to a runtime preload step. Textures generated on first load instead of
> shipped as assets.

**Feasibility: ✅ Yes — estimated ~30ms on mobile.**

| Operation | Est. time |
|---|---|
| Branch generator (4 variants, CPU) | ~2ms |
| Branch mesh build + render (4×) | ~8ms |
| Leaf bake (8 variants × 64×64) | ~15ms |
| Atlas readback | ~5ms |
| Leaf extraction from segments (CPU) | ~1ms |
| **Total** | **~30ms** |

Well within a loading screen budget. Same shaders, same generator —
output is deterministic for a given seed. Random seed per session gives
infinite variety at zero storage cost.

**What needs to change:**
1. Move bake shaders from `Shaders/.../Editor/` to `Hidden/` runtime paths
2. Extract `BushBranchGenerator`, `BushLeafExtractor`, bake settings from
   editor assembly to runtime (generator + extractor are pure math, no
   editor deps)
3. Create runtime bakers (replace `AssetDatabase` calls, use `Object.Destroy`)
4. `RuntimeLeafAtlasPacker`: render to `Texture2D` directly, compute UV rects
   manually (no `Sprite` objects needed — `BushView` already uses raw `UVRect`)
5. `BushBakeService` orchestrator: runs during preload, outputs variant data
6. Keep editor bake pipeline intact for preview/iteration

**Benefits:** ~1–2 MB build size reduction, infinite per-session variety,
no export step for design iteration, single source of truth (settings SO).

**Priority:** Low — current pipeline works. Best as polish after all bush
phases complete. Combine with section 10 into a unified preload bake pass.

---

## 12 — Losing Conditions

The game currently has **no fail state**: each turn `BalloonSpawner` adds lines,
`BalloonBalancer` consolidates them, and the player chases per-color level-ups
indefinitely. Picking a losing condition effectively picks the genre feel —
survival-puzzle, score-attack with a clock, or run-based. The ideas below are
grounded in systems that already exist.

A shared property: **all of these reuse the `Navigation` state machine.** A
`GameOver` state slots in beside `Launch` / `Game` / `LevelUp`, and the thrower
already no-ops its `Tick` outside the `Game` state, so input-lockout on loss is
essentially free.

### 12.1 Grid encroachment — the natural fit (Puzzle Bobble lineage)

Balloons advance toward the thrower; if any actor crosses a danger boundary on
the thrower's side of the grid, the player loses.

- **Why it fits:** the whole engine already exists — `BalloonSpawner` adds lines
  each turn, `BalloonBalancer` consolidates rows, and the **post-spawn balance
  phase** is a clean, already-present "end of turn" checkpoint for a fail check.
  `SlotGrid` knows every occupied row/slot.
- **Build surface:** a `BreachDetector` (`IStartable`) that, after the post-spawn
  balance settles, checks whether any `ISlotActor` occupies the deadline row(s);
  if so, transition `Navigation` to a new `GameOver` state (mirrors `LevelUp`
  handling). One config value (`DeadlineRow`) + lines-per-turn from
  `BalloonsConfiguration` is the whole difficulty curve.
- **Tension knobs:** lines/turn, deadline depth, and — elegantly —
  `UnbreakableBalloonModel` becomes the threat (only pops on `Piercing`, so it
  deflects and clogs, pushing the front toward the deadline). Tough / Gatekeeper
  accumulation does the same.

**Recommended default** — most mechanically native, readable to the player, and
reuses the turn pipeline almost entirely.

### 12.2 Resource economy — make the existing Shield item matter

- **Finite ammo / magazine.** Today the thrower reloads infinitely on
  `ProjectileDestroyedMessage`. Cap reloads per level; the **Shield item** (which
  already grants projectile shields) becomes the lifeline that extends a run. Run
  dry before the level-up threshold → lose. Turns a currently-minor item into a
  must-grab and makes every deflect (wasted shield) sting.
- **Shield-only survival.** Keep infinite reloads but projectile shields are the
  player's life — if a projectile dies with zero productive pops (all
  deflects/absorbs), bleed a global life counter. Ties into the existing
  `Deflect` / `Absorb` outcomes and `AbsorberActorModel`.

### 12.3 Clock / turn pressure — score-attack flavor

- **Turn limit per level.** `BalloonSpawner` already tracks `_turnCount`. Give
  each level a throw budget; `ScoreController` already knows the level threshold
  and per-color confirmed progress. Don't reach the next level in N throws →
  lose. Because level-up needs **all** colors to meet threshold, a starved color
  naturally creates the failure pressure without anything new.
- **Combo decay variant.** Use `ColorStreakTracker` inversely — a "heat" bar that
  drains over time and only refills on pops/streaks; hits zero → lose. Leans into
  the streak system already built.

### 12.4 Lockout / soft-lock (safety net, not a headline mechanic)

- **Column lockout.** `GatekeeperActorModel` blocks a column until destroyed. If
  every firing lane is blocked by Gatekeepers / Absorbers / Unbreakables so no
  productive shot exists, declare loss. Niche, but a good backstop so a clogged
  board doesn't simply stall.

### 12.5 How these compose

The strongest design usually pairs **one spatial pressure** (12.1) with **one
economy or clock** (12.2 or 12.3): the grid creeps down while ammo/turns run out,
so the player is squeezed from two directions. 12.4 is the safety net underneath
either.

---

## 13 — Roguelike Run Modifiers (Unlockables)

> Per-run modifiers earned through progression that reshape spawn odds and grant
> triggered board effects — a strategy / roguelike layer on top of each run. Offered
> at level-up, presented as cards, and reset when the run resets. Pairs strongly with a
> fail state (§ 12) — picks matter far more when a run can be lost.

### 13.1 Concept

As a run progresses the player unlocks modifiers that bias which balloons/items spawn
and grant triggered effects. Two archetypes:

- **Passive (stacking)** — a permanent boost for the rest of the run; adds to a
  run-scoped modifier stack. E.g. *"+0.15% bomb-item spawn chance"*, *"+1 item type
  per spawn pass"*, *"Tough balloons start with one less hit"*.
- **Active ("spawn now" / triggered)** — a one-shot or charge-limited effect the
  player fires during gameplay. E.g. *"Spawn Bomb"* activates a bomb on a current grid
  balloon; *"Clear Row"* wipes the deadline line.

Visual representation: **cards** (the open presentation question). Level-up deals a
choice of N cards; pick one, it joins the run's build.

### 13.2 When unlocks are offered

Level-up is the natural trigger — `ScoreLevelUpMessage` already fires and the level-up
ceremony (`LevelUpCinematic` + popup) is an existing frozen beat. A card-select step
slots into or just after the popup: freeze via `TimeScaleService` (already the pattern
there), present the cards, apply the pick, release. Reuse the level-up gating so
nothing else runs during selection; a new `Navigation`/popup sub-phase, not a new scene.

### 13.3 Model sketch

```csharp
internal interface IRunModifier { ModifierKind Kind { get; } }   // Passive | Active

// Passive: contributes to the run-scoped accumulator queried at decision points.
internal interface IPassiveModifier : IRunModifier { void Apply(RunModifierState state); }

// Active: fired by the player; one-shot board effect.
internal interface IActiveModifier : IRunModifier
{
    int Charges { get; }
    void Trigger(/* SlotGrid, IHitDispatcher, BalloonControllerRegistry … */);
}
```

`RunModifierStack : IRunResettable` — holds acquired modifiers, rebuilds the
accumulated passive state, clears on `ResetRun`. **Must be `IRunResettable`** so a new
run starts clean (same reset graph as `ScoreController` / `LevelDifficultyResolver`).

`RunModifierState` — the accumulated passive values read at decision points: per-type
spawn-weight multipliers, "+N item types per pass", and global tuning (bomb radius,
starting shields, hits-to-pop deltas).

### 13.4 Passive modifier ideas

Plug into existing decision points:

- **Spawn odds** (via `ItemAssigner` weighting / the § 2.1 per-level weights): "+X%
  chance for item type T", "+1 item slot per spawn pass", "beneficial balloon type
  more common". The run stack is a run-scoped sibling of § 2.1's per-level
  `DifficultyProfile` weights — same multiply-on-base mechanism, different scope.
- **Item power** — wider bomb radius, longer lightning chains, bigger paint splash;
  the handler reads `RunModifierState` alongside `ItemConfiguration`.
- **Projectile** — +1 starting shield on load, an extra reload, a free color-swap.
- **Balloon softening** — Tough/Gatekeeper start with fewer hits.
- **Economy** (if a fail state ships, § 12) — +turn budget, slower grid encroachment.
- **Meta-tuning as cards** — expose the pity threshold (§ 2.2) and streak bias
  (§ 2.3): "droughts end faster", "longer color streaks".

### 13.5 Active "spawn now" + grid-effect ideas

Most reuse the existing item handlers and hit pipeline — an active modifier is largely
"fire an item/effect without a projectile carrying it":

- **Spawn Bomb / Lightning / Paint** — pick a target grid balloon (rule-picked or
  player-tapped) and run the item through `ItemActivator` / `IHitDispatcher` as if it
  had been carried there. Reuses `BombItemHandler` etc. wholesale.
- **Board clear (full)** — pop every poppable balloon; the panic button. Iterate
  `BalloonControllerRegistry`, dispatch a pop hit each. Rare / expensive.
- **Row / column clear** — wipe one line; the deadline row is the natural target if
  encroachment loss ships (§ 12.1). Cheaper and more tactical than a full clear.
- **Color purge** — pop or recolor every balloon of one color. Pairs with the
  all-colors level-up requirement: rescue a starved color or thin a dominant one.
- **Targeted strike** — player taps one balloon; pop it + neighbors (small AoE). Good
  for digging out a buried Unbreakable/Absorber.
- **Downgrade** — knock one hit off every Tough/Gatekeeper, or make one Unbreakable
  poppable for a turn.
- **Gravity / shuffle** — force a balance pass or reshuffle colors to manufacture a
  streak opportunity (ties to § 2.3's StreakBalancer).
- **Rain** — spawn a burst of item-carrying balloons on the next pass (a loot beat).
- **Freeze** — halt grid encroachment for N turns (defensive, if § 12.1 ships).

Two sub-flavors: **untargeted** (fires on a rule-picked target — simplest UI) vs
**targeted** (player selects — needs a targeting mode: tap-to-select with a highlight,
gated behind a pause/`Navigation` state so the shot input doesn't fire instead).

### 13.6 Integration points (existing systems)

- `ItemActivator` / `IHitDispatcher` / item handlers — active item effects, no new path.
- `ItemAssigner` weighting + § 2.1 weights — the passive spawn-odds stack.
- `LevelDifficultyResolver` / `IActiveLevelParameters` — passive values could resolve
  here so per-level params and run modifiers come through one read.
- `BalloonControllerRegistry` — board / color / targeted effects iterate live balloons.
- `ScoreLevelUpMessage` + level-up ceremony — the card-offer trigger.
- `Navigation` + `TimeScaleService` — freeze for card-select / targeting.
- `IRunResettable` — the modifier stack and any charges reset per run.

### 13.7 Card presentation (the open visual question)

- Level-up deals N cards (3?); pick 1. Rarity tiers (common/rare) tune the pool.
- Active cards live in a small HUD tray with a charge count; arming one either
  auto-fires or enters targeting.
- Passive cards show as the run's collected build — readable at a glance so the player
  understands their current odds.
- Card = icon + name + one-line effect + rarity frame. The `SpriteLayerCombiner` baked
  sprite tooling could pre-compose card art.

### 13.8 Open questions

1. **In-run only vs meta-progression** — the examples are all run-scoped ("whole
   run"). Is there also a cross-run meta layer (which cards can appear)? Start in-run;
   meta is a separate axis.
2. **Offer economy** — free pick each level-up, or a currency (score, or a new drop)
   spent in a shop? Free-at-level-up is simplest and most readable.
3. **Active trigger UX** — charges vs cooldown vs one-shot-consumed, and how arming
   interacts with the thrower input (needs a mode switch so a tap doesn't fire the shot).
4. **Balance / stacking caps** — do passives cap (spawn chance ≤ 100%)? Do duplicate
   cards stack linearly or with diminishing returns?
5. **Targeting on touch** — reuse the aim/tap input or a dedicated select mode? The
   prediction-trace UI may repurpose for target highlighting.
