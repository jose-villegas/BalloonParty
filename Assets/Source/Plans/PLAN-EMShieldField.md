@page plan_em_shield_field Electromagnetic Shield Field

# Electromagnetic Shield Field — procedural magnetosphere shader for projectile shields

> Replace the current sprite-stacking shield visual with a single-quad procedural shader
> rendering dipole magnetic field lines that wrap around the projectile. N configurable
> concentric shells dissolve apex-first when a shield is consumed.

---

## Visual Goal

Earth's magnetosphere deflecting solar wind — concentric field lines curving pole-to-pole
around a sphere, with the projectile's heading as the pole axis. Each shield count adds a
visible shell. Dissolution starts at the leading tip and peels backward (like popping an
atmosphere from its apex).

---

## Architecture Overview

```
IProjectileModel.ShieldsRemaining (ReactiveProperty<int>)
        │
        ▼
ProjectileShieldView (MonoBehaviour, subscribes via UniRx)
        │  ┌──────────────────────────────────────────────┐
        │  │ On change:                                    │
        │  │  • gain → tween dissolve[layer] 1→0          │
        │  │  • lose → tween dissolve[layer] 0→1          │
        │  └──────────────────────────────────────────────┘
        │
        ▼
MaterialPropertyBlock (pushed per-frame during tween)
        │
        ▼
EMShieldField.shader — single quad, 1 draw call
```

---

## Shader Design

### Approach: 2D Dipole Field-Line SDF

The fragment shader renders the 2D cross-section of a magnetic dipole. In polar coordinates,
field lines satisfy `r = R_max × sin²(θ)`. UV space maps:

- **V-axis** → heading axis (poles at V=0 and V=1)
- **U-axis** → lateral extent

For each layer `i` (unrolled loop, max 5):
1. Compute shell radius: `R = BaseRadius + i × LayerSpacing`
2. Evaluate field-line SDF at this layer's R
3. Check dissolve: `valueNoise(uv × NoiseScale) + (1 - V) × DirectionalBias < DissolveProgress[i]` → clip
4. Accumulate core + glow

### Dissolve Mechanism

Directional noise dissolve biased by V-coordinate:
- Apex (V≈1) has lowest threshold → dissolves first
- Base (V≈0) has highest → dissolves last
- Result: the shield "pops" from the tip and peels backward/downward

### Performance Budget

- **1 quad, 1 draw call** (replaces N SpriteRenderers)
- ~40 ALU instructions per layer × 5 layers = ~200 fragment instructions
- No texture fetches (pure math, hash-based noise)
- Fill: ~2× projectile screen area
- Well within mobile budget for a single on-screen quad

### Pulse Animation

`sin(_Time.y × PulseSpeed + layer × phaseOffset)` modulates line brightness/thickness per layer
to convey "living energy."

---

## Shader Properties

```hlsl
[HDR] _Color ("Field Tint", Color) = (0.5, 0.8, 1, 1)
_BaseRadius ("Base Shell Radius", Range(0.1, 0.5)) = 0.2
_LayerSpacing ("Layer Spacing", Range(0.02, 0.15)) = 0.06
_ActiveLayers ("Active Layers", Range(0, 5)) = 3
_FieldLineThickness ("Line Thickness", Range(0.002, 0.05)) = 0.015
_GlowWidth ("Glow Width", Range(0.01, 0.2)) = 0.06
_GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.0
_PulseSpeed ("Pulse Speed", Range(0, 10)) = 3.0
_NoiseScale ("Dissolve Noise Scale", Range(1, 20)) = 8.0
_DirectionalBias ("Dissolve Direction Bias", Range(0, 1)) = 0.6
// Per-layer dissolve progress: float array [0..4] via MPB (not in Properties block)
```

**Shader-side array declaration** (outside `Properties {}`, in the CGPROGRAM block):
```hlsl
float _DissolveProgress[5];  // pushed via MPB.SetFloatArray — arrays cannot be in Properties{}
```

### Pole Singularity Handling

At the poles (θ=0, θ=π) all field lines converge to r=0. The shader must fade out line
thickness as `sin²(θ)` approaches zero via `smoothstep` to avoid visual pinching artifacts.

---

## C# Integration

### Reworked `ProjectileShieldView`

- Replaces the `List<SpriteRenderer>` with a single `SpriteRenderer` + EM shield material
- Caches a `MaterialPropertyBlock` and a `float[5]` dissolve array (no GC)
- Property IDs cached as `static readonly int` (e.g., `Shader.PropertyToID("_DissolveProgress")`)
- On `ShieldsRemaining` change:
  - **Gain**: tweens `_layerDissolve[newLayer]` from 1→0 (appear)
  - **Lose**: tweens `_layerDissolve[oldLayer]` from 0→1 (dissolve)
