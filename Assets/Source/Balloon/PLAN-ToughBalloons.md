# Tough / Unbreakable Balloons — Implementation Plan

## Goal

Add a balloon durability mechanic: some balloons require multiple projectile hits before popping, and some are completely unbreakable. When the projectile hits a tough balloon without popping it, the projectile **reflects** off the balloon's surface and the balloon plays a **pushback nudge** animation.

As part of this feature, **redesign the balloon configuration system** from color-centric to type-centric, introducing a proper type hierarchy that supports future balloon variants beyond simple colored balloons.

---

## Architecture Redesign: Balloon Types

### Current state (color = type)

Today each balloon is defined by a `BalloonColorConfiguration` entry — a `(name, color)` pair. The name is used everywhere as the balloon's identity: scoring, streaks, progress bars, model state. This conflates color with type.

### New state (type → optional color)

Balloon types are **MonoBehaviour components** that live on the balloon **prefab**. Each prefab variant carries a component implementing the `IBalloonTypeConfiguration` interface. **The balloon is responsible for its own initialization** — each type component configures the model (color, type name, hits) via `Initialize()`. The spawner no longer manages color; it delegates entirely to the type component.

```
IBalloonTypeConfiguration          ← interface: TypeName, HitsToPop, Initialize(model)
├── ColorableBalloonType           ← abstract MonoBehaviour: injects GamePalette, picks random color
│   └── SimpleBalloonType          ← concrete MonoBehaviour: standard colored balloon (HitsToPop = 1)
├── ToughBalloonType               ← concrete MonoBehaviour: no color, fixed visual, HitsToPop > 1 or -1
└── (future types)                 ← any MonoBehaviour implementing IBalloonTypeConfiguration
```

All type files live under `Balloon/Type/` with namespace `BalloonParty.Balloon.Type`.

#### `IBalloonTypeConfiguration` (interface)

The contract that every balloon type must satisfy. Lives on the balloon **prefab** as a MonoBehaviour component. Registered in `BalloonLifetimeScope` via `RegisterComponentInHierarchy<IBalloonTypeConfiguration>()`.

```csharp
public interface IBalloonTypeConfiguration
{
    string TypeName { get; }
    int HitsToPop { get; }           // 1 = pop on first hit, >1 = tough, -1 = unbreakable
    void Initialize(IWriteableBalloonModel model);  // configures model on spawn
}
```

Color picking is **not** on the interface — only colorable types need it. This keeps `ToughBalloonType` (and future colorless types) clean.

#### `ColorableBalloonType` (abstract MonoBehaviour base for colored balloons)

An abstract MonoBehaviour for any balloon type that wants color selection. Injects `GamePalette` via VContainer. `Initialize()` sets `TypeName`, `HitsRemaining`, and picks a random color from the serialized allowed list.

```csharp
public abstract class ColorableBalloonType : MonoBehaviour, IBalloonTypeConfiguration
{
    [SerializeField] private string _typeName;
    [SerializeField] private int _hitsToPop = 1;
    [SerializeField] private string[] _allowedColorNames;  // subset of GamePalette entries

    [Inject] private GamePalette _palette;

    public string TypeName => _typeName;
    public int HitsToPop => _hitsToPop;

    public virtual void Initialize(IWriteableBalloonModel model)
    {
        model.TypeName.Value = _typeName;
        model.HitsRemaining.Value = _hitsToPop;
        model.Color.Value = PickColor() ?? "";
    }

    private string PickColor()
    {
        if (_allowedColorNames == null || _allowedColorNames.Length == 0)
            return null;
        return _allowedColorNames[Random.Range(0, _allowedColorNames.Length)];
    }
}
```

#### `SimpleBalloonType` (concrete — current colored balloons)

```csharp
public class SimpleBalloonType : ColorableBalloonType { }
```

