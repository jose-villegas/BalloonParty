@page plan_bush Bush Obstacle — Ideation (Archived)

# Bush Obstacle — Ideation (Archived)

> **This document is the ideation record.** All design decisions are resolved.
> Implementation is driven by **[PLAN-Bush-Implementation.md](PLAN-Bush-Implementation.md)**.
>
> Design plan for the **Bush** structural grid obstacle — a permanent, non-traversable
> actor rendered with a procedural shader. Adjacent Bush slots merge into a single
> cluster visual (one draw call), mirroring the Puff cloud cluster architecture.

---

## Context

Phase 8.2a introduced two structural archetypes:

- **Puff** — traversable (`IPassThrough`), permanent, no hit reaction
- **Bush** — non-traversable, permanent, no hit reaction

Bush is code-complete (`BushObstacleModel`) but has no art, prefab, animator, or config
entry. This plan covers the visual system needed to turn it into a playable, recognisable
in-game object.

---

## What it is

Non-traversable structural actor. Blocks spawn-path and balance-animation routing —
balloons must go around it. No collider, no hit reaction. Projectiles fly over it
(no interaction). Purely visual terrain that constrains the grid topology.

**Puff vs Bush — the key difference:**
Both occupy a slot, so `SlotGrid.IsEmpty` returns false and both provide structural
support for balloons above them. The difference is purely **visual pathing**:
`SlotGrid.IsTraversable` returns true for Puff (`IPassThrough`) but false for Bush.
A balloon's animation can arc through a Puff slot; it cannot travel through a Bush slot.

---

## Code status

- ✅ `BushObstacleModel` — `IWriteableSlotActor`, no `IPassThrough`, no `IHitable`
- ✅ `GridActorType.Bush` enum value
- ✅ `GridActorView` — shared view component for all static grid actors
- ✅ `PuffClusterRegistry` — existing cluster system (to be generified in Phase A)
- ❌ Shared cluster infrastructure — `SlotClusterRegistry<T>`, `ClusterView`, `ClusterViewController<T,S>`
- ❌ Shader — procedural top-down bush
- ❌ `BushView` — `ClusterView` subclass for bush-specific shader properties
- ❌ `IBushSettings` / `BushSettings` — configuration SO
- ❌ Prefab — `Assets/Prefabs/Grid/Bush.prefab`
- ❌ Config entry in `GridActorConfiguration` SO
- ❌ `BushDisturbanceController` — disturbance stamping + leaf VFX

---

## Visual direction

### Top-down cartoony bush

The Bush is rendered as a **top-down view** of a round, leafy shrub — as if the camera
is looking down at a park from above. The look is cartoony and stylised, not realistic.
Think of a round cluster of leafy blobs with visible colour variation, a subtle darker
centre shadow, and a light edge highlight.

**Key visual reads at a glance:**
- **Solid and opaque** — completely hides the slot beneath it; strong contrast with
  Puff's semi-transparent cloud
- **Round, organic silhouette** — irregular but clearly bounded; no hard geometric edges
- **Rich green** — saturated leaf colour with noise-driven variation; stands out from
  the muted Puff clouds
- **Grounded** — optional subtle ground shadow ring around the base to reinforce
  solidity; feels planted, not floating

### Differentiation from Puff

| Property | Puff | Bush |
|---|---|---|
| Opacity | Semi-transparent; see-through | Fully opaque; hides content behind it |
| Edge quality | Soft, blurred, wispy | Rounded but defined; cartoony leaf bumps |
| Colour | Light, desaturated | Rich, saturated green with leaf-level variation |
| Motion | Drifting, ethereal noise scroll | Grounded; subtle wind-in-leaves sway |
| Shape driver | Smooth Simplex noise falloff | Layered circle SDFs with noise distortion |
| Surface detail | None — pure cloud volume | Pseudo-lighting + leaf highlight bumps |

---

## Cluster system — shared abstraction

Adjacent Bush slots merge into a single continuous visual, rendered as **one draw call**
per the entire Bush population. This is the same architecture Puff uses — and the two
systems are identical enough that introducing Bush without extracting the common
infrastructure would mean duplicating ~400 lines of non-trivial flood-fill/merge/split
logic. The abstraction is done **now**, as a prerequisite step, not deferred.

### Why now, not later

The Puff cluster registry is ~350 lines. Every line except the model type check
(`actor is PuffObstacleModel`) is identical for Bush. The view controller is another
~120 lines — again identical except for the settings type. Duplicating ~470 lines of
tested, working code to change 3 lines is not "premature abstraction" — it's copy-paste
tech debt created at the moment of creation. With Bush as the second consumer, the
pattern is proven and the generic boundary is obvious.

### Architecture overview

```
IClusterableSlotActor              ← new interface: IWriteableSlotActor + ClusterId
    │
    ├── PuffObstacleModel          ← already implements; add interface
    └── BushObstacleModel          ← add ClusterId + interface
    │
    ▼
SlotCluster                        ← replaces PuffCluster; actor-agnostic
    │
    ▼
SlotClusterRegistry<TModel> : IStartable, IDisposable
    │  where TModel : IClusterableSlotActor
    │  Subscribes to SlotGrid.OnChanged
    │  Flood-fills hex adjacency via SlotGrid.HexNeighborIndices
    │  Handles cluster create / merge / split / resize
    │  Publishes SlotClusterChangedEvent
    │
    ▼
ClusterViewController<TView, TSettings> : IStartable, IDisposable
    │  where TView : ClusterView
    │  where TSettings : IClusterViewSettings
    │  Subscribes to registry.OnClusterChanged
    │  Manages a single TView instance
    │  Collects all slot positions → reconfigures the view
    │
    ▼
ClusterView : MonoBehaviour [ExecuteAlways]      ← abstract base
    │  Drives shader via MaterialPropertyBlock
    │  Pushes _SlotCentersWorld[], _SlotCount, _TimeOffset per frame
    │  Quad scaled to combined bounding box + padding
    │
    ├── PuffCloudView              ← concrete; Puff-specific shader properties
    └── BushView                   ← concrete; Bush-specific shader properties
```

