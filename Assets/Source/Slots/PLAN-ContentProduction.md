# Content Production Plan — Pre-8.3 Assets

> Asset and content work required before Phase 8.3 (Procedural Placement) can run
> meaningfully in-game. The procedural engine selects actors by weight and places them
> into slots — it can only be tuned if the actors it places actually exist as playable,
> recognisable objects.
>
> **This plan covers art, animation, VFX, prefabs, and config wiring for every actor
> and balloon archetype introduced in the Phase 8 code plan.**

---

## Context

The procedural algorithm (8.3) draws from two config lists:

- `BalloonsConfiguration.Entries` — already contains Simple and Tough entries
- `GridActorConfiguration.Entries` — **does not exist yet** (needs a new SO)

Neither list is useful until the prefabs those entries reference actually exist with
correct visuals, animations, and hit feedback. Placeholder grey boxes make it impossible
to read the grid, tune difficulty, or playtest spawn density.

---

## Asset status overview

| Actor / Balloon | Model class | Prefab | Sprite / Art | Animator | Animations | VFX | Config entry | Notes |
|---|---|---|---|---|---|---|---|---|
| **Simple** | `BalloonModel` | ✅ `Balloon.prefab` | ✅ | ✅ `Balloon.controller` | ✅ Stable/Unstable Idle | ✅ `PSVFX_BalloonPop` | ✅ `BalloonsConfiguration` | Baseline; no blockers |
| **Cracking** | `BalloonModel` (config) | ✅ reuse Simple | ❌ crack-state sprites | ❌ needs crack states | ❌ `OnHit` state | ❌ dust/crumble | ✅ add entry in config | Same prefab; view must react to `HitsRemaining` |
| **Tough** | `ToughBalloonModel` | ✅ `ToughBalloon.prefab` | ✅ | ✅ `ToughBalloon.controller` | ✅ Stable/Unstable Idle | ✅ `PSVFX_ToughBalloonPop` | ✅ `BalloonsConfiguration` | Baseline; no blockers |
| **Unbreakable** | `UnbreakableBalloonModel` | ❌ | ❌ | ❌ | ❌ Idle, Deflect react | ❌ deflect hit, pierce-pop | ❌ add to `BalloonsConfiguration` | Permanent obstacle feel; no crack states |
| **Puff** | `PuffObstacleModel` | ⚠️ `StaticTest.prefab` | ⚠️ placeholder | ❌ | ❌ Idle float | — | ❌ `GridActorConfiguration` | Dandelion puff / soft cloud; traversable |
| **Bush** | `BushObstacleModel` | ❌ | ❌ | ❌ | ❌ Idle sway | — | ❌ `GridActorConfiguration` | Park shrub; blocks paths, no hit reaction |
| **Deflector** | `DeflectorActorModel` | ❌ | ❌ | ❌ | ❌ Idle, Deflect flash | ❌ bounce flash | ❌ `GridActorConfiguration` | Reflective surface; indestructible |
| **Absorber** | `AbsorberActorModel` | ❌ | ❌ | ❌ | ❌ Idle pulse, Absorb | ❌ absorb burst | ❌ `GridActorConfiguration` | Hazard; ends the turn on contact |
| **Gatekeeper** | `GatekeeperActorModel` | ❌ | ❌ | ❌ | ❌ Idle, Hit crack, Break | ❌ hit dust, break burst | ❌ `GridActorConfiguration` | Degrades visually as `HitsRemaining` drops |

**Legend:** ✅ exists and production-ready · ⚠️ placeholder exists · ❌ does not exist

---

## Shared infrastructure needed

These items are pre-requisites that unlock multiple actors at once.

### `GridActorConfiguration` ScriptableObject

A new SO holding `GridActorPrefabEntry[]` — the list the procedural spawner reads.
Needs to be created, added to the scene's `GameLifetimeScope` bindings, and registered
just like `BalloonsConfiguration` is today.