This is the 1:1 replacement for `BalloonColorConfiguration`. `HitsToPop = 1`, allowed colors = all palette colors. Added as a component on the standard balloon prefab.

#### `ToughBalloonType` (concrete — new tough/unbreakable)

```csharp
public class ToughBalloonType : MonoBehaviour, IBalloonTypeConfiguration
{
    [SerializeField] private string _typeName;
    [SerializeField] private int _hitsToPop = 2;

    public string TypeName => _typeName;
    public int HitsToPop => _hitsToPop;

    public void Initialize(IWriteableBalloonModel model)
    {
        model.TypeName.Value = _typeName;
        model.HitsRemaining.Value = _hitsToPop;
    }
}
```

Added as a component on a separate tough balloon prefab variant. No color, no `GamePalette` dependency.

### Pooling & Re-initialization

A "poolable scope" broadcasting pool events to child components is **not needed**. The spawner already orchestrates respawn correctly:

```
PoolManager.Get<BalloonView>()    → BalloonView.OnSpawned()  (resets visual state)
SpawnBalloon():
  → new BalloonModel()            (fresh model every spawn)
  → typeConfig.Initialize(model)  (re-randomizes color, sets type, hits)
  → new BalloonController(...)    → view.Bind(model)  (rebinds reactively)
```

The type component is a **stateless configurator** — it doesn't hold per-instance state, so it doesn't need pool lifecycle callbacks. Each spawn creates a fresh model, and `Initialize()` re-picks a random color. The existing `IPoolable` on `BalloonView` handles visual reset; the spawner handles logical initialization.

### `GamePalette` (new ScriptableObject)

Extracts the color definitions out of `GameConfiguration` into a standalone palette:

```csharp
[CreateAssetMenu(menuName = "Configuration/Game Palette")]
public class GamePalette : ScriptableObject
{
    [SerializeField] private PaletteEntry[] _colors;

    public PaletteEntry[] Colors => _colors;
    public Color GetColor(string colorName) => _colors.First(c => c.Name == colorName).Color;
}

[Serializable]
public class PaletteEntry
{
    [SerializeField] private string _name;
    [SerializeField] private Color _color;
    public string Name => _name;
    public Color Color => _color;
}
```

Registered in `GameLifetimeScope` as a singleton. All consumers that currently call `_config.BalloonColor(name)` inject `GamePalette` instead.

### `GameConfiguration` changes

- Remove `BalloonColorConfiguration[] _balloonColors` field and `BalloonColors` property
- Remove `BalloonColor(string name)` method — moves to `GamePalette.GetColor(string colorName)`
- Remove `BalloonColors` from `IGameConfiguration`
- No `_balloonTypes` array needed — types live on prefabs

### `BalloonLifetimeScope` changes

Register the type component from the prefab hierarchy:

```csharp
builder.RegisterComponentInHierarchy<IBalloonTypeConfiguration>();
```

This makes the type injectable into the balloon's `BalloonController` and `BalloonView`.

---

## Current Hit Flow

```
ProjectileView.OnTriggerEnter2D
  → publishes BalloonHitMessage(balloon, worldPos)

BalloonController subscribes:
  → plays pop VFX
  → removes from grid
  → returns to pool (or hides + waits for item activation)

BalloonNudgeHandler subscribes:
  → nudges neighboring balloons

ProjectileView also:
  → tracks color streak (shield gain)
  → sets LastHitBalloon (prevents double-hit)
```

**Problem:** Every `BalloonHitMessage` results in an immediate pop. There is no concept of "hit but survive."

---

## Design

### Durability values

`HitsRemaining` is a truthful live counter — it literally reads "hits left until pop".

| Value | Meaning |
|---|---|
| `1` | Normal balloon — pops on first hit (default for `SimpleBalloonType`) |
| `> 1` | Tough balloon — `HitsToPop = N` means exactly N total hits to pop |
| `-1` | Unbreakable — never pops, always deflects |

