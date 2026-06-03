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
1.1  [x] Create Slots/Actor/Cluster/ folder + namespace
1.2  [x] IClusterableSlotActor interface
         └── Resolved: public set on internal interface — matches IWriteableSlotActor pattern
1.3  [x] SlotCluster (rename + move PuffCluster)
         └── Resolved: bounds padding — kept hardcoded 0.5f; git mv for history
1.4  [x] SlotClusterChangedEvent + SlotClusterChangeType (rename + move)
1.5  [x] ISlotClusterSource interface
         └── Resolved: don't register in DI yet — inject concrete closed generic
1.6  [x] SlotClusterRegistry<TModel> with setupOnly flag
         └── Resolved: thin PuffClusterRegistry subclass avoids bool injection issue
         └── Resolved: RebuildAll event timing — current pattern handles late subscribers
1.7  [x] IClusterViewSettings interface
1.8  [x] ClusterView abstract base
         └── Resolved: [SerializeField] fields live on base; prefab needs manual re-link
         └── Added: OnUpdateBlock virtual hook for per-frame subclass properties
         └── Verified: shader property name contract (_TimeOffset, _SlotCentersWorld, _SlotCount)
1.9  [x] ClusterViewController<TModel, TView, TSettings> — abstract with GetPrefab
         └── Resolved: 3 type params + thin subclass per actor type
         └── Fixed: UnityEngine.Object.Destroy ambiguity
1.10 [x] PuffObstacleModel — implement IClusterableSlotActor
         └── Resolved: ClusterId setter changed from internal to public
1.11 [x] PuffCloudView — subclass ClusterView (179 lines → 8 lines)
         └── Pending: manual prefab re-link of SpriteRenderer + animation speed
1.12 [x] IPuffCloudSettings — extend IClusterViewSettings
         └── Verified: PuffCloudSettings SO compiles without changes
1.13 [x] GameLifetimeScope — no changes needed (thin subclasses preserve class names)
1.14 [x] Delete old files — PuffCluster + PuffClusterChangedEvent git-mv'd;
         PuffClusterRegistry + PuffCloudViewController kept as thin subclasses
         └── Use git mv for history preservation
         └── Verified: no remaining references to deleted types
1.15 [x] Validation pass
         └── Zero compile errors across all 15 affected files
         └── Pending: manual smoke test — cluster rendering, merge/split, disturbance
         └── Pending: prefab re-link of SpriteRenderer on PuffCloudView (field moved
             to ClusterView base class)
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

---

#### Task Details

##### 2.1 — Shader file scaffold

Create `Assets/Shaders/BalloonParty/Grid/Bush.shader` with the basic structure
mirroring PuffCloud.shader conventions:

```hlsl
Shader "BalloonParty/Grid/Bush"
{
    Properties
    {
        // Shape
        _SlotRadius         ("Slot Radius",         Float)              = 0.40
        _RadiusJitter       ("Radius Jitter",       Range(0, 0.15))     = 0.06
        _EdgeNoiseFreq      ("Edge Noise Frequency",Float)              = 4.0
        _EdgeNoiseAmount    ("Edge Noise Amount",   Range(0, 0.2))      = 0.08

        // Surface
        _BaseColor          ("Base Color",          Color)              = (0.18, 0.45, 0.12, 1.0)
        _LeafVariationColor ("Leaf Variation Color",Color)              = (0.25, 0.55, 0.15, 1.0)
        _LeafNoiseFreq      ("Leaf Noise Frequency",Float)              = 6.0

        // Lighting
        _LightDir           ("Light Direction",     Vector)             = (-0.4, 0.7, 0, 0)
        _LightColor         ("Highlight Color",     Color)              = (1, 1, 0.9, 1)
        _AmbientColor       ("Shadow Tint",         Color)              = (0.08, 0.22, 0.05, 1)
        _LightIntensity     ("Light Intensity",     Range(0, 1))        = 0.50
        _NormalStrength     ("Normal Strength",     Range(0, 3))        = 1.5
        _NormalEpsilon      ("Normal Sample Offset",Range(0.001, 0.05))= 0.015

        // Rim
        _RimWidth           ("Rim Width",           Range(0, 0.15))     = 0.04
        _RimIntensity       ("Rim Intensity",       Range(0, 1))        = 0.35

        // Animation
        _WindSpeed           ("Wind Speed",          Range(0, 2))        = 0.4
        _WindAmount          ("Wind Amount",         Range(0, 0.1))      = 0.02
        _TimeOffset          ("Time Offset",         Float)              = 0.0

        // Shadow
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float) = 1
        _ShadowColor         ("Shadow Color",        Color)              = (0.04, 0.04, 0.08, 0.45)
        _ShadowOffsetX       ("Shadow Offset X",     Range(-0.15, 0.15)) = 0.03
        _ShadowOffsetY       ("Shadow Offset Y",     Range(-0.15, 0.15)) = -0.04
        _ShadowSoftness      ("Shadow Softness",     Range(0, 0.10))     = 0.04

        // Centre Shadow
        [Toggle(_CENTER_SHADOW_ON)] _EnableCenterShadow ("Enable Center Shadow", Float) = 0

        // Disturbance
        [Toggle(_DISTURBANCE_ON)] _EnableDisturbance ("Enable Disturbance", Float) = 0
        _DisplaceWorldScale  ("Displace World Scale", Range(0, 2))       = 0.3
        _EdgeDisturbanceScale("Edge Disturbance Scale", Range(0, 3))     = 1.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout"
               "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off   Lighting Off   ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        // ...
    }
}
```

**Key structural decisions:**

- **Render queue:** `Transparent` queue like PuffCloud, NOT `Geometry`. Even though the
  bush body is fully opaque, the SDF boundary and ground shadow need alpha blending for
  soft antialiased edges. The alpha clip gives a hard interior but the edge pixels need
  sub-pixel blending to avoid jaggies.
- **`ZWrite Off`:** Same as PuffCloud — both are 2D overlays on a SpriteRenderer quad.
- **`#define MAX_SLOTS 16`:** Same cap as PuffCloud, matching `ClusterView._slotCenters`.
- **Includes:** `UnityCG.cginc` + `SimplexNoise2D.cginc` (same path as PuffCloud).
- **Shader keywords:** `_SHADOW_ON`, `_CENTER_SHADOW_ON`, `_DISTURBANCE_ON` — three
  `shader_feature` pragmas.
- **MPB contract:** Must declare `_SlotCentersWorld[MAX_SLOTS]`, `_SlotCount`,
  `_TimeOffset` — matches `ClusterView` base class (Phase 1, task 1.8).