Scope of work:
- [ ] Write `GridActorConfiguration.cs` (mirrors `BalloonsConfiguration` structure)
- [ ] Register in `GameLifetimeScope` with `builder.RegisterInstance`
- [ ] Create the SO asset in `Assets/ScriptableObjects/` (or equivalent)
- [ ] Wire into `StaticActorSpawner` and eventually `GridSpawner`

### `GridActorView` prefab root pattern

All five grid actors share the same `GridActorView` MonoBehaviour. Each actor gets
its own prefab with the `GridActorView` component, its own sprite renderer, and its
own animator. Pool key is derived from the prefab GameObject name.

Prefab location: `Assets/Prefabs/Grid/`

---

## Per-actor detail

---

### Cracking Balloon

**What it is:** A Simple balloon that takes N hits before popping. The view must show
visible damage between hits — cracks, dents, surface tears — that grow as
`HitsRemaining` decreases reactively.

**Art direction:** Balloon skin visibly cracking under pressure; same floaty shape as
Simple, progressively damaged look.

- [ ] **Sprites** — crack overlay sprites for each intermediate hit state (N-1 states)
      Suggested approach: sprite swap or crack overlay sprites layered on top of the base
      balloon sprite, driven by `HitsRemaining` subscription in the view
- [ ] **View update** — `BalloonView` (or a `CrackingBalloonVariant`) subscribes to
      `IHasDurability.HitsRemaining` and swaps/blends crack overlay at each threshold
- [ ] **VFX** — small dust/debris puff on each non-killing hit (`OnHit` feedback)
- [ ] **Config entry** — add a Cracking entry to `BalloonsConfiguration` with `HitsToPop = 3`
      (or 5); weight and maxCount to be tuned in playtesting

**Dependency:** Uses existing `Balloon.prefab` — needs a crack overlay child or a
sprite swap mechanism, not a new prefab root.

---

### Unbreakable Balloon

**What it is:** A permanently present balloon obstacle. Deflects every hit. Only a
Piercing item (Bomb, Laser) can pop it.

**Art direction:** Heavier, more opaque than regular balloons — implies "this won't
move easily". Could be metallic, stone-textured, or wrapped in thorns. Park theme
suggests a **knotted/tied-off balloon** with thick rubber texture, or a **stone balloon**
(floaty but clearly different weight class).

- [ ] **Sprite** — distinct from all other balloon types; should read as "tough/permanent"
      at a glance
- [ ] **Prefab** — `Assets/Prefabs/Balloon/UnbreakableBalloon.prefab` with `BalloonView`
      component; reference in `BalloonsConfiguration`
- [ ] **Animator / Controller** — `UnbreakableBalloon.controller`
      - `StableIdle` — slow, heavy float (slower than Simple idle)
      - `UnstableIdle` — subtle wobble during balancer movement
      - `DeflectReact` — brief recoil/shake when hit (does NOT pop)
- [ ] **VFX** — `PSVFX_UnbreakableDeflect` — spark/clank on deflect hit
      `PSVFX_UnbreakablePop` — pop VFX for when Piercing finally destroys it
- [ ] **Config entry** — add to `BalloonsConfiguration` with `HitsToPop = 1` (the Piercing
      check bypasses HitsRemaining entirely); weight = low; maxCount = 2–3

---

### Puff

**What it is:** Traversable structural actor. Occupies a slot; balloon spawn paths arc
through it freely. Not interactive. No hit reaction.

**Art direction:** Dandelion puff, tuft of cotton, wispy cloud of floating seed-heads.
Semi-transparent. Gentle idle float animation. Feels soft and permeable.

- [ ] **Sprite** — semi-transparent dandelion puff or cotton wisp; multiple frames for
      idle float cycle (3–4 frames is enough)
- [ ] **Material/Shader** — soft additive or alpha-blended sprite; slight glow or bloom
      to read as "passable air"
