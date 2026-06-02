@page plan_bush Bush Obstacle — Implementation Plan

# Bush Obstacle — Implementation Plan

> Implementation plan for the **Bush** structural grid obstacle. Covers the shared
> cluster abstraction (refactoring Puff), the procedural shader, disturbance integration,
> and spawner wiring. All design decisions are resolved — this document drives execution.

---

## Summary

Bush is a permanent, non-traversable, non-hitable grid actor rendered with a procedural
top-down cartoony shader. Adjacent Bush slots merge into a single draw call via the
cluster system. Projectiles fly over Bushes, stamping the disturbance field (leaf warp +
edge wobble) and spawning a leaf particle burst on exit.

The model (`BushObstacleModel`) and enum (`GridActorType.Bush`) already exist. This plan
delivers the visual system, cluster infrastructure, and spawner integration.

---

## Phases

### Phase 1 — Shared Cluster Infrastructure

**Goal:** Extract the Puff cluster system into generic, reusable types. Zero gameplay
change — pure refactor. Existing Puff tests must pass unmodified.

**New files** (`Slots/Actor/Cluster/`, namespace `BalloonParty.Slots.Actor.Cluster`):

| File | Description |
|---|---|
| `IClusterableSlotActor.cs` | `IWriteableSlotActor` + `int ClusterId { get; set; }` |
| `ISlotClusterSource.cs` | Non-generic read interface: `OnClusterChanged`, `Clusters`, `GetClusterAtWorldPosition` |
| `SlotCluster.cs` | Cluster model — list of `Vector2Int` + `Rect WorldBounds` (replaces `PuffCluster`) |
| `SlotClusterChangedEvent.cs` | Event struct + `SlotClusterChangeType` enum (replaces `PuffClusterChangedEvent`) |
| `SlotClusterRegistry.cs` | `SlotClusterRegistry<TModel> where TModel : class, IClusterableSlotActor` — flood-fill/merge/split; `setupOnly` constructor flag skips `OnChanged` subscription (replaces `PuffClusterRegistry`) |
| `IClusterViewSettings.cs` | Shared settings: `AnimationSpeed`, `Padding`, `SortingLayerId`, `SortingOrderOffset` |
| `ClusterView.cs` | Abstract `MonoBehaviour` base — `_SlotCentersWorld[]`, `_SlotCount`, `_TimeOffset` MPB; `OnConfigured()` virtual hook |
| `ClusterViewController.cs` | `ClusterViewController<TView, TSettings>` — `IStartable`; instantiates view, subscribes to registry, collects positions, calls `Configure()` |

**Modified files:**

| File | Change |
|---|---|
| `PuffObstacleModel.cs` | Implement `IClusterableSlotActor` (already has `ClusterId`) |
| `PuffCloudView.cs` | Subclass `ClusterView`; remove duplicated MPB logic (~160 lines → ~20) |
| `IPuffCloudSettings.cs` | Extend `IClusterViewSettings` |
| `GameLifetimeScope.cs` | Register `SlotClusterRegistry<PuffObstacleModel>` + `ClusterViewController<PuffCloudView, IPuffCloudSettings>` |

**Deleted files:**

| File | Replaced by |
|---|---|
| `PuffCluster.cs` | `SlotCluster.cs` |
| `PuffClusterRegistry.cs` | `SlotClusterRegistry<PuffObstacleModel>` |
| `PuffClusterChangedEvent.cs` | `SlotClusterChangedEvent.cs` |
| `PuffCloudViewController.cs` | `ClusterViewController<PuffCloudView, IPuffCloudSettings>` (or thin alias) |

**Tasks:**

```
1.1  [ ] Create Slots/Actor/Cluster/ folder + namespace
1.2  [ ] IClusterableSlotActor interface
1.3  [ ] SlotCluster (rename PuffCluster → SlotCluster; same API)
1.4  [ ] SlotClusterChangedEvent + SlotClusterChangeType (rename Puff*)
1.5  [ ] ISlotClusterSource interface
1.6  [ ] SlotClusterRegistry<TModel> with setupOnly flag (extract from PuffClusterRegistry)
1.7  [ ] IClusterViewSettings interface
1.8  [ ] ClusterView abstract base (extract from PuffCloudView)
1.9  [ ] ClusterViewController<TView, TSettings> (extract from PuffCloudViewController)
1.10 [ ] PuffObstacleModel — add IClusterableSlotActor
1.11 [ ] PuffCloudView — refactor to subclass ClusterView
1.12 [ ] IPuffCloudSettings — extend IClusterViewSettings
1.13 [ ] GameLifetimeScope — update Puff registrations to generics
1.14 [ ] Delete PuffCluster.cs, PuffClusterRegistry.cs, PuffClusterChangedEvent.cs, PuffCloudViewController.cs
1.15 [ ] Run existing Puff cluster tests — all green, zero logic changes
```