**Gap — render order vs. Puff:** Both Bush and Puff use `Queue=Transparent`. Sorting
order is controlled by C# (`SortingLayerId`, `SortingOrderOffset` from settings).
Bush should render *below* Puff (bush is ground-level, cloud is above). This is a
Phase 3 config concern (set `SortingOrderOffset` lower on `IBushSettings`), but the
shader must not assume any particular sort order. **Document as a Phase 3 tuning note.**

**Gap — vertex struct:** PuffCloud passes `worldPos` as `TEXCOORD1`. Bush needs the
same (world-space noise sampling). Should also pass `texcoord` for potential future
UV-based effects. Identical `appdata_t` / `v2f` structs — copy from PuffCloud.

**Gap — `_RendererColor` instancing block:** PuffCloud includes the Unity sprite
instancing boilerplate (`UNITY_INSTANCING_BUFFER_START` etc.). Bush should include the
same block for consistency, even though GPU instancing is disabled via MPB. Without it,
the SpriteRenderer's color tint won't apply. **Copy verbatim from PuffCloud.**

##### 2.2 — Per-slot circle SDF + smooth-minimum merging

The core shape primitive. Each slot center defines a circle SDF; adjacent slots merge
via polynomial smooth-minimum.

```hlsl
// Polynomial smooth-min (k controls blend radius)
float smin(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

// Returns the minimum SDF distance to any slot circle,
// with smooth merging between adjacent slots.
// Also outputs the nearest slot's seed (.z) for per-slot variation.
float BushSDF(float2 wp, out float slotSeed)
{
    float d = 999.0;
    slotSeed = 0.0;
    float minRawDist = 999.0;

    for (int i = 0; i < _SlotCount; i++)
    {
        float2 center = _SlotCentersWorld[i].xy;
        float seed = _SlotCentersWorld[i].z;

        // Per-slot radius jitter from position hash
        float hash = frac(sin(dot(center, float2(127.1, 311.7))) * 43758.5453);
        float radius = _SlotRadius + (hash - 0.5) * 2.0 * _RadiusJitter;

        float dist = length(wp - center) - radius;
        d = smin(d, dist, _SminK);

        // Track nearest slot for seed
        float rawDist = length(wp - center);
        if (rawDist < minRawDist)
        {
            minRawDist = rawDist;
            slotSeed = seed;
        }
    }
    return d;
}
```

**Property:** `_SminK` — smooth-min blend radius. Controls how aggressively adjacent
slot circles merge. Suggested range 0.1–0.4 world units. At the reference slot
separation of (1.0, 0.85), a value of ~0.2 gives a gentle organic merge without
losing the individual circle shapes.

**Gap — `_SminK` not in the Properties block yet.** Add it:
```
_SminK ("Smooth Min K", Range(0.05, 0.5)) = 0.20
```

**Gap — seed tracking during smin:** The `smin` operation blends distances from
multiple slots, so the concept of "nearest slot" becomes ambiguous in the blend region.
The seed selection uses raw (unsmoothed) distance — nearest center wins. This means the
seed can pop discontinuously at the midpoint between two slots. For Bush this is
acceptable because the seed only drives subtle noise phase offsets, not colour. If it
causes visible seams in the leaf noise, consider blending seeds by inverse distance.
**Test during tuning (Phase 5).**

**Gap — loop unrolling / performance:** The SDF loop runs `_SlotCount` iterations per
fragment, each calling `smin`. For 16 slots this is 16 iterations × 2 noise samples
(the slot falloff + noise) — expensive on mobile. PuffCloud has the same O(N) loop for
`SlotFalloff`. Both shaders share the same worst case. If performance is an issue, the
fix is reducing `MAX_SLOTS` or spatially partitioning — not a Phase 2 concern.

**Gap — SDF sign convention:** `dist < 0` means inside the shape, `dist > 0` means
outside. The alpha clip (task 2.4) discards pixels where `dist > 0`. Make sure all
consumers agree on this convention. PuffCloud uses a `smoothstep` falloff (not an SDF)
so there's no precedent to conflict with — Bush is the first true SDF shape.

##### 2.3 — Edge noise distortion with per-slot phase offset + radius jitter

Apply 2-octave Simplex noise to the SDF boundary to create organic leaf bumps:

```hlsl
float EdgeNoise(float2 wp, float t, float slotSeed)
{
    float2 seedOffset = float2(slotSeed * 61.7, slotSeed * 37.3);
    float2 p1 = (wp + seedOffset) * _EdgeNoiseFreq;
    float2 p2 = (wp + seedOffset) * _EdgeNoiseFreq * 2.17;

    float n  = SimplexNoise2D(p1 + float2(t * 0.1, 0.0)) * 0.65;
    n       += SimplexNoise2D(p2 + float2(0.0, t * 0.15)) * 0.35;

    return n;  // [-1, 1] range
}
```

The edge noise modulates the SDF distance before the alpha clip:
```hlsl
float d = BushSDF(wp, slotSeed);
float edgeN = EdgeNoise(wp, t, slotSeed);
d -= edgeN * _EdgeNoiseAmount;  // push boundary in/out
```

**Per-slot phase offset:** `slotSeed * 61.7` and `slotSeed * 37.3` shift the noise
sampling origin per slot, so adjacent slots don't share the exact same bump pattern
even when their circles merge. The seed comes from `_SlotCentersWorld[i].z`, set by
`ClusterViewController.Reconfigure` as `(clusterId * 0.7123) % 1`.

**Gap — edge noise interacting with smin blend:** In the merge region between two
slots, the SDF is blended but the edge noise uses only the *nearest* slot's seed.
This can create a visible noise pattern boundary at the merge seam. Options:
1. Accept it — the merge region is small and the noise is subtle.
2. Blend edge noise from both nearest slots weighted by proximity — expensive (two
   extra noise samples per fragment in the blend region).

**Recommendation:** option 1 for now. The smin blend radius is small enough that the
seam should be hidden. Revisit in Phase 5 tuning.

**Gap — edge noise frequency vs. slot radius:** At `_SlotRadius = 0.40` and
`_EdgeNoiseFreq = 4.0`, one noise period spans ~0.25 world units — about 60% of the
radius. This should produce 3–4 bumps per circle edge. Too many bumps look busy; too
few look like a blob. **Tune in Phase 5.**

##### 2.4 — Alpha clip at SDF boundary

Hard alpha clip at `d = 0` with a thin antialiased edge:

```hlsl
float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, d);
if (alpha < 0.001) discard;
```

**Property:** `_AAWidth` — antialiasing transition width. Suggested value 0.005–0.015
world units (1–3 pixels at game resolution). Smaller = sharper, larger = softer.

**Gap — `_AAWidth` not in Properties block.** Add:
```
_AAWidth ("AA Edge Width", Range(0.001, 0.03)) = 0.008
```

