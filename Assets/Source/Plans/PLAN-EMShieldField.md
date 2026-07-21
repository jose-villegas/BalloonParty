@page plan_em_shield_field Electromagnetic Shield Field

# Electromagnetic Shield Field — procedural magnetosphere shader for projectile shields

> The projectile's shield is a glowing force field — visible concentric rings wrapping around
> the ball, rendered by a single procedural shader (`EMShieldField.shader`). Each shield the
> ball carries adds a ring. The field stretches into a comet during travel and tucks into a
> circle at wall bounces. When a shield is lost, its ring dissolves away starting from the
> leading tip.

---

## Visual Goal

Earth's magnetosphere deflecting solar wind — concentric glowing rings curving pole-to-pole
around a sphere, with the projectile's heading as the pole axis. Each shield count adds a
visible ring. When a ring dissolves, it starts at the leading tip and peels backward (like
popping an atmosphere from its apex).

During travel the field stretches into a comet with a trailing tail. As the projectile
approaches a wall, the field smoothly closes into a circle (bracing for impact), holds
that shape through the bounce, then opens back into the comet as it flies away.

---

## Architecture Overview

```
IProjectileModel.ShieldsRemaining (ReactiveProperty<int>)
        │
        ▼
ProjectileShieldView (MonoBehaviour, subscribes via UniRx)
        │  ┌──────────────────────────────────────────────────┐
        │  │ On ShieldsRemaining change:                       │
        │  │  • gain → tween dissolve[layer] 1→0              │
        │  │  • lose → tween dissolve[layer] 0→1              │
        │  │ Per frame:                                        │
        │  │  • step noise spring + squash spring + morph FSM  │
        │  │  • WriteAllProperties() → SetPropertyBlock()      │
        │  └──────────────────────────────────────────────────┘
        │
        ▼
MaterialPropertyBlock (10 uniforms pushed per frame)
        │
        ▼
EMShieldField.shader — single quad, 1 draw call
```

### Shape Morph FSM

The field's outline is controlled by `_ShapeLerp` (0 = circle, 1 = comet-with-tail).
A four-state machine drives the transitions:

```
Cruising (sl=1)  ──wall < MorphCloseDistance──▶  Closing (sl: 1→0)
      ▲                                               │
      │                                          closeT ≥ 1
      │                                               ▼
Opening (sl: 0→1)  ◀──braceDuration elapsed──  Bracing (sl=0)
```

`OnBounce` force-snaps to **Bracing** regardless of the current state, so the field is
always a circle at the moment of impact. Wall proximity is predicted each frame via
`WallLimits.TryFindCrossing`.

---

## Shader Design

### Approach: 2D Dipole Field-Line SDF

The fragment shader renders the 2D cross-section of a magnetic dipole. In polar coordinates,
field lines satisfy `r = R_max × sin²(θ)`. UV space maps:

- **V-axis** → heading axis (poles at V=0 and V=1)
- **U-axis** → lateral extent

For each layer `i` (loop, max 30):
1. Compute shell radius: `R = BaseRadius + i × LayerSpacing`
2. Evaluate field-line SDF at this layer's R
3. Check dissolve: `valueNoise(uv × NoiseScale) + (1 - V) × DirectionalBias < DissolveProgress[i]` → clip
4. Accumulate core + glow

### Comet Shape

The shader blends between two outlines controlled by `_ShapeLerp`:

- **Circle** (`sl = 0`) — defined by `_CircleRadius` and `_CircleCenter`, masked by
  `_CircleMaskWidth`. Used when bracing for or recovering from a bounce.
- **Comet** (`sl = 1`) — the dome plus a tapered tail (`_TailLength`, `_TailWidth`,
  `_TailPower`), smoothed at the junction (`_JunctionSmooth`). `_CometWidthScale`
  controls lateral spread.

### Dissolve Mechanism

Directional noise dissolve biased by V-coordinate:
- Apex (V≈1) has lowest threshold → dissolves first
- Base (V≈0) has highest → dissolves last
- Result: the shield "pops" from the tip and peels backward/downward

### Performance Budget

- **1 quad, 1 draw call** (replaces N SpriteRenderers)
- ~40 ALU instructions per layer × active layers (typically 5–10 of 30 max)
- No texture fetches (pure math, hash-based noise)
- Fill: ~2× projectile screen area
- Well within mobile budget for a single on-screen quad

