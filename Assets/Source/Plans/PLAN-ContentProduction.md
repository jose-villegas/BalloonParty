@page plan_content_production Content Production Plan — Pre-8.3 Assets

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
- `GridActorConfiguration.Entries` — ✅ **code + asset exist** (`GridActorConfiguration.cs`, `Assets/Configuration/GridActorConfiguration.asset`, registered in `GameLifetimeScope`). Spawner consumption beyond the structural actors lands with the Phase 8.3 procedural engine.

Neither list is useful until the prefabs those entries reference actually exist with
correct visuals, animations, and hit feedback. Placeholder grey boxes make it impossible
to read the grid, tune difficulty, or playtest spawn density.

---

## Asset status overview

| Actor / Balloon | Model class | Prefab | Sprite / Art | Animator | Animations | VFX | Config entry | Notes |
|---|---|---|---|---|---|---|---|---|
| **Simple** | `BalloonModel` | ✅ `Balloon.prefab` | ✅ | ✅ `Balloon.controller` | ✅ Stable/Unstable Idle | ✅ `PSVFX_BalloonPop` | ✅ `BalloonsConfiguration` | Baseline; no blockers |
| **Soap Cluster** | `BubbleClusterModel` (`BalloonType.BubbleCluster`) | ✅ `SoapCluster.prefab` | n/a — fully procedural shader | ✅ `SoapCluster.controller` | ✅ shader handles motion; Idle states wired | ⚠️ pop VFX deferred (Phase 9) | ✅ `BalloonsConfiguration` | Done; scores one point per damage to random palette colors with `BreaksStreak = true` |
| **Tough** | `ToughBalloonModel` | ✅ `ToughBalloon.prefab` | ✅ | ✅ `ToughBalloon.controller` | ✅ Stable/Unstable Idle | ✅ `PSVFX_ToughBalloonPop` | ✅ `BalloonsConfiguration` | No `IHasColor`; scores via `IHasScoreColor` with `Inherited` strategy (killer earns points in their color) |
| **Unbreakable** | `UnbreakableBalloonModel` | ✅ `Unbreakable.prefab` | ✅ procedural shader | ✅ `Unbreakable.controller` | ✅ StableIdle | ⚠️ deflect/pop VFX deferred (Phase 9) | ✅ `BalloonsConfiguration` | `IHasScoreColor` mode `Inherited` — scores in killer's color at hit time |
| **Puff** | `PuffObstacleModel` | ✅ `Puff.prefab` (invisible marker) + `PuffCloud.prefab` | ✅ procedural shader (`PuffCloud.shader`) | n/a — invisible marker; cloud is shader-driven | n/a — cluster cloud animates via shader | ✅ disturbance/reform via `DisturbanceFieldService` | ✅ `GridActorConfiguration` (asset created & registered) | Traversable cloud cluster; full MVC system done (model + registry + view + controller) |
| **Bush** | `BushObstacleModel` | ✅ `BushCluster` + `BushSlot` | ✅ baked variants (`Art/Bush/Baked/`) | n/a — shader-driven | ✅ GPU wind + rattle | ✅ `PSVFX_BushPassby` | ✅ `GridActorConfiguration` | **✅ Done via the baked skeletal pipeline — see `PLAN-Bush-Sprite-Baking.md`.** Supersedes the simple-sprite spec in the per-actor section below. |
| **Deflector** | `DeflectorActorModel` | ❌ | ❌ | ❌ | ❌ Idle, Deflect flash | ❌ bounce flash | ❌ `GridActorConfiguration` | Reflective surface; indestructible |
| **Absorber** | `AbsorberActorModel` | ❌ | ❌ | ❌ | ❌ Idle pulse, Absorb | ❌ absorb burst | ❌ `GridActorConfiguration` | Hazard; ends the turn on contact |
| **Gatekeeper** | `GatekeeperActorModel` | ❌ | ❌ | ❌ | ❌ Idle, Hit crack, Break | ❌ hit dust, break burst | ❌ `GridActorConfiguration` | Degrades visually as `HitsRemaining` drops |
| **Spider Web** | `SpiderWebObstacleModel` | ❌ | ❌ | ❌ | ❌ Idle sway, Hit tear, Break | — | ❌ `GridActorConfiguration` | Traversable + degradable; disappears after N hits |

**Legend:** ✅ exists and production-ready · ⚠️ placeholder exists · ❌ does not exist

---

## Shared infrastructure needed

These items are pre-requisites that unlock multiple actors at once.

### `IHasScoreColor` — score attribution ✅ Complete