`0` is invalid. The editor should enforce a minimum of `1` (or `-1`) on all type configs.

### New hit flow

```
ProjectileView.OnTriggerEnter2D
  → publishes BalloonHitMessage(balloon, worldPos)  ← unchanged

BalloonController subscribes:
  → reads HitsRemaining
  → IF == -1: deflect (unbreakable, no decrement)
  → IF <= 1:  pop (existing flow, unchanged)
  → IF >  1:  decrement HitsRemaining, deflect

ProjectileView subscribes to BalloonDeflectedMessage:
  → calculates reflection direction from balloon surface
  → applies reflected direction to model
  → does NOT track color streak (deflections don't count)

BalloonView subscribes to BalloonDeflectedMessage:
  → plays pushback nudge in the opposite direction of the projectile
```

### Reflection physics

```csharp
Vector2 surfaceNormal = (projectilePosition - balloonCenter).normalized;
Vector2 newDirection = Vector2.Reflect(projectileDirection, surfaceNormal);
```

- `balloonCenter` = `SlotGrid.IndexToWorldPosition(slotIndex)` (the slot center, which is the circle center)
- `projectilePosition` = `transform.position` at the moment of collision
- No need for the collider radius — the direction from center to impact point gives the surface normal directly

### Pushback nudge

The hit balloon itself nudges in the direction the projectile was traveling (pushback). This reuses the existing `Nudge()` method on `BalloonView` via `BalloonNudgeMessage`, but the direction is the projectile's incoming direction rather than the neighbor-to-hit direction.

---

## Step-by-step Implementation

### ✅ Step 1 — Create `GamePalette` ScriptableObject

**New files:** `Configuration/GamePalette.cs`, `Configuration/PaletteEntry.cs`

- `PaletteEntry`: `[Serializable]` class with `string Name` and `Color Color`
- `GamePalette`: ScriptableObject with `PaletteEntry[] _colors`, `Color GetColor(string colorName)`, `PaletteEntry[] Colors`
- Register in `GameLifetimeScope` as `builder.RegisterInstance(_gamePalette)`
- Created `GamePalette.asset` with existing colors copied from `GameConfiguration.asset`

### ✅ Step 2 — Create `IBalloonTypeConfiguration` interface

**New file:** `Balloon/Type/IBalloonTypeConfiguration.cs`

```csharp
public interface IBalloonTypeConfiguration
{
    string TypeName { get; }
    int HitsToPop { get; }
    void Initialize(IWriteableBalloonModel model);
}
```

No `PickColor()` on the interface — color picking is an internal concern of `ColorableBalloonType` only.

### ✅ Step 3 — Create `ColorableBalloonType` abstract base + `SimpleBalloonType`

**New files:** `Balloon/Type/ColorableBalloonType.cs`, `Balloon/Type/SimpleBalloonType.cs`

- `ColorableBalloonType`: abstract **MonoBehaviour** implementing `IBalloonTypeConfiguration`. Injects `GamePalette`. Has `_allowedColorNames` (string array). `Initialize()` sets `TypeName`, `HitsRemaining`, and picks a random allowed color for `model.Color`. `PickColor()` is private.
- `SimpleBalloonType`: concrete subclass. No additional fields — `HitsToPop` defaults to `1` (pops on first hit). Added as a component on the standard balloon prefab.

### ✅ Step 4 — Create `ToughBalloonType`

**New file:** `Balloon/Type/ToughBalloonType.cs`

- **MonoBehaviour** implementing `IBalloonTypeConfiguration` directly (not `ColorableBalloonType` — no color)
- `HitsToPop` configurable per prefab variant (default `2`)
- `Initialize()` sets `TypeName` and `HitsRemaining` only — no color, no `GamePalette` dependency

### ✅ Step 5 — Model: add `HitsRemaining` and `TypeName`

**Files:** `IBalloonModel.cs`, `IWriteableBalloonModel.cs`, `BalloonModel.cs`

