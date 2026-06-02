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

---

#### Task Details

##### 1.1 — Create `Slots/Actor/Cluster/` folder + namespace

Create directory `Assets/Source/Slots/Actor/Cluster/`. All new generic cluster types
live here under namespace `BalloonParty.Slots.Actor.Cluster`.

No code — just the folder and a `.meta` file (Unity generates this on folder creation).
All subsequent tasks (1.2–1.9) place files here.

##### 1.2 — `IClusterableSlotActor` interface

```csharp
internal interface IClusterableSlotActor : IWriteableSlotActor
{
    int ClusterId { get; set; }
}
```

Trivial — carries the existing `ClusterId` contract that `PuffObstacleModel` already
satisfies informally. The setter must be `set` (not `internal set`) because the
generic registry assigns it from outside the model's assembly-internal scope.

**Gap — setter visibility:** `PuffObstacleModel.ClusterId` currently has an `internal`
setter. The interface demands a public setter (interfaces can't declare `internal set`
in C#). Two options:
1. Make the setter `public` on the interface and on models — acceptable since both the
   interface and the models are `internal` types, so `public` on an `internal` type is
   effectively `internal`.
2. Use an explicit interface implementation to keep the public setter off the model's
   own API surface.

**Decision needed at implementation time:** option 1 is simpler and consistent with
`IWriteableSlotActor.SlotIndex` which is already `{ get; set; }` on an internal
interface. Go with option 1 unless it creates an unexpected compile issue.

##### 1.3 — `SlotCluster` (rename from `PuffCluster`)

Direct rename + namespace move. The class is 43 lines with no external behaviour
changes:

- `PuffCluster` → `SlotCluster`
- Namespace `BalloonParty.Slots.Actor.Archetype` → `BalloonParty.Slots.Actor.Cluster`
- Same API: `ClusterId`, `Slots`, `WorldBounds`, `SetSlots()`, `AddSlot()`

**Gap — `WorldBounds` padding value:** Both `PuffClusterRegistry.ComputeWorldBounds`
and `PuffCloudViewController.Reconfigure` use a hardcoded `const float halfSlotPadding
= 0.5f` for bounds expansion. This value is duplicated in two places. When extracting
to the generic `SlotCluster`/`SlotClusterRegistry`, we need to decide:
- Keep it hardcoded (simplest — it's a grid constant, not a per-actor-type setting)
- Move it to `IClusterViewSettings` (more flexible, but bounds padding is a registry
  concern, not a view concern)
- Pass it as a constructor parameter to `SlotClusterRegistry<T>`

**Recommendation:** keep hardcoded for now. Both Puff and Bush operate on the same hex
grid with the same slot separation. Parameterise later only if a future cluster type
needs different bounds math.

##### 1.4 — `SlotClusterChangedEvent` + `SlotClusterChangeType`

Direct rename + namespace move from `PuffClusterChangedEvent` / `PuffClusterChangeType`.

```csharp
internal readonly struct SlotClusterChangedEvent
{
    public readonly int ClusterId;
    public readonly SlotClusterChangeType ChangeType;
    public readonly SlotCluster Cluster;
    // constructor
}

internal enum SlotClusterChangeType { Created, Resized, Removed }
```

The event struct references `SlotCluster` (not the generic model type). This means
consumers that subscribe to `OnClusterChanged` receive a `SlotCluster` and don't need
to know `TModel` — good for the non-generic `ISlotClusterSource` interface (task 1.5).

**No gaps.** Straightforward rename.

##### 1.5 — `ISlotClusterSource` interface

Non-generic read-only façade for consumers that need cluster data without knowing the
model type (e.g. disturbance controllers, future `BushDisturbanceController`):

```csharp
internal interface ISlotClusterSource
{
    IObservable<SlotClusterChangedEvent> OnClusterChanged { get; }
    IReadOnlyDictionary<int, SlotCluster> Clusters { get; }
    SlotCluster GetClusterForSlot(Vector2Int slot);
    SlotCluster GetClusterAtWorldPosition(Vector3 worldPos);
}
```

`SlotClusterRegistry<TModel>` implements this interface.

**Gap — multiple registries in DI:** When both `SlotClusterRegistry<PuffObstacleModel>`
and `SlotClusterRegistry<BushObstacleModel>` are registered, a consumer that needs
`ISlotClusterSource` can't resolve it unambiguously — VContainer has no built-in
`IEnumerable<T>` multi-binding. Options:
1. Don't register via `ISlotClusterSource` — consumers inject the concrete closed
   generic type (`SlotClusterRegistry<PuffObstacleModel>`) directly.
2. Register with a named/keyed binding (VContainer doesn't support this natively).
3. Create a composite `AllClusterSources` wrapper that aggregates all registries.

**Decision needed at implementation time:** option 1 is safest for Phase 1 (Puff is the
only cluster consumer). `ISlotClusterSource` becomes useful in Phase 4 when
`BushDisturbanceController` needs to query bush clusters — at that point, inject
`SlotClusterRegistry<BushObstacleModel>` directly. Keep the interface as a contract
marker but don't register it in DI until a composite is actually needed.

##### 1.6 — `SlotClusterRegistry<TModel>` with `setupOnly` flag

The core extraction. Takes the 350-line `PuffClusterRegistry` and genericises it:

```csharp
internal class SlotClusterRegistry<TModel> : IStartable, IDisposable, ISlotClusterSource
    where TModel : class, IClusterableSlotActor
```

**Key changes from `PuffClusterRegistry`:**

| Aspect | Before (Puff-specific) | After (generic) |
|---|---|---|
| Type check in `OnGridChanged` | `actor is PuffObstacleModel` | `actor is TModel` |
| Type check in `RebuildAll` | `_grid.At(idx) is PuffObstacleModel` | `_grid.At(idx) is TModel` |
| `AssignClusterIdToModel` cast | `_grid.At(slot) as PuffObstacleModel` | `_grid.At(slot) as TModel` → set via `IClusterableSlotActor.ClusterId` |
| Cluster type | `PuffCluster` | `SlotCluster` |
| Event type | `PuffClusterChangedEvent` | `SlotClusterChangedEvent` |
| `Start()` subscription | Always subscribes | Guarded by `!_setupOnly` |

**`setupOnly` constructor flag:**
```csharp
internal SlotClusterRegistry(SlotGrid grid, bool setupOnly = false)
{
    _grid = grid;
    _setupOnly = setupOnly;
}
```
When `setupOnly == true`, `Start()` calls `RebuildAll()` but does **not** subscribe to
`_grid.OnChanged`. The registry is a static snapshot — no per-frame work after initial
build. Bush uses this mode because bush slots never change at runtime.

**Gap — VContainer constructor injection of `bool setupOnly`:** VContainer resolves
constructor parameters via DI. A bare `bool` parameter can't be injected — there's no
`bool` binding in the container. Solutions:
1. **Factory method / wrapper registration:** Register with a lambda:
   `builder.Register(c => new SlotClusterRegistry<BushObstacleModel>(c.Resolve<SlotGrid>(), setupOnly: true), Lifetime.Singleton)`
   — but `RegisterEntryPoint` doesn't support factory lambdas.
2. **Two-constructor overload:** Add a parameterless `setupOnly` constructor that
   defaults to `true`, and a normal constructor. Use the parameterless one via a
   thin subclass: `internal class BushClusterRegistry : SlotClusterRegistry<BushObstacleModel> { ... }`
3. **Settings object:** Pass a `ClusterRegistryOptions` struct/record via DI.
4. **Post-construction init:** Use `[Inject]` method to set the flag after construction.

**Recommendation:** Option 2 is cleanest. A thin `BushClusterRegistry` subclass (3
lines) avoids polluting the generic type with DI workarounds and is explicit about
the setup-only intent. For Puff, the existing non-setupOnly behaviour is the default,
so `SlotClusterRegistry<PuffObstacleModel>` works with its normal constructor.

**Alternatively:** if we find VContainer's `Register<T>` with `.WithParameter(...)` works
for `bool`, we can skip the subclass. **Test this during implementation.**

**Gap — `_neighborBuffer` thread safety:** `PuffClusterRegistry` uses a shared
`Vector2Int[6] _neighborBuffer` to avoid allocation in `FloodFill`. In the generic
version, both Puff and Bush registries will have their own instance, so this is fine.
But if `RebuildAll` is ever called off the main thread, the shared buffer is unsafe.
Currently not an issue — `Start()` runs on main thread.

**Gap — `OnClusterChanged` during `RebuildAll`:** `RebuildAll` fires `Created` events
for every cluster found. If a view controller subscribes to `OnClusterChanged` before
`Start()` is called (possible with VContainer's entry point ordering), it will miss
the initial events. The current code works because `PuffCloudViewController.Start()`
subscribes and then `Reconfigure()` is called reactively when the registry emits.
But if `PuffClusterRegistry.Start()` runs first and emits events before
`PuffCloudViewController.Start()` subscribes, the initial build is missed.

The current code is safe because VContainer calls `Start()` in registration order
(registry first, view controller second) **but** the view controller subscribes in
its own `Start()`, which runs after the registry's `Start()` has already emitted.
This means the initial `Created` events ARE missed — the view controller only picks
up subsequent changes.

**Wait — how does it work today?** Looking at the code again:
`PuffCloudViewController.Start()` subscribes to `OnClusterChanged` and the callback is
`_ => Reconfigure()`. `Reconfigure()` reads `_registry.Clusters` (the dictionary), not
the event payload. So it doesn't matter that it missed the initial events — the first
*any* event triggers a full reconfigure from the current state.

This pattern works but is fragile. The generic version should either:
- Keep the same pattern (subscribe + full reconfigure on any change) — **do this**
- Or replay initial state to late subscribers via `ReplaySubject` — over-engineered

##### 1.7 — `IClusterViewSettings` interface

Shared visual configuration contract:

```csharp
internal interface IClusterViewSettings
{
    float AnimationSpeed { get; }
    float Padding { get; }
    int SortingLayerId { get; }
    int SortingOrderOffset { get; }
}
```

This is exactly the current `IPuffCloudSettings` minus the `CloudPrefab` property.

**Gap — prefab reference type:** `IPuffCloudSettings.CloudPrefab` returns
`PuffCloudView` — a concrete MonoBehaviour type. The generic `IClusterViewSettings`
can't declare a prefab property without knowing the view type. Options:
1. Don't include the prefab in `IClusterViewSettings` — each concrete settings
   interface (`IPuffCloudSettings`, `IBushSettings`) declares its own typed prefab.
   The generic `ClusterViewController<TView, TSettings>` accesses it via a required
   abstract method or a second generic constraint.
2. Add `MonoBehaviour Prefab { get; }` to the base interface and downcast in the
   controller.

**Recommendation:** option 1. Add an abstract method to `ClusterViewController`:
```csharp
protected abstract TView GetPrefab(TSettings settings);
```
Each concrete controller (or the settings interface) provides the typed prefab. This
avoids unsafe downcasts and keeps the base interface clean.

**This means `ClusterViewController` must be abstract,** not a fully concrete generic
that can be used directly. Puff and Bush each need a thin subclass (~5 lines) that
implements `GetPrefab`. This contradicts the original plan's assumption of using
`ClusterViewController<PuffCloudView, IPuffCloudSettings>` directly.

**Alternative:** add a `TView Prefab { get; }` to a combined constraint
`TSettings : IClusterViewSettings, IClusterPrefabProvider<TView>`. Then the controller
reads `settings.Prefab` without an abstract method. This lets the controller stay
concrete. **Evaluate at implementation time** — if VContainer can resolve the closed
generic `ClusterViewController<PuffCloudView, IPuffCloudSettings>`, this is cleaner.

##### 1.8 — `ClusterView` abstract base (extract from `PuffCloudView`)

Extracts the ~100 lines of MPB boilerplate from `PuffCloudView` into a reusable base:

```csharp
[ExecuteAlways]
internal abstract class ClusterView : MonoBehaviour
{
    // Shader property IDs (static readonly)
    // _renderer, _block, _slotCenters[16], _slotCount, _configured, _animationSpeed

    // Awake, OnEnable, Update (time offset push), OnValidate
    // EnsureBlock, PushSlotCentersDefault, PushSlotCentersConfigured
    // Configure(Vector4[], int, Rect, IClusterViewSettings)
    // Clear()

    // Hook for subclasses to push extra MPB properties
    protected virtual void OnConfigured(MaterialPropertyBlock block) { }

    internal SpriteRenderer Renderer => _renderer;
}
```

**What moves to the base:** everything currently in `PuffCloudView` — the class is
entirely boilerplate for MPB management + quad sizing. There is no Puff-specific logic.

**What stays in `PuffCloudView`:** the subclass becomes nearly empty:
```csharp
internal class PuffCloudView : ClusterView
{
    // Potentially: override OnConfigured to push Puff-specific shader params
    // Currently: nothing — Puff has no extra MPB properties beyond the base set
}
```

**Gap — `Configure` signature change:** The current `PuffCloudView.Configure` takes
`IPuffCloudSettings` as a parameter. The base class should take `IClusterViewSettings`
instead. But `PuffCloudView.Configure` uses `settings.AnimationSpeed` and
`settings.Padding` — both are on `IClusterViewSettings`. So the signature change is
clean.

**However:** `PuffCloudViewController.Reconfigure` currently calls:
```csharp
_view.Configure(_positionsBuffer, count, combinedBounds, _settings);
```
where `_settings` is `IPuffCloudSettings`. If `Configure` now takes
`IClusterViewSettings`, this still works (contravariant parameter — `IPuffCloudSettings`
extends `IClusterViewSettings`). **No issue.**

**Gap — `[ExecuteAlways]` inheritance:** Unity's `[ExecuteAlways]` attribute is
inherited by subclasses. Verify that both `PuffCloudView : ClusterView` and
`BushView : ClusterView` get edit-mode `Update` calls. **Should work** — Unity
respects inherited `[ExecuteAlways]`.

**Gap — `SceneView.RepaintAll()` in edit mode:** The `Update` method calls
`SceneView.RepaintAll()` in the editor when not playing. This is in a `#if
UNITY_EDITOR` block. Moving this to the base class means every cluster type triggers
scene repaints. This is fine — it's already happening for Puff, and Bush will need it
too. But if we ever have many cluster types, the repeated `RepaintAll()` calls could
cause editor performance issues. Low risk — note and move on.

**Gap — shader property ID names:** The base class hardcodes `_TimeOffset`,
`_SlotCentersWorld`, `_SlotCount` as shader property names. Both the Puff shader and
the future Bush shader must use these exact names. **This is a cross-phase contract**
— document it as a requirement for the Bush shader (Phase 2).

##### 1.9 — `ClusterViewController<TView, TSettings>` (extract from `PuffCloudViewController`)

Extracts the 124-line `PuffCloudViewController` into a generic controller:

```csharp
internal class ClusterViewController<TView, TSettings> : IStartable, IDisposable
    where TView : ClusterView
    where TSettings : IClusterViewSettings
```

**What moves to the generic:**
- `_registry` (becomes `SlotClusterRegistry<TModel>` — but the controller doesn't
  need `TModel`, it only reads clusters via `ISlotClusterSource`)
- `_grid`, `_settings`, `_resolver`
- `_positionsBuffer` (Vector4[16])
- `_view` instance management
- `Start()` — instantiate prefab, set sorting, subscribe to registry
- `Dispose()` — cleanup
- `Reconfigure()` — collect all slot positions across all clusters, compute combined
  bounds, call `_view.Configure()`

**Gap — three generic type parameters or two?** The controller needs:
1. `TView` — to instantiate the correct prefab type
2. `TSettings` — to read config (padding, animation speed, prefab reference)
3. `TModel` — to know which `SlotClusterRegistry<TModel>` to inject

Three type parameters (`ClusterViewController<TModel, TView, TSettings>`) is
unwieldy. The alternative: inject `ISlotClusterSource` instead of the concrete
`SlotClusterRegistry<TModel>`, removing the need for `TModel`.

**But** (see gap in task 1.5): VContainer can't disambiguate multiple
`ISlotClusterSource` registrations. So the controller must inject the concrete
registry type, which requires `TModel`.

**Options:**
1. Three type parameters: `ClusterViewController<TModel, TView, TSettings>` — ugly
   but explicit.
2. Two type parameters + abstract method: `ClusterViewController<TView, TSettings>`
   with `protected abstract ISlotClusterSource GetRegistry()` — subclass provides it.
3. Two type parameters + constructor parameter: pass `ISlotClusterSource` as a
   constructor param, and each concrete registration in `GameLifetimeScope` binds the
   right registry explicitly.
4. **Thin concrete subclass per actor type** (from the "DI wiring" design decision):
   ```csharp
   internal class PuffCloudViewController
       : ClusterViewController<PuffObstacleModel, PuffCloudView, IPuffCloudSettings> { }
   ```
   This is already anticipated in the plan's design decisions table.

**Recommendation:** option 4. Each actor type gets a 3–5 line subclass that closes all
three generic parameters. VContainer registers the concrete subclass. The generic base
holds all logic. This also solves the prefab access problem from task 1.7 — the
subclass can override `GetPrefab`.

**Gap — `_positionsBuffer` size (16):** Both `PuffCloudView` and
`PuffCloudViewController` use a hardcoded cap of 16 slots (matching the shader's array
size `_SlotCentersWorld`). The generic base inherits this cap. If a future cluster type
needs more than 16 slots, both the shader and the C# buffer must change. For now, 16 is
adequate — the grid is small. **Document as a known constraint.**

**Gap — combined bounds recomputation:** `PuffCloudViewController.Reconfigure`
recomputes combined bounds across ALL clusters every time ANY cluster changes. This is
O(total_slots) per event. For the current grid sizes (~5–10 puff slots) this is
negligible. For Bush with `setupOnly = true`, `Reconfigure` runs only once (at startup).
No performance concern — but note that if a future dynamic cluster type has frequent
changes and many slots, this could become a hot path.

**Gap — seed calculation:** `Reconfigure` uses `cluster.ClusterId * 0.7123f % 1f` as a
per-cluster noise seed passed in `_SlotCentersWorld[i].z`. This is Puff-specific
visual logic (it makes adjacent clusters look distinct in the noise field). Bush may
want a different seed strategy (e.g. position-based hash for radius jitter). The
generic controller should either:
- Keep the same seed logic (it's generic enough — any SDF-based shader benefits from
  per-cluster seed variation)
- Make seed computation virtual: `protected virtual float ComputeSeed(SlotCluster c)`

**Recommendation:** keep the same seed logic in the base. Override in `BushView` if
needed (via `OnConfigured` — the view already has the seed in `.z`).

##### 1.10 — `PuffObstacleModel` — add `IClusterableSlotActor`

Minimal change. `PuffObstacleModel` already has `ClusterId { get; internal set; }`.
Add `: IClusterableSlotActor` to the declaration:

```csharp
internal class PuffObstacleModel : IClusterableSlotActor, IPassThrough
```

The `IClusterableSlotActor` extends `IWriteableSlotActor`, so the existing
`IWriteableSlotActor` can be removed from the class declaration (it's inherited).

**Gap — explicit interface implementation for `SlotIndex`:** `PuffObstacleModel`
currently uses an explicit interface implementation for `IWriteableSlotActor.SlotIndex`
(the setter). If `IClusterableSlotActor` inherits `IWriteableSlotActor`, the explicit
implementation should still work — C# resolves explicit implementations through the
most-derived interface. **Verify at compile time.**

##### 1.11 — `PuffCloudView` — refactor to subclass `ClusterView`

Replace the 179-line `PuffCloudView` with a thin subclass of `ClusterView`:

```csharp
[ExecuteAlways]
internal class PuffCloudView : ClusterView
{
    // No overrides needed — Puff has no extra MPB properties
}
```

All current fields, methods, and MPB logic move to `ClusterView` (task 1.8).

**Gap — serialized field migration:** `PuffCloudView` has `[SerializeField] private
SpriteRenderer _renderer` and `[SerializeField] private float _animationSpeed`. If
these fields move to the base class `ClusterView`, **existing prefab references will
break** — Unity serializes fields per-class, and moving a field to a base class changes
its serialization path. The prefab asset will lose its `_renderer` and `_animationSpeed`
values.

**Mitigation options:**
1. **Re-assign in the prefab** after the refactor — open the PuffCloud prefab, re-drag
   the SpriteRenderer, re-set animation speed. Quick but manual.
2. Use `[FormerlySerializedAs("_renderer")]` on the base class field — Unity will
   migrate the value from the old path. **This only works if the field name stays the
   same**, which it does. Test whether `FormerlySerializedAs` handles base-class
   migration (it should — Unity serializes by field name, not declaring type).
3. Keep the `[SerializeField]` fields on the subclass and pass them to the base via
   a protected property — avoids the serialization issue but adds boilerplate.

**Recommendation:** option 2 first. If it doesn't migrate cleanly, fall back to option
1 (manual prefab fix). **Flag as a manual verification step during implementation.**

##### 1.12 — `IPuffCloudSettings` — extend `IClusterViewSettings`

Change from:
```csharp
internal interface IPuffCloudSettings
{
    PuffCloudView CloudPrefab { get; }
    float AnimationSpeed { get; }
    float Padding { get; }
    int SortingLayerId { get; }
    int SortingOrderOffset { get; }
}
```

To:
```csharp
internal interface IPuffCloudSettings : IClusterViewSettings
{
    PuffCloudView CloudPrefab { get; }
}
```

The four shared properties (`AnimationSpeed`, `Padding`, `SortingLayerId`,
`SortingOrderOffset`) are inherited from `IClusterViewSettings`.

**Gap — `PuffCloudSettings` SO:** The concrete `PuffCloudSettings : ScriptableObject,
IPuffCloudSettings` already implements all five properties. After this change, it
implicitly implements `IClusterViewSettings` through `IPuffCloudSettings`. No code
changes needed in the SO — the existing property implementations satisfy both
interfaces. **Verify at compile time.**

##### 1.13 — `GameLifetimeScope` — update Puff registrations to generics

Replace:
```csharp
builder.RegisterEntryPoint<PuffClusterRegistry>().AsSelf();
builder.RegisterEntryPoint<PuffCloudViewController>();
```

With (assuming thin subclass approach from task 1.9):
```csharp
builder.RegisterEntryPoint<SlotClusterRegistry<PuffObstacleModel>>().AsSelf();
builder.RegisterEntryPoint<PuffCloudViewController>();
```

If using a renamed `PuffCloudViewController` that now extends the generic base, the
second line stays identical.

**Gap — VContainer open generic support:** VContainer's `RegisterEntryPoint<T>()` works
with closed generic types (`SlotClusterRegistry<PuffObstacleModel>`). This should work
— VContainer resolves closed generics the same as concrete types. **Verify at runtime**
— if VContainer fails to resolve, fall back to the thin-subclass approach:
```csharp
internal class PuffClusterRegistry : SlotClusterRegistry<PuffObstacleModel>
{
    [Inject]
    internal PuffClusterRegistry(SlotGrid grid) : base(grid) { }
}
```
This preserves the existing class name and DI registration, minimising blast radius.

**Gap — `AsSelf()` semantics:** Currently `PuffClusterRegistry` is registered `.AsSelf()`
so that `PuffCloudViewController` can inject `PuffClusterRegistry` directly. After
genericising, the controller injects `SlotClusterRegistry<PuffObstacleModel>` (or
`ISlotClusterSource`). If we use the thin subclass, `.AsSelf()` registers the subclass
type — the controller must inject the subclass type. **Keep consistent** — either both
use the subclass name, or both use the closed generic.

##### 1.14 — Delete old Puff-specific files

Remove these files after all references are updated:
- `Slots/Actor/Archetype/PuffCluster.cs` → replaced by `Slots/Actor/Cluster/SlotCluster.cs`
- `Slots/Actor/Archetype/PuffClusterRegistry.cs` → replaced by `Slots/Actor/Cluster/SlotClusterRegistry.cs`
- `Slots/Actor/Archetype/PuffClusterChangedEvent.cs` → replaced by `Slots/Actor/Cluster/SlotClusterChangedEvent.cs`
- `Slots/Actor/Archetype/PuffCloudViewController.cs` → replaced by thin subclass in `Slots/Actor/Cluster/` or stays in `Archetype/` as a subclass

**Gap — `.meta` file cleanup:** Each deleted `.cs` file has a corresponding `.meta`
file. Unity tracks assets by GUID in `.meta` files. Deleting them may break:
- Prefab references (if any prefab has a script reference to these MonoBehaviours) —
  none of the deleted files are MonoBehaviours, so no prefab references.
- Assembly definition references — not applicable.
- Test file `using` statements — **check if any test files import the old namespaces.**

From the earlier search, no test files reference `PuffCluster` types. Safe to delete.

**Gap — git history:** Renaming files (vs. delete + create) preserves git history.
Use `git mv` where possible:
- `PuffCluster.cs` → `../Cluster/SlotCluster.cs`
- `PuffClusterRegistry.cs` → `../Cluster/SlotClusterRegistry.cs`
- `PuffClusterChangedEvent.cs` → `../Cluster/SlotClusterChangedEvent.cs`

Then modify content after the move. Git's rename detection will link the history.

##### 1.15 — Run existing Puff cluster tests — all green

**Gap — no test files found.** The grep for `PuffCluster` in test files returned zero
results. This means either:
1. Puff cluster behaviour is tested indirectly through integration tests
2. There are no dedicated cluster tests

If option 2, the refactor has no automated safety net. **Mitigation:** run the full
edit-mode test suite (`BalloonParty.Tests.EditMode`) and verify zero regressions. Also
do a manual play-mode smoke test: verify Puff clouds render, clusters merge/split when
slots are added/removed, and the disturbance field reacts to projectile fly-overs.

**Recommendation:** before starting the refactor, write 2–3 focused tests for
`SlotClusterRegistry<T>` covering:
- Single slot → one cluster created
- Adjacent slots → merged into one cluster
- Non-adjacent slots → two separate clusters
- Slot removal splits a cluster → two clusters

These tests use a mock `IClusterableSlotActor` and a `SlotGrid`, so they're fast
edit-mode tests. Writing them first gives a safety net for the refactor.

---

#### Task Checklist

```
1.1  [ ] Create Slots/Actor/Cluster/ folder + namespace
1.2  [ ] IClusterableSlotActor interface
         └── Resolve: setter visibility (public on internal type — go with it)
1.3  [ ] SlotCluster (rename + move PuffCluster)
         └── Resolve: bounds padding — keep hardcoded 0.5f for now
1.4  [ ] SlotClusterChangedEvent + SlotClusterChangeType (rename + move)
1.5  [ ] ISlotClusterSource interface
         └── Resolve: don't register in DI yet — inject concrete closed generic
1.6  [ ] SlotClusterRegistry<TModel> with setupOnly flag
         └── Resolve: VContainer bool injection — try .WithParameter first, fall back
             to thin subclass
         └── Verify: RebuildAll event timing vs. view controller subscription
1.7  [ ] IClusterViewSettings interface
         └── Resolve: prefab reference — keep off base interface, use abstract
             method or IClusterPrefabProvider<TView>
1.8  [ ] ClusterView abstract base
         └── Resolve: [SerializeField] migration — try FormerlySerializedAs, verify
             prefab survives
         └── Verify: [ExecuteAlways] inheritance
         └── Document: shader property name contract (_TimeOffset, _SlotCentersWorld,
             _SlotCount)
1.9  [ ] ClusterViewController<TView, TSettings> (or <TModel, TView, TSettings>)
         └── Resolve: 2 vs 3 type parameters — recommend thin subclass (option 4)
         └── Verify: VContainer closed generic resolution
1.10 [ ] PuffObstacleModel — implement IClusterableSlotActor
         └── Verify: explicit interface implementation compiles
1.11 [ ] PuffCloudView — subclass ClusterView
         └── Verify: prefab serialization survives field migration
1.12 [ ] IPuffCloudSettings — extend IClusterViewSettings
         └── Verify: PuffCloudSettings SO compiles without changes
1.13 [ ] GameLifetimeScope — update registrations
         └── Verify: VContainer resolves closed generic entry points
         └── Fallback: thin subclass preserving original class name
1.14 [ ] Delete old files (PuffCluster, PuffClusterRegistry, PuffClusterChangedEvent,
         PuffCloudViewController)
         └── Use git mv for history preservation
         └── Verify: no remaining references in tests or other files
1.15 [ ] Validation pass
         └── Gap: no dedicated cluster tests exist — write 2–3 before refactoring
         └── Run full edit-mode test suite
         └── Manual smoke test: cluster rendering, merge/split, disturbance reaction
```

#### Consolidated Gap Summary

| # | Gap | Risk | Recommendation |
|---|---|---|---|
| G1 | `IClusterableSlotActor.ClusterId` setter visibility — interfaces can't declare `internal set` | Low | Use `public set` on internal interface — effectively internal |
| G2 | Bounds padding `0.5f` duplicated in registry and controller | Low | Keep hardcoded; both actor types share the same grid geometry |
| G3 | Multiple `ISlotClusterSource` registrations ambiguous in VContainer | Medium | Don't register `ISlotClusterSource` in DI — inject closed generic directly |
| G4 | VContainer can't inject bare `bool setupOnly` constructor parameter | Medium | Try `.WithParameter("setupOnly", true)`; fall back to thin subclass |
| G5 | Prefab reference type-safety across generic controller | Medium | Abstract method `GetPrefab(TSettings)` or `IClusterPrefabProvider<TView>` on settings |
| G6 | 2 vs 3 generic type parameters on `ClusterViewController` | Medium | Use 3 params + thin per-actor subclass to close them (plan already anticipates this) |
| G7 | `[SerializeField]` fields moving to base class breaks prefab serialization | High | Use `[FormerlySerializedAs]` on base class fields; manually verify prefab |
| G8 | Shader property name contract (`_TimeOffset`, `_SlotCentersWorld`, `_SlotCount`) | Low | Document as requirement for all cluster shaders; enforced by ClusterView base |
| G9 | No dedicated cluster tests exist | High | Write 3–4 registry unit tests BEFORE starting the refactor |
| G10 | `RebuildAll` events fired before view controller subscribes | Low | Current pattern (full reconfigure on any event) handles this; keep as-is |
| G11 | `_positionsBuffer` capped at 16 (shader array limit) | Low | Adequate for current grid sizes; document as known constraint |

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