- [ ] **Prefab** — `Assets/Prefabs/Grid/Puff.prefab` with `GridActorView` component;
      rename/replace `StaticTest.prefab` once art is finalised
- [ ] **Animator / Controller** — `Puff.controller`
      - `Idle` — gentle up-down float with light scale breathe (looping)
- [ ] **Config entry** — add to `GridActorConfiguration` with high weight; no hit reaction
      needed

---

### Bush

**What it is:** Non-traversable structural actor. Blocks balloon spawn-path computation;
balloons must route around it. Projectiles pass through (no collider).

**Art direction:** Small park shrub — round, leafy, clearly solid-looking. Like something
you'd walk around in a park but throw a ball over. Different enough from Puff that players
immediately understand "this one is solid".

- [ ] **Sprite** — round leafy shrub; slightly above-ground root base; reads as solid
- [ ] **Prefab** — `Assets/Prefabs/Grid/Bush.prefab` with `GridActorView` component
- [ ] **Animator / Controller** — `Bush.controller`
      - `Idle` — gentle leaf sway in breeze (very subtle; 2–3 frame loop)
- [ ] **Config entry** — add to `GridActorConfiguration`

**Note:** Bush currently causes a `ComputePath` warning when a spawn path crosses it
(Phase 9 will add full rerouting). Weight should be low in early difficulty levels to
avoid blocking spawn columns before rerouting exists.

---

### Deflector

**What it is:** Indestructible hitable. Always deflects the projectile. Creates
predictable geometry — players can exploit bounce angles.

**Art direction:** Something visibly angled, shiny, reflective. Park theme could be:
a **pinwheel** (angled vanes); a **reflective game paddle**; or a **butterfly wing**
spread at an angle. The key read is "bouncy/reflective surface", not "wall".

- [ ] **Sprite** — angled/reflective shape; clear bounce-direction implied by the art
- [ ] **Prefab** — `Assets/Prefabs/Grid/Deflector.prefab` with `GridActorView` component
- [ ] **Animator / Controller** — `Deflector.controller`
      - `Idle` — subtle shimmer or spin (pinwheel slowly turning)
      - `DeflectReact` — brief flash/spin-up on hit
- [ ] **VFX** — `PSVFX_DeflectorBounce` — small sparkle/flash at contact point
- [ ] **Config entry** — add to `GridActorConfiguration`; maxCount = 1–2 per grid

---

### Absorber

**What it is:** Indestructible hitable. Absorbs the projectile on contact, ending the
turn. A genuine danger zone — a column with an Absorber requires a different shot path.

**Art direction:** Something that consumes or swallows. Park theme could be: a
**venus flytrap** (open, waiting to snap); a **whirlpool puddle** on the grass;
or a **black hole soap bubble** that pulses. The key read is "danger — don't shoot here".

- [ ] **Sprite** — visually reads "hungry / consuming"; contrasts sharply with other actors
      to signal danger
- [ ] **Material/Shader** — pulsing distortion or dark inner glow to reinforce danger
- [ ] **Prefab** — `Assets/Prefabs/Grid/Absorber.prefab` with `GridActorView` component
- [ ] **Animator / Controller** — `Absorber.controller`
      - `Idle` — slow pulse (open/close or breathe cycle)
      - `AbsorbReact` — quick snap/suck-in animation on contact
- [ ] **VFX** — `PSVFX_AbsorberPop` — implosion / suction burst at contact point
- [ ] **Config entry** — add to `GridActorConfiguration`; gated behind difficulty level
      (see Phase 8.4); maxCount = 1 per grid to start

---

### Gatekeeper

**What it is:** Destructible hitable. Deflects projectiles until `HitsRemaining` reaches
zero, then pops. Visual degradation must make remaining hits readable at a glance.

**Art direction:** Something that blocks a column visually and clearly degrades.
Park theme could be: a **thorn-covered log**; a **rickety wooden gate** with planks
falling off; or a **mossy rock** that cracks. The key reads are "it blocks" and "it's
close to breaking".

