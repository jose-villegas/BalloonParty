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

### Velocity-Driven Dynamics (Phase 3)

The shader warps UVs before the field-line SDF evaluation, giving the field a reactive, physical
feel:

1. **Ripple** — `sin(V × RippleFrequency + _Time.y × RippleSpeed) × RippleAmplitude × VelocityFactor`
   offsets U, creating velocity-gated sine waves along the heading axis.
2. **Directional lean** — `_DeformDirection.xy × LeanStrength × pow(1 - V, LeanBendPower)` bends
   the field toward the trailing edge. The progressive power curve (`LeanBendPower > 1`) means
   the dome barely moves while the tail sweeps wide — like a flag trailing in wind.
3. **Noise scroll** — dissolve noise UV scrolls at `_NoiseScrollSpeed × _Time.y`, adding subtle
   turbulence independent of the warp.

The deformation is driven by a **3-point trailing system** rather than a single lean vector:
three `DampedSpring2D` instances (fast, mid, slow) track the projectile's heading at different
spring frequencies. Each spring's offset from the current facing is projected into quad-local
space, packed into a `_DeformPoints` vector array, and pushed per frame. The shader reads
`[0]` = dome (fast settle), `[1]` = mid, `[2]` = tail (most lag) — this gives the field a
physically convincing trailing shape rather than a uniform tilt.

A **squash-on-impact** effect uses a `DampedSpring1D` (scalar spring, `Shared/Math/`) whose
rest position is zero (no squash). `OnBounce` injects an impulse proportional to the direction
change magnitude (`(1 − dot) × 0.5 × SquashImpulseScale`); the spring overshoots and oscillates
back to rest, compressing the field along the impact normal. The shader reads `_SquashMag`,
`_SquashStrength`, and `_SquashNormal` to apply an oriented UV compression.

On bounce, lean impulses are injected into the fast and slow springs (slow receives a reduced
fraction) and a separate noise-scroll spring. The sqrt velocity-factor curve ensures the ripple
activates quickly at low speeds but saturates gracefully.

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