### Pulse Animation

`sin(_Time.y × PulseSpeed + layer × phaseOffset)` modulates line brightness/thickness per layer
to convey "living energy."

### Velocity-Driven Dynamics

The shader warps UVs before the field-line SDF evaluation, giving the field a reactive, physical
feel:

1. **Ripple** — `sin(V × RippleFrequency + _Time.y × RippleSpeed) × RippleAmplitude × VelocityFactor`
   offsets U, creating velocity-gated sine waves along the heading axis.
2. **Noise scroll** — a `DampedSpring2D` tracks the projectile's heading and produces a
   smoothed direction offset (`_NoiseScrollDir`). The dissolve noise UV scrolls in this
   direction, adding subtle turbulence that trails behind turns.

The sqrt velocity-factor curve (`_VelocityFactor = sqrt(speed / MaxVisualSpeed)`) ensures the
ripple activates quickly at low speeds but saturates gracefully.

---

## Shader Properties

### Comet Shape
```hlsl
_DomeCenter, _DomeRadius            // dome origin and size
_CircleRadius, _CircleCenter        // circle mode origin and size
_TailLength, _TailWidth, _TailPower // tapered tail geometry
_JunctionSmooth                     // dome–tail blend
_CometWidthScale                    // lateral spread of comet
_ShapeLerp                          // 0 = circle, 1 = comet (driven by MPB)
```

### Shells
```hlsl
_BaseRadius      // innermost shell radius
_LayerSpacing    // gap between concentric shells
_ActiveLayers    // visible layer count (driven by MPB)
```

### Line Appearance
```hlsl
_FieldLineThickness, _GlowWidth, _GlowIntensity, _PulseSpeed
```

### Flow
```hlsl
_FlowSpeed, _FlowFrequency, _FlowStrength
```

### Layer Color
```hlsl
_ColorShift, _ColorPhase
```

### Dissolve
```hlsl
_NoiseScale ("Dissolve Noise Scale", Range(1, 100))
_NoiseScrollSpeed
[Toggle] _NoiseEnabled
_NoiseVelocityIntensity
_NoiseStartLayer
_DirectionalBias
```

### Reveal
```hlsl
_RevealEdge
```

### Deformation
```hlsl
_VelocityFactor      // sqrt-normalized speed (driven by MPB)
_RippleAmplitude, _RippleFrequency, _RippleSpeed
_SquashAmount        // 0–1, spring-driven compression (driven by MPB)
_SquashAxis          // Vector2, impact surface normal in UV space (driven by MPB)
```

### Tip & Shape Mask
```hlsl
_TipFade
_MaskCenterV, _MaskWidth, _CircleMaskWidth, _MaskHeight, _MaskRoundness, _MaskFade
```

### Per-Layer Arrays (pushed via MPB, not in Properties block)
```hlsl
float _DissolveProgress[30];   // noise dissolve per layer (0 = solid, 1 = gone)
float _RevealProgress[30];     // reveal wipe per layer (0 = hidden, 1 = shown)
```

### MPB Uniforms (10 values pushed from C# each frame)
| Uniform | Type | Source |
|---------|------|--------|
| `_DissolveProgress[]` | float[30] | Tween-driven per-layer dissolve |
| `_RevealProgress[]` | float[30] | Tween-driven per-layer reveal wipe |
| `_ActiveLayers` | float | `IShieldFieldSettings.MaxVisualLayers` |
| `_Color` | Color | Current projectile color |
| `_VelocityFactor` | float | `sqrt(speed / MaxVisualSpeed)` |
| `_NoiseScrollDir` | Vector4 | Noise spring offset in quad-local space |
| `_ShapeLerp` | float | Morph FSM output (0 = circle, 1 = comet) |
| `_NoiseIntensity` | float | Velocity-scaled noise strength |
| `_SquashAmount` | float | `abs(squashSpring.Position)`, clamped 0–1 |
| `_SquashAxis` | Vector4 | Impact normal in local UV space |

### Pole Singularity Handling

At the poles (θ=0, θ=π) all field lines converge to r=0. The shader fades out line
thickness as `sin²(θ)` approaches zero via `smoothstep` to avoid visual pinching artifacts.