`IHasScoreColor` is implemented on all balloon models. `ResolveScoreAttribution` is the single extension point — no config fields required. `ScoreController` calls it on every `Pop` or `PassThrough` hit and publishes all returned attributions as one scatter group.

- [x] `BalloonModel` — scores in own color; no attribution when balloon survived
- [x] `ToughBalloonModel` — scatters score to random palette colors on pop
- [x] `UnbreakableBalloonModel` — scatters score to random palette colors on pop (Piercing-only)
- [x] `BubbleClusterModel` — `BreaksStreak = true`; one attribution per damage point to random palette colors

---

### `GridActorConfiguration` ScriptableObject

A new SO holding `GridActorPrefabEntry[]` — the list the procedural spawner reads.
Needs to be created, added to the scene's `GameLifetimeScope` bindings, and registered
just like `BalloonsConfiguration` is today.

Scope of work:
- [x] Write `GridActorConfiguration.cs` (mirrors `BalloonsConfiguration` structure)
- [x] Register in `GameLifetimeScope` with `builder.RegisterInstance`
- [x] Create the SO asset — `Assets/Configuration/GridActorConfiguration.asset`
- [ ] Full consumption by the Phase 8.3 `GridSpawner` (structural actors already place via `StaticActorSpawner`)

### `GridActorView` prefab root pattern

All five grid actors share the same `GridActorView` MonoBehaviour. Each actor gets
its own prefab with the `GridActorView` component, its own sprite renderer, and its
own animator. Pool key is derived from the prefab GameObject name.

Prefab location: `Assets/Prefabs/Grid/`

---

## Per-actor detail

---

### Soap Cluster ✅ Done

**What it is:** A cluster of iridescent soap bubbles that floats as a single balloon-type
unit. Each projectile hit pops one bubble — the cluster visibly shrinks through distinct
geometric layouts (5→pentagon, 4→square, 3→triangle, 2→pair, 1→single). When the last
bubble pops, the cluster is destroyed. Uses a dedicated `BubbleClusterModel` (no color
slot, no item slot, no score per pop) distinguished from Simple by `BalloonType.BubbleCluster`
in the spawner and config. Projectiles pass through (no deflect); hit VFX per-entry
override via the `HitVfxOverride[]` system on `BalloonPrefabEntry`.

**Art direction:** Cluster of iridescent soap bubbles. Thin rainbow-film rim per bubble,
transparent interior, visible Plateau junction membranes between bubbles, soft specular
highlight. Fully procedural — no sprites or textures required.

Key differentiation from **Puff**: Puff is wispy, semi-transparent, static, never reacts.
Soap Cluster has defined iridescent rim, animated breathe/float, and visibly loses bubbles on hit.

Key differentiation from **Tough**: Tough is matte rubber/leather, deflects the projectile.
Soap Cluster is translucent/iridescent, lets the projectile pass through.

**Shader — `BalloonParty/Balloon/SoapBubbleCluster`** (investigation complete):
- 5 independent circle SDFs; Voronoi ownership — bubbles never merge
- Per-count shape layouts: 5=pentagon, 4=square, 3=triangle, 2=pair, 1=single
- Per-bubble radius variance (`kRadiusVar[]`) — no two bubbles the same size
- Iridescent hue: rim angle + slow global drift (`_IridescenceSpeed`)
- Plateau junction: Voronoi boundary LINE (runs through interior) + overlap zone fill
- Specular highlight: computed in unrotated UV space — does not spin with cluster
- Shadow (`_SHADOW_ON`): projects rim + seams only (no interior fill);
  offset in unrotated UV space — direction fixed regardless of cluster rotation;
  `_ShadowFilmWidth` / `_ShadowSeamWidth` independently thin; `_ShadowSoftness` blurs edges without thickening
- Cluster breathe (`_BreatheAmount`, `_BreatheSpeed`) — primary driver of seam interchange
- Per-bubble micro-float (`_FloatAmount`) — independent phase per bubble
- Shader-level rotation (`_Rotation` in radians via MPB) — transform stays identity
- `_TimeOffset` driven by C# each frame (edit + play mode); `_Time.y` not used

**C# — `Balloon/Type/SoapBubbleClusterVariant.cs`** (complete):
- Implements `IBalloonVariant` + `IBalloonViewBinding`; discovered automatically by `BalloonView`
- `[ExecuteAlways]` — animation runs in edit mode without Play mode
- `Bind()`: random spawn rotation + random rotation speed (5–12 °/s ± direction) via `_Rotation` MPB
- Subscribes to `IHasDurability.HitsRemaining` → `Mathf.Clamp(hits, 1, _maxBubbles)` → `_BubbleCount`
- `Update()` accumulates `_rotationAngle` and pushes `_TimeOffset` using
  `EditorApplication.timeSinceStartup` (edit) or `Time.time` (play)
