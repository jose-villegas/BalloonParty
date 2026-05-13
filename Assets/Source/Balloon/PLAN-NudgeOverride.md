# NudgeOverride System — Implementation Plan

## Goal

Replace the current flat `NudgeDistanceOverride`/`NudgeDurationOverride` fields on the balloon model with a reusable, list-driven `NudgeOverride[]` system. Each override entry targets one or more nudge sources via a `[Flags]` enum, enabling fine-grained per-source nudge tuning directly from the inspector. The same type is reused in `ItemSettings` for the bomb, replacing its flat `_bombNudgeDistance`.

---

## Core Types

### `NudgeType` (new enum)

```csharp
[Flags]
public enum NudgeType
{
    None     = 0,
    Deflect  = 1 << 0,   // projectile hits this balloon
    Neighbor = 1 << 1,   // chain nudge from neighboring pop/bomb
    All      = Deflect | Neighbor
}
```

Lives in `Shared/` — shared across balloon and item systems.

---

### `NudgeOverride` (new serializable class)

```csharp
[Serializable]
public class NudgeOverride
{
    [SerializeField] private NudgeType _appliesTo;
    [SerializeField] private float _distance;
    [SerializeField] private float _duration;

    public NudgeType AppliesTo => _appliesTo;
    public float Distance => _distance;
    public float Duration => _duration;
}
```

Lives in `Shared/` alongside `NudgeType`. Reused by both `BalloonPrefabEntry` and `ItemSettings`.

---

### `NudgeOverrideResolver` (new static helper)

```csharp
public static class NudgeOverrideResolver
{
    /// <summary>
    /// Finds the first override whose AppliesTo flags include the given source,
    /// then falls through to the per-message override, then the global config default.
    /// </summary>
    public static float ResolveDistance(
        NudgeOverride[] overrides,
        NudgeType source,
        float? messageOverride,
        float configDefault)
    {
        var entry = overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
        return entry != null ? entry.Distance : messageOverride ?? configDefault;
    }

    public static float ResolveDuration(
        NudgeOverride[] overrides,
        NudgeType source,
        float? messageOverride,
        float configDefault)
    {
        var entry = overrides?.FirstOrDefault(o => o.AppliesTo.HasFlag(source));
        return entry != null ? entry.Duration : messageOverride ?? configDefault;
    }
}
```

Lives in `Shared/`. Both `BalloonView` and `BombItemHandler` call this.

---

### `NudgeOverrideDrawer` (new PropertyDrawer)

A `PropertyDrawer` for `NudgeOverride` showing:
- `AppliesTo` as an `EditorGUI.EnumFlagsField`
- `Distance` float
- `Duration` float

Works correctly inside collections (same approach as `BalloonPrefabEntryDrawer`).

Lives in `Configuration/Editor/` (or `Shared/Editor/`).

---

## `BalloonNudgeMessage` — add `NudgeType Source`

```csharp
public readonly struct BalloonNudgeMessage
{
    public readonly IBalloonModel Balloon;
    public readonly Vector3 HitSlotPosition;
    public readonly NudgeType Source;       // ← new: Deflect or Neighbor
    public readonly float? NudgeDistance;   // per-message override (e.g. bomb proximity attenuation)
    public readonly float? NudgeDuration;
}
```

All publishers must pass a `Source`:
- `BalloonController.Deflect()` → `Source = NudgeType.Deflect`
- `BalloonNudgeHandler` → `Source = NudgeType.Neighbor`
- `BombItemHandler` → `Source = NudgeType.Neighbor`

---

## `BalloonPrefabEntry` — replace flat nudge fields with list

**Before:**
```csharp
[SerializeField] private bool _overrideNudge;
[SerializeField] private float _nudgeDistanceOverride;
[SerializeField] private float _nudgeDurationOverride;
```

**After:**
```csharp
[SerializeField] private NudgeOverride[] _nudgeOverrides;

public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
```

Empty list = no overrides, use global defaults. Each entry in the list covers one or more sources. The PropertyDrawer renders each element via `NudgeOverrideDrawer` — the existing `BalloonPrefabEntryDrawer` just draws the list field normally; the element drawer handles the rest.

---

## Balloon Model — replace two `float?` with `NudgeOverride[]`

### `IBalloonModel`

```csharp
// Remove:
float? NudgeDistanceOverride { get; }
float? NudgeDurationOverride { get; }

// Add:
NudgeOverride[] NudgeOverrides { get; }
```

### `IWriteableBalloonModel`

```csharp
// Remove:
new float? NudgeDistanceOverride { get; set; }
new float? NudgeDurationOverride { get; set; }

// Add:
new NudgeOverride[] NudgeOverrides { get; set; }
```

### `BalloonModel`

```csharp
// Remove two float? auto-properties
// Add:
public NudgeOverride[] NudgeOverrides { get; set; }
```

---

## `BalloonController` — set overrides on model, publish with Source

```csharp
// Start():
_model.NudgeOverrides = _nudgeOverrides;   // passed in constructor, from BalloonPrefabEntry

// Deflect():
_nudgePublisher.Publish(new BalloonNudgeMessage(
    _model,
    balloonWorldPos - msg.ProjectileDirection.normalized,
    NudgeType.Deflect));   // ← Source = Deflect, no per-message distance override needed
```