// Dynamics (Phase 3)
_VelocityFactor ("Velocity Factor", Range(0, 1)) = 0
_RippleAmplitude ("Ripple Amplitude", Range(0, 0.1)) = 0.02
_RippleFrequency ("Ripple Frequency", Range(1, 20)) = 8.0
_RippleSpeed ("Ripple Speed", Range(0, 20)) = 5.0
_LeanStrength ("Lean Strength", Range(0, 1)) = 0.3
_LeanBendPower ("Lean Bend Power", Range(1, 5)) = 2.0
_DeformDirection ("Deform Direction", Vector) = (0, 0, 0, 0)
_RevealEdge ("Reveal Edge Softness", Range(0.01, 0.3)) = 0.05
_NoiseScrollSpeed ("Noise Scroll Speed", Range(0, 5)) = 1.0
[Toggle] _EditorPreview ("Editor Preview", Float) = 0
// Per-layer dissolve/reveal progress: float arrays [0..4] via MPB (not in Properties block)
```

**Shader-side array declarations** (outside `Properties {}`, in the CGPROGRAM block):
```hlsl
float _DissolveProgress[5];  // pushed via MPB.SetFloatArray — arrays cannot be in Properties{}
float _RevealProgress[5];    // reveal wipe per layer (0 = hidden, 1 = fully revealed)
```

### Pole Singularity Handling

At the poles (θ=0, θ=π) all field lines converge to r=0. The shader must fade out line
thickness as `sin²(θ)` approaches zero via `smoothstep` to avoid visual pinching artifacts.

---

## C# Integration

### Reworked `ProjectileShieldView`

- Replaces the `List<SpriteRenderer>` with a single `SpriteRenderer` + EM shield material
- Caches a `MaterialPropertyBlock` and `float[5]` dissolve + reveal arrays (no GC)
- Property IDs cached as `static readonly int` (e.g., `Shader.PropertyToID("_DissolveProgress")`)
- On `ShieldsRemaining` change:
  - **Gain**: sets dissolve to 0, tweens `_layerReveal[newLayer]` from 0→1 (reveal wipe, apex→tail)
  - **Lose**: tweens `_layerDissolve[oldLayer]` from 0→1 (noise dissolve)
- Pushes MPB each tween frame: `SetFloatArray` for both `_DissolveProgress` and `_RevealProgress`
- All shader properties are funnelled through `PushProperties()` — both tween callbacks and
  `Update()` call it, guaranteeing every `SetPropertyBlock` writes the full uniform set
  (see [Architectural Lesson](#architectural-lesson--single-push-materialpropertyblock))
- **Per-frame dynamics** (`Update`): steps a 3-point trailing spring system
  (`DampedSpring2D` × 3 at fast/mid/slow rates) and a scalar squash spring (`DampedSpring1D`),
  transforms each into quad-local space, and pushes `_DeformPoints` + `_VelocityFactor` +
  squash uniforms via the shared `PushProperties()` call
- **`OnBounce(Vector2 oldDir, Vector2 newDir)`** — called by `ProjectileView` on wall bounce and
  balloon deflect. Injects lean impulses into the trailing springs and a squash impulse
  proportional to direction change severity; clamped to unit magnitude
- Quad orientation: child of projectile (inherits rotation automatically — no manual update)
- **Color tinting**: subscribes to `model.ColorName`, pushes tint via `MPB.SetColor`
- **Zero shields / initial state**: renderer disabled (`enabled = false`) when shields = 0;
  re-enabled on first shield gained. Avoids paying a draw call for a fully-transparent quad.
- **`PlayBounceVfx(Vector3, Color)`** remains public — called externally by `ProjectileView`
- **Tween cleanup**: caches a `Tween _dissolveTween` reference; `Reset()` / `OnDespawned()` calls
  `_dissolveTween?.Kill()` — no `DOTween.Kill(object)` scan. Clears `CompositeDisposable`.

### New Configuration

```csharp
// Assets/Source/Configuration/Effects/IShieldFieldSettings.cs
internal interface IShieldFieldSettings
{
    float DissolveSeconds { get; }
    float FinalDissolveSeconds { get; }   // slower dissolve for the last shield
    float AppearSeconds { get; }
    int MaxVisualLayers { get; }
    float MaxVisualSpeed { get; }         // speed ceiling for the velocity-factor curve
    float SpringFrequency { get; }        // fast (dome) spring Hz
    float SpringDamping { get; }          // fast spring damping ratio
    float SpringFrequencySlow { get; }    // slow (tail) spring Hz
    float SpringDampingSlow { get; }      // slow spring damping ratio
    float LeanImpulseScale { get; }       // multiplier on direction-change → lean push
    float NoiseSpringFrequency { get; }   // noise-scroll spring Hz
    float NoiseSpringDamping { get; }     // noise-scroll spring damping
    float SquashStrength { get; }         // shader-side UV compression strength
    float SquashFrequency { get; }        // squash spring Hz
    float SquashDamping { get; }          // squash spring damping ratio
    float SquashImpulseScale { get; }     // bounce severity → squash impulse
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

### Phase 1 — Shader & Config Skeleton ✓
- Create `EMShieldField.shader` with the dipole field-line loop and dissolve logic
- Create `IShieldFieldSettings` + `ShieldFieldSettings` SO
- Register in `GameLifetimeScope`

### Phase 2 — View Rework ✓
- Rework `ProjectileShieldView` to use single SpriteRenderer + MPB
- Wire DOTween dissolve/appear animations to `ShieldsRemaining` subscription
- Preserve existing VFX spawning (gain/lose/bounce particles)

### Phase 3 — Dynamics ✓
- **3-point trailing system**: three `DampedSpring2D` instances (fast dome / mid EMA / slow
  tail) replaced the single lean-vector approach. Packed into `_DeformPoints` vector array
  (pushed per frame via MPB). Gives the field a convincing trailing shape on direction change.
- **UV warp system**: velocity-driven ripple + directional lean bend
  - `_VelocityFactor` (sqrt of normalized speed) modulates ripple amplitude
  - `_RippleAmplitude/Frequency/Speed` drive a sine warp along the V-axis
  - `_LeanStrength` + `_LeanBendPower` apply a progressive power-curve bend from dome to tail,
    stronger at the trailing edge — the field "trails behind" the projectile on direction change
- **Reveal wipe**: layer gain now animates a directional wipe (apex→tail) via `_RevealProgress`
  array; layer loss still uses the existing noise dissolve via `_DissolveProgress`
- **Editor preview toggle**: `_EditorPreview` property lets artists see deformation in Scene view
  without play mode

### Phase 4 — Squash-on-Impact ✓
- **`DampedSpring1D`** (`Shared/Math/`) — new scalar spring (mirrors `DampedSpring2D`'s API).
  Rests at zero; `OnBounce` injects an impulse proportional to direction change severity.
  The spring overshoots and oscillates back, compressing the field along the impact normal.
- Shader reads `_SquashMag`, `_SquashStrength`, `_SquashNormal` for oriented UV compression.
- `FinalDissolveSeconds` added to `IShieldFieldSettings` for a slower last-shield dissolve.

### Phase 5 — Tuning & Polish
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

## Architectural Lesson — Single-Push MaterialPropertyBlock

Early iterations had two independent code paths calling `SetPropertyBlock` on the same
`MaterialPropertyBlock`: tween callbacks wrote dissolve/reveal/color, while `Update()` wrote
deform/squash/velocity. Each path only set its own subset of properties before calling
`SetPropertyBlock`, so whichever ran second would push a block missing the other's latest
values — making new shader features (squash, deform points) silently invisible whenever a
tween was active.

**Fix:** a single `PushProperties()` method writes **every** uniform onto the block before
calling `SetPropertyBlock`. Both `Update()` and all tween callbacks call `PushProperties()`
instead of writing their own subset. This guarantees each push is a complete snapshot.

**Rule:** when multiple code paths share a `MaterialPropertyBlock`, always funnel through one
method that writes the full property set. Never let two paths each write a subset and race to
`SetPropertyBlock`.

---

## Notes

- `dotnet build` cannot validate shaders — shader edits need in-editor verification.
- The quad must be sized to cover the full field extent (~2× projectile radius).
- Max 5 visual layers recommended; beyond that, ALU budget gets tight on low-end mobile.
  For >5 shields, outer layers can merge visually (brighter outer shell).