- `_previewBubbleCount` inspector field + `OnValidate()` for edit-mode cluster-state preview
- `_renderer.transform.localRotation = Quaternion.identity` on each `Bind()` — rotation is shader-owned

**C# — `Balloon/Model/BubbleClusterModel.cs`** (complete):
- Dedicated model; no color, no item slot, no score-per-pop
- Implements `IHasDurability`; `HitsRemaining` drives `_BubbleCount` in the variant
- Spawner: `BalloonType.BubbleCluster => new BubbleClusterModel(config)`

**Infrastructure — `HitVfxOverride`** (complete):
- `HitVfxOverride[]` on `BalloonPrefabEntry` — per-entry override for hit/pass-through VFX
- `BalloonController` receives the array and passes it to `BalloonView.SetHitVfxOverrides()`
- `HitVfxOverrideDrawer` in editor for inspector display

- [x] **Shader** — `Shaders/BalloonParty/Balloon/SoapBubbleCluster.shader`
- [x] **C# Variant** — `Balloon/Type/SoapBubbleClusterVariant.cs`
- [x] **C# Model** — `Balloon/Model/BubbleClusterModel.cs` — implements `IHasScoreColor`; one attribution per damage point to random palette colors; `BreaksStreak = true`
- [x] **HitVfxOverride system** — `Configuration/HitVfxOverride.cs` + drawer + wired into spawner
- [x] **BalloonType.BubbleCluster** — enum value added; spawner wired (`=> new BubbleClusterModel(config)`)
- [x] **Shader tuning at game scale** — rim/seam readability confirmed at ~0.9 world units
- [x] **Prefab** — `Assets/Prefabs/Balloon/SoapCluster.prefab`:
      `BalloonView` root + `SoapBubbleClusterVariant` + `SpriteRenderer` (no sprite assigned — procedural quad);
      material uses `BalloonParty/Balloon/SoapBubbleCluster` with `_SHADOW_ON` enabled
- [x] **Animator / Controller** — `SoapCluster.controller`
      - `StableIdle` / `UnstableIdle` state switching only (shader handles all motion)
      - `BubblePop` trigger — one-shot DOTween scale+dissolve on the removed bubble position
        (future: drive from a `BubblePopController` that knows which bubble index was removed)
- [x] **Config entry** — `BubbleCluster` entry added to `BalloonsConfiguration` with `HitsToPop = 5`
- [ ] **Pop VFX** *(deferred — Phase 9)* — `PSVFX_SoapBubblePop` — iridescent soap-film ring + mist burst
      at the removed bubble's world position; `PSVFX_SoapClusterBurst` — larger multi-ring final pop.
      Not blocking; game plays correctly without them.


**Scoring design — "inherits killer's color":**
Destroying an Unbreakable costs an item — the opportunity cost justifies a high score
reward (3–5× base). The Unbreakable has no inherent color, but the item that delivers
the Piercing damage does. Score is attributed to the color of that item's host balloon,
making the payoff emotionally legible: "I spent my Red Bomb, I earned Red points."

Implemented via `IHasScoreColor`. `ResolveScoreAttribution` appends a single
`ScoreAttribution(context.SourceColor, scoreValue)` — reading the killer's color
directly from `DamageContext` at the moment of destruction. The model caches nothing
and stores no color state.

- Do NOT implement `IHasScoreColor` with a non-zero mask — no visual color, no score color; `Inherited` mode covers the score side
- Do NOT add `IHasWriteableColor` — paint cannot coat it

- [x] **Sprite** — fully procedural shader (`UnbreakableBalloon.shader` + `UnbreakableBalloonRim.shader`); no sprite texture needed
- [x] **Prefab** — `Assets/Prefabs/Balloon/Unbreakable.prefab` with `BalloonView`
      component + `UnbreakableBalloonVariant`; reference in `BalloonsConfiguration`
- [x] **Animator / Controller** — `Unbreakable.controller`
      - `StableIdle` — slow, heavy float (slower than Simple idle)
      - `UnstableIdle` — subtle wobble during balancer movement
      - `DeflectReact` — brief recoil/shake when hit (does NOT pop)