Constructor: replace `float? nudgeDistanceOverride, float? nudgeDurationOverride` with `NudgeOverride[] nudgeOverrides`.

---

## `BalloonSpawner`

Pass `entry.NudgeOverrides` to `BalloonController` instead of the two nullable floats.

---

## `BalloonView.OnNudge` — use resolver

```csharp
var nudgeDistance = NudgeOverrideResolver.ResolveDistance(
    Model.NudgeOverrides, msg.Source, msg.NudgeDistance, _balloonsConfig.NudgeDistance);

var nudgeDuration = NudgeOverrideResolver.ResolveDuration(
    Model.NudgeOverrides, msg.Source, msg.NudgeDuration, _balloonsConfig.NudgeDuration);
```

---

## `BalloonNudgeHandler` — publish with Source = Neighbor

The handler currently publishes `BalloonNudgeMessage` for every neighbor of the hit balloon. Add `Source = NudgeType.Neighbor` to all publishes.

---

## `ItemSettings` / `BombItemHandler` — replace flat field with list

### `ItemSettings`

```csharp
// Remove:
[SerializeField] private float _bombNudgeDistance = 0.15f;
public float BombNudgeDistance => _bombNudgeDistance;

// Add:
[SerializeField] private NudgeOverride[] _nudgeOverrides;
public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
```

The bomb's default entry:
```
AppliesTo: Neighbor
Distance:  0.15
Duration:  (global default)
```

### `BombItemHandler`

```csharp
// Instead of:
var nudgeDistance = settings.BombNudgeDistance;

// Use:
var nudgeDistance = NudgeOverrideResolver.ResolveDistance(
    settings.NudgeOverrides, NudgeType.Neighbor, null, _balloonsConfig.NudgeDistance);
var nudgeDuration = NudgeOverrideResolver.ResolveDuration(
    settings.NudgeOverrides, NudgeType.Neighbor, null, _balloonsConfig.NudgeDuration);
```

Publish `BalloonNudgeMessage` with `Source = NudgeType.Neighbor`.

---

## Files Changed — Summary

| # | File | Change |
|---|---|---|
| 1 | `Shared/NudgeType.cs` | **New** — `[Flags]` enum: `None, Deflect, Neighbor, All` |
| 2 | `Shared/NudgeOverride.cs` | **New** — `[Serializable]` class: `AppliesTo`, `Distance`, `Duration` |
| 3 | `Shared/NudgeOverrideResolver.cs` | **New** — static helper: `ResolveDistance`, `ResolveDuration` |
| 4 | `Configuration/Editor/NudgeOverrideDrawer.cs` | **New** — PropertyDrawer: `EnumFlagsField` + two floats |
| 5 | `Shared/Messages/BalloonNudgeMessage.cs` | Add `NudgeType Source` field + constructor param |
| 6 | `Configuration/BalloonPrefabEntry.cs` | Replace `_overrideNudge` + two floats with `NudgeOverride[]` |
| 7 | `Configuration/Editor/BalloonPrefabEntryDrawer.cs` | Remove old nudge rows; draw `_nudgeOverrides` list field normally |
| 8 | `Balloon/Model/IBalloonModel.cs` | Replace two `float?` with `NudgeOverride[]` |
| 9 | `Balloon/Model/IWriteableBalloonModel.cs` | Replace two `float?` with `NudgeOverride[]` |
| 10 | `Balloon/Model/BalloonModel.cs` | Replace two `float?` auto-properties with `NudgeOverride[]` |
| 11 | `Balloon/Controller/BalloonController.cs` | Replace two `float?` fields with `NudgeOverride[]`; set on model in `Start()`; publish `Deflect` with `Source = Deflect` |
| 12 | `Balloon/Spawner/BalloonSpawner.cs` | Pass `entry.NudgeOverrides` to controller |
| 13 | `Balloon/View/BalloonView.cs` | Replace manual override lookup with `NudgeOverrideResolver` |
| 14 | `Balloon/Controller/BalloonNudgeHandler.cs` | Publish `BalloonNudgeMessage` with `Source = Neighbor` |
| 15 | `Configuration/ItemSettings.cs` | Replace `_bombNudgeDistance` with `NudgeOverride[]` |
| 16 | `Item/Bomb/BombItemHandler.cs` | Use `NudgeOverrideResolver`; publish with `Source = Neighbor` |

---

## Inspector UX

### `BalloonsConfiguration._entries` — tough balloon entry

```
▶ Element 1  (Tough balloon)
    Prefab            [ToughBalloon]
    Weight            0.3
    Max Count         2
    Nudge Overrides   [size: 1]
      ▶ Element 0
          Applies To   Deflect
          Distance     0.5
          Duration     0.08
    Override Pop VFX  [✓]
      VFX Prefab       [ToughPopVFX]
```

### `ItemConfiguration` — bomb settings

```
Nudge Overrides   [size: 1]
  ▶ Element 0
      Applies To   Neighbor
      Distance     0.15
      Duration     (0 = global default)
```

---

## Resolution Priority (unchanged concept)

```
NudgeOverrides.FirstOrDefault(o => o.AppliesTo.HasFlag(source))?.Distance
  ?? msg.NudgeDistance        ← per-message override (proximity attenuation)
  ?? _balloonsConfig.NudgeDistance  ← global default
```