---

### Shared infrastructure — new files in `Slots/Actor/Cluster/`

All cluster infrastructure lives in a new `Cluster/` folder under `Slots/Actor/`,
namespace `BalloonParty.Slots.Actor.Cluster`.

#### `IClusterableSlotActor`

Marker interface for any model that participates in cluster grouping:

```csharp
internal interface IClusterableSlotActor : IWriteableSlotActor
{
    int ClusterId { get; internal set; }
}
```

Both `PuffObstacleModel` and `BushObstacleModel` implement this. The registry uses
the interface for type filtering and cluster ID assignment — no casting to concrete
types needed.

#### `SlotCluster`

Replaces `PuffCluster`. Identical data structure, actor-agnostic name:

```csharp
internal class SlotCluster
{
    private readonly List<Vector2Int> _slots = new();

    public int ClusterId { get; }
    public IReadOnlyList<Vector2Int> Slots => _slots;
    public Rect WorldBounds { get; internal set; }

    internal SlotCluster(int clusterId) { ... }
    internal SlotCluster(int clusterId, IEnumerable<Vector2Int> slots, Rect worldBounds) { ... }
    internal void SetSlots(IEnumerable<Vector2Int> slots, Rect worldBounds) { ... }
    internal void AddSlot(Vector2Int slot) { ... }
}
```

#### `SlotClusterChangedEvent`

Replaces `PuffClusterChangedEvent`:

```csharp
internal readonly struct SlotClusterChangedEvent
{
    public readonly int ClusterId;
    public readonly SlotClusterChangeType ChangeType;
    public readonly SlotCluster Cluster;

    // ...constructor...
}

internal enum SlotClusterChangeType
{
    Created,
    Resized,
    Removed
}
```

#### `SlotClusterRegistry<TModel>`

Generic registry that replaces `PuffClusterRegistry`. The only parameterisation
is the model type — the flood-fill, merge, split, and bounds computation are identical:

```csharp
internal class SlotClusterRegistry<TModel> : IStartable, IDisposable, ISlotClusterSource
    where TModel : class, IClusterableSlotActor
{
    private readonly SlotGrid _grid;
    private readonly bool _setupOnly;
    private readonly Subject<SlotClusterChangedEvent> _onClusterChanged = new();
    private readonly Dictionary<int, SlotCluster> _clusters = new();
    private readonly Dictionary<Vector2Int, int> _slotToCluster = new();
    // ...same fields as PuffClusterRegistry...

    internal IObservable<SlotClusterChangedEvent> OnClusterChanged => _onClusterChanged;
    internal IReadOnlyDictionary<int, SlotCluster> Clusters => _clusters;

    // setupOnly: when true, skips SlotGrid.OnChanged subscription after
    // initial RebuildAll(). See "Static lifecycle" section.
    internal SlotClusterRegistry(SlotGrid grid, bool setupOnly = false)
    {
        _grid = grid;
        _setupOnly = setupOnly;
    }

    // Start(), Dispose() — identical to PuffClusterRegistry
    // (subscription to OnChanged is conditional on !_setupOnly)

    private void OnGridChanged(SlotGridChangedEvent evt)
    {
        var actor = _grid.At(evt.Index);
        if (evt.ChangeType == SlotGridChangeType.Placed)
        {
            if (actor is TModel)
            {
                OnActorPlaced(evt.Index);
            }
        }
        else if (evt.ChangeType == SlotGridChangeType.Removed)
        {
            if (_slotToCluster.ContainsKey(evt.Index))
            {
                OnActorRemoved(evt.Index);
            }
        }
    }

    // OnActorPlaced, OnActorRemoved, RebuildAll, FloodFill,
    // CreateCluster, RecalculateBounds, ComputeWorldBounds
    // — identical logic, uses TModel instead of PuffObstacleModel

    private void AssignClusterIdToModel(Vector2Int slot, int clusterId)
    {
        if (_grid.At(slot) is TModel actor)
        {
            actor.ClusterId = clusterId;
        }
    }
}
```

The `class` constraint on `TModel` is needed for the `is` pattern match and the
`as` cast. `IClusterableSlotActor` ensures `ClusterId` is settable.

**Concrete registries are just type aliases via DI registration:**
```csharp
// GameLifetimeScope
builder.Register<SlotClusterRegistry<PuffObstacleModel>>(Lifetime.Singleton)
    .As<IStartable>()
    .AsSelf();

builder.Register<SlotClusterRegistry<BushObstacleModel>>(Lifetime.Singleton)
    .As<IStartable>()
    .AsSelf();
```

No subclassing needed. Each registration creates an independent registry that tracks
only its own model type.

#### `IClusterViewSettings`

Shared settings interface for any cluster view:

```csharp
internal interface IClusterViewSettings
{
    float AnimationSpeed { get; }
    float Padding { get; }
    int SortingLayerId { get; }
    int SortingOrderOffset { get; }
}
```

`IPuffCloudSettings` and `IBushSettings` both extend this, adding their own
type-specific properties (prefab reference typed to their concrete view, etc.).

