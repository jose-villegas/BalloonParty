# Startup Performance Audit

> Investigates the frame-rate hitch observed at game start on device.

---

## 🔴 Critical Issues

### 1. No Pool Pre-warming — 36 Balloons Instantiated Synchronously on First Frame

**Location:** `BalloonSpawner.Start()` → `PopulateInitialGrid()`

The grid is **6 columns × 11 rows**, and `_gameStartedBalloonLines` is **6**.
That means **36 balloons** are spawned in `Start()`. Every balloon calls `PoolChannel.Get()`, which on the first frame has an empty pool, so it calls `Create()` every single time.

Each `BalloonPoolChannel.Create()` does:
```
_parentScope.CreateChildFromPrefab(_prefab)  // VContainer DI resolve
childScope.GetComponentInChildren<BalloonView>()
```

The `Balloon.prefab` has **27 GameObjects**. Instantiating 36 of them = **~972 GameObjects** created in a single frame, each going through VContainer's DI resolution, `Awake()`, `GetComponent` calls, Animator initialization, and sprite renderer setup.

**This is almost certainly the primary cause of the startup hitch.**

**Fix:** Add pre-warming to `PoolChannel<T>` and call it during a loading screen or spread it across frames:

```csharp
public async UniTask PrewarmAsync(int count, CancellationToken ct)
{
    for (var i = 0; i < count; i++)
    {
        var item = Create();
        item.gameObject.SetActive(false);
        _available.Push(item);
        await UniTask.Yield(ct); // spread across frames
    }
}
```

### 2. ~~Zero GPU Instancing on All 41 Materials~~ ✅ FIXED

**Location:** Every `.mat` file under `Assets/Materials/`

All 41 materials now have `m_EnableInstancingVariants: 1`. All 6 custom shaders (`ToughBalloon`, `SpriteBlur`, `SpriteShine`, `SpriteShadow`, `SpriteShadowComposite`, `PaintBlob`) now include `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID` in input/output structs, `UNITY_SETUP_INSTANCE_ID` in vertex and fragment functions, and `UNITY_TRANSFER_INSTANCE_ID` where needed.

### 3. ~~No Sprite Atlases — Every Sprite is a Separate Draw Call~~ ✅ FIXED

**Location:** `Assets/Sprites/`

Created 4 sprite atlases grouping all 47 sprites by usage domain:
- `Balloons.spriteatlas` — balloon, balloon_blur, balloon_shine, balloon_spec, bomb_cord, bomb_spec
- `UI.spriteatlas` — progress, shield, shield-fill, shield-fill-blur, shield-fx, background_counter, score_trail, play, plus, plus_blur
- `VFX.spriteatlas` — star, smoke, spark, circle_dust, cartoon-smoke, circle, circle_glow, blur_circle, light, trace, trail, gradient, pixel, q-moon, bullet, bullet_blur, laser, laser_shine, lightning, lightning_chain
- `Thrower.spriteatlas` — thrower, thrower_blur, thrower_spec, thrower_caparace_back/front, thrower_tap, thrower_tap_filled, thrower_tap_filled_hlf

Also enabled `m_SpritePackerMode: 2` (Sprite Atlas V1 — Always Enabled) in `ProjectSettings/EditorSettings.asset`.

---

## 🟡 Moderate Issues

### 4. Oversized Textures for Mobile

Several sprites are 1024×1024 for elements that render quite small on screen:

| Sprite | Size | Likely Use |
|--------|------|------------|
| `balloon_shine.png` | 1024×1024 | Specular overlay on balloons |
| `bomb_spec.png` | 1024×1024 | Specular overlay on bombs |
| `circle_dust.png` | 1024×1024 | Particle effect |
| `cartoon-smoke.png` | 1024×1024 | Particle effect |
| `background_counter.png` | 1024×1024 | UI background |

Balloons render at roughly 0.375 world units wide. Even on a high-DPI phone, a 256×256 or 512×512 texture is sufficient for overlays. Particle sprites rarely need more than 256×256.

**Fix:** Set platform-specific max texture sizes in the import settings: 512 or 256 for mobile for overlay/particle sprites.

### 5. LINQ Allocations in `PickRandom()` — Called 36× at Startup

**Location:** `BalloonsConfiguration.PickRandom()`

```csharp
var candidates = _entries.Where(e => ...).ToArray();  // allocation
var totalWeight = candidates.Sum(e => e.Weight);      // allocation
```

This is called once per balloon spawn (36 times at startup), creating temporary arrays each time.