- [ ] **VFX** *(deferred — Phase 9)* — `PSVFX_UnbreakableDeflect` — spark/clank on deflect hit;
      `PSVFX_UnbreakablePop` — pop VFX for when Piercing finally destroys it.
      Not blocking; game plays correctly without them.
- [x] **Config entry** — added to `BalloonsConfiguration`; `BalloonType.Unbreakable` wired in spawner
- [x] **Code — `IHasScore` on `UnbreakableBalloonModel`** — reads `ScoreValue` from config
- [x] **Code — `IHasScoreColor` on `UnbreakableBalloonModel`** — `ResolveScoreAttribution` appends `(context.SourceColorId, scoreValue)`; mode `Inherited`


---

### Puff ✅ Done

**What it is:** Traversable structural obstacle. Occupies one or more grid slots; balloon spawn paths arc through freely. Not interactive — no hit reaction, no projectile collider. Adjacent Puff slots merge into a single continuous procedural cloud body.

**Art direction:** Soft, wispy cloud of floating seed-heads. Fully procedural — no sprite or texture required. The `PuffCloud.shader` generates a 3-octave Simplex noise cloud in world space with slot-center boundary falloff. Shadow and displacement channels give it depth and interaction with moving objects.

**Visual system — `BalloonParty/Grid/PuffCloud`** (complete):
- 3-octave world-space Simplex noise with animated time offset (`_TimeOffset` driven per-instance by `PuffCloudView`)
- Slot-center boundary falloff — shape follows the union of all cluster member slot positions
- Optional density masking (`_DENSITY_ON`) — the per-cluster `DisturbanceFieldService`-derived RT punches holes that reform over time
- Displacement crossfade for wind-driven warp
- Pseudo-normal lighting from a configurable light direction
- Optional shadow (`_SHADOW_ON`) — offset soft shadow below the cloud body
- Up to 16 slot centers uploaded via `_SlotCentersWorld` (Vector4 array) and `_SlotCount` per `MaterialPropertyBlock` — zero per-instance material allocations

**C# — Cluster System** (complete):
- `PuffObstacleModel` — `IWriteableSlotActor` + `IPassThrough`; gains `ClusterId` on placement
- `PuffCluster` — plain C# model: list of `Vector2Int` slot indices + world-space AABB
- `PuffClusterRegistry` — `IStartable` + `IDisposable`; subscribes to `SlotGrid.OnChanged`; flood-fills hex adjacency via `SlotGrid.HexNeighborIndices`; handles cluster creation, merge (new Puff bridges two clusters), split (removed Puff was bridging), and resize; publishes `PuffClusterChangedEvent`
- `PuffCloudView` — `MonoBehaviour` + `IPoolable` + `[ExecuteAlways]`; drives shader via `MaterialPropertyBlock`; pushes `_TimeOffset` each frame; calls `DisturbanceFieldService.Stamp()` for projectile/spawn disturbances
- `PuffCloudViewController` — `IStartable`; subscribes to `PuffClusterRegistry.OnClusterChanged`; manages pooled `PuffCloudView` lifecycle (spawn on `Created`, reconfigure on `Resized`, return on `Removed`)

**Prefabs** (complete):
- `Puff.prefab` — `GridActorView` + `TweenTracker`; invisible grid occupancy marker (no renderer)
- `PuffCloud.prefab` — `SpriteRenderer` (no sprite assigned; quad driven by shader) + `PuffCloudView`; pooled by `PuffCloudPoolChannel`; referenced by `PuffCloudSettings.CloudPrefab`

**Configuration** (complete):
- `PuffCloudSettings` — noise speed, density field resolution/timing, wind, displacement, padding, sorting layer/order, disturbance radii/strengths, `CloudPrefab` reference
- [x] `GridActorConfiguration.cs` — code exists; `GridActorPrefabEntry` for Puff with `SlotPlacementMode.Cluster`
- [x] `GridActorConfiguration` SO asset — `Assets/Configuration/GridActorConfiguration.asset`, registered in `GameLifetimeScope`

- [x] **Shader** — `Shaders/BalloonParty/Grid/PuffCloud.shader` + `DisturbanceDiffusion.shader` + `DisturbanceStampBatched.shader`
- [x] **C# Model** — `PuffObstacleModel.cs` — `IWriteableSlotActor` + `IPassThrough`
- [x] **C# Cluster Model** — `PuffCluster.cs`, `PuffClusterRegistry.cs`, `PuffClusterChangedEvent.cs`
- [x] **C# View** — `PuffCloudView.cs`
- [x] **C# Controller** — `PuffCloudViewController.cs`
- [x] **Pool** — `GridActorPoolChannel`, `PuffCloudPoolChannel` (implicit via `PuffCloudSettings.CloudPrefab`)
- [x] **Prefabs** — `Puff.prefab` (marker) + `PuffCloud.prefab` (cloud visual)
- [x] **GridActorConfiguration SO asset** — created at `Assets/Configuration/GridActorConfiguration.asset` and wired into `GameLifetimeScope`