#### `ClusterView` — abstract base MonoBehaviour

Extracts the common `_SlotCentersWorld[]` / `_SlotCount` / `_TimeOffset` MPB logic:

```csharp
[ExecuteAlways]
internal abstract class ClusterView : MonoBehaviour
{
    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
    private static readonly int SlotCentersWorldId = Shader.PropertyToID("_SlotCentersWorld");
    private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");

    [SerializeField] private SpriteRenderer _renderer;

    private readonly Vector4[] _slotCenters = new Vector4[16];
    private MaterialPropertyBlock _block;
    private bool _configured;
    private int _slotCount;
    private float _animationSpeed;

    internal SpriteRenderer Renderer => _renderer;

    // Awake, OnEnable, OnValidate — same as PuffCloudView

    private void Update()
    {
        // ...time offset logic identical to PuffCloudView...
        PushTimeOffset(currentTime * _animationSpeed);
    }

    internal void Configure(Vector4[] allSlotPositions, int count,
        Rect combinedBounds, IClusterViewSettings settings)
    {
        _configured = true;
        _animationSpeed = settings.AnimationSpeed;
        _slotCount = Mathf.Min(count, _slotCenters.Length);
        // ...copy positions, set transform, push MPB...
        OnConfigured(settings);
    }

    internal void Clear() { ... }

    /// <summary>
    /// Called after base configuration is pushed. Subclasses can push
    /// additional shader properties to the MaterialPropertyBlock here.
    /// </summary>
    protected virtual void OnConfigured(IClusterViewSettings settings) { }

    protected MaterialPropertyBlock Block => _block;
}
```

**`PuffCloudView`** becomes a thin subclass — any Puff-specific MPB properties
(currently none beyond the base set) go in `OnConfigured`. The existing ~180 lines
collapse to ~20.

**`BushView`** — subclass that pushes `_SlotRadius`, `_DisplaceWorldScale`, etc. in
`OnConfigured`.

#### `ClusterViewController<TView, TSettings>`

Generic view controller that replaces `PuffCloudViewController`:

```csharp
internal class ClusterViewController<TView, TSettings> : IStartable, IDisposable
    where TView : ClusterView
    where TSettings : class, IClusterViewSettings
{
    private readonly ISlotClusterSource _registry;
    private readonly SlotGrid _grid;
    private readonly TSettings _settings;
    private readonly IObjectResolver _resolver;
    private readonly TView _prefab;
    // ...

    public void Start()
    {
        _view = _resolver.Instantiate(_prefab);
        // ...sorting layer setup, subscribe to registry...
    }

    private void Reconfigure()
    {
        // ...collect all slot positions from all clusters,
        //    compute combined bounds, call _view.Configure(...)...
        // Identical to PuffCloudViewController.Reconfigure()
    }
}
```

`ISlotClusterSource` is a non-generic interface exposed by
`SlotClusterRegistry<TModel>` so the view controller doesn't need to know the model
type:

```csharp
internal interface ISlotClusterSource
{
    IObservable<SlotClusterChangedEvent> OnClusterChanged { get; }
    IReadOnlyDictionary<int, SlotCluster> Clusters { get; }
    SlotCluster GetClusterAtWorldPosition(Vector3 worldPos);
}
```

`SlotClusterRegistry<TModel>` implements `ISlotClusterSource`. The view controller
injects a named/keyed instance (e.g., via VContainer `RegisterFactory` or explicit
`[Inject]` with the concrete registry type).

In practice, the Puff and Bush view controllers are registered as:

```csharp
// Puff
builder.Register<ClusterViewController<PuffCloudView, IPuffCloudSettings>>(Lifetime.Singleton)
    .WithParameter("registry", resolver => resolver.Resolve<SlotClusterRegistry<PuffObstacleModel>>())
    .WithParameter("prefab", settings.CloudPrefab)
    .As<IStartable>();

// Bush
builder.Register<ClusterViewController<BushView, IBushSettings>>(Lifetime.Singleton)
    .WithParameter("registry", resolver => resolver.Resolve<SlotClusterRegistry<BushObstacleModel>>())
    .WithParameter("prefab", settings.BushPrefab)
    .As<IStartable>();
```

If VContainer's parameter wiring becomes awkward, a thin concrete subclass per actor
type is acceptable — the point is the logic lives in the base, not duplicated.

---

### Migration plan for Puff

The generic extraction is a **refactor** of the existing Puff system, not an addition
alongside it. The migration is:

1. Create `Slots/Actor/Cluster/` with all shared types
2. Add `IClusterableSlotActor` to `PuffObstacleModel` (it already has `ClusterId`)
3. Replace `PuffCluster` → `SlotCluster` (same API, rename)
4. Replace `PuffClusterChangedEvent` → `SlotClusterChangedEvent`
5. Replace `PuffClusterRegistry` → `SlotClusterRegistry<PuffObstacleModel>`
6. Extract `ClusterView` from `PuffCloudView`; `PuffCloudView` becomes subclass
7. Extract `ClusterViewController<TView, TSettings>` from `PuffCloudViewController`
8. Delete the old Puff-specific classes (they are now generic)
9. Update `GameLifetimeScope` registrations
10. Run existing tests — they should pass with zero logic changes

**Risk:** Low. The Puff cluster system has tests (`PuffClusterRegistryTests`). The
generic extraction changes no logic — only type parameters. All tests continue to
pass by substituting the generic types.

### What stays type-specific

