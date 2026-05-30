@page plan_bubble_cluster_hit_feedback Bubble Cluster Hit Feedback

# Bubble Cluster Hit Feedback

> Two visual improvements for the Soap Cluster + a structural cleanup of how balloon
> view components communicate hit and pop context.
>
> **No changes until this plan is actioned.**

---

## Goals

1. **VFX position** — passthrough and pop VFX spawn at the position of the bubble that
   was actually hit, not the cluster centre.
2. **Spin impulse** — a projectile passing through imparts a torque to the cluster's
   rotation based on where it crossed relative to the centre.
3. **Structural cleanup** — replace the `ITransformCapture` / `ItemDisplayService._activeCapture`
   workaround with a unified, well-timed mechanism that also serves goals 1 and 2.

---

## Root cause of the existing laser rotation bug

`ItemDisplayService._activeCapture` was introduced because `BalloonView` tried to cache
`ITransformCapture` at `Bind()` time — before `ItemAssigner` had ever parented the item
visual. The cached value was always `null`, so `TransformCapturedMessage` was never
published, and `LaserItemHandler` always used `default(Quaternion)`.

The fix was to move caching to `ItemDisplayService.OnItemChanged()`. This works but is
a band-aid: `BalloonView` still exposes a `TransformCapture` property that delegates
to the service, and the service holds a separate `ITransformCapture` reference
alongside its item visual reference.

The plan below removes the root cause entirely.

---

## New interface — `IBalloonHitHandler`

Single interface, two lifecycle moments, owned by `Balloon/View/`:

```csharp
namespace BalloonParty.Balloon.View
{
    /// <summary>
    /// Implement on balloon prefab components (or item visual prefab components) that
    /// need to react to hit events or export state at pop time.
    /// Discovered by <see cref="BalloonView"/> — never referenced directly by name.
    /// </summary>
    public interface IBalloonHitHandler
    {
        // Called on every hit outcome (PassThrough, Deflect, etc.).
        // Return a non-null position to redirect where the hit VFX spawns.
        // Return null to use the default (balloon transform.position).
        Vector3? GetVfxWorldPosition(HitOutcome outcome, int hitsRemainingAfterHit);

        // React to the hit — spin impulse, internal state changes, etc.
        void OnHit(HitOutcome outcome, int hitsRemainingAfterHit, Vector3 projectileDir);

        // Called once, immediately before the balloon leaves the grid.
        // Return a snapshot if this component needs to export transform state
        // (e.g. LaserItemRotation capturing its angle). Return null otherwise.
        TransformSnapshot? OnPrePop();
    }
}
```

Implementors return `null` from any method they don't use.

---

## Discovery and caching

### Root components — cache at `Bind()` time

`SoapBubbleClusterVariant` lives on the balloon prefab root and is always present.
`BalloonView.Bind()` collects `IBalloonHitHandler[]` via `GetComponentsInChildren` once.

### Item visual components — register via `ItemDisplayService`

`LaserItemRotation` is parented **after** `Bind()` fires. `ItemDisplayService` already
owns that lifecycle:

```
ItemDisplayService.OnItemChanged()
  → spawn and parent item visual as today
  → _activeHandler = _activeView.GetComponentInChildren<IBalloonHitHandler>()
  → _balloonView.RegisterHitHandler(_activeHandler)   ← new

ItemDisplayService.ReturnActiveVisual()
  → _balloonView.UnregisterHitHandler(_activeHandler)  ← new, symmetric
  → _activeHandler = null
  → return visual to pool as today
```

`BalloonView` holds a `List<IBalloonHitHandler>`. `Bind()` populates static handlers;
`ItemDisplayService` adds/removes dynamic ones. No scanning at runtime, no GC at hit time.

### What goes away