Add to the balloon model:
- `HitsRemaining`: reactive int (`1` = normal, `> 1` = tough, `-1` = unbreakable). Default `1`.
- `TypeName`: reactive string (replaces the role `Color` played as the balloon's identity for non-colored types)

The existing `Color` property remains — it's the visual color name (null/empty for colorless balloon types).

### ✅ Step 5b — Register `IBalloonTypeConfiguration` in `BalloonLifetimeScope`

**File:** `BalloonLifetimeScope.cs`

- `builder.RegisterComponentInHierarchy<IBalloonTypeConfiguration>()` — makes the type component injectable into the balloon's controller and view

### ✅ Step 5c — Update `BalloonSpawner` to delegate initialization to type component

**File:** `BalloonSpawner.cs`

- `SpawnBalloon` no longer takes a `colorName` parameter
- Gets `IBalloonTypeConfiguration` from the prefab via `view.GetComponentInParent<IBalloonTypeConfiguration>()`
- Calls `typeConfig.Initialize(model)` — the type component sets color, type name, and hits remaining
- Removed all `_grid.RandomColorName()` calls

### ✅ Step 5d — Remove `SlotGrid.RandomColorName()`

**File:** `SlotGrid.cs`

- Removed `RandomColorName()` method — no longer needed since balloon types pick their own color
- Removed unused `using Random = UnityEngine.Random;`

### Step 6 — Update `GameConfiguration`

**Files:** `GameConfiguration.cs`, `IGameConfiguration.cs`

- Remove `BalloonColorConfiguration[] _balloonColors` field and `BalloonColors` property
- Remove `BalloonColor(string name)` method — moves to `GamePalette.GetColor(string colorName)`
- Remove `BalloonColors` from `IGameConfiguration`
- No `_balloonTypes` array needed — types live on prefabs

### Step 7 — Update all `BalloonColor()` consumers to use `GamePalette`

**Files affected:**
- `BalloonView.ApplyColor()` — inject `GamePalette`, call `_palette.GetColor(colorName)`
- `BalloonController` — inject `GamePalette` for pop VFX color
- `ProjectileView` (glow color) — inject `GamePalette`
- `ProjectileShieldView` — inject `GamePalette`
- `ColorProgressBar` — inject `GamePalette`
- `ColorProgressBarInstancer` — iterate `_palette.Colors` instead of `_config.BalloonColors`
- `ScoreController` — iterate `_palette.Colors` for persistence keys
- `LaserItemHandler` — inject `GamePalette`
- `ItemDisplayService` — inject `GamePalette`
- `BombItemHandler` — inject `GamePalette`
- `ShieldItemHandler` — inject `GamePalette`
- Cheats (`TriggerLevelUpCheat`, `NearLevelUpCheat`) — iterate `_palette.Colors`

### Step 8 — New message: `BalloonDeflectedMessage`

**New file:** `Shared/Messages/BalloonDeflectedMessage.cs`

```csharp
public readonly struct BalloonDeflectedMessage
{
    public readonly IBalloonModel Balloon;
    public readonly Vector3 BalloonWorldPosition;
    public readonly Vector3 ProjectileDirection;
}
```

### Step 9 — Update `BalloonHitMessage`

**Files:** `BalloonHitMessage.cs`

Add `Vector3 ProjectileDirection` field — published from `ProjectileView.OnTriggerEnter2D` using `_model.Direction`.

### Step 10 — Register `BalloonDeflectedMessage` broker

**Files:** `GameLifetimeScope.cs`

### Step 11 — `BalloonController`: deflect-or-pop logic

**Files:** `BalloonController.cs`

Inject `IPublisher<BalloonDeflectedMessage>` and `IPublisher<BalloonNudgeMessage>`.

In the `BalloonHitMessage` handler, before popping:

```
read hitsRemaining from model

if hitsRemaining == -1  → deflect (unbreakable, no decrement), do NOT pop, do NOT dispose subscription
if hitsRemaining <= 1   → pop (existing code, no change)
if hitsRemaining >  1   → HitsRemaining--, deflect, do NOT pop, do NOT dispose subscription
```

Also publish `BalloonNudgeMessage` targeting self for pushback nudge on deflection.

### Step 12 — `ProjectileView`: subscribe to `BalloonDeflectedMessage`

**Files:** `ProjectileView.cs`

On receive:
1. Guard: only react if `msg.Balloon == _model.LastHitBalloon`
2. Calculate reflection: `surfaceNormal = (transform.position - msg.BalloonWorldPosition).normalized`
3. `_model.Direction = Vector2.Reflect(_model.Direction, surfaceNormal)`
4. Do **not** call `TrackColorStreak`

Gate `TrackColorStreak` in `OnTriggerEnter2D` on `balloonModel.HitsRemaining.Value <= 1`.

### Step 13 — Score system: only score colored balloons

**Files:** `ScoreController.cs`

The `OnBalloonHit` handler already gates on `_persistentScore.ContainsKey(color)` — non-colored balloons (where `Color` is empty/null) will naturally be ignored. No change needed unless TypeName scoring is desired later.

### Step 14 — Delete old files

- Delete `BalloonColorConfiguration.cs`
- Delete `IBalloonColorConfiguration.cs`

---

## Files Changed — Summary

| # | File | Change |
|---|---|---|
| 1 | `Configuration/GamePalette.cs` | **New** — ScriptableObject holding color definitions |
| 2 | `Configuration/PaletteEntry.cs` | **New** — `[Serializable]` name + color pair |
| 3 | `Balloon/Type/IBalloonTypeConfiguration.cs` | **New** — interface: `TypeName`, `HitsToPop`, `Initialize()` |
| 4 | `Balloon/Type/ColorableBalloonType.cs` | **New** — abstract MonoBehaviour base for colored balloon types; injects `GamePalette`; `Initialize()` sets color |
| 5 | `Balloon/Type/SimpleBalloonType.cs` | **New** — concrete colored balloon MonoBehaviour (`HitsToPop = 1`) |
| 6 | `Balloon/Type/ToughBalloonType.cs` | **New** — concrete tough/unbreakable balloon MonoBehaviour (no color); `Initialize()` sets type + hits |
| 7 | `Shared/Messages/BalloonDeflectedMessage.cs` | **New** — deflection message |
| 8 | `Balloon/Model/IBalloonModel.cs` | Add `HitsRemaining`, `TypeName` |
| 9 | `Balloon/Model/IWriteableBalloonModel.cs` | Add `HitsRemaining`, `TypeName` |
| 10 | `Balloon/Model/BalloonModel.cs` | Add `HitsRemaining`, `TypeName` |
| 11 | `Shared/Messages/BalloonHitMessage.cs` | Add `ProjectileDirection` |
| 12 | `Shared/IGameConfiguration.cs` | Remove `BalloonColors` and `BalloonColor()` |
| 13 | `Configuration/GameConfiguration.cs` | Remove `_balloonColors` field, `BalloonColors` property, and `BalloonColor()` method |
| 14 | `GameLifetimeScope.cs` | Register `GamePalette`, `BalloonDeflectedMessage` broker |
| 15 | `Balloon/BalloonLifetimeScope.cs` | Register `IBalloonTypeConfiguration` component from prefab |
| 16 | `Balloon/Spawner/BalloonSpawner.cs` | Delegate to `typeConfig.Initialize(model)`; remove color parameter; remove `RandomColorName()` calls |
| 17 | `Balloon/Controller/BalloonController.cs` | Deflect-or-pop branching; inject deflect + nudge publishers; inject `GamePalette` |
| 18 | `Projectile/View/ProjectileView.cs` | Subscribe to deflection; reflect direction; gate streak on `HitsRemaining <= 1`; inject `GamePalette` |
| 19 | `Balloon/View/BalloonView.cs` | Inject `GamePalette` for color lookup |
| 20 | `Slots/SlotGrid.cs` | Remove `RandomColorName()` and unused `Random` import |
| 21 | `UI/Score/ColorProgressBar.cs` | Inject `GamePalette`; receive `PaletteEntry` instead of `BalloonColorConfiguration` |
| 22 | `UI/Score/ColorProgressBarInstancer.cs` | Iterate `GamePalette.Colors` |
| 23 | `Game/ScoreController.cs` | Iterate `GamePalette.Colors` for keys |
| 24 | `Projectile/View/ProjectileShieldView.cs` | Inject `GamePalette` |
| 25 | `Item/Laser/LaserItemHandler.cs` | Inject `GamePalette` |
| 26 | `Item/Bomb/BombItemHandler.cs` | Inject `GamePalette` |
| 27 | `Item/Shield/ShieldItemHandler.cs` | Inject `GamePalette` |
| 28 | `Item/ItemDisplayService.cs` | Inject `GamePalette` |
| 29 | `Cheats/TriggerLevelUpCheat.cs` | Iterate `GamePalette.Colors` |
| 30 | `Cheats/NearLevelUpCheat.cs` | Iterate `GamePalette.Colors` |
| 31 | `Configuration/BalloonColorConfiguration.cs` | **Delete** |
| 32 | `Configuration/IBalloonColorConfiguration.cs` | **Delete** |

---

## Edge Cases

| Case | Resolution |
|---|---|
| Tough balloon with an item | Items should only be assigned to normal balloons (`HitsToPop == 1`). Enforce in item assignment logic. |
| Projectile hits unbreakable, reflects into same balloon | `LastHitBalloon` guard already prevents immediate re-hit. After reflecting and hitting a different balloon, the guard resets. |
| Multiple projectiles in flight (future) | Each projectile tracks its own `LastHitBalloon` — no conflict. |
| Tough balloon at 1 hit remaining gets hit | `HitsRemaining == 1` → `<= 1` branch → pops normally. Streak tracks, items activate, neighbors nudge — all existing flow. |
| Color streak on deflection | Deflections do **not** count toward the color streak. Only `HitsRemaining <= 1` hits (actual pops) do. |
| Non-colored balloon popped (future tough with 0 remaining) | `ScoreController` ignores empty color keys — no score, no progress bar. This is correct: tough balloons don't contribute to color scoring. |
| `ColorProgressBar` only shows palette colors | Correct — progress bars are per-palette-color, not per-balloon-type. Tough/unbreakable balloons have no progress bar. |
| Different balloon prefabs in pool | Each type variant uses a separate pool key (e.g., "Balloon", "ToughBalloon"). The spawner selects the appropriate pool key based on game rules. |
| Pooled balloon respawn re-randomizes color | Handled by design: spawner creates a fresh `BalloonModel` each time and calls `typeConfig.Initialize(model)`, which re-picks a random color. No pool lifecycle hooks needed on the type component — it's a stateless configurator. |

---

## Unity Editor Setup (after implementation)

1. **Create GamePalette asset:** Right-click → Create → Configuration → Game Palette. Move existing color entries from `GameConfiguration._balloonColors` into `_colors`.
2. **Assign GamePalette** to `GameLifetimeScope._gamePalette` field.
3. **Standard balloon prefab:** Add a `SimpleBalloonType` component. Set `TypeName` (e.g., "Simple"), `HitsToPop` to `1`, and `AllowedColorNames` to all palette color names (`Green`, `Red`, `Blue`, `Purple`).
4. **Tough balloon prefab:** Duplicate the balloon prefab. Replace the `SimpleBalloonType` component with `ToughBalloonType`. Set `TypeName` (e.g., "Tough"), `HitsToPop` to `2` or more (or `-1` for unbreakable).
5. **Register prefab pools:** Add the tough balloon prefab to the pool system (new pool key in `BalloonSpawner`).
6. **Remove old config:** The `_balloonColors` field on `GameConfiguration` is deleted — no migration needed beyond step 1.

---

## Future Improvements

### Visual

| Idea | Description |
|---|---|
| **Durability indicator overlay** | A secondary sprite (band, cracks, or shield icon) layered on the balloon that visually communicates remaining hits. Subscribe to `HitsRemaining` in `BalloonView` — swap sprites or tint as durability decreases. |
| **Hit flash / shake** | On deflection, briefly flash the balloon white or play a screen-shake-style local shake (separate from the pushback nudge) to reinforce the "armored" feeling. |
| **Crack progression** | Use a sprite sheet or shader mask that reveals cracks as `HitsRemaining` drops. At 1 hit remaining the balloon looks visibly damaged, cueing the player that one more hit will pop it. |
| **Unbreakable distinct visual** | Unbreakable balloons should look fundamentally different — metallic material, animated shimmer, or a unique shape — so players learn to aim around them rather than waste hits. |
| **Deflection spark VFX** | Spawn a small particle burst at the contact point when the projectile reflects off a tough balloon. Color-matched to the balloon. Pooled via `ParticlePoolChannel`. |
| **Projectile bounce trail color shift** | On deflection, briefly tint the projectile trail to the balloon's color to show the interaction, then fade back. |
| **Camera micro-shake on unbreakable** | A subtle camera impulse when hitting an unbreakable balloon — reinforces that this one cannot be broken. |
| **Per-type prefab** | Each `IBalloonTypeConfiguration` could reference a distinct prefab (different shape, material, particle system) instead of reusing the single balloon prefab for all types. |

### Mechanics

| Idea | Description |
|---|---|
| **Weakening via items** | Certain items (bomb, lightning) could reduce `HitsRemaining` by more than 1, or bypass durability entirely, giving the player tools to deal with tough balloons strategically. |
| **Durability per-instance, not per-type** | Decouple durability from type config so that any balloon can be individually made tough (e.g., boss wave spawns tough balloons of random colors). Add a durability parameter to `SpawnBalloon()`. |
| **Progressive difficulty** | As the game progresses (level-ups or turn count), spawn increasingly tough balloons. Start with all-normal, introduce 1-hit-tough at level 3, 2-hit-tough at level 6, unbreakable at level 10. Driven by a difficulty curve in config. |
| **Shield gain on deflection** | Award a partial shield charge or a small score bonus for deflections, rewarding skilled bank shots that use tough balloons as reflectors. |
| **Chain deflection bonus** | Track consecutive deflections in a single projectile flight. Hitting 3+ tough balloons in a row without popping any could trigger a special reward or VFX. |
| **Unbreakable as strategic walls** | Place unbreakable balloons in fixed patterns to create maze-like levels where the player must bank the projectile around obstacles to reach poppable balloons behind them. |
| **Tough balloon healing** | Tough balloons could regenerate 1 hit of durability per turn if not fully broken, adding urgency to focus fire on a single tough balloon before it recovers. |
| **Color-matching weakness** | A tough balloon could lose extra durability (or pop instantly) if hit by a projectile that has a matching color streak, rewarding the player for building streaks before targeting tough balloons. |
| **Knockback physics** | Instead of a fixed nudge distance, scale the pushback by how many shields the projectile has — a heavily shielded projectile hitting a tough balloon sends it further, potentially pushing it into a neighbor and triggering a chain pop. |
| **Weighted type selection** | Add spawn weights to `IBalloonTypeConfiguration` so the spawner can probabilistically mix types. E.g., 80% simple, 15% tough, 5% unbreakable. Weights could shift with difficulty. |
| **Type-specific items** | Certain item types could only spawn inside specific balloon types, or tough balloons could drop unique rewards when finally broken. |