| Component | Why it stays concrete |
|---|---|
| `PuffCloudView` (subclass of `ClusterView`) | Puff-specific shader properties (if any in `OnConfigured`) |
| `BushView` (subclass of `ClusterView`) | Bush-specific shader properties (`_SlotRadius`, `_DisplaceWorldScale`, etc.) |
| `IPuffCloudSettings` (extends `IClusterViewSettings`) | `CloudPrefab: PuffCloudView` typed reference |
| `IBushSettings` (extends `IClusterViewSettings`) | `BushPrefab: BushView` typed reference, stamp parameters |
| `BushDisturbanceController` | Bush-specific projectile overlap → stamp logic |
| Shaders | Completely different visual outputs |

### `BushObstacleModel` update

Add `ClusterId` property and `IClusterableSlotActor` interface:

```csharp
internal class BushObstacleModel : IClusterableSlotActor
{
    public Vector2Int SlotIndex { get; private set; }
    public int ClusterId { get; internal set; }

    Vector2Int IWriteableSlotActor.SlotIndex
    {
        get => SlotIndex;
        set => SlotIndex = value;
    }

    int IClusterableSlotActor.ClusterId
    {
        get => ClusterId;
        set => ClusterId = value;
    }

    public SlotActorKind Kind => SlotActorKind.Static;

    internal BushObstacleModel() { }
}
```

### `PuffObstacleModel` update

Add `IClusterableSlotActor` interface (already has `ClusterId`):

```csharp
internal class PuffObstacleModel : IClusterableSlotActor, IPassThrough
{
    // ...existing code — only the interface list changes...
}
```

---

## Shader design — `BalloonParty/Grid/Bush`

### Approach

Procedural top-down bush rendered entirely in the fragment shader. No sprite textures.
The shader receives an array of slot center positions in world space and generates a
leafy canopy shape around each, with adjacent slots merging into a continuous bush body.

### Shape generation

1. **Per-slot circle SDF** — each slot center defines a base circle (radius from
   `_SlotRadius`). For each fragment, compute distance to nearest slot center.
2. **Edge noise distortion** — apply 2-octave Simplex noise to the circle boundary to
   create irregular, organic leaf-bump edges. Amplitude (`_EdgeNoiseAmount`) and
   frequency (`_EdgeNoiseFreq`) are tunable. This is the key differentiator from Puff's
   smooth falloff.
3. **Slot merging** — use smooth-minimum (polynomial smin) across all slot SDFs so
   adjacent bush circles blend into a continuous body rather than overlapping circles.
   Same principle as Puff's slot-center falloff but with a hard-edged result instead of
   a soft cloud fade.
4. **Alpha cutoff** — hard-edged alpha clip at the SDF boundary. Bush is fully opaque
   inside, fully transparent outside. No soft transparency — reinforces the "solid" read.

### Surface detail

5. **Leaf noise layer** — a higher-frequency noise (3–4 octaves) modulates the base
   green colour to create visible "leaf clumps". Driven by world-space UV so adjacent
   slots share a continuous texture.
6. **Pseudo-lighting** — a configurable light direction (`_LightDir`) creates a subtle
   bright-to-shadow gradient across the canopy. The side facing the light is lighter;
   the opposite side is darker. Sells the "round bush from above" read.
7. **Edge highlight** — a thin bright rim along the SDF boundary (leaf tips catching
   light). Width and intensity tunable (`_RimWidth`, `_RimIntensity`).
8. **Centre shadow** — a subtle darkening toward the centre of each slot circle to
   suggest depth/volume (looking down into the canopy centre). Optional — toggled via
   `_CENTER_SHADOW_ON` keyword.

### Ground shadow

9. **Ground shadow** (`_SHADOW_ON`) — a larger, softer SDF offset below the bush body.
   Same approach as Puff's shadow: offset in unrotated UV space, `_ShadowSoftness` blurs
   edges. Colour from `_ShadowColor`. Reinforces the grounded, solid feel.

### Animation

10. **Wind sway** — slow, low-frequency noise displacement applied to the leaf noise
    layer (not the shape boundary). Creates a subtle "breeze through the leaves" effect
    without deforming the silhouette. Speed from `_WindSpeed`, amount from `_WindAmount`.
11. **`_TimeOffset`** — driven by C# each frame (edit + play mode), same as Puff.
    `_Time.y` not used.

### Disturbance field integration (`_DISTURBANCE_ON`)

The Bush shader samples the global `_DisturbanceTex` (set by `DisturbanceFieldService`
each tick) to react to projectile and spawn disturbances. This is the same RT that Puff
reads — `_FieldBoundsMin` and `_FieldBoundsSize` are global shader properties, so no
per-instance setup is needed.

**Two layers of reaction:**

1. **Leaf displacement** — the displacement channels (GB → XY) warp the leaf noise
   sampling coordinates. When a projectile passes through, leaves visually shift in the
   direction of the disturbance, then settle back as the field reforms. This creates a
   convincing "rustled by wind" effect tied to actual gameplay events.

   ```hlsl
   float2 fieldUV = (wp - _FieldBoundsMin) / _FieldBoundsSize;
   float3 field = tex2D(_DisturbanceTex, fieldUV).rgb;
   float2 displace = (field.gb - 0.5) * 2.0 * _DisplaceWorldScale;
   float disturbance = saturate(length(displace) / (_DisplaceWorldScale * 0.5 + 0.001));

   // Crossfade between undisturbed and displaced leaf noise
   float2 wpDisp = wp + displace;
   float leafOrig = LeafNoise(wp, t, clusterSeed);
   float leafDisp = LeafNoise(wpDisp, t, clusterSeed);
   float leaf = lerp(leafOrig, leafDisp, disturbance);
   ```

   Unlike Puff (which uses density to punch transparent holes), Bush stays **fully
   opaque** — the displacement only warps the leaf colour pattern, not the alpha. The
   silhouette boundary is unaffected. This preserves the "solid, impassable" read while
   still giving clear visual feedback that something just flew through.