**Exit criteria:** Puff renders identically. All existing tests pass. No Puff-specific
cluster/event/registry files remain.

---

### Phase 2 — Bush Shader

**Goal:** Procedural top-down cartoony bush shader with slot merging, surface detail,
ground shadow, wind sway, and disturbance field integration.

**File:** `Assets/Shaders/BalloonParty/Grid/Bush.shader`

**Shape generation:**
- Per-slot circle SDF with `_SlotRadius` base radius
- Per-slot radius jitter via hash of slot center (`_RadiusJitter`)
- 2-octave Simplex edge noise with per-slot phase offset for organic leaf bumps
- Smooth-minimum (polynomial smin) across all slot SDFs for continuous merging
- Hard alpha clip at SDF boundary — fully opaque inside, transparent outside

**Surface detail:**
- Leaf noise layer (3–4 octave Simplex, world-space UV) modulating `_BaseColor` ↔ `_LeafVariationColor`
- Pseudo-lighting from `_LightDir` (half-Lambert on noise-derived normal)
- Edge highlight rim (`_RimWidth`, `_RimIntensity`)
- Optional centre shadow (`_CENTER_SHADOW_ON`)

**Ground shadow** (`_SHADOW_ON`, default on):
- Larger/softer SDF offset below bush body, `_ShadowColor` + `_ShadowSoftness`

**Animation:**
- Wind sway via low-frequency noise displacement on leaf noise layer (`_WindSpeed`, `_WindAmount`)
- `_TimeOffset` driven by C# per frame

**Disturbance** (`_DISTURBANCE_ON`):
- Samples global `_DisturbanceTex` — leaf noise displacement warp (opaque; no density holes)
- Edge boundary wobble — `_EdgeDisturbanceScale` modulates edge noise amplitude with disturbance

**Per-slot randomness:**
- `.z` seed in `_SlotCentersWorld` offsets noise sampling coordinates per slot
- `_RadiusJitter` varies circle radius per slot via position hash
- Edge noise phase offset per slot seed

**Tasks:**

```
2.1  [ ] Shader file scaffold — Properties, vertex, SimplexNoise2D include
2.2  [ ] Per-slot circle SDF + smooth-minimum merging
2.3  [ ] Edge noise distortion with per-slot phase offset + radius jitter
2.4  [ ] Alpha clip at SDF boundary
2.5  [ ] Leaf noise colour modulation (world-space, multi-octave)
2.6  [ ] Pseudo-lighting (half-Lambert from noise gradient)
2.7  [ ] Edge highlight rim
2.8  [ ] Centre shadow (optional keyword)
2.9  [ ] Ground shadow (_SHADOW_ON)
2.10 [ ] Wind sway animation
2.11 [ ] Disturbance field integration (_DISTURBANCE_ON) — leaf warp + edge wobble
2.12 [ ] Create material asset with _SHADOW_ON + _DISTURBANCE_ON enabled
```

**Exit criteria:** Shader renders a visually distinct, cartoony top-down bush in the
scene view with edit-mode animation. Clearly differentiated from Puff at a glance.

---

### Phase 3 — Bush C# (Model + View + Config)

**Goal:** Wire the Bush into the cluster system, create the view and configuration,
register in DI.

**New files:**

| File | Location | Description |
|---|---|---|
| `BushView.cs` | `Slots/Actor/Archetype/` | `ClusterView` subclass; pushes `_SlotRadius`, `_RadiusJitter`, `_DisplaceWorldScale` in `OnConfigured()` |
| `IBushSettings.cs` | `Configuration/` | Extends `IClusterViewSettings`; adds `BushPrefab`, `SlotRadius`, `StampRadius`, `StampStrength` |
| `BushSettings.cs` | `Configuration/` | SO implementing `IBushSettings` |

**Modified files:**

| File | Change |
|---|---|
| `BushObstacleModel.cs` | Add `ClusterId` + implement `IClusterableSlotActor` |
| `GameLifetimeScope.cs` | Register `SlotClusterRegistry<BushObstacleModel>` (setupOnly=true), `ClusterViewController<BushView, IBushSettings>`, `IBushSettings` |
| `StaticActorSpawner.cs` | Add `GridActorType.Bush => new BushObstacleModel()` case |

**Tasks:**