**Why smoothstep, not hard clip?** A raw `clip(d)` produces aliased 1-pixel staircase
edges. The smoothstep transition over a thin band gives sub-pixel antialiasing without
requiring MSAA. PuffCloud already uses smoothstep for its noise-based edges — same
approach, different domain.

**Gap — alpha vs. opaque body:** The plan says Bush is "fully opaque inside, transparent
outside." The smoothstep transition creates a thin semi-transparent edge band. Interior
pixels have `alpha = 1.0`. This is correct — the edge blending handles the cutout
antialiasing while the body reads as solid. Verify that `ZWrite Off` + `Blend SrcAlpha
OneMinusSrcAlpha` produces the expected result (the semi-transparent edge should blend
with whatever is behind it — the game background).

##### 2.5 — Leaf noise colour modulation (world-space, multi-octave)

Interior surface detail — 3-octave Simplex noise in world space modulating between
`_BaseColor` and `_LeafVariationColor`:

```hlsl
float LeafNoise(float2 wp, float t, float slotSeed)
{
    float2 seedOff = float2(slotSeed * 91.3, slotSeed * 53.7);
    float2 p = (wp + seedOff) * _LeafNoiseFreq;

    float n  = SimplexNoise2D(p + float2(t * 0.02, t * 0.01)) * 0.50;
    n       += SimplexNoise2D(p * 2.13 + float2(-t * 0.03, t * 0.02)) * 0.30;
    n       += SimplexNoise2D(p * 4.37 + float2(t * 0.01, -t * 0.04)) * 0.20;

    return n * 0.5 + 0.5;  // remap to [0, 1]
}
```

Colour mixing:
```hlsl
float leafN = LeafNoise(wp, t, slotSeed);
fixed3 surfaceColor = lerp(_BaseColor.rgb, _LeafVariationColor.rgb, leafN);
```

**Gap — noise call count:** By this point the fragment shader has:
- `BushSDF`: 0 noise calls (pure math)
- `EdgeNoise`: 2 Simplex calls
- `LeafNoise`: 3 Simplex calls
- Total so far: 5 per fragment

Adding lighting (task 2.6) will add 4 more (central differences on LeafNoise). Total:
~9 Simplex calls per fragment. PuffCloud uses 3 (CloudNoise) + 4 (lighting) = 7. Bush
is ~30% more expensive. On mobile, each SimplexNoise2D call involves a permute + 3
gradient evaluations.

**Mitigation:** reduce LeafNoise to 2 octaves if profiling shows a problem. The third
octave (4.37× frequency) adds fine detail that may not be visible at game scale
(~0.9 world units per slot × ~100 pixels across). **Profile in Phase 5.**

**Gap — wind sway on leaf noise (task 2.10 dependency):** The time-based scroll
offsets in LeafNoise already produce gentle drift. Wind sway (task 2.10) adds an
additional UV displacement. Need to decide: does wind affect leaf noise sampling
coordinates, or is it a separate post-process displacement? If it affects sampling
coords, the leaf pattern shifts with the wind — looks like leaves rustling. If it's a
post-process, the leaf pattern stays fixed and the geometry wobbles — looks like the
whole bush swaying. **Recommend: affect sampling coords** for the leaf-rustle look.
Wind sway in task 2.10 will displace `wp` before passing it to `LeafNoise`.

##### 2.6 — Pseudo-lighting (half-Lambert from noise gradient)

Same technique as PuffCloud — derive a pseudo-normal from the leaf noise gradient via
central differences, then apply half-Lambert lighting:

```hlsl
fixed3 BushLighting(float2 wp, float t, float slotSeed)
{
    float eps = _NormalEpsilon;
    float nR = LeafNoise(wp + float2( eps, 0), t, slotSeed);
    float nL = LeafNoise(wp + float2(-eps, 0), t, slotSeed);
    float nU = LeafNoise(wp + float2(0,  eps), t, slotSeed);
    float nD = LeafNoise(wp + float2(0, -eps), t, slotSeed);

    float dX = (nR - nL) * _NormalStrength;
    float dY = (nU - nD) * _NormalStrength;
    float3 normal = normalize(float3(-dX, -dY, 1.0));

    float2 ld = normalize(_LightDir.xy);
    float3 lightVec = normalize(float3(ld, 0.6));
    float NdotL = dot(normal, lightVec);
    float halfLambert = NdotL * 0.5 + 0.5;

    fixed3 lit = lerp(_AmbientColor.rgb, _LightColor.rgb, halfLambert);
    return lerp(fixed3(1,1,1), lit, _LightIntensity);
}
```

This is nearly identical to PuffCloud's `CloudLighting`. The only difference is it
calls `LeafNoise` instead of `CloudNoise` for the gradient samples.

**Gap — 4 extra LeafNoise calls:** Each `LeafNoise` call is 3 Simplex evaluations
(if we keep 3 octaves). The lighting gradient needs 4 calls = 12 extra Simplex
evaluations, bringing the total to ~5 + 12 = **17 Simplex calls per fragment**. This
is expensive.

**Mitigation options:**
1. Use a simplified 1-octave `LeafNoiseFast(wp)` for the gradient samples — the
   lighting doesn't need fine detail, just the broad slope. Reduces to 4 Simplex calls
   for lighting instead of 12.
2. Compute the gradient from the already-evaluated `LeafNoise` at the fragment center
   using screen-space derivatives (`ddx`/`ddy`). This is free (GPU computes derivatives
   in hardware) but gives screen-resolution gradients, not world-resolution — may be
   too noisy at certain zoom levels.
3. Skip the gradient and use a fixed normal — flat lighting, no depth perception. Loses
   the cartoony volume.

**Recommendation:** option 1. Create `LeafNoiseLite(wp, t, slotSeed)` that uses only
the base octave (1 Simplex call). Total cost: 5 (shape + leaf) + 4 (lighting) =
**9 Simplex calls** — same as PuffCloud.

**Gap — shared lighting code with PuffCloud:** The lighting function is identical
except for which noise function it calls. If both shaders live in the same `Grid/`
folder, consider extracting the half-Lambert + normal derivation into a shared
`.cginc` include. For now, duplicating is fine — the function is 15 lines. **Note for
future cleanup.**

##### 2.7 — Edge highlight rim

A bright rim along the SDF boundary adds visual definition (reads as leaf edges
catching light):

```hlsl
float rim = smoothstep(_RimWidth, 0.0, abs(d));  // 1 at edge, 0 away
fixed3 finalColor = lerp(surfaceColor * lighting, _LightColor.rgb, rim * _RimIntensity);
```

`d` is the SDF distance (negative inside, positive outside). `abs(d)` is the unsigned
distance to the boundary. `_RimWidth` controls how deep the rim penetrates inward.

**Gap — rim on merged edges:** When two slots merge via smin, the internal boundary
between them has `d < 0` (well inside the shape). There is no SDF edge there — the rim
only appears on the outer perimeter. This is correct and desired — we don't want a
bright line between merged slots.