2. **Edge boundary wobble** — a secondary, subtler effect: the SDF edge noise amplitude
   is modulated by disturbance intensity. When displacement is high, the leaf-bump edges
   wobble more aggressively (amplitude scaled by `_EdgeDisturbanceScale`). When the
   field is at rest, edges return to their calm sway. This makes the whole bush canopy
   feel like it's physically reacting — not just the surface texture, but the outline
   itself shivers.

   ```hlsl
   float edgeNoise = SimplexNoise2D(wp * _EdgeNoiseFreq + t * _WindSpeed);
   float edgeAmount = _EdgeNoiseAmount + disturbance * _EdgeDisturbanceScale;
   float sdf = slotSDF + edgeNoise * edgeAmount;
   ```

**Puff vs Bush disturbance comparison:**

| Aspect | Puff | Bush |
|---|---|---|
| Density (R channel) | Punches transparent holes — cloud fades out | **Not used** — bush stays fully opaque |
| Displacement (GB) | Warps noise sampling + crossfade | Warps leaf noise sampling + crossfade |
| Edge effect | None — edges are already soft | Edge bump amplitude scales with disturbance |
| Reform behaviour | Holes fill back in (density returns to 1) | Leaf pattern settles back; edges calm down |

This means the Bush reacts to the same projectile stamps as Puff but expresses the
disturbance through motion rather than transparency. Both the leaf surface pattern and
the canopy boundary respond, creating a layered, physical-feeling reaction.

### Shader properties summary

| Property | Type | Purpose |
|---|---|---|
| `_SlotCentersWorld` | `Vector4[16]` | World-space slot centers (.xy = position, .z = cluster seed) |
| `_SlotCount` | `int` | Number of active slots |
| `_SlotRadius` | `float` | Base circle radius per slot |
| `_RadiusJitter` | `float` | Max ± radius variation per slot |
| `_TimeOffset` | `float` | Animation time driven by C# |
| `_BaseColor` | `Color` | Primary leaf green |
| `_LeafVariationColor` | `Color` | Secondary colour for leaf noise |
| `_LeafNoiseFreq` | `float` | Leaf clump noise frequency |
| `_LeafNoiseOctaves` | `int` | Leaf detail octaves (2–4) |
| `_EdgeNoiseFreq` | `float` | Edge distortion frequency |
| `_EdgeNoiseAmount` | `float` | Edge distortion amplitude (base, at rest) |
| `_EdgeDisturbanceScale` | `float` | Extra edge wobble amplitude per unit of disturbance |
| `_LightDir` | `Vector2` | Pseudo-light direction (normalized) |
| `_RimWidth` | `float` | Edge highlight width |
| `_RimIntensity` | `float` | Edge highlight brightness |
| `_WindSpeed` | `float` | Leaf sway animation speed |
| `_WindAmount` | `float` | Leaf sway displacement amount |
| `_DisplaceWorldScale` | `float` | World-space scale for displacement field readout |
| `_ShadowColor` | `Color` | Ground shadow colour |
| `_ShadowOffset` | `Vector2` | Ground shadow position offset |
| `_ShadowSoftness` | `float` | Ground shadow blur |
| `_SHADOW_ON` | keyword | Enable/disable ground shadow |
| `_CENTER_SHADOW_ON` | keyword | Enable/disable centre depth shadow |
| `_DISTURBANCE_ON` | keyword | Enable/disable disturbance field sampling |

---

## Shape randomness

Each bush slot should look distinct — not a stamped copy of the same circle. The shader
generates variation from two sources:

### 1. Per-slot seed (`.z` in `_SlotCentersWorld`)

Each slot center carries a seed value in its `.z` component (derived from slot index
hash or cluster ID). The seed offsets all noise sampling coordinates for that slot:

```hlsl
float2 seedOffset = float2(seed * 73.37, seed * 41.13);
float2 noisePos = wp + seedOffset;
```

This means two adjacent bush slots share the same noise **function** but sample at
different offsets, producing different leaf clump patterns, edge bump profiles, and
colour variation — while still blending smoothly where they merge (the smin SDF
merging operates on the combined distance field, not per-slot noise).

### 2. Per-slot radius jitter (`_RadiusJitter`)

Each slot's base circle radius is jittered by a hash of its grid index:

```hlsl
float jitter = frac(sin(dot(slotCenter.xy, float2(12.9898, 78.233))) * 43758.5453);
float radius = _SlotRadius + (jitter - 0.5) * _RadiusJitter;
```

This breaks the regularity of the hex grid — some slots bulge slightly, others
contract. Combined with the edge noise distortion, no two slots look alike.

### 3. Edge noise phase offset

The edge distortion noise is also phase-shifted per slot, so even if two slots share
the same radius, their silhouette bumps are different:

```hlsl
float edgePhase = seed * 6.2831; // full rotation offset
float edgeNoise = SimplexNoise2D(wp * _EdgeNoiseFreq + edgePhase);
```

### Result

A cluster of 3 adjacent Bush slots should read as a single organic shape with visible
internal variation — not three identical circles pasted together. The smooth-minimum
merging creates natural-looking pinch points between slots, and the per-slot noise
offsets ensure each lobe has its own character.