- Pushes MPB each tween frame: `SetFloatArray("_DissolveProgress", _layerDissolve)`
- Quad orientation: child of projectile (inherits rotation automatically — no manual update)
- **Color tinting**: subscribes to `model.ColorName`, pushes tint via `MPB.SetColor` (or vertex color)
- **Zero shields / initial state**: renderer disabled (`enabled = false`) when shields = 0;
  re-enabled on first shield gained. Avoids paying a draw call for a fully-transparent quad.
- **`PlayBounceVfx(Vector3, Color)`** remains public — called externally by `ProjectileView`
- **Tween cleanup**: caches a `Tween _dissolveTween` reference; `Reset()` / `OnDespawned()` calls
  `_dissolveTween?.Kill()` — no `DOTween.Kill(object)` scan. Clears `CompositeDisposable`.

### New Configuration

```csharp
// Assets/Source/Configuration/Effects/IShieldFieldSettings.cs
public interface IShieldFieldSettings
{
    float LayerSpacing { get; }
    float DissolveSeconds { get; }
    float AppearSeconds { get; }
    float NoiseScale { get; }
    float FieldLineDensity { get; }
    float PulseSpeed { get; }
    float GlowIntensity { get; }
    float TintAlpha { get; }
    float BaseRadius { get; }
}
```

Registered in `GameLifetimeScope` as `RegisterInstance<IShieldFieldSettings>(asset)`.

---

## File Structure

```
Assets/
├── Shaders/BalloonParty/Display/
│   └── EMShieldField.shader              ← NEW
├── Source/Configuration/Effects/
│   ├── IShieldFieldSettings.cs           ← NEW
│   └── ShieldFieldSettings.cs            ← NEW (ScriptableObject)
├── Source/Projectile/View/
│   └── ProjectileShieldView.cs           ← MODIFIED (drives shader via MPB)
└── Configuration/
    └── ShieldFieldSettings.asset         ← NEW (authored in editor)
```

---

## Phases

### Phase 1 — Shader & Config Skeleton
- Create `EMShieldField.shader` with the dipole field-line loop and dissolve logic
- Create `IShieldFieldSettings` + `ShieldFieldSettings` SO
- Register in `GameLifetimeScope`

### Phase 2 — View Rework
- Rework `ProjectileShieldView` to use single SpriteRenderer + MPB
- Wire DOTween dissolve/appear animations to `ShieldsRemaining` subscription
- Preserve existing VFX spawning (gain/lose/bounce particles)

### Phase 3 — Tuning & Polish
- Author `ShieldFieldSettings.asset` with sensible defaults
- Tune noise scale, directional bias, glow, pulse speed in editor
- Verify on target mobile GPU (requires in-editor playtest)

---

## Performance Notes (from optimizer review)

- **True ALU budget**: closer to 250–300 instructions (not 200). `atan2` = ~10 ALU on Mali,
  hash noise × 5 = ~50 ALU, plus SDF/smoothstep/glow. Still within mobile ceiling (~400 max
  for a single transparent quad at ≤5% screen fill).
- **Quality toggle**: add `_MaxVisualLayers` in settings — if profiling exceeds 0.3 ms on
  target GPU, drop to 3 visual layers.
- **Dual-quad overlap**: during PIERCING state, `PierceConeSpiral` (~80 ALU) may overlap with
  this shader (~300 ALU). Confirm whether shields + piercing coexist in gameplay. If yes,
  consider reducing glow ops during piercing or shrinking the quad to avoid overlap.
- **Noise fallback**: provide `#pragma multi_compile _ _NOISE_TEXTURE` so hash noise can be
  swapped for a 64×64 R8 texture fetch if needed on older Mali/PowerVR GPUs.
- **Net improvement**: replaces 3–5 draw calls with 1. The fragment ALU increase is paid on
  tiny fill (~1% screen pixels). Strict batching win.

---

## Alternatives Rejected

| Alternative | Reason |
|-------------|--------|
| Mesh-based ring generation | N draw calls, doesn't match quad-procedural pattern |
| Particle system per layer | Harder to synchronize dissolve, more expensive |
| Multi-pass shader | N draw calls, overdraw compounds |
| Geometry shader | Not supported on mobile GPUs |
| Shared material instance | Violates MPB convention for per-instance properties |

---

## Notes

- `dotnet build` cannot validate shaders — shader edits need in-editor verification.
- The quad must be sized to cover the full field extent (~2× projectile radius).
- Max 5 visual layers recommended; beyond that, ALU budget gets tight on low-end mobile.
  For >5 shields, outer layers can merge visually (brighter outer shell).