```
3.1  [ ] BushObstacleModel — add ClusterId + IClusterableSlotActor
3.2  [ ] IBushSettings interface (extends IClusterViewSettings)
3.3  [ ] BushSettings ScriptableObject
3.4  [ ] BushView (subclass of ClusterView)
3.5  [ ] Bush.prefab — SpriteRenderer (procedural quad) + BushView
3.6  [ ] Create BushSettings SO asset in Unity, assign prefab + material
3.7  [ ] GameLifetimeScope — register all Bush services (registry setupOnly=true)
3.8  [ ] StaticActorSpawner — add Bush case in CreateModel
3.9  [ ] Add Bush entry to GridActorConfiguration SO
3.10 [ ] In-game validation: bushes spawn, cluster merging works, shader renders
```

**Exit criteria:** Bushes appear in-game with the procedural shader. Adjacent slots
merge visually. Wind sway animates. Registry does no work after initial setup.

---

### Phase 4 — Disturbance + VFX

**Goal:** Bush reacts to projectile fly-overs via disturbance field stamps and a
leaf particle burst on exit.

**New files:**

| File | Location | Description |
|---|---|---|
| `BushDisturbanceController.cs` | `Slots/Actor/Archetype/` | `IStartable`; tracks projectile overlap with bush clusters; stamps `DisturbanceFieldService` per tick during overlap; spawns `PSVFX_BushRustle` once on exit |
| `PSVFX_BushRustle.prefab` | `Assets/Prefabs/VFX/` | ParticleSystem — 3–5 leaf quads scattering in projectile direction; 0.3–0.5s lifetime |

**Modified files:**

| File | Change |
|---|---|
| `GameLifetimeScope.cs` | Register `BushDisturbanceController` |

**Tasks:**

```
4.1  [ ] BushDisturbanceController — subscribe to projectile position updates
4.2  [ ] Per-tick disturbance stamp during projectile-cluster overlap
4.3  [ ] Track overlap state per projectile per cluster (enter/during/exit)
4.4  [ ] Spawn PSVFX_BushRustle on exit — burst in projectile travel direction
4.5  [ ] PSVFX_BushRustle prefab — leaf particle burst
4.6  [ ] GameLifetimeScope registration
4.7  [ ] In-game validation: leaf warp on projectile fly-over, edge wobble, exit burst
4.8  [ ] Stamp tuning — radius, strength (smaller/stronger than Puff)
4.9  [ ] Edge wobble tuning — _EdgeDisturbanceScale
```

**Exit criteria:** Projectile flying over a bush cluster causes visible leaf warp +
edge shiver that reforms naturally. Leaf burst fires at exit point. No interaction
when no projectile is active.

---

### Phase 5 — Polish + Tuning

**Goal:** Final visual tuning at game scale and validation against Puff.

**Tasks:**

```
5.1  [ ] Shader tuning at game scale (~0.9 world units per slot)
         - _SlotRadius, _RadiusJitter range
         - _EdgeNoiseFreq / _EdgeNoiseAmount balance
         - _LeafNoiseFreq / octave count
         - _BaseColor / _LeafVariationColor palette
         - _LightDir direction
         - _RimWidth / _RimIntensity
         - _WindSpeed / _WindAmount
         - Shadow offset, softness, colour
5.2  [ ] Visual contrast validation — Bush vs Puff side by side
         - Opacity: Bush fully opaque, Puff see-through ✓
         - Edges: Bush defined bumps, Puff soft fade ✓
         - Colour: Bush rich green, Puff desaturated ✓
         - Motion: Bush grounded sway, Puff ethereal drift ✓
5.3  [ ] Cluster merging validation — 1, 2, 3+ adjacent slots
5.4  [ ] Disturbance reaction validation — natural settle time, no visual artefacts
5.5  [ ] Performance check — confirm zero per-frame CPU cost from registry/controller after setup
```

**Exit criteria:** Bush is visually polished, clearly differentiated from Puff, and
adds zero unnecessary runtime cost.

---

## Design decisions (resolved)

| Decision | Resolution |
|---|---|
| Art direction | Procedural shader — top-down cartoony, fully opaque, SDF-based |
| Cluster system | Generic `SlotClusterRegistry<TModel>` shared with Puff |
| Palette tinting | Not applicable — terrain uses fixed colours |
| Ground shadow | `_SHADOW_ON` default on |
| Visual variants | Seed-driven subtle variation; all clusters read as one ground layer |
| VFX trigger | Disturbance stamps continuously during overlap; particle burst once on exit |
| Disturbance expression | Leaf noise warp + edge wobble (no transparency holes — stays opaque) |
| Spawn pathing | Balloons spawn below or above Bushes; never route through |
| Lifecycle | `setupOnly = true` — registry does no work after initial build |
| Rerouting | Ships without it; low weight + max count limits `ComputePath` warnings |
| DI wiring | Thin concrete subclass fallback if VContainer generics are awkward |