| Shader property | Type | Purpose |
|---|---|---|
| `_RadiusJitter` | `float` | Max ± radius variation per slot (world units) |

---

## Static lifecycle — setup-once optimisation

Unlike Puff (which can theoretically have clusters change during gameplay via the
disturbance system's density punching), Bushes are **placed once at spawn time and
never added or removed**. `StaticActorSpawner` places all Bush models during
`SpawnStage.StaticActors`, then never touches them again. No gameplay event creates
or destroys a Bush mid-game.

This means the cluster registry, view controller, and reconfigure pipeline are only
useful during the initial spawn pass. After that, the only per-frame work needed is:

1. **Shader `_TimeOffset`** — drives wind sway animation (cheap; one `SetFloat` per frame)
2. **Global `_DisturbanceTex` sampling** — already running for Puff; Bush shader reads
   the same global RT at zero additional CPU cost
3. **`BushDisturbanceController`** — stamps the field when a projectile overlaps; only
   active during projectile flight (not every frame)

Everything else can stop.

### What this means for the generic `SlotClusterRegistry<TModel>`

The registry subscribes to `SlotGrid.OnChanged` and runs flood-fill on every grid
mutation. For Puff this is necessary — Puff clusters can theoretically resize if Puffs
are removed (e.g., by future Phase 9 behaviour actors). For Bush, every single
`OnGridChanged` callback after the initial spawn is wasted work: the callback checks
`actor is BushObstacleModel`, fails (because balloons and other actors are being
placed/removed, not Bushes), and returns immediately.

**Design: `SetupMode` flag on `SlotClusterRegistry<TModel>`**

Add a configuration option that lets the registry unsubscribe from grid changes after
the initial `RebuildAll()`:

```csharp
internal class SlotClusterRegistry<TModel> : IStartable, IDisposable
    where TModel : class, IClusterableSlotActor
{
    private readonly bool _setupOnly;

    internal SlotClusterRegistry(SlotGrid grid, bool setupOnly = false)
    {
        _grid = grid;
        _setupOnly = setupOnly;
    }

    public void Start()
    {
        if (!_setupOnly)
        {
            _grid.OnChanged
                .Subscribe(OnGridChanged)
                .AddTo(_disposables);
        }

        RebuildAll();
    }
    // ...
}
```

When `setupOnly = true`:
- `RebuildAll()` runs once during `Start()`, building all clusters
- No subscription to `SlotGrid.OnChanged` — zero per-frame overhead
- The registry remains alive for queries (`GetClusterAtWorldPosition`,
  `GetClusterForSlot`) but does no reactive work

**DI registration:**
```csharp
// Puff — dynamic (may change in future)
builder.Register<SlotClusterRegistry<PuffObstacleModel>>(Lifetime.Singleton)
    .WithParameter("setupOnly", false);

// Bush — static; unsubscribe after initial build
builder.Register<SlotClusterRegistry<BushObstacleModel>>(Lifetime.Singleton)
    .WithParameter("setupOnly", true);
```

### What this means for `ClusterViewController`

The view controller subscribes to `registry.OnClusterChanged` and calls `Reconfigure()`
on every event. For Bush with `setupOnly = true`, the registry still emits `Created`
events during `RebuildAll()` — the view controller processes those, configures the
`BushView` once, and then receives no further events. The subscription stays alive
(harmless — no events flow) but does zero work.

If we want to be aggressive, the view controller could also dispose its subscription
after the first `Reconfigure()` when it detects it's in setup-only mode. But this is
a micro-optimisation — the subscription costs nothing when no events fire.

### What stays per-frame

| Component | Per-frame work | Cost |
|---|---|---|
| `BushView.Update()` | Push `_TimeOffset` to MPB | 1 `GetPropertyBlock` + 1 `SetFloat` + 1 `SetPropertyBlock` |
| Bush shader | Sample `_DisturbanceTex` + noise + SDF | GPU only; shared with Puff field |
| `BushDisturbanceController` | Check projectile overlap (only during projectile flight) | 1 bounds check per cluster per active projectile |
| `SlotClusterRegistry<Bush>` | Nothing | Subscription disposed / no events |
| `ClusterViewController<Bush>` | Nothing | No events from registry |

---

## Projectile pass-through VFX

When a projectile flies over a Bush slot, two feedback layers fire:

### 1. Disturbance field stamp (shader-driven)

The primary visual feedback comes from the disturbance field. `BushView` (or a
dedicated controller) calls `DisturbanceFieldService.Stamp()` when a projectile
overlaps the bush cluster bounds — same API that Puff uses. The stamp creates a
displacement ripple in the global `_DisturbanceTex` that the Bush shader reads on
the next frame, warping the leaf pattern and wobbling the edges.

This is automatic and continuous — no spawned prefab, no particle allocation. The
disturbance reforms naturally via the field's diffusion/reform cycle, so the bush
settles back to calm over ~0.5–1s.

**Stamp parameters (tunable via `IBushSettings`):**
- `BushStampRadius` — world-space radius of the stamp (smaller than Puff's — bush is
  denser, so the ripple should be tighter)
- `BushStampStrength` — intensity (higher than Puff's — the opaque surface needs a
  stronger warp to be visible)

### 2. Leaf particle burst (on exit)

A small burst of 3–5 leaf particles that scatter outward from the exit point in the
projectile's travel direction. Fires **once per crossing, on exit** — not on entry,
not during overlap. This punctuates the crossing with a satisfying pop of feedback
after the shader disturbance has already been warping leaves throughout the traversal.

### Design

- **Trigger:** Projectile exits the Bush cluster's world-space bounds. Detected via
  `BushClusterRegistry.GetClusterAtWorldPosition(projectilePos)` — the controller
  tracks which clusters each projectile is currently inside and fires the burst when
  overlap ends.
- **Stamp:** `DisturbanceFieldService.Stamp(worldPos, radius, strength, direction)` —
  fires continuously each physics tick while the projectile overlaps the cluster. The
  field naturally handles the falloff and reform.
- **Particles:** Burst of 3–5 small leaf quads that scatter outward in the projectile's
  travel direction. Quick lifetime (0.3–0.5s), slight gravity pull. Fires once on exit.
- **Sound:** Optional soft rustle SFX on exit. Decide before audio pass.
- **Implementation:** `BushDisturbanceController : IStartable` subscribes to projectile
  position updates and tracks per-projectile cluster overlap state. On overlap start:
  begins stamping the disturbance field. On overlap end: spawns the particle burst at
  the exit point and stops stamping.

### Prefab

`PSVFX_BushRustle.prefab` — ParticleSystem with leaf burst settings. Pooled via
existing VFX pool pattern.

---

## Configuration

### `IBushSettings`

```csharp
internal interface IBushSettings
{
    BushView BushPrefab { get; }
    float AnimationSpeed { get; }
    float Padding { get; }
    float SlotRadius { get; }
    float StampRadius { get; }
    float StampStrength { get; }
    int SortingLayerId { get; }
    int SortingOrderOffset { get; }
}
```

### `BushSettings` ScriptableObject

```csharp
[CreateAssetMenu(menuName = "Configuration/Bush Settings", fileName = "BushSettings")]
internal class BushSettings : ScriptableObject, IBushSettings
{
    [SerializeField] private BushView _bushPrefab;
    [SerializeField] private float _animationSpeed = 0.5f;
    [SerializeField] private float _padding = 0.3f;
    [SerializeField] private float _slotRadius = 0.45f;
    [SerializeField] private float _stampRadius = 0.3f;
    [SerializeField] private float _stampStrength = 0.8f;
    [SerializeField] private int _sortingLayerId;
    [SerializeField] private int _sortingOrderOffset;

    // ...property implementations...
}
```

Registered in `GameLifetimeScope`:
```csharp
builder.RegisterInstance<IBushSettings>(_bushSettings);
```

---

## Prefab structure

```
Bush.prefab  (visual quad — one instance, covers all bush slots)
├── SpriteRenderer (no sprite assigned — procedural quad)
└── BushView
```

Location: `Assets/Prefabs/Grid/Bush.prefab`

The existing `Puff.prefab` (invisible grid marker with `GridActorView`) remains the
pattern for the per-slot grid occupancy marker. Bush occupancy markers are placed by the
spawner using `BushObstacleModel` — they have no renderer. The `Bush.prefab` above is the
single visual instance managed by `BushViewController`, identical to how `PuffCloud.prefab`
is managed by `PuffCloudViewController`.

---

## File plan

### New shared cluster infrastructure (`Slots/Actor/Cluster/`)

| File | Role |
|---|---|
| `IClusterableSlotActor.cs` | Interface: `IWriteableSlotActor` + `ClusterId { get; set; }` |
| `ISlotClusterSource.cs` | Non-generic read interface: `OnClusterChanged`, `Clusters`, `GetClusterAtWorldPosition` |
| `SlotCluster.cs` | Cluster model: list of `Vector2Int` slots + `Rect WorldBounds` |
| `SlotClusterChangedEvent.cs` | Event struct + `SlotClusterChangeType` enum (Created, Resized, Removed) |
| `SlotClusterRegistry.cs` | `SlotClusterRegistry<TModel> : IStartable, IDisposable, ISlotClusterSource` — generic flood-fill/merge/split |
| `IClusterViewSettings.cs` | Shared settings interface: `AnimationSpeed`, `Padding`, `SortingLayerId`, `SortingOrderOffset` |
| `ClusterView.cs` | Abstract `MonoBehaviour` base: `_SlotCentersWorld[]`, `_SlotCount`, `_TimeOffset` MPB logic |
| `ClusterViewController.cs` | `ClusterViewController<TView, TSettings>` — generic `IStartable` managing a single `ClusterView` |

### New Bush-specific files

| File | Location | Role |
|---|---|---|
| `Bush.shader` | `Assets/Shaders/BalloonParty/Grid/` | Procedural top-down bush shader |
| `BushView.cs` | `Slots/Actor/Archetype/` | `ClusterView` subclass; Bush-specific shader properties |
| `BushDisturbanceController.cs` | `Slots/Actor/Archetype/` | IStartable; stamps disturbance field + spawns leaf VFX on projectile overlap |
| `IBushSettings.cs` | `Configuration/` | Extends `IClusterViewSettings`; bush-specific tuning knobs |
| `BushSettings.cs` | `Configuration/` | ScriptableObject implementing `IBushSettings` |
| `PSVFX_BushRustle.prefab` | `Assets/Prefabs/VFX/` | Leaf burst on projectile exit from bush cluster |

### Modified files

| File | Change |
|---|---|
| `BushObstacleModel.cs` | Add `ClusterId` property + implement `IClusterableSlotActor` |
| `PuffObstacleModel.cs` | Add `IClusterableSlotActor` interface (already has `ClusterId`) |
| `PuffCloudView.cs` | Refactor to subclass `ClusterView`; delete duplicated MPB logic |
| `PuffCloudViewController.cs` | Replace with `ClusterViewController<PuffCloudView, IPuffCloudSettings>` registration (delete file or keep as thin alias) |
| `IPuffCloudSettings.cs` | Extend `IClusterViewSettings` instead of standalone |
| `GameLifetimeScope.cs` | Register generic registries + view controllers for both Puff and Bush |

### Deleted files (subsumed by generics)

| File | Replaced by |
|---|---|
| `PuffCluster.cs` | `SlotCluster.cs` |
| `PuffClusterRegistry.cs` | `SlotClusterRegistry<PuffObstacleModel>` |
| `PuffClusterChangedEvent.cs` | `SlotClusterChangedEvent.cs` |

---

## Production checklist

### Phase A — Shared cluster infrastructure (refactor Puff + build generic)

```
1. [ ] Create Slots/Actor/Cluster/ folder + namespace
2. [ ] IClusterableSlotActor interface
3. [ ] SlotCluster (rename from PuffCluster; same API)
4. [ ] SlotClusterChangedEvent + SlotClusterChangeType (rename from Puff*)
5. [ ] ISlotClusterSource interface
6. [ ] SlotClusterRegistry<TModel> (extract from PuffClusterRegistry)
7. [ ] IClusterViewSettings interface
8. [ ] ClusterView abstract base (extract from PuffCloudView)
9. [ ] ClusterViewController<TView, TSettings> (extract from PuffCloudViewController)
10. [ ] PuffObstacleModel — add IClusterableSlotActor interface
11. [ ] PuffCloudView — refactor to subclass ClusterView
12. [ ] IPuffCloudSettings — extend IClusterViewSettings
13. [ ] GameLifetimeScope — update Puff registrations to use generics
14. [ ] Delete PuffCluster.cs, PuffClusterRegistry.cs, PuffClusterChangedEvent.cs
15. [ ] Run existing Puff cluster tests — must pass with zero logic changes
```

### Phase B — Bush implementation (uses shared infrastructure)

```
16. [ ] BushObstacleModel — add ClusterId + IClusterableSlotActor
17. [ ] IBushSettings + BushSettings SO (extends IClusterViewSettings)
18. [ ] Bush.shader — procedural top-down bush with slot merging + disturbance
19. [ ] BushView (subclass of ClusterView)
20. [ ] Bush.prefab (visual quad)
21. [ ] GameLifetimeScope — register SlotClusterRegistry<BushObstacleModel>,
       ClusterViewController<BushView, IBushSettings>, BushDisturbanceController
22. [ ] BushDisturbanceController — disturbance stamping on projectile overlap
23. [ ] Create BushSettings SO asset in Unity
24. [ ] Add Bush entry to GridActorConfiguration SO
25. [ ] Wire into spawner
26. [ ] PSVFX_BushRustle particle effect (on-exit leaf burst)
27. [ ] In-game validation: cluster merging, visual contrast with Puff, disturbance reaction
28. [ ] Shader tuning at game scale (~0.9 world units per slot)
29. [ ] Disturbance stamp tuning — radius, strength, edge wobble scale
```

---

## Resolved questions

1. **Rerouting priority** — ✅ Bush can ship without rerouting. Low weight and max count
   will limit placement enough that `ComputePath` warnings are tolerable. Rerouting
   remains a Phase 9 concern.

2. **Palette tinting** — ✅ Not applicable to terrain actors. Bush uses fixed
   `_BaseColor` / `_LeafVariationColor` — no palette coupling. Terrain reads as
   environment, not as gameplay-coloured content.

3. **Shadow** — ✅ Include shadow support (`_SHADOW_ON` keyword), enabled by default on
   the material. Optional per-instance toggle if needed for performance, but default-on
   is the correct starting point for the grounded read.

4. **Multiple visual variants** — ✅ Yes. The cluster seed drives per-slot noise offsets
   and colour hue shifts, but all clusters must still read as **one continuous ground
   layer** — same base green, same lighting direction, same opacity. Variation is subtle
   (leaf clump pattern, edge bump phase) not structural (no different colours or shapes
   between clusters). The bush is terrain, not individual objects.

5. **Rustle VFX trigger** — ✅ Once per crossing, on **exit**. The particle burst fires
   when the projectile leaves the cluster bounds, not on entry. The disturbance stamp
   fires continuously while overlapping (the field handles natural falloff). This means:
   - Entry: disturbance stamp begins, leaves start warping — no particles yet
   - During: stamp continues each tick, visual warp intensifies
   - Exit: particle burst fires at the exit point — leaves scatter outward in the
     projectile's direction, punctuating the crossing

6. **Disturbance vs Puff tuning** — Deferred to in-game tuning. Smaller radius, higher
   strength than Puff is the starting assumption.

7. **Spawn pathing near Bushes** — ✅ Balloon spawn paths should never route *through*
   a Bush (it's non-traversable). If a Bush occupies a slot in a spawn column, the
   spawner must place balloons either **below** the Bush (closer to the player) or
   **above** it (further from the player), provided there is vertical space. Never
   target a path between Bush slots — the path would visually clip through the bush
   body. This is a spawner/path-computation constraint, not a Bush visual concern.
   Implementation note: `StaticActorSpawner` already places Bushes before balloons
   (`SpawnStage.StaticActors` < `SpawnStage.BalloonActors`), so balloon slot selection
   can read Bush positions and avoid paths that would cross them.

8. **`ClusterViewController` DI wiring** — If VContainer's generic parameter wiring is
   awkward, a thin concrete subclass that just calls `base(...)` is acceptable. Keep
   logic in the base either way.