---

### Bush ✅ Done

**What it is:** Non-traversable structural actor. Blocks balloon spawn-path computation;
balloons must route around it. Projectiles pass through (no collider).

Implemented as a **baked 2D skeletal plant system** — not the simple sprite originally
sketched here. See **`PLAN-Bush-Sprite-Baking.md`** for the full design (cluster
infrastructure shared with Puff, offline-baked branch maps + leaf extraction, GPU wind +
rattle driven by the disturbance field). Shipped assets:

- ✅ **Prefabs** — `Assets/Prefabs/Grid/Bush/BushCluster.prefab` + `BushSlot.prefab`
- ✅ **Baked variants** — `Assets/Art/Bush/Baked/BushVariant_V*.asset`
- ✅ **Settings** — `Assets/Configuration/BushSettings.asset` (`IBushSettings`)
- ✅ **VFX** — `PSVFX_BushPassby`
- ✅ **Config entry** — `GridActorConfiguration`

**Note:** Bush still causes a `ComputePath` warning when a spawn path crosses it
(Phase 9 will add full rerouting). Keep weight low in early difficulty levels to avoid
blocking spawn columns before rerouting exists.

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
1. [x] GridActorConfiguration SO + registration         ← code, asset, and registration all done
2. [x] Puff — full MVC cloud system + config entry done
3. [x] Bush — baked skeletal system (see PLAN-Bush-Sprite-Baking); done
4. [x] Soap Cluster shader + C# Variant + model + prefab + config  ← done; pop VFX deferred to Phase 9
5. [x] Unbreakable balloon — shader + variant + model + prefab + controller + config  ← done; VFX deferred to Phase 9
6. [ ] Deflector — first hitable grid actor          ← REMAINING; introduces Deflect VFX pipeline
7. [ ] Absorber                                      ← REMAINING; danger actor; needs distinctive look
8. [ ] Gatekeeper                                    ← REMAINING; most complex; needs N-state animator

These last three (Deflector / Absorber / Gatekeeper) are the **only content blocking
Phase 8.3 procedural placement** — their models + hit routing already exist; what's
missing is prefab/art/animator/VFX/config-entry for each.
```

---

## Asset folder conventions

```
Assets/
├── Prefabs/
│   ├── Balloon/
│   │   ├── Balloon.prefab               ← Simple
│   │   ├── SoapCluster.prefab           ← NEW Soap Cluster (own prefab, not Simple variant)
│   │   ├── ToughBalloon.prefab          ← Tough
│   │   └── UnbreakableBalloon.prefab    ← NEW
│   └── Grid/
│       ├── Puff.prefab              ← exists
│       ├── Bush.prefab              ← NEW
│       ├── Deflector.prefab         ← NEW
│       ├── Absorber.prefab          ← NEW
│       └── Gatekeeper.prefab        ← NEW
├── Animation/
│   ├── Balloon/
│   │   ├── SoapCluster.controller       ← NEW (StableIdle/UnstableIdle only — shader owns motion)
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

1. ~~**Soap Cluster vs Unbreakable read**~~ — ✅ Resolved. Soap Cluster is iridescent/translucent,
   shrinks on hit, projectile passes through. Unbreakable is opaque/heavy, deflects all hits.
   Art directions are distinct by definition; confirm Unbreakable sprite before commission.

2. ~~**Unbreakable scoring**~~ — ✅ Fully resolved — implemented via the `IHasScoreColor` design (see *Shared infrastructure needed* above).

3. **Deflector angle** — Should the deflector have a fixed angle or rotate to reflect
   the projectile direction? Fixed angle is simpler and more strategic; rotating is
   more physically satisfying. Resolve before animator design.

4. **Gatekeeper hits-to-pop default** — 3 hits is the suggested default. This affects
   how many intermediate sprite states are needed. Confirm before commissioning art.

5. **Absorber vs Projectile destroyed sound** — When the Absorber consumes the
   projectile, the turn ends. Does it play a unique SFX, or reuse
   `ProjectileDestroyedMessage` audio? Resolve before VFX production.

