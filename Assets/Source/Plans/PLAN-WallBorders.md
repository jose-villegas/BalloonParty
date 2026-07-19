@page plan_wall_borders Wall Borders — four tennis nets that billow

# Wall Borders — the play-area frame as four deforming tennis nets

> Replaces the static top/bottom border sprites with four procedural **net-strip** meshes — one
> per play-area edge — that billow where the projectile strikes and idle-breathe at rest. All
> motion is a vertex-shader tap on the existing disturbance field: no CPU sim, no wall-hit event,
> no new plumbing. Art direction is **dreamy/floaty**, not sharp neon — the glow comes from HDR +
> bloom, not saturation.

---

## 1. What exists today, and what it replaces

The "walls" are not a system — they are two hand-authored `SpriteRenderer` children in
`Assets/Prefabs/Game/Scenario.prefab`: `Top` (block sprite, localPos `(0,0)`, scale `(5,1)`) and
` Bottom` (localPos `(0,-6.75)`, scale `(5,1)`), sorting layer 6. **There is no left/right wall
sprite** — the side bounds exist only logically. No View or Controller sizes these from config;
the visual frame and the logical frame are independently authored and hand-synced.

The logical rectangle is `IGameConfiguration.LimitsClockwise` (a `Vector4`,
`Assets/Source/Shared/IGameConfiguration.cs:15`), unpacked by the `WallLimits` readonly struct
(`Assets/Source/Shared/WallLimits.cs:9`) as `x=Top, y=Right, z=Bottom, w=Left`. This is the same
rectangle the projectile billiard reflects off in `WallLimits.Reflect()` (line 98) — the single
source of truth for where the walls are.

This plan makes all four edges visible for the first time, sourced from that rectangle, and
retires the `Top`/` Bottom` block sprites.

## 2. Geometry — four procedural net-strip meshes

Each edge is **not** a line — it is a **net-strip**: a band of width `W` running the length of the
edge, tessellated into a quad grid fine enough to deform smoothly. Four strips (top, right,
bottom, left), each built **once** at startup from `WallLimits`, laid inward from the edge.

| Parameter | Proposed budget | Rationale |
|---|---|---|
| Strip width `W` | ~0.4–0.6 wu | wide enough to read as a net, narrow enough not to eat the play area |
| Cells along edge | ~2 cells / wu (long edge ≈ 9 wu → ~18–20 cells) | fine enough that the billow reads as a smooth wave, not a fold |
| Cells across width | ~3–4 | enough rows for the net weave + a smooth perpendicular billow |
| Verts / strip | ~80–100 | four strips ≈ 350–400 verts total — trivial |

> **This introduces the project's first procedural `new Mesh()` construction.** Nothing in
> `Assets/Source/` builds a mesh today — the closest precedents are `LineRenderer`
> (`PredictionTraceView`) and `TrailRenderer` (`FlyingTrail`), neither of which fits a deformable
> quad band. The mesh build is plain, allocation-once C# (positions, UVs, triangle indices set at
> startup and never rewritten from the CPU — all subsequent motion is in the shader), so it is
> `dotnet build`-checkable even though the look is not.

UVs run `0→1` across the strip width and tile along its length, so the fragment net-pattern and
the vertex idle-phase both key off a stable per-strip parameterisation regardless of edge length.

## 3. Shader — one world-space material, all animation on the GPU

One material shared by all four strips. World-space so it can tap the screen-space disturbance and
light fields directly (`#pragma target 3.5` for the vertex texture read — the BushLeaf precedent).

### Vertex stage — deform by sampling the disturbance field
The disturbance field is a camera-sized `ARGBHalf` RT republished every blit as the global
`_DisturbanceTex` (**R = density, G/B = displacement XY, 0.5-biased ±0.5**), covering the full
orthographic viewport — so it already covers the wall lines. See
`Assets/Source/Shared/Disturbance/README.md`.

Per vertex, at its **rest world position**:
1. `float2 uv = (worldPos - _FieldBoundsMin) / _FieldBoundsSize;`
2. `float2 disp = tex2Dlod(_DisturbanceTex, float4(uv,0,0)).gb * 2 - 1;` — the un-biased displacement.
3. Displace the vertex **perpendicular to its edge** (inward normal from `WallLimits`, passed per
   strip) by `dot`/magnitude of `disp`, scaled by an amplitude uniform.

This is the exact mechanism of `BushLeaf.shader:150-171` (`_RATTLE_ON`), which taps
`_DisturbanceTex` in the vertex stage and converts the GB displacement to motion — proven, shipped,
target 3.5.

**Idle-breathing** (added to the same perpendicular offset): a low-amplitude
`sin(_Time.y * f + phase)` where `phase` varies along the strip (from the length-UV), so the net
gently undulates even at rest. Amplitude is a small fraction of the hit-billow amplitude — a slow
floaty shimmer, never a distraction.

### Fragment stage — the tennis-net look + dreamy glow
- **Net pattern:** procedural grid lines from the strip UV — `frac()` the tiled UV on both axes,
  `smoothstep` a thin line band at each cell boundary, union the two axes. This is the cleanest
  path (no texture asset, crisp at any resolution). *(Alt: a tiling net texture on `_MainTex` if a
  woven/organic thread look is wanted later.)*