| Removed | Replaced by |
|---|---|
| `ITransformCapture` interface | `IBalloonHitHandler.OnPrePop()` |
| `TransformSnapshot.CaptureSnapshot()` on `LaserItemRotation` | `OnPrePop()` on same class |
| `ItemDisplayService._activeCapture` field | `ItemDisplayService._activeHandler` |
| `ItemDisplayService.TransformCapture` property | Removed — `BalloonView` manages the list |
| `BalloonView.TransformCapture` property | Removed |

---

## `BalloonView` changes

### New methods

```csharp
// Called by BalloonController for every hit outcome.
// Dispatches to handlers, resolves VFX position, plays VFX.
public void OnActorHit(HitOutcome outcome, int hitsRemainingAfterHit, Vector3 projectileDir)

// Called by BalloonController just before Pop() removes from grid.
// Returns the first non-null snapshot from registered handlers.
public TransformSnapshot? PrePop()
```

### Removed

- `TransformCapture` property

### Internal

```csharp
private readonly List<IBalloonHitHandler> _hitHandlers = new();

internal void RegisterHitHandler(IBalloonHitHandler handler)   // called by ItemDisplayService
internal void UnregisterHitHandler(IBalloonHitHandler handler) // called by ItemDisplayService
```

VFX resolution in `OnActorHit`:
```
foreach handler → handler.OnHit(outcome, hits, dir)
pos = first non-null from handler.GetVfxWorldPosition(outcome, hits) ?? transform.position
PlayHitVfxAtPosition(outcome, pos)   ← existing pool/color logic, just uses resolved pos
```

---

## `BalloonController` changes

```csharp
// PassThrough case — was:
_view.PlayHitVfxForOutcome(HitOutcome.PassThrough);
// becomes:
_view.OnActorHit(HitOutcome.PassThrough, _model.HitsRemaining.Value, msg.ProjectileDirection);

// Pop() — was:
_view.PlayHitVfxForOutcome(HitOutcome.Pop);
var snapshot = _view.TransformCapture?.CaptureSnapshot();
// becomes:
var snapshot = _view.PrePop();   // handlers notified; snapshot returned if any
_view.OnActorHit(HitOutcome.Pop, 0, msg.ProjectileDirection);
```

`msg.ProjectileDirection` is already in `ActorHitMessage` — no new data needed.
`_model.HitsRemaining.Value` is the post-hit value, which is what the math requires.

---

## `SoapBubbleClusterVariant` — implements `IBalloonHitHandler`

### C# mirror of shader bubble layouts

The shader `kLayout1..kLayout5` arrays are pure constants. Mirror them in C#:

```csharp
private static readonly Vector2[][] BubbleLayouts =
{
    new[] { Vector2.zero },                                        // count 1 — single
    new[] { new Vector2(-0.110f, 0f), new Vector2(0.110f, 0f) }, // count 2 — pair
    new[] { new Vector2(-0.110f,-0.063f), new Vector2(0.110f,-0.063f),
            new Vector2( 0.000f, 0.127f) },                       // count 3 — triangle
    new[] { new Vector2(-0.110f,-0.110f), new Vector2(0.110f,-0.110f),
            new Vector2(-0.110f, 0.110f), new Vector2(0.110f, 0.110f) }, // count 4 — square
    new[] { new Vector2( 0.000f, 0.187f), new Vector2( 0.178f, 0.058f),
            new Vector2( 0.110f,-0.151f), new Vector2(-0.110f,-0.151f),
            new Vector2(-0.178f, 0.058f) },                       // count 5 — pentagon
};
```

These never change independently of the shader — any layout edit touches both files.

### `GetVfxWorldPosition`

```
oldCount = hitsRemainingAfterHit + 1        // layout that was displayed during the hit
removedIndex = hitsRemainingAfterHit        // bubble index that was removed

layoutPos = BubbleLayouts[oldCount - 1][removedIndex]

rotated = rotate(layoutPos, _rotationAngle)

worldScale = _renderer.bounds.size.x        // world width of the rendered quad
return transform.position + (Vector3)(rotated * worldScale)
```