**Fix:** Use a reusable list or iterate without LINQ:
```csharp
private readonly List<BalloonPrefabEntry> _candidateBuffer = new();

public BalloonPrefabEntry PickRandom(IReadOnlyDictionary<string, int> activeCounts)
{
    _candidateBuffer.Clear();
    var totalWeight = 0f;

    foreach (var e in _entries)
    {
        if (e.MaxCount == 0 || activeCounts.GetValueOrDefault(e.PoolKey) < e.MaxCount)
        {
            _candidateBuffer.Add(e);
            totalWeight += e.Weight;
        }
    }
    // ... rest using _candidateBuffer
}
```

### 6. `FindFirstObjectByType<GameLifetimeScope>()` in Child Scopes

**Location:** `GameChildLifetimeScope.FindParent()` and `ItemViewScope.FindParent()`

`FindFirstObjectByType` scans all loaded objects. This is called by every `GameChildLifetimeScope` subclass (`ScoreUILifetimeScope`, `ShieldUILifetimeScope`, `ThrowerLifetimeScope`, `LevelUpLifetimeScope`, `BalloonLifetimeScope`) during their `Awake`. For balloon scopes (created 36× at startup), this is particularly expensive.

**Fix:** Cache the `GameLifetimeScope` reference in a static field after the first lookup, or use VContainer's parent scope wiring instead of `FindFirstObjectByType`.

### 7. Per-Balloon `GetComponentsInChildren<IBalloonViewBinding>()` in Bind()

**Location:** `BalloonView.Bind()` line 91

Every time a balloon is bound (36× at startup), `GetComponentsInChildren<IBalloonViewBinding>()` walks the hierarchy. Since the Balloon prefab has 27 GameObjects, this is 36 × 27 = ~972 component lookups.

**Fix:** Cache the bindings array in `Awake()`:
```csharp
private IBalloonViewBinding[] _viewBindings;

private void Awake()
{
    // ...existing code...
    _viewBindings = GetComponentsInChildren<IBalloonViewBinding>();
}
```

### 8. `GetComponentInParent<IBalloonVariant>()` per Balloon Spawn

**Location:** `BalloonSpawner.SpawnBalloon()` line 187

Called 36 times at startup, each walking up the hierarchy.

**Fix:** Cache on the `BalloonView` or resolve through DI.

### 9. 36 Concurrent DOTween Sequences at Startup

**Location:** `BalloonSpawner.AnimateSpawn()` — called per balloon

Each balloon spawn creates 2 tweens (`DOMove` + `DOScale`). At startup that's **72 tweens** created simultaneously. DOTween's internal data structures resize dynamically, causing GC allocations from array resizing.

**Fix:** Call `DOTween.SetTweensCapacity(200, 50)` early in startup to pre-allocate DOTween's internal arrays.

---

## 🟢 Minor Issues

### 10. `Camera.main` Not Cached

**Location:** `ThrowerController.UpdateDirection()` (called every `Tick`)

`Camera.main` does a `FindObjectWithTag` internally. Not a startup issue but a per-frame cost.

### 11. Shader Compilation on First Use

With 7+ unique custom shaders and no shader variant pre-warming (`ShaderVariantCollection`), the first frame that renders each material will trigger on-device shader compilation. With 36 balloons + UI + thrower all appearing on the first frame, multiple shader variants compile simultaneously.

**Fix:** Create a `ShaderVariantCollection` asset containing all used shader/keyword combinations, and call `WarmUp()` during a loading screen.

### 12. No `[DefaultExecutionOrder]` Coordination

`GameLifetimeScope` has `[DefaultExecutionOrder(-5001)]`, but none of the child scopes or views specify execution order. This means the startup order depends on Unity's default (creation order), which may cause unnecessary re-layouts or redundant work if UI updates before data is ready.

---

## Summary — Recommended Priority Order

| # | Fix | Impact | Effort |
|---|-----|--------|--------|
| 1 | **Pre-warm balloon pool** (spread across loading frames) | 🔴 High | Medium |
| 3 | ~~**Add Sprite Atlases**~~ ✅ | 🔴 High | Low |
| 2 | ~~**Enable GPU Instancing** (shaders + materials)~~ ✅ | 🔴 High | Medium |
| 11 | **Shader variant pre-warming** | 🟡 Medium | Low |
| 6 | **Cache `FindFirstObjectByType` result** | 🟡 Medium | Low |
| 7 | **Cache `IBalloonViewBinding[]`** | 🟡 Medium | Low |
| 9 | **Pre-allocate DOTween capacity** | 🟡 Medium | Low |
| 5 | **Remove LINQ from `PickRandom`** | 🟡 Medium | Low |
| 4 | **Reduce texture sizes for mobile** | 🟡 Medium | Low |
| 8 | **Cache `IBalloonVariant` lookup** | 🟢 Low | Low |
| 10 | **Cache `Camera.main`** | 🟢 Low | Low |