- **Colour:** net pattern × `SceneLightTintAtLOD(worldPos)` (`Assets/Shaders/BalloonParty/Include/SceneLight.cginc:296`)
  so the net picks up the scene palette and warms near local lights — **receive only, no
  `RegisterLight`/cast**. × an HDR emissive term > 1 so bloom blooms it.
- **Blend:** soft additive or alpha; the "dreamy, not neon" read leans on **HDR + bloom**, not on
  channel saturation — keep the authored colour gentle and let the pipeline glow it.

## 4. Why no broadcast and no CPU sim

The projectile **already** stamps `StampSource.ProjectileImpact` at `step.WallContact` on every
bounce and on the fatal hit (`Assets/Source/Projectile/View/ProjectileView.cs:393` and `:406`),
plus a continuous `Projectile` wake each step (`:414`). So the disturbance field already carries a
radial impulse **at the wall contact point** — the net billows there for free, and also reacts to
pops, bombs, laser, and the passing wake near the edge, all from the one field. A dedicated
`WallHitMessage` or a `ShieldLostMessage` subscriber would be a redundant parallel source; the
field is the single source of truth. Because the tap is entirely in the vertex shader, there is no
CPU sim, no Verlet, and no `AsyncGPUReadback` — **zero per-frame CPU**.

The one property the field does not give us is a long plucked-string ring: its local displacement
diffuses within a fraction of a second (it is a cloud-reform sim). That is *accepted* — the target
here is a soft dreamy billow that settles as the field settles, plus the idle breathing, not a
twanging string. If a longer ring is ever wanted, tune the `ProjectileImpact` stamp profile so the
field itself plucks harder — still no new event.

## 5. MVC / DI fit

| Layer | Piece | Notes |
|---|---|---|
| **View** (`MonoBehaviour`) | `WallNetView` | Owns the four meshes + the shared material; builds the meshes in `Awake`/`Start` from injected `IGameConfiguration` → `new WallLimits(config.LimitsClockwise)`. Parented under the Scenario hierarchy in place of the retired block sprites. |
| **Controller** | *(none)* | Pure shader — there is no CPU simulation state to own. |
| **Messaging** | *(none)* | Field-driven; no MessagePipe subscriber. |

Registered with `builder.RegisterComponentInHierarchy<WallNetView>()` in
`GameScopeRegistration` — the pattern used for `SpeckField`, `SlotGridView`, `ScreenSpaceLightService`
(`Assets/Source/Game/GameScopeRegistration.cs:115-189`). Structural precedent for a scenario-frame
View that reshapes its geometry from state: `DangerGradientView`
(`Assets/Source/UI/Danger/DangerGradientView.cs:13`), which captures rest geometry in `Awake` and
drives it from a reactive level.

## 6. Performance (120 Hz Android, honest)

- **Draw calls:** 4 (one mesh per edge). *Option:* build a single combined mesh with per-strip
  edge-normal packed into a vertex attribute → **1 draw call**; recommended once the four-strip
  version is validated in-editor.
- **CPU/frame:** zero. Meshes are built once at startup and never rewritten from C#; all deformation
  and breathing live in the vertex shader. No per-frame allocation, no LINQ, no closures.
- **Vertex cost:** ~350–400 verts total, each doing one `tex2Dlod` — negligible.
- **Fill:** four thin strips of transparent/additive fill around the frame — small screen area; the
  HDR/bloom pass already runs for the rest of the scene.

## 7. Phasing

1. **Static net-strips.** Build the four meshes from `WallLimits`, apply the net-pattern fragment
   shader (no deformation, no breathing). Retire the `Top`/` Bottom` block sprites and add the
   missing left/right edges. Verifies geometry, sizing-from-config, and the tennis-net look.
2. **Disturbance billow.** Add the vertex `_DisturbanceTex` tap (BushLeaf precedent) so the nets
   billow where the projectile hits and where anything else stamps the field near the edge.
3. **Idle breathing.** Add the phase-varied idle sine to the perpendicular offset.
4. **Light + glow polish.** `#include SceneLight.cginc`, multiply by `SceneLightTintAtLOD`, tune the
   HDR emissive and blend for the dreamy read against bloom.

**Deferred stretch (not in scope):** a full **interior net** covering the play area (not just the
four edge strips). Higher vert/fill cost; decide only after the edge nets are seen in motion.

## 8. Validation — José's eye, every phase

> **`dotnet build` does not compile shaders/HLSL and cannot run the game.** Every phase above is
> gated on an **in-editor playtest by José** — the net pattern, the billow deformation, the idle
> breathing, and the glow are all shader/visual and cannot be verified headless. The **C# only**
> (procedural mesh generation, the `WallNetView` build, the DI registration) is `dotnet build`
> -checkable; the look and the deformation are his call. Flag any shader edit as needing an
> in-editor check, per `CLAUDE.md`.

## 9. Locked decisions (José, this round)

1. Edge-first — four edges ship; interior net deferred (§7 stretch).
2. Field-driven — sample `_DisturbanceTex`; no wall-hit broadcast, no `ShieldLostMessage`
   subscriber.
3. Shader-only — vertex tap, no CPU sim / Verlet / readback.
4. Idle-breathing — yes, low-amplitude vertex sine.
5. Receive light only — `SceneLightTintAtLOD`; no `RegisterLight`/cast.
6. All four edges visible, shaped as **four tennis nets** — net-strip meshes, not single lines.
