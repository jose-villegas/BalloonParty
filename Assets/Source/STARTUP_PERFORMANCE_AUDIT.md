# Startup Performance Audit

> Investigates the frame-rate hitch observed at game start on device.

---

## 🔴 Critical Issues

### 1. ~~No Pool Pre-warming — 36 Balloons Instantiated Synchronously on First Frame~~ ✅ FIXED

**Location:** `BalloonSpawner.PrewarmAndPopulateAsync()`

Pool pre-warming now spreads `Create()` calls across frames via `PrewarmAsync()` before the Game scene transitions. `PopulateInitialGrid()` runs only after pre-warming completes and the navigation state reaches `Game`.

**Remaining hitch:** Even with a warm pool, `PopulateInitialGrid()` calls `SpawnBalloon()` **36 times synchronously** in one frame. Each call does: `PickRandom` (LINQ alloc), `Get` (pool hit — fast), `new BalloonModel`, 8 model property sets, `GetComponentInParent<IBalloonVariant>` (hierarchy walk), `Initialize`, `new BalloonController` (10+ injected deps), `controller.Start` (message subscriptions), `grid.Place`, `view.Bind` (3 UniRx subscriptions + `GetComponentsInChildren<IBalloonViewBinding>` hierarchy walk + `ItemDisplayService.Bind`), and `AnimateSpawn` (2 DOTween tweens). The per-balloon cost is moderate individually, but **×36 in one frame** creates a visible hitch. The fixes below (#5–#9) reduce per-balloon cost; if still insufficient, `PopulateInitialGrid` itself should be spread across frames.

### 2. ~~Zero GPU Instancing on All 41 Materials~~ ✅ FIXED

**Location:** Every `.mat` file under `Assets/Materials/`, every `.shader` under `Assets/Shaders/BalloonParty/`

All 6 custom shaders now support GPU instancing following Unity's `UnitySprites.cginc` pattern:

- `#pragma multi_compile_instancing` and `UNITY_VERTEX_INPUT_INSTANCE_ID` in vertex input/output structs
- Per-instance renderer color via `unity_SpriteRendererColorArray` in a `PerDrawSprite` instancing buffer (matches Unity's built-in SpriteRenderer instancing path)
- `_RendererColor` fallback uniform for non-SpriteRenderer components, declared in the `Properties` block with default white `(1,1,1,1)` so trails, particles, and UI renderers work without SpriteRenderer populating the buffer
- Vertex shader multiplies `IN.color * _Color * _RendererColor`

**30 materials** have GPU instancing enabled — 12 SpriteRenderer-based custom-shader materials (balloon, specular overlays, projectile, UI sprites) and 18 particle/trail/line materials using Unity's built-in shaders.

**11 materials** have GPU instancing deliberately disabled:
- `ToughBalloonMaterial`, `PaintBlob`, `PaintFlyingBlob` — require per-instance `MaterialPropertyBlock` properties (`_DamageProgress`, `_VoronoiSeed`, `_TimeOffset`) that are incompatible with instancing batching
- 8 particle/trail materials using custom shaders (`PSMaterial_BalloonPop`, `PSMaterial_ToughBalloonPopSmoke`, `PSMaterial_BombRangePU`, `PSMaterial_ShieldGain`, `PSMaterial_ShieldGainPU`, `PSMaterial_ShieldLose`, `TrailMaterial_Projectile`, `TrailMaterial_Shield`) — ParticleSystem and TrailRenderer do not populate `unity_SpriteRendererColorArray`

### 3. ~~No Sprite Atlases — Every Sprite is a Separate Draw Call~~ ✅ FIXED

**Location:** `Assets/Sprites/`

Created 4 sprite atlases grouping all 47 sprites by usage domain:
- `Balloons.spriteatlas` — balloon, balloon_blur, balloon_shine, balloon_spec, bomb_cord, bomb_spec
- `UI.spriteatlas` — progress, shield, shield-fill, shield-fill-blur, shield-fx, background_counter, score_trail, play, plus, plus_blur
- `VFX.spriteatlas` — star, smoke, spark, circle_dust, cartoon-smoke, circle, circle_glow, blur_circle, light, trace, trail, gradient, pixel, q-moon, bullet, bullet_blur, laser, laser_shine, lightning, lightning_chain
- `Thrower.spriteatlas` — thrower, thrower_blur, thrower_spec, thrower_caparace_back/front, thrower_tap, thrower_tap_filled, thrower_tap_filled_hlf

Also enabled `m_SpritePackerMode: 2` (Sprite Atlas V1 — Always Enabled) in `ProjectSettings/EditorSettings.asset`.

---

## 🟡 Moderate Issues — Per-Balloon Cost During `PopulateInitialGrid()`

> With pool pre-warming solved, the remaining hitch is the **synchronous per-balloon work** inside `PopulateInitialGrid()`. Each fix below reduces per-balloon cost; together they should eliminate or significantly reduce the frame spike. If the hitch persists after all moderate fixes, the final step is spreading `PopulateInitialGrid` itself across frames (1–2 rows per frame).

### 4. ~~Oversized Textures for Mobile~~ ✅ FIXED

Mobile platform overrides (iPhone + Android) now cap texture sizes. Desktop/Standalone remains at original 1024×1024.

| Sprite | Source | Mobile Max | Reason |
|--------|--------|------------|--------|
| `balloon_shine.png` | 1024×1024 | 256 | Specular overlay on ~0.375 world-unit balloons |
| `bomb_spec.png` | 1024×1024 | 256 | Specular overlay on bombs |
| `circle_dust.png` | 1024×1024 | 256 | Particle effect |
| `cartoon-smoke.png` | 1024×1024 | 256 | Particle effect |
| `background_counter.png` | 1024×1024 | 512 | UI background element (needs slightly more detail) |

### 5. ~~LINQ Allocations in `PickRandom()` — Called 36× at Startup~~ ✅ FIXED

**Location:** `BalloonsConfiguration.PickRandom()`

Replaced LINQ `Where().ToArray()` and `.Sum()` with a reusable `List<BalloonPrefabEntry>` buffer (`_candidateBuffer`) and a manual `foreach` loop that accumulates `totalWeight` inline. The buffer is cleared and reused on each call, eliminating all per-call allocations. Also removed the now-unused `System.Linq` import.

### 6. ~~`FindFirstObjectByType<GameLifetimeScope>()` in Child Scopes~~ ✅ FIXED

**Location:** `GameChildLifetimeScope.FindParent()` and `ItemViewScope.FindParent()`

`GameChildLifetimeScope` has been removed. All child scopes extend `LifetimeScope` directly, with parent references set via VContainer's `parentReference` field in the Inspector. `ItemViewScope` no longer overrides `FindParent()` either. This eliminates all runtime parent-lookup cost.

### 7. Per-Balloon `GetComponentsInChildren<IBalloonViewBinding>()` in Bind()

**Location:** `BalloonView.Bind()` line 91

Every time a balloon is bound (36× during `PopulateInitialGrid()`), `GetComponentsInChildren<IBalloonViewBinding>()` walks the 27-GameObject hierarchy. This is 36 × 27 = ~972 component lookups in a single frame.

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

Called 36 times during `PopulateInitialGrid()`, each walking up the hierarchy.

**Fix:** Cache on the `BalloonView` or resolve through DI.

### 9. 36 Concurrent DOTween Sequences at Startup

**Location:** `BalloonSpawner.AnimateSpawn()` — called per balloon in `PopulateInitialGrid()`

Each balloon spawn creates 2 tweens (`DOMove` + `DOScale`). That's **72 tweens** created simultaneously in one frame. DOTween's internal data structures resize dynamically, causing GC allocations from array resizing.

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
| 13 | ~~**Eliminate per-balloon child scopes** (`InjectingPoolChannel`)~~ ✅ | 🔴 High | Medium |
| 1 | ~~**Pre-warm balloon pool**~~ ✅ | 🔴 High | Medium |
| 3 | ~~**Add Sprite Atlases**~~ ✅ | 🔴 High | Low |
| 2 | ~~**Enable GPU Instancing** (shaders + materials)~~ ✅ | 🔴 High | Medium |
| 7 | **Cache `IBalloonViewBinding[]`** | 🟡 Medium | Low |
| 8 | **Cache `IBalloonVariant` lookup** | 🟡 Medium | Low |
| 5 | ~~**Remove LINQ from `PickRandom`**~~ ✅ | 🟡 Medium | Low |
| 9 | **Pre-allocate DOTween capacity** | 🟡 Medium | Low |
| 6 | ~~**Cache `FindFirstObjectByType` result**~~ ✅ | 🟡 Medium | Low |
| 11 | **Shader variant pre-warming** | 🟡 Medium | Low |
| 4 | ~~**Reduce texture sizes for mobile**~~ ✅ | 🟡 Medium | Low |
| 10 | **Cache `Camera.main`** | 🟢 Low | Low |

> **If the hitch persists after all moderate fixes:** spread `PopulateInitialGrid()` across frames — spawn 1–2 rows per frame using `async UniTask` with `UniTask.Yield()` between rows. This is the nuclear option and should be unnecessary if the per-balloon cost is reduced sufficiently.

---

## ~~🔴 High-Impact Opportunity — Eliminate Per-Balloon Child Scopes~~ ✅ FIXED

### 13. ~~`CreateChildFromPrefab` creates 2 VContainer child scopes per balloon during pre-warm~~ ✅ FIXED

**Location:** `BalloonPoolChannel.Create()`, `ProjectilePoolChannel.Create()`

Introduced `InjectingPoolChannel<TItem>` — a generic `PoolChannel` that uses `IObjectResolver.InjectGameObject()` instead of `LifetimeScope.CreateChildFromPrefab()`. It instantiates the prefab while inactive, sets `autoRun = false` on any `LifetimeScope` components on the clone (preventing child container builds on activation), then injects all `[Inject]` fields from the parent container in a single flat pass.

Both `BalloonPoolChannel` and `ProjectilePoolChannel` now extend `InjectingPoolChannel<T>` and accept `IObjectResolver` + the view prefab instead of `LifetimeScope` + scope prefab. `BalloonPrefabEntry._prefab` changed from `BalloonLifetimeScope` to `BalloonView`; `ThrowerSettings` changed from `ProjectileLifetimeScope` to `ProjectileView`. `BalloonSpawner` and `ThrowerController` inject `IObjectResolver` instead of `LifetimeScope`.

**Eliminated per balloon:** 2× `Build()` (child container allocation), 2× `Configure()` with 3× `RegisterComponentInHierarchy` hierarchy walks, 2× `InstallTo()` reflection overhead. Replaced with a single `InjectGameObject()` pass.

**Note:** The `BalloonLifetimeScope` and `ProjectileLifetimeScope` classes still exist but are no longer referenced by pool channels. The `LifetimeScope` components remain on prefabs for now — `InjectingPoolChannel` neutralises them via `autoRun = false`. They can be removed from prefabs in the Unity Editor when convenient.

