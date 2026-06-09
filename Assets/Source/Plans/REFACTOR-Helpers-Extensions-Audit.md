# Refactor Audit — Helpers & Extensions Candidates

> Audit date: June 9, 2026
> Scanned: 295 source files under `Assets/Source/`

---

## 1. Pool Fire-and-Forget — `PoolManager` Extension

**Pattern:** `GetOrRegister` → `Play(pos, …)` → `Return(key, effect)` callback. Every VFX spawn repeats this 3-step boilerplate.

```csharp
var key = prefab.name;
var effect = _poolManager.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
effect.Play(position, color, () => _poolManager.Return(key, effect));
```

**Occurrences (13):**
- `BalloonView.cs` (×3)
- `ProjectileShieldView.cs` (×2)
- `BombItemHandler.cs`
- `LaserItemHandler.cs`
- `PaintItemHandler.cs`
- `PaintSplashView.cs`
- `ShieldItemHandler.cs`
- `LightningItemHandler.cs`
- `ItemDisplayService.cs`
- `BushView.cs`
- `ColorProgressBar.cs` (×2)

**Suggestion:** `PoolManagerExtensions.PlayEffect(this PoolManager, ParticleSystem prefab, Vector3 pos, Color? tint = null, Quaternion? rotation = null)` in `Shared/Extensions/`.

---

## 2. Color.WithAlpha — `Color` Extension

**Pattern:** Create a new `Color` keeping RGB but changing alpha.

```csharp
new Color(color.r, color.g, color.b, _glowAlpha)
new Color(_currentColor.r, _currentColor.g, _currentColor.b, _alpha)
new Color(c.r, c.g, c.b, g.color.a)
```

**Occurrences (6):**
- `ColorProgressBar.cs` (×2)
- `ProjectileView.cs`
- `ProjectileShieldView.cs`
- `ItemVisualView.cs`
- `GradientTextureDrawer.cs`

**Suggestion:** `ColorExtensions.WithAlpha(this Color c, float alpha)` in `Shared/Extensions/`.

---

## 3. Gradient-to-Texture Baking — Shared Helper

**Pattern:** Bake a `Gradient` to a 64×1 `Texture2D` with `Clamp` + `Bilinear`. Identical code in two places; both define `GradientResolution = 64`.

```csharp
var tex = new Texture2D(GradientResolution, 1, TextureFormat.RGBA32, false)
{
    wrapMode = TextureWrapMode.Clamp,
    filterMode = FilterMode.Bilinear
};
var pixels = new Color[GradientResolution];
for (var i = 0; i < GradientResolution; i++)
    pixels[i] = gradient.Evaluate(i / (float)(GradientResolution - 1));
tex.SetPixels(pixels);
tex.Apply();
```

**Occurrences (2):**
- `BushLeafBaker.cs` — `BakeGradientTexture()`
- `BushView.cs` — `GetBranchGradientTexture()`

**Suggestion:** `GradientTextureHelper.Bake(Gradient, int resolution = 64)` in `Shared/Rendering/`.

---

## 4. `IBalloonModel` Source Color Extraction — Extension

**Pattern:** Cast `_balloon` to `IHasColor`, null-propagate `.Color.Value`, fallback to `""`.

```csharp
var sourceColorId = (_balloon as IHasColor)?.Color.Value ?? "";
var balloonColor = _palette.GetColor((_balloon as IHasColor)?.Color.Value);
```

**Occurrences (6):**
- `BombItemHandler.cs` (×2)
- `LaserItemHandler.cs` (×2)
- `LightningItemHandler.cs`
- `ShieldItemHandler.cs`

**Suggestion:** `BalloonModelExtensions.GetColorId(this IBalloonModel)` returning `string` (empty if not `IHasColor`) in `Shared/Extensions/`.

---

## 5. `GetProfile` + `Stamp` Always Paired — Convenience Overload

**Pattern:** Every `Stamp` call is preceded by `GetProfile(StampSource.X)` to unpack radius/strength/duration.

```csharp
var stamp = _disturbanceSettings.GetProfile(StampSource.Bomb);
_disturbanceField.Stamp(_worldPosition, stamp.Radius, stamp.Strength, Vector2.zero, stamp.Duration);
```

**Occurrences (7):**
- `BalloonController.cs`
- `BalloonBalancer.cs`
- `BalloonSpawner.cs`
- `ProjectileView.cs`
- `BombItemHandler.cs`
- `PaintItemHandler.cs`
- `LaserItemHandler.cs`

**Suggestion:** `DisturbanceFieldService.Stamp(StampSource source, Vector3 pos, Vector2 dir)` overload that reads its own `_settings.GetProfile(source)` internally. Callers drop from 3 lines to 1.

---

## 6. `EvaluateHit` + `ActorHitMessage` Publish Combo

**Pattern:** Evaluate hit outcome on a model then immediately publish the result as a message.

```csharp
_hitPublisher.Publish(new ActorHitMessage(
    balloonView.Model,
    balloonView.transform.position,
    Vector3.zero,
    balloonView.Model.EvaluateHit(context),
    context));
```

**Occurrences (5):**
- `BombItemHandler.cs`
- `LaserItemHandler.cs`
- `LightningItemHandler.cs` (×2)
- `ProjectileView.cs`