**Gap — rim vs. edge noise interaction:** Edge noise pushes the SDF boundary in/out,
which shifts where the rim appears. The rim naturally follows the noisy edge — good,
it makes the rim look organic. No issue.

**No new properties needed** — `_RimWidth` and `_RimIntensity` are already planned in
the Properties block (task 2.1).

##### 2.8 — Centre shadow (optional keyword `_CENTER_SHADOW_ON`)

Darkens the centre of each slot circle to add perceived volume (like the bush is
denser / self-shadowing at its middle):

```hlsl
#ifdef _CENTER_SHADOW_ON
float centerDist = 999.0;
for (int i = 0; i < _SlotCount; i++)
{
    centerDist = min(centerDist, length(wp - _SlotCentersWorld[i].xy));
}
float centerFade = 1.0 - smoothstep(_SlotRadius * 0.3, _SlotRadius * 0.8, centerDist);
surfaceColor *= lerp(1.0, _CenterShadowDarkness, centerFade);
#endif
```

**Property:** `_CenterShadowDarkness` — how dark the centre gets. Range 0.5–0.9.
Add to Properties block:
```
_CenterShadowDarkness ("Center Shadow Darkness", Range(0.3, 1.0)) = 0.75
```

**Gap — centre shadow in merge region:** When two slots merge, the midpoint between
them could receive centre shadow from *both* slots (the `min` distance may be large
there, so no shadow). This is actually correct — the merge region is far from both
centers so it won't be darkened. Only the core of each individual slot gets the
darkening. **Good behaviour — no fix needed.**

**Gap — interaction with lighting:** Centre shadow multiplies `surfaceColor` before
lighting is applied. This means the darkening affects the normal-derived lighting as
a base colour change (darker base → darker lit result). Alternatively, apply it after
lighting as a post-multiply. Difference is subtle — **test both during tuning.**

##### 2.9 — Ground shadow (`_SHADOW_ON`)

A larger, offset, softer version of the bush SDF rendered behind the main body.
Same architectural pattern as PuffCloud's shadow pass:

```hlsl
#ifdef _SHADOW_ON
float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);
float shadowSeed;
float shadowD = BushSDF(shadowWp, shadowSeed);
shadowD -= EdgeNoise(shadowWp, t, shadowSeed) * _EdgeNoiseAmount;

float shadowAlpha = (1.0 - smoothstep(-_ShadowSoftness, 0.0, shadowD))
                  * _ShadowColor.a * IN.color.a;

// Compose: shadow behind, main body in front
if (alpha < 0.001 && shadowAlpha < 0.001) discard;

if (alpha < 0.001)
{
    return fixed4(_ShadowColor.rgb, shadowAlpha);
}

// Main body + shadow composite (same as PuffCloud)
fixed combinedA = alpha + shadowAlpha * (1.0 - alpha);
fixed3 combinedRGB = (mainRgb * alpha + _ShadowColor.rgb * shadowAlpha * (1.0 - alpha))
                   / max(combinedA, 0.0001);
return fixed4(combinedRGB, combinedA);
#endif
```

**Gap — shadow SDF recalculation cost:** The shadow pass calls `BushSDF` + `EdgeNoise`
a second time at the offset position. This doubles the SDF + edge noise cost
(2 Simplex calls extra). Total per fragment with shadow:
- Main body: SDF (0) + edge (2) + leaf (3) + lighting (4) + shadow SDF (0) + shadow
  edge (2) = **11 Simplex calls** (using LeafNoiseLite for lighting).
- Acceptable — PuffCloud shadow does the same (CloudNoise × 2 = 6 Simplex).

**Gap — shadow softness vs. AA width:** The shadow uses `_ShadowSoftness` for its edge
transition. The main body uses `_AAWidth`. Make sure `_ShadowSoftness > _AAWidth` so
the shadow is visibly softer than the body edge. Default values: `_ShadowSoftness = 0.04`,
`_AAWidth = 0.008`. **Good defaults — 5× softer.**

**Gap — shadow shape matching body shape:** The shadow SDF recomputes edge noise at
the offset position, so the shadow silhouette matches the noisy body edge (offset by
the shadow offset). This is correct — the shadow looks like the bush's cast shadow.
If we wanted a simpler shadow (smooth circle, no edge noise), we'd skip `EdgeNoise`
in the shadow pass. **Keep edge noise in shadow** for visual consistency.

##### 2.10 — Wind sway animation

Low-frequency noise displacement on the world-space sampling coordinate. Produces a
gentle swaying motion as if wind is rustling the leaves:

```hlsl
float2 WindDisplace(float2 wp, float t)
{
    float2 windP = wp * 0.5 + float2(t * _WindSpeed, t * _WindSpeed * 0.7);
    float windN = SimplexNoise2D(windP);
    return float2(windN, windN * 0.6) * _WindAmount;
}
```

Apply before all noise sampling:
```hlsl
float2 wpAnim = wp + WindDisplace(wp, t);
// Use wpAnim for EdgeNoise, LeafNoise, BushLighting
// Use wp (original) for BushSDF — the shape boundary stays fixed
```

**Key design choice:** wind displaces the *noise sampling coordinates* (leaf pattern
shifts), NOT the SDF boundary. The bush shape stays anchored to the grid; only the
surface texture moves. This looks like leaves rustling in place rather than the whole
bush sliding.

**Gap — wind on edge noise?** If wind displaces `wpAnim` and we use `wpAnim` for
`EdgeNoise`, the edge bumps will sway too — the silhouette wiggles slightly. This
could be a nice effect (bush edges rippling in wind) or distracting. **Control via a
toggle or by using `wp` (not `wpAnim`) for EdgeNoise.**

**Recommendation:** use `wp` for EdgeNoise (stable silhouette) and `wpAnim` for
LeafNoise (rustling interior). Let Phase 5 tuning decide if edge sway should be
added.

**Gap — 1 extra Simplex call for wind.** Total becomes: 11 + 1 = **12 Simplex calls**
per fragment (with shadow, without disturbance). Acceptable.

##### 2.11 — Disturbance field integration (`_DISTURBANCE_ON`)

When a projectile flies over the bush, the disturbance field is stamped. The shader
reads the global `_DisturbanceTex` and reacts — but differently from Puff:

**Bush disturbance expression (opaque — no density holes):**
1. **Leaf noise warp:** Disturbance displacement offsets the leaf noise sampling
   coordinate, making the leaf pattern visibly shift/warp at the stamp point.
2. **Edge boundary wobble:** Disturbance modulates `_EdgeNoiseAmount`, making the edge
   bumps more pronounced (bush edges shiver).