---

## C# Integration

### `ProjectileShieldView`

- Single `SpriteRenderer` + EM shield material; single `MaterialPropertyBlock`
- `float[30]` dissolve + reveal arrays (pre-allocated, no GC)
- Property IDs cached as `static readonly int`
- On `ShieldsRemaining` change:
  - **Gain**: sets dissolve to 0, tweens `_layerReveal[newLayer]` from 0→1 (reveal wipe, apex→tail)
  - **Lose**: tweens `_layerDissolve[oldLayer]` from 0→1 (noise dissolve)
- All shader properties are funnelled through `WriteAllProperties()` — both tween callbacks and
  `Update()` call it, guaranteeing every `SetPropertyBlock` writes the full uniform set
  (see [Architectural Lesson](#architectural-lesson--single-push-materialpropertyblock))
- **Per-frame dynamics** (`Update`): steps the noise-scroll spring (`DampedSpring2D`), runs the
  morph FSM (`UpdateMorphState`), computes `_VelocityFactor`, and pushes all 7 uniforms via
  `WriteAllProperties()`
- **Shape morph**: `UpdateMorphState` runs the Cruising→Closing→Bracing→Opening FSM.
  `ComputeWallDistance` uses `WallLimits.TryFindCrossing` to predict how far the next wall is
  along the current heading. When that distance drops below `MorphCloseDistance`, the field
  begins closing from comet to circle.
- **`OnBounce(Vector2 oldDir, Vector2 newDir, float speed)`** — called by `ProjectileView` on
  wall bounce and balloon deflect. Force-snaps to **Bracing** (circle shape, `_ShapeLerp = 0`)
  and injects a squash impulse into the spring scaled by `speed / MaxVisualSpeed`.
- Quad orientation: child of projectile (inherits rotation automatically)
- **Color tinting**: subscribes to `model.ColorName`, pushes tint via `MPB.SetColor`
- **Zero shields / initial state**: renderer disabled when shields = 0; re-enabled on first
  shield gained. Avoids paying a draw call for a fully-transparent quad.
- **`PlayBounceVfx(Vector3, Color)`** remains public — called externally by `ProjectileView`
- **Tween cleanup**: `Reset()` kills tweens via `DOTween.Kill(this)`, clears
  `CompositeDisposable`, and resets all spring/morph state to defaults.

### Configuration

```csharp
// Assets/Source/Configuration/Effects/IShieldFieldSettings.cs
internal interface IShieldFieldSettings
{
    float DissolveSeconds { get; }
    float FinalDissolveSeconds { get; }   // slower dissolve for the last shield
    float AppearSeconds { get; }
    int MaxVisualLayers { get; }           // up to 30
    float MaxVisualSpeed { get; }          // speed ceiling for the velocity-factor curve
    float NoiseSpringFrequency { get; }    // noise-scroll spring Hz
    float NoiseSpringDamping { get; }      // noise-scroll spring damping
    float MorphCloseDistance { get; }      // wall distance that triggers closing
    float MorphCloseDuration { get; }      // seconds to morph from comet to circle
    float MorphOpenDuration { get; }       // seconds to morph from circle back to comet
    float MorphBraceDuration { get; }      // seconds to hold the circle shape after closing
    float SquashFrequency { get; }         // spring oscillation Hz (default 14)
    float SquashDamping { get; }           // underdamped for visible bounce-back (default 0.35)
    float SquashImpulseStrength { get; }   // base impulse before speed scaling (default 18)
}
```

Registered in `GameLifetimeScope` as `RegisterInstance<IShieldFieldSettings>(asset)`.

---

## File Structure

```
Assets/
├── Shaders/BalloonParty/Display/
│   └── EMShieldField.shader
├── Source/Configuration/Effects/
│   ├── IShieldFieldSettings.cs
│   └── ShieldFieldSettings.cs            (ScriptableObject)
├── Source/Projectile/View/
│   └── ProjectileShieldView.cs           (drives shader via MPB)
└── Configuration/
    └── ShieldFieldSettings.asset
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
- **Noise-scroll spring**: a single `DampedSpring2D` tracks the projectile's heading to produce
  a smoothed scroll direction for dissolve noise. Pushed per frame as `_NoiseScrollDir`.
- **Velocity-driven ripple**: `_VelocityFactor` (sqrt of normalized speed) modulates
  `_RippleAmplitude/Frequency/Speed` for sine-wave warping along the V-axis.
- **Reveal wipe**: layer gain animates a directional wipe (apex→tail) via `_RevealProgress`;
  layer loss uses noise dissolve via `_DissolveProgress`.

### Phase 4 — Shape Morph ✓
- **Comet/circle morph**: `_ShapeLerp` blends between a tailed comet shape (travel) and a
  circle (bounce). A 4-state FSM (Cruising → Closing → Bracing → Opening) drives the
  transitions based on wall proximity and bounce events.
- **Wall prediction**: `ComputeWallDistance` uses `WallLimits.TryFindCrossing` to detect
  upcoming walls. When the wall is closer than `MorphCloseDistance`, the field begins closing.
- **Bounce snap**: `OnBounce` force-snaps to **Bracing** (circle) regardless of current state.
- Max visual layers increased from 5 to 30.
- Removed: 3-point trailing springs, lean/deform direction, dome overlay — replaced by the
  simpler morph approach.

### Phase 4b — Squash-on-Impact ✓
- **Squash deformation**: on bounce, the circle-mode shield visually compresses against the
  impact surface then springs back. A `DampedSpring1D` (underdamped, 14 Hz default) drives
  `_SquashAmount`; the impact normal — transformed to local UV space — sets `_SquashAxis`.
- **UV axis scaling** (shader): decomposes polar `p` into along/perpendicular components
  relative to `_SquashAxis`, compresses along and expands perpendicular (area-preserving:
  `compress × expand ≈ 1`). Max 25% compression; ~4 ALU cost.
- **Circle-only guard**: squash fades by `(1 - sl)`, so it is only visible while the field is
  in circle mode (Bracing state) and naturally disappears during comet morph.
- **Speed-scaled impulse**: `OnBounce(oldDir, newDir, speed)` computes the wall/deflector
  normal as `normalize(newDir - oldDir)`, scales the impulse by `speed / MaxVisualSpeed`, and
  injects it into the spring. Works for both axis-aligned wall bounces and radial deflector
  bounces — the formula naturally yields the correct surface normal in both cases.

### Phase 5 — Tuning & Polish
- Author `ShieldFieldSettings.asset` with sensible defaults
- Tune noise scale, directional bias, glow, pulse speed in editor
- Verify on target mobile GPU (requires in-editor playtest)

---

## Performance Notes

- **ALU budget**: ~40 ALU per layer. With 30 max layers but typically 5–10 active
  (`_ActiveLayers` via MPB), expect ~200–400 fragment instructions in practice.
- **Quality toggle**: `MaxVisualLayers` in settings caps how many layers render — drop to 3
  if profiling exceeds 0.3 ms on target GPU.
- **Dual-quad overlap**: during PIERCING state, `PierceConeSpiral` (~80 ALU) may overlap with
  this shader. Consider reducing glow ops during piercing if both coexist.
- **Noise fallback**: `#pragma multi_compile _ _NOISE_TEXTURE` allows swapping hash noise for
  a 64×64 R8 texture fetch on older Mali/PowerVR GPUs.
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
dynamics uniforms. Each path only set its own subset of properties before calling
`SetPropertyBlock`, so whichever ran second would push a block missing the other's latest
values — making new shader features silently invisible whenever a tween was active.

**Fix:** a single `WriteAllProperties()` method writes **every** uniform onto the block before
calling `SetPropertyBlock`. Both `Update()` and all tween callbacks call `WriteAllProperties()`
instead of writing their own subset. This guarantees each push is a complete snapshot.

**Rule:** when multiple code paths share a `MaterialPropertyBlock`, always funnel through one
method that writes the full property set. Never let two paths each write a subset and race to
`SetPropertyBlock`.

---

## Notes

- `dotnet build` cannot validate shaders — shader edits need in-editor verification.
- The quad must be sized to cover the full field extent (~2× projectile radius).
- Max 30 layers declared in arrays; `MaxVisualLayers` in settings controls how many actually
  render. Typically 5–10 active layers in gameplay.