**Suggestion:** `ActorHitMessage.From(IBalloonModel model, Vector3 pos, Vector3 dir, DamageContext ctx)` factory that calls `EvaluateHit` internally. Or an extension on `IPublisher<ActorHitMessage>`.

---

## 7. Squared Distance Check — `MathUtils` Addition

**Pattern:** Manual `dx * dx + dy * dy` for 2D proximity without `sqrt`.

```csharp
var dx = a.x - b.x;
var dy = a.y - b.y;
if (dx * dx + dy * dy > radiusSq) { ... }
```

**Occurrences (5):**
- `BushView.cs` (×2)
- `BushBakerWindow.cs`
- `BushLeafExtractor.cs`
- `PaintSplashPreviewModule.cs`

**Suggestion:** `MathUtils.SqrDistance2D(Vector2 a, Vector2 b)` or `MathUtils.WithinRadius(Vector2 a, Vector2 b, float radius)` in the existing `Shared/MathUtils.cs`.

---

## 8. Quad Mesh Creation — `MeshHelper`

**Pattern:** Create a unit quad mesh (center-pivoted or bottom-pivoted) with vertices, UVs, triangles, then `UploadMeshData(true)`.

```csharp
new Mesh
{
    vertices = new[] { (-0.5f,-0.5f,0), (0.5f,-0.5f,0), (0.5f,0.5f,0), (-0.5f,0.5f,0) },
    uv = new[] { (0,0), (1,0), (1,1), (0,1) },
    triangles = new[] { 0,1,2, 0,2,3 }
};
mesh.UploadMeshData(true);
```

**Occurrences (2 distinct quads):**
- `BushView.cs` — `GetBranchQuadMesh()` (center-pivoted) and `GetLeafQuadMesh()` (bottom-pivoted)

**Suggestion:** `MeshHelper.CreateQuad(PivotMode pivot)` in `Shared/Rendering/` with `Center` and `Bottom` options. Static, cached, uploaded.

---

## 9. `ContactFilter2D` + `Physics2D` + `GetComponentInParent<BalloonView>` — Physics Helper

**Pattern:** Set up a `ContactFilter2D` for balloons layer, run `OverlapCircle` or `CircleCast`, iterate results, extract `BalloonView` via `GetComponentInParent`, skip null/self.

```csharp
var count = Physics2D.OverlapCircle(pos, radius, _balloonFilter, _overlapResults);
for (var i = 0; i < count; i++)
{
    var balloonView = _overlapResults[i].GetComponentInParent<BalloonView>();
    if (balloonView == null || balloonView.Model == null) continue;
    if (balloonView.Model == _balloon) continue;
    ...
}
```

**Occurrences (2):**
- `BombItemHandler.cs` — `OverlapCircle`
- `LaserItemHandler.cs` — `CircleCast`

**Suggestion:** `BalloonPhysicsHelper.FindBalloonsInRadius(Vector3 pos, float radius, IBalloonModel exclude)` and `FindBalloonsAlongRay(...)` in `Shared/`. Returns `IReadOnlyList<(BalloonView, IBalloonModel)>`.

---

## 10. `DisturbanceFieldService` + `IDisturbanceFieldSettings` Double-Inject

**Pattern:** Many classes inject both `DisturbanceFieldService` and `IDisturbanceFieldSettings` just to call `GetProfile` + `Stamp`. If we add the convenience overload from #5, callers only need `DisturbanceFieldService`.

**Occurrences (7 classes inject both):**
- `BalloonController`
- `BalloonBalancer`
- `BalloonSpawner`
- `ProjectileView`
- `BombItemHandler`
- `PaintItemHandler`
- `LaserItemHandler`

**Suggestion:** Combined with #5. The service already holds `_settings` — expose `Stamp(StampSource, pos, dir)` and drop `IDisturbanceFieldSettings` injection from all 7 callers.

---

## Summary — Priority Order

| # | Pattern | Occurrences | Impact | Effort | Status |
|---|---|---|---|---|---|
| 1 | Pool fire-and-forget | 13 | High | Low | ✅ Done — `PoolManagerExtensions.PlayParticle/PlayEffect` |
| 2 | Color.WithAlpha | 6 | Medium | Trivial | ✅ Done — `ColorExtensions.WithAlpha` |
| 5+10 | Stamp convenience overload | 7+7 | High | Low | ✅ Done — `DisturbanceFieldService.Stamp(StampSource, pos, dir)` + `GetProfile()` |
| 4 | Source color extraction | 6 | Medium | Trivial | ✅ Done — `BalloonModelExtensions.GetColorId` |
| 6 | EvaluateHit + publish | 5 | Medium | Low | ✅ Done — `ActorHitMessage.From` |
| 7 | Squared distance | 5 | Low | Trivial | ✅ Done — `MathUtils.SqrDistance2D/WithinRadius` |
| 3 | Gradient texture bake | 2 | Low | Trivial | ✅ Done — `GradientTextureHelper.Bake` |
| 8 | Quad mesh creation | 2 | Low | Trivial | ✅ Done — `MeshHelper.CreateQuad(QuadPivot)` |
| 9 | Balloon physics query | 2 | Medium | Medium | ⏭ Skipped — patterns differ enough (OverlapCircle vs CircleCast, different post-processing) |