3. **No alpha/density reduction:** Unlike Puff clouds which dissolve under disturbance,
   Bush stays fully opaque — the shape doesn't change, only the surface reacts.

```hlsl
#ifdef _DISTURBANCE_ON
float2 fieldUV = (wp - _FieldBoundsMin) / _FieldBoundsSize;
float3 field = tex2D(_DisturbanceTex, fieldUV).rgb;
float2 displace = (field.gb - 0.5) * 2.0 * _DisplaceWorldScale;
float displaceLen = length(displace);
float disturbance = saturate(displaceLen / (_DisplaceWorldScale * 0.5 + 0.001));

// Warp leaf noise sampling
wpAnim += displace;

// Amplify edge noise near disturbance
float edgeAmplified = _EdgeNoiseAmount * (1.0 + disturbance * _EdgeDisturbanceScale);
d -= edgeN * edgeAmplified;  // replaces the normal edge noise application
#endif
```

**Gap — density channel (field.r):** PuffCloud uses `field.r` (density) to dissolve
the cloud. Bush ignores density — it's always opaque. But `field.r` is still being
written by `DisturbanceFieldService` for all stamps. Bush simply doesn't read it.
**No issue — just don't sample `density`.**

**Gap — disturbance on shadow:** Should the shadow react to disturbance? PuffCloud
applies `density` to the shadow. For Bush, since there's no density effect, the shadow
should probably wobble its edge with the main body. This means the shadow pass should
use the same amplified `edgeAmplified` value. **Apply `_EdgeDisturbanceScale` to the
shadow pass too.**

**Gap — `_FieldBoundsMin` / `_FieldBoundsSize` availability:** These are global shader
properties set by `DisturbanceFieldService`. They're available to any shader regardless
of material. The `_DISTURBANCE_ON` keyword gates the code path — when disabled, the
globals aren't sampled, so there's no dependency on `DisturbanceFieldService` being
active. **No issue.**

**Gap — disturbance + wind interaction:** Both wind and disturbance offset `wpAnim`.
Wind is gentle and continuous; disturbance is localized and transient. They stack
additively, which should look natural — wind rustles normally, then a projectile causes
a stronger local warp that settles back via the disturbance diffusion. **No issue.**

##### 2.12 — Create material asset

Create `Assets/Materials/Grid/Bush.mat`:
- Shader: `BalloonParty/Grid/Bush`
- Enable `_SHADOW_ON` (default)
- Enable `_DISTURBANCE_ON` (default)
- Leave `_CENTER_SHADOW_ON` off (optional, enable during tuning)
- Set colour palette: deep green `_BaseColor`, lighter green `_LeafVariationColor`
- All other properties at shader defaults

**This is a manual Unity step** — create the material asset in the editor and assign
the shader. Can't be done via code.

**Gap — material not assigned to anything yet.** The material lives as an asset but
won't be visible until Phase 3 creates the `Bush.prefab` with a `SpriteRenderer` that
references this material. **Phase 3 dependency — note only.**

---

#### Task Checklist

```
2.1  [ ] Shader file scaffold
         └── Properties block with all parameters (incl. _SminK, _AAWidth,
             _CenterShadowDarkness — caught in gap review)
         └── SubShader tags, blend mode, includes
         └── appdata_t / v2f structs, vertex shader
         └── MPB contract: _SlotCentersWorld, _SlotCount, _TimeOffset
         └── _RendererColor instancing boilerplate
         └── #define MAX_SLOTS 8 (bush clusters are small; saves register pressure)
         └── shader_feature pragmas: _SHADOW_ON, _CENTER_SHADOW_ON,
             _DISTURBANCE_ON, _LIGHTING_ON
         └── Structure frag() for early discard before expensive work
2.2  [ ] Per-slot circle SDF + smooth-minimum merging
         └── smin helper function
         └── BushSDF function with per-slot radius jitter
         └── Add _SminK property
2.3  [ ] Edge noise distortion
         └── EdgeNoise function (2-octave, per-slot seed offset)
         └── Modulate SDF distance by edge noise
2.4  [ ] Alpha clip at SDF boundary
         └── smoothstep AA transition
         └── Add _AAWidth property
         └── discard fully transparent pixels
2.5  [ ] Leaf noise colour modulation
         └── LeafNoise function (3-octave or 2-octave, world-space)
         └── LeafNoiseLite (1-octave, for lighting gradient)
         └── lerp _BaseColor ↔ _LeafVariationColor
2.6  [ ] Pseudo-lighting (_LIGHTING_ON keyword-guarded)
         └── Central differences on LeafNoiseLite → pseudo-normal
         └── Half-Lambert with _LightDir, _LightColor, _AmbientColor
         └── Guarded by #ifdef _LIGHTING_ON — flat fallback when off
2.7  [ ] Edge highlight rim
         └── smoothstep on abs(SDF distance)
         └── Blend with _LightColor at _RimIntensity
2.8  [ ] Centre shadow (_CENTER_SHADOW_ON)
         └── Per-slot center distance → darkening
         └── Add _CenterShadowDarkness property
2.9  [ ] Ground shadow (_SHADOW_ON)
         └── Offset SDF + edge noise at shadow position
         └── Shadow alpha with _ShadowSoftness
         └── Composite shadow behind main body
2.10 [ ] Wind sway animation
         └── WindDisplace function (1-octave, low freq)
         └── Apply to leaf noise coords, NOT SDF coords
2.11 [ ] Disturbance field integration (_DISTURBANCE_ON)
         └── Sample _DisturbanceTex globals
         └── Warp leaf noise coords with displacement
         └── Amplify edge noise with _EdgeDisturbanceScale
         └── Apply to shadow pass too
2.12 [ ] Create material asset
         └── Manual Unity step — set shader, enable keywords, set palette
```

#### Mobile Performance Analysis

This is a mobile game targeting 60 fps (16.6 ms frame budget). The GPU budget for
*all* procedural cluster rendering (Puff + Bush + disturbance blits) should be
**< 0.5 ms** (per the existing target in PLAN-FutureIdeas § 6.2). This section
audits the Bush shader's cost and proposes mitigations.

##### Reference numbers

| Parameter | Value |
|---|---|
| Grid size | 6 columns × 10 rows = 60 slots |
| Slot separation | (1.0, 0.85) world units |
| Max bush slots (typical) | 3–6 (low spawn weight, max count capped) |
| Max bush slots (worst case) | 16 (shader array cap) |
| Quad coverage at 3 slots | ~2.5 × 2.0 world units ≈ 5 world-space sq units |
| Quad coverage at 16 slots | ~7 × 9 world units ≈ 63 world-space sq units (full grid) |
| Screen resolution (target low-end) | ~750 × 1334 (iPhone 8 / budget Android) |
| Pixels per world unit (approx.) | ~120–150 px (depends on camera orthographic size) |
| Fragments per bush quad (3 slots) | ~90k pixels (before discard) |
| Fragments per bush quad (16 slots) | ~500k+ pixels (before discard — most of screen) |