- [ ] **Sprites** — one per durability threshold:
      - Full (3 hits): pristine
      - Damaged (2 hits): first cracks/chips
      - Critical (1 hit): heavily damaged, parts falling
      Driven by `IHasDurability.HitsRemaining` subscription in the view
- [ ] **Prefab** — `Assets/Prefabs/Grid/Gatekeeper.prefab` with `GridActorView` component
- [ ] **Animator / Controller** — `Gatekeeper.controller`
      - `Idle_Full` / `Idle_Damaged` / `Idle_Critical` — three idle states, driven by
        `HitsRemaining` threshold (use an `int` Animator parameter)
      - `HitReact` — brief shake/crack-flash on any hit
      - `Break` — final pop/shatter animation when `HitsRemaining` reaches zero
- [ ] **VFX** —
      - `PSVFX_GatekeeperHit` — hit dust/chip on non-killing hits
      - `PSVFX_GatekeeperBreak` — larger debris/burst on destruction
- [ ] **Config entry** — add to `GridActorConfiguration` with `HitsToPop = 3` default;
      weight medium; maxCount = 1 per column

---

## Suggested production order

Work bottom-up by complexity. Items that unblock config wiring come first so the
procedural engine can be tested with real (even rough) assets as early as possible.

```
1. GridActorConfiguration SO + registration         ← unblocks 8.3 wiring immediately
2. Puff — simplest new actor; replaces placeholder  ← unblocks StaticActorSpawner tests in-game
3. Bush — same pipeline as Puff, no hit reaction
4. Simple balloon crack states                      ← view-only change, no new prefab
5. Unbreakable balloon                              ← new prefab + controller
6. Deflector — first hitable grid actor             ← introduces Deflect VFX pipeline
7. Absorber                                         ← danger actor; needs distinctive look
8. Gatekeeper                                       ← most complex; needs N-state animator
```

---

## Asset folder conventions

```
Assets/
├── Prefabs/
│   ├── Balloon/
│   │   ├── Balloon.prefab           ← Simple
│   │   ├── ToughBalloon.prefab      ← Tough
│   │   └── UnbreakableBalloon.prefab  ← NEW
│   └── Grid/
│       ├── StaticTest.prefab        ← retire once Puff is ready
│       ├── Puff.prefab              ← NEW
│       ├── Bush.prefab              ← NEW
│       ├── Deflector.prefab         ← NEW
│       ├── Absorber.prefab          ← NEW
│       └── Gatekeeper.prefab        ← NEW
├── Animation/
│   ├── Balloon/
│   │   └── UnbreakableBalloon.controller  ← NEW
│   └── Grid/                        ← NEW folder for grid actor controllers
│       ├── Puff.controller
│       ├── Bush.controller
│       ├── Deflector.controller
│       ├── Absorber.controller
│       └── Gatekeeper.controller
└── Sprites/
    └── Grid/                        ← NEW folder for grid actor sprites
        └── (one sub-folder per actor)
```

---

## Open questions for art direction

1. **Cracking vs Unbreakable read** — Simple and Cracking use the same prefab root.
   Crack-state sprites must be distinct enough that players understand "this takes
   multiple hits" without confusing it with Unbreakable (which looks permanently solid).

2. **Deflector angle** — Should the deflector have a fixed angle or rotate to reflect
   the projectile direction? Fixed angle is simpler and more strategic; rotating is
   more physically satisfying. Resolve before animator design.

3. **Gatekeeper hits-to-pop default** — 3 hits is the suggested default. This affects
   how many intermediate sprite states are needed. Confirm before commissioning art.

4. **Absorber vs Projectile destroyed sound** — When the Absorber consumes the
   projectile, the turn ends. Does it play a unique SFX, or reuse
   `ProjectileDestroyedMessage` audio? Resolve before VFX production.
````