**Approximation accuracy:** breathe offset `±0.007 wu` and micro-float `±0.018 wu` are
both below bubble radius `≈ 0.15 wu`. Canonical layout + current rotation is sufficient
for VFX placement.

### `OnHit` — spin impulse

```
offset = hitWorldPos - transform.position          // lever arm
torqueZ = offset.x * dir.y − offset.y * dir.x     // 2D cross product z-component
_rotationSpeedRad += torqueZ * _spinImpulseStrength
```

`_spinImpulseStrength` is a `[SerializeField]` tunable (start around `0.5`). Optionally
add a damping term so the speed decays back toward the base value over time.

`hitWorldPos` is `GetVfxWorldPosition(outcome, hitsRemainingAfterHit) ?? transform.position`
— computed before `OnHit` is called so `OnHit` receives the resolved position.

> **Note:** `BalloonController` should call `GetVfxWorldPosition` once and pass the
> resolved position to `OnHit` rather than recomputing it. The method order inside
> `BalloonView.OnActorHit` handles this naturally.

### `OnPrePop`

```csharp
public TransformSnapshot? OnPrePop() => null; // cluster has no state to export
```

---

## `LaserItemRotation` — migrates to `IBalloonHitHandler`

```csharp
public class LaserItemRotation : MonoBehaviour, IBalloonHitHandler
{
    // existing rotation logic unchanged

    public Vector3? GetVfxWorldPosition(HitOutcome outcome, int hitsRemainingAfterHit)
        => null;  // laser balloon is always a single standard balloon

    public void OnHit(HitOutcome outcome, int hitsRemainingAfterHit, Vector3 projectileDir)
    {
        // nothing — laser rotation is autonomous
    }

    public TransformSnapshot? OnPrePop()
    {
        _stopped = true;
        return new TransformSnapshot(transform);  // same as old CaptureSnapshot()
    }
}
```

`ITransformCapture` and `CaptureSnapshot()` are deleted from this class.

---

## File summary

| File | Change |
|---|---|
| **New** `Balloon/View/IBalloonHitHandler.cs` | New interface |
| `Balloon/View/BalloonView.cs` | `OnActorHit`, `PrePop`, `Register/UnregisterHitHandler`; remove `TransformCapture` |
| `Balloon/Controller/BalloonController.cs` | Call `OnActorHit` + `PrePop` instead of `PlayHitVfxForOutcome` + `TransformCapture` |
| `Balloon/Type/SoapBubbleClusterVariant.cs` | Implement `IBalloonHitHandler`; add layout mirror, spin impulse, bubble position |
| `Item/ItemDisplayService.cs` | Add `_activeHandler`; call `Register/Unregister`; remove `_activeCapture` + `TransformCapture` |
| `Item/LaserItemRotation.cs` | Implement `IBalloonHitHandler`; remove `ITransformCapture` |
| `Item/ITransformCapture.cs` | **Delete** — `TransformSnapshot` struct moves to `Balloon/View/` or stays in `Item/` as a plain struct |

---

## Open questions

1. **`TransformSnapshot` location** — currently in `Item/ITransformCapture.cs`. After
   deleting `ITransformCapture`, does it move to `Balloon/View/`, stay in `Item/`, or
   move to `Shared/`? It is referenced by `TransformCapturedMessage` (in `Shared/Messages/`)
   and `LaserItemHandler` (in `Item/Laser/`). Staying in `Item/` as a standalone struct
   is simplest.

2. **Spin damping** — after the impulse, should `_rotationSpeedRad` decay back toward
   the sign-preserved base speed, or just decay to zero? Natural-feel: decay toward
   base (preserves the original slow drift direction). Implement with a configurable
   `_spinDamping` field.

3. **Hit VFX for cluster vs default pop VFX** — currently `PlayHitVfxForOutcome` for
   `Pop` falls back to `BalloonsConfiguration.DefaultPopVfxPrefab` if no `HitVfxOverride`
   is set. With the new signature the resolved position feeds into that same fallback
   path — no change needed there.