##### Simplex noise cost per fragment

Each `SimplexNoise2D` call involves a permutation hash + 3 gradient evaluations +
3 dot products. On low-end mobile GPUs (Adreno 505, Mali-G52) this is roughly
**4–6 ALU cycles**. The cost scales linearly with call count.

**Bush shader call count breakdown (worst case — shadow ON, disturbance ON):**

| Stage | Simplex calls | Notes |
|---|---|---|
| BushSDF | 0 | Pure distance math, no noise |
| EdgeNoise (main body) | 2 | 2-octave |
| LeafNoise (colour) | 3 | 3-octave (or 2 if reduced) |
| LeafNoiseLite × 4 (lighting gradient) | 4 | 1-octave × 4 central differences |
| WindDisplace | 1 | 1-octave, low freq |
| EdgeNoise (shadow body) | 2 | Shadow pass recomputes at offset pos |
| **Total** | **12** | |

**PuffCloud comparison:** PuffCloud uses 3 (CloudNoise) + 4 (CloudLighting gradient)
= **7 Simplex calls** per fragment in its worst case (shadow + density ON).

**Bush is ~70% more expensive per fragment than Puff.** However, Bush quads are
typically smaller (3–6 slots vs. Puff's potentially larger clusters), so total
fragment throughput may be similar.

##### Cost estimates

| Scenario | Fragments | Simplex calls | Estimated GPU time (Adreno 505) |
|---|---|---|---|
| 3-slot bush, shadow ON | ~90k (40% discarded → ~54k shaded) | 54k × 12 = 648k | ~0.10–0.15 ms |
| 6-slot bush, shadow ON | ~160k (45% discarded → ~88k shaded) | 88k × 12 = 1.06M | ~0.15–0.25 ms |
| 16-slot bush (worst case) | ~500k (60% discarded → ~200k shaded) | 200k × 12 = 2.4M | ~0.35–0.50 ms |
| Puff (6 slots, density ON) | ~160k (50% discarded → ~80k shaded) | 80k × 7 = 560k | ~0.08–0.15 ms |
| **Bush + Puff combined** (typical) | ~142k shaded | ~1.6M calls | **~0.25–0.40 ms** |

The typical case (3–6 bush slots + Puff) fits within the 0.5 ms budget. The worst
case (16 bush slots) alone approaches the budget limit.

##### Overdraw concern — quad covers transparent area

Both PuffCloud and Bush render on a single SpriteRenderer quad sized to cover all
slot positions + padding. The quad is rectangular but the actual shape (cloud/bush)
is irregular. Fragments outside the shape are discarded, but the GPU still invokes
the fragment shader for them — on mobile GPUs without early-Z for transparent objects,
**every fragment in the quad runs the full shader before `discard`**.

For Bush, the SDF computation (`BushSDF` — 0 noise calls, just a distance loop) and
the `discard` happen early in the fragment shader, before the expensive LeafNoise
and lighting calls. Structuring the shader to **early-out before expensive work**
is critical:

```hlsl
// Compute SDF + edge noise first (cheap: 2 Simplex calls)
float d = BushSDF(wp, slotSeed);
float edgeN = EdgeNoise(wp, t, slotSeed);
d -= edgeN * _EdgeNoiseAmount;
float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, d);

// EARLY DISCARD — skip all expensive work for outside-shape fragments
if (alpha < 0.001) discard;

// Only shaded fragments reach here:
float leafN = LeafNoise(wp, t, slotSeed);
fixed3 lighting = BushLighting(wp, t, slotSeed);
// ...
```

**Gap — `discard` on mobile GPUs:** On tile-based architectures (all mobile GPUs —
Adreno, Mali, Apple), `discard` prevents early-Z optimization for the *entire draw
call*, not just the discarded fragments. This means the quad's pixels can't use
hidden-surface removal and must all be shaded. However, since we already use
`ZWrite Off` + `Blend SrcAlpha OneMinusSrcAlpha` (transparent pipeline), early-Z is
already disabled. The `discard` doesn't make things *worse* — it just prevents the
fragment from writing to the framebuffer. The real savings come from the early-out
branching *within* the shader.

**Gap — branching efficiency on mobile:** Mobile GPUs execute fragments in warps/waves
(typically 16–64 fragments). If *any* fragment in a warp is inside the shape, the
entire warp executes the full shader. The early `discard` only helps warps that are
*entirely* outside the shape (which is most of them in the padded quad corners). For
warps straddling the edge, all fragments pay the full cost regardless. This is
unavoidable with procedural SDF shapes.

##### Mitigation strategies

**M1 — Reduce LeafNoise to 2 octaves (save 1 Simplex/fragment):**
Drop the fine octave (4.37× frequency). At game scale (~0.4 world units per slot
radius × ~120 px/unit ≈ 48 pixels across), the fine detail is barely perceptible.
Saves ~54k–200k Simplex evaluations per frame.
**Total: 12 → 11 calls.** Small win.

**M2 — Skip lighting on low-end devices (save 4 Simplex/fragment):**
Add a `_LIGHTING_ON` shader keyword. On low-quality settings, disable it — the bush
renders as flat-shaded LeafNoise colour without the pseudo-normal gradient. This is
the single largest cost reduction.
**Total: 12 → 7 calls** (shadow ON) or **5 calls** (shadow OFF).

**M3 — Skip shadow on low-end (save 2 Simplex/fragment):**
`_SHADOW_ON` is already a keyword. Disabling it on low-quality settings removes the
shadow pass entirely. Combined with M2:
**Total: 5 calls.**

**M4 — Reduce MAX_SLOTS for bush (save loop iterations):**
Bush clusters are typically 1–4 slots. The SDF loop runs `_SlotCount` iterations, not
`MAX_SLOTS`, so the shader already adapts. But reducing `MAX_SLOTS` from 16 to 8 in
the Bush shader (separate from PuffCloud's 16) reduces register pressure and helps
the compiler optimize. The C# `ClusterView` already caps at 16 — the shader can
clamp independently.
```hlsl
#define MAX_SLOTS 8
```
If a bush cluster exceeds 8 slots, the extra slots are ignored — acceptable since the
spawner caps bush count anyway.

**M5 — Split shadow into a separate pass (reduce overdraw):**
Currently the shadow is composited in the same fragment shader as the body, meaning
every fragment computes *both* SDF evaluations (body + shadow). An alternative: render
the shadow in a separate pass with a simpler shader (SDF only, no leaf noise, no
lighting). This reduces fragment cost for shadow-only pixels but adds a second draw
call.

On mobile, **draw calls are expensive** (CPU-side state changes). A second pass for
each bush cluster adds 1 draw call per cluster. With 1–3 bush clusters, this is 1–3
extra draw calls — negligible on modern mobile. But the current single-pass approach
is already the same pattern PuffCloud uses successfully, so only split if profiling
shows the shadow path is the bottleneck.

**Recommendation:** don't split — keep single pass like PuffCloud.

**M6 — LOD: distance-based simplification:**
Not applicable — the game is a fixed-camera 2D game with no zoom. All bushes are
always at the same screen scale.

##### Recommended quality tiers

| Setting | High (default) | Low |
|---|---|---|
| LeafNoise octaves | 3 | 2 |
| `_LIGHTING_ON` | ON | OFF |
| `_SHADOW_ON` | ON | OFF |
| `_DISTURBANCE_ON` | ON | ON (cheap — just a tex2D + add) |
| `_CENTER_SHADOW_ON` | OFF | OFF |
| Simplex calls/frag | 12 | 5 |
| Estimated cost (3 slots) | ~0.15 ms | ~0.05 ms |

Tier selection can be driven by `QualitySettings.GetQualityLevel()` or a device
capability check at startup. This requires the C# side to toggle material keywords —
add a `MaterialKeywordSetter` or do it in `BushView.OnConfigured()`.

**Gap — no quality tier system exists yet.** The project has `FrameRateSettings` but
no GPU quality tier. Implementing per-shader quality keywords is a small addition:
```csharp
if (QualitySettings.GetQualityLevel() == 0)
{
    material.DisableKeyword("_LIGHTING_ON");
    material.DisableKeyword("_SHADOW_ON");
}
```
This is a Phase 5 task — for Phase 2, ship with high quality defaults and profile.

##### Summary

| Concern | Risk | Status |
|---|---|---|
| Simplex calls per fragment (12) | Medium | Acceptable for typical 3–6 slot clusters; mitigate with M2/M3 on low-end |
| Overdraw on transparent quad | Medium | Early discard before expensive work; unavoidable on TBDR without opaque Z |
| 16-slot worst case approaching 0.5 ms | Low | Spawner caps bush count; unlikely in practice |
| Combined Bush + Puff cost | Low | ~0.25–0.40 ms typical; within 0.5 ms budget |
| No quality tier system | Medium | Ship high quality; add keyword toggle in Phase 5 if profiling demands it |
| SDF loop (up to 16 iterations) | Low | Reduce MAX_SLOTS to 8 for bush; loop is ALU-only (no texture/noise) |

**Bottom line:** The Bush shader is ~70% more expensive per fragment than PuffCloud,
but bush clusters are smaller and fewer. The combined Puff + Bush cost stays within
the 0.5 ms GPU budget for typical configurations. The main insurance policy is the
`_LIGHTING_ON` keyword (M2) — toggling it off saves 4 Simplex calls and cuts the
cost nearly in half. Ship with all features ON, profile on target devices in Phase 5,
and toggle keywords if needed.

---

#### Consolidated Gap Summary — Phase 2

| # | Gap | Risk | Recommendation |
|---|---|---|---|
| S1 | Render order Bush vs Puff — both use Transparent queue | Low | Control via `SortingOrderOffset` in Phase 3 config |
| S2 | `_SminK` property missing from initial Properties block | Low | Add to Properties in task 2.1 |
| S3 | Seed discontinuity at smin merge boundary | Low | Accept — seed drives subtle noise offset; test in Phase 5 |
| S4 | Edge noise seam at merge region (different seeds) | Low | Accept — smin blend region is narrow; revisit if visible |
| S5 | `_AAWidth` property missing from initial Properties block | Low | Add to Properties in task 2.1 |
| S6 | `_CenterShadowDarkness` property missing from initial Properties block | Low | Add to Properties in task 2.1 |
| S7 | 17 Simplex calls per fragment (3-octave LeafNoise + lighting gradient) | Medium | Use LeafNoiseLite (1-octave) for lighting gradient — reduces to 12 |
| S8 | Wind on edge noise — should silhouette sway? | Low | Use static `wp` for edge noise; test edge sway in Phase 5 |
| S9 | Shadow edge should react to disturbance wobble | Low | Apply `_EdgeDisturbanceScale` in shadow pass too |
| S10 | Material asset not used until Phase 3 prefab exists | None | Expected — note only |
| S11 | Shared lighting code duplicated from PuffCloud | Low | Accept for now; extract to .cginc if a third cluster type appears |
| S12 | LeafNoise octave count may be invisible at game scale | Low | Test 2 vs 3 octaves in Phase 5; default to 3, fallback to 2 |
| S13 | No quality tier system exists — can't toggle keywords per device | Medium | Ship high quality; add keyword toggle in Phase 5 if profiling demands |
| S14 | `_LIGHTING_ON` keyword not in task 2.1 Properties/pragmas | Low | Add `shader_feature _LIGHTING_ON`; guard lighting code path |
| S15 | `discard` on TBDR — early-out branching saves cost, but warp straddling limits benefit | Medium | Structure shader to discard before expensive work (SDF + edge = 2 calls before discard) |
| S16 | MAX_SLOTS 16 wastes register pressure for bush | Low | Use `#define MAX_SLOTS 8` in Bush.shader |
| S17 | Combined Puff + Bush approaching 0.5 ms budget on low-end | Medium | M2 + M3 (disable lighting + shadow) brings bush to 5 calls — safe fallback |

**Exit criteria:** Shader renders a visually distinct, cartoony top-down bush in the
scene view with edit-mode animation. Clearly differentiated from Puff at a glance.

---

#### Phase 2 — Quick Reference (Session Recovery)

> Read this section cold to resume implementation. Everything below is resolved —
> no decisions remain.

**File:** `Assets/Shaders/BalloonParty/Grid/Bush.shader`

**Reference shader:** `Assets/Shaders/BalloonParty/Grid/PuffCloud.shader` (328 lines)
— copy its scaffold (SubShader tags, blend mode, instancing block, vertex shader,
`MAX_SLOTS`, `_SlotCentersWorld`/`_SlotCount`/`_TimeOffset` MPB contract) then
replace the fragment logic.

**Include:** `Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc`
— `float SimplexNoise2D(float2 p)` returns `[-1, 1]`.

**Render config:** `Queue=Transparent`, `Blend SrcAlpha OneMinusSrcAlpha`,
`ZWrite Off`, `Cull Off`. Same as PuffCloud.

**Keywords (4 `shader_feature` pragmas):**
`_SHADOW_ON`, `_CENTER_SHADOW_ON`, `_DISTURBANCE_ON`, `_LIGHTING_ON`

**`#define MAX_SLOTS 8`** (not 16 — bush clusters are small).

**Disturbance globals** (set by `DisturbanceFieldService`, NOT in Properties):
`sampler2D _DisturbanceTex`, `float2 _FieldBoundsMin`, `float2 _FieldBoundsSize`

##### Properties (all in one block)

```
// Shape
_SlotRadius          Float    0.40
_RadiusJitter        Range    0–0.15       0.06
_EdgeNoiseFreq       Float    4.0
_EdgeNoiseAmount     Range    0–0.2        0.08
_SminK               Range    0.05–0.5     0.20
_AAWidth             Range    0.001–0.03   0.008

// Surface
_BaseColor           Color    (0.18, 0.45, 0.12, 1)
_LeafVariationColor  Color    (0.25, 0.55, 0.15, 1)
_LeafNoiseFreq       Float    6.0

// Lighting
_LightDir            Vector   (-0.4, 0.7, 0, 0)
_LightColor          Color    (1, 1, 0.9, 1)
_AmbientColor        Color    (0.08, 0.22, 0.05, 1)
_LightIntensity      Range    0–1          0.50
_NormalStrength      Range    0–3          1.5
_NormalEpsilon       Range    0.001–0.05   0.015

// Rim
_RimWidth            Range    0–0.15       0.04
_RimIntensity        Range    0–1          0.35

// Wind
_WindSpeed           Range    0–2          0.4
_WindAmount          Range    0–0.1        0.02

// Animation
_TimeOffset          Float    0.0

// Shadow (_SHADOW_ON)
_ShadowColor         Color    (0.04, 0.04, 0.08, 0.45)
_ShadowOffsetX       Range    -0.15–0.15   0.03
_ShadowOffsetY       Range    -0.15–0.15  -0.04
_ShadowSoftness      Range    0–0.10       0.04

// Center Shadow (_CENTER_SHADOW_ON)
_CenterShadowDarkness Range   0.3–1.0      0.75

// Disturbance (_DISTURBANCE_ON)
_DisplaceWorldScale   Range   0–2          0.3
_EdgeDisturbanceScale Range   0–3          1.5
```

##### Fragment shader data flow

```
wp = worldPos (from vertex)
t  = _TimeOffset

1. WIND        wpAnim = wp + WindDisplace(wp, t)           ← 1 Simplex
2. SDF         d = BushSDF(wp, slotSeed)                   ← 0 Simplex (pure math)
3. EDGE NOISE  edgeN = EdgeNoise(wp, t, slotSeed)          ← 2 Simplex (static wp, not wpAnim)
               d -= edgeN * _EdgeNoiseAmount
4. DISTURBANCE if _DISTURBANCE_ON:
                 fieldUV = (wp - _FieldBoundsMin) / _FieldBoundsSize
                 displace = (tex2D(_DisturbanceTex, fieldUV).gb - 0.5) * 2 * _DisplaceWorldScale
                 disturbance = saturate(length(displace) / ...)
                 wpAnim += displace                         ← warp leaf noise
                 d -= edgeN * (_EdgeNoiseAmount * disturbance * _EdgeDisturbanceScale)  ← edge wobble
5. CLIP        alpha = 1 - smoothstep(-_AAWidth, 0, d)
               if alpha < 0.001: discard                   ← EARLY OUT before expensive work
6. LEAF COLOR  leafN = LeafNoise(wpAnim, t, slotSeed)      ← 3 Simplex (or 2 on low)
               color = lerp(_BaseColor, _LeafVariationColor, leafN)
7. LIGHTING    if _LIGHTING_ON:
                 lit = BushLighting(wpAnim, t, slotSeed)   ← 4× LeafNoiseLite = 4 Simplex
                 color *= lit
8. RIM         rim = smoothstep(_RimWidth, 0, abs(d))
               color = lerp(color, _LightColor, rim * _RimIntensity)
9. CENTER      if _CENTER_SHADOW_ON:
                 centerFade = per-slot center distance
                 color *= lerp(1, _CenterShadowDarkness, centerFade)
10. SHADOW     if _SHADOW_ON:
                 shadowD = BushSDF(wp - shadowOffset) + EdgeNoise  ← 2 Simplex
                 composite shadow behind body
11. OUTPUT     return fixed4(color, alpha)
```

**Total Simplex calls: 12** (shadow ON, lighting ON, disturbance ON, 3-octave leaf).
**Early discard at step 5** — fragments outside the SDF skip steps 6–10.

##### Key functions (signatures)

```hlsl
float smin(float a, float b, float k)
// Polynomial smooth-min. k = _SminK.

float BushSDF(float2 wp, out float slotSeed)
// Loop _SlotCount slots. Per-slot: hash → radius jitter, distance - radius.
// smin across all. Track nearest seed via raw distance.

float EdgeNoise(float2 wp, float t, float slotSeed)
// 2-octave Simplex. seedOffset = slotSeed * (61.7, 37.3). Returns [-1,1].

float2 WindDisplace(float2 wp, float t)
// 1-octave Simplex at 0.5× freq. Returns displacement * _WindAmount.

float LeafNoise(float2 wp, float t, float slotSeed)
// 3-octave Simplex (or 2). seedOffset = slotSeed * (91.3, 53.7). Returns [0,1].

float LeafNoiseLite(float2 wp, float t, float slotSeed)
// 1-octave only. Same seed. For lighting gradient only.

fixed3 BushLighting(float2 wp, float t, float slotSeed)
// Central differences on LeafNoiseLite → pseudo-normal → half-Lambert.
// Same pattern as PuffCloud's CloudLighting (lines 200-221 of PuffCloud.shader).
```

##### PuffCloud.shader line references (copy from)

| What | Lines |
|---|---|
| SubShader tags + blend | 61–75 |
| `#define MAX_SLOTS`, appdata, v2f | 88–103 |
| Instancing block (`_RendererColor`) | 105–112 |
| `_SlotCentersWorld` / `_SlotCount` | 147–148 |
| `SlotFalloff` (→ adapt to `BushSDF`) | 179–193 |
| `CloudLighting` (→ adapt to `BushLighting`) | 200–221 |
| Vertex shader | 223–235 |
| Disturbance sampling | 245–253 |
| Shadow composite | 279–312 |

##### Performance budget

- Target: **< 0.5 ms combined** Puff + Bush on mobile
- Typical (3–6 bush slots): **~0.15–0.25 ms**
- Worst (16 slots): **~0.35–0.50 ms**
- Fallback: disable `_LIGHTING_ON` → 12 → 7 calls; disable `_SHADOW_ON` → 7 → 5

##### Material asset

`Assets/Materials/Grid/Bush.mat` — manual Unity step.
Shader: `BalloonParty/Grid/Bush`. Enable `_SHADOW_ON` + `_DISTURBANCE_ON` +
`_LIGHTING_ON`. Leave `_CENTER_SHADOW_ON` off. Set green palette.

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

