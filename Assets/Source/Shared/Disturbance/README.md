@page disturbance_field Disturbance Field Service

# Disturbance Field Service

Shared screen-space disturbance field that any game system can stamp into. Puff cloud shaders and future effects sample from it.

## Contents

| File | What it does |
|---|---|
| `DisturbanceFieldService` | Plain C# `IStartable` + `ITickable` + `IDisposable` — drives the simulation. Runs one diffusion blit per tick (spatial blur + reform toward equilibrium + wind advection + pressure fill + displacement decay), sets per-pass shader uniforms, and batches pending stamps (up to 32 per blit pass) via `DisturbanceStampBatched.shader`. Exposes two `Stamp()` overloads — `Stamp(StampSource, position, direction)` resolves the source's configured `StampProfile` (radius/strength/duration) internally, and the explicit-parameter overload underneath handles instant and lerp stamps. Every stamp also `Report()`s to `ImpactEventBus` (in `Shared/`) for same-frame visual consumers like bush rustle. Pushes `_FieldBoundsMin`, `_FieldBoundsSize` as global shader properties so all consumers (cloud views, the speck field, future effects) read the field without per-instance setup. (`_DisturbanceTex` itself is republished as a global by `DisturbanceFieldResources` on each swap.) Delegates all GPU resource ownership to `DisturbanceFieldResources`. Registered as a singleton in `GameLifetimeScope` |
| `DisturbanceFieldResources` | Plain C# — owns the GPU resources: the camera-sized `ARGBHalf` RT pair (density in R, displacement XY in GB) that ping-pongs as read/write, the diffusion + batched-stamp materials, and the `_STAMPS_ON` keyword. `BlitAndSwap()` blits read→write through a material, flips the buffers, and republishes the read texture as the global `_DisturbanceTex`. The service owns the shader-param IDs and per-pass uniforms; this just holds, blits, and flips. |
| `DisturbanceFieldCoordinates` | Plain C# — world ↔ field-UV geometry: derives the field's world-space bounds and texel dimensions from the camera framing (`GetOrthogonalSize()` × `TexelsPerUnit`), converts world positions (`WorldToUV`) and radii (`WorldRadiusToUV`) into the normalised UV space the stamp shaders expect |
| `LerpStampScheduler` | Plain C# — ramps `duration > 0` stamps into a sequence of instant sub-stamps (see "Lerp stamp lifecycle" below). Capped at `MaxLerpStamps`; the oldest ramp is evicted when full |

## Architecture

@dot
digraph DisturbanceField {
    rankdir=LR;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_config {
        label="Configuration";
        style=filled;
        fillcolor="#f5f5dc";
        Settings [label="DisturbanceFieldSettings
(ScriptableObject)"];
        Display  [label="GameDisplayConfiguration
(ScriptableObject)"];
    }

    subgraph cluster_callers {
        label="Stamp Callers";
        style=filled;
        fillcolor="#dce8f5";
        ProjView  [label="ProjectileView"];
        BallSpawn [label="BalloonFactory"];
        BallBal   [label="BalloonBalancer"];
        BallCtrl  [label="BalloonController"];
        BombH     [label="BombItemHandler"];
        LaserH    [label="LaserItemHandler"];
        PaintH    [label="PaintItemHandler"];
    }

    subgraph cluster_service {
        label="DisturbanceFieldService  (IStartable / ITickable / IDisposable) — drives the simulation";
        style=filled;
        fillcolor="#f0f0f0";

        StampEntry [label="Stamp(pos, radius, strength,
direction, duration)", shape=ellipse];
        Pending    [label="_pendingStamps
List<PendingStamp>"];
        Uniforms   [label="SetDiffusionUniforms()
wind / reform / displace", shape=ellipse];

        subgraph cluster_paths {
            label="Tick Routing (at most 1 blit per frame)";
            style=filled;
            fillcolor="#e0e8f0";
            Combined  [label="TickCombinedPass()
diffusion + stamps", shape=ellipse];
            StampOnly [label="FlushPendingStamps()
stamp-only", shape=ellipse];
            DiffOnly  [label="TickDiffusion()
diffusion-only", shape=ellipse];
        }
    }

    subgraph cluster_helpers {
        label="Extracted collaborators";
        style=filled;
        fillcolor="#e8f0e0";
        Lerp   [label="LerpStampScheduler
(_lerpScheduler)
ramps duration > 0 stamps
over several frames"];
        Coords [label="DisturbanceFieldCoordinates
(_coords)
WorldToUV, Bounds,
Width x Height"];
    }

    subgraph cluster_resources {
        label="DisturbanceFieldResources  (_resources) — owns the GPU state";
        style=filled;
        fillcolor="#e8e8e8";

        BlitSwap [label="BlitAndSwap(material)
Graphics.Blit read->write,
flip, republish texture", shape=ellipse];
        DiffMat  [label="diffusion material
(_STAMPS_ON keyword)", fillcolor="#ffe8cc"];
        StampMat [label="batched-stamp material", fillcolor="#ffe8cc"];
        FieldA   [label="_fieldA
R=density G=dispX B=dispY"];
        FieldB   [label="_fieldB
R=density G=dispX B=dispY"];
        PushTex  [label="_DisturbanceTex
(global, set each blit)", shape=ellipse];
    }

    subgraph cluster_globals {
        label="Global Shader Properties";
        style=filled;
        fillcolor="#e8f5dc";
        Bounds [label="_FieldBoundsMin
_FieldBoundsSize
(PushGlobalBounds)"];
    }

    subgraph cluster_consumers {
        label="Consumers";
        style=filled;
        fillcolor="#f5dce8";
        PuffCloud [label="PuffCloudView"];
        BushLeaf  [label="BushLeaf.shader
(_RATTLE_ON)"];
        Future    [label="Future effects
sampling _DisturbanceTex", style=dashed];
    }

    Settings -> StampEntry [lhead=cluster_service, label="tuning + shader refs"];
    Display  -> Coords     [label="ortho size -> RT bounds"];

    ProjView  -> StampEntry;
    BallSpawn -> StampEntry;
    BallBal   -> StampEntry;
    BallCtrl  -> StampEntry;
    BombH     -> StampEntry;
    LaserH    -> StampEntry;
    PaintH    -> StampEntry;

    StampEntry -> Pending [label="duration == 0"];
    StampEntry -> Lerp    [label="duration > 0"];
    Lerp -> StampEntry    [label="emits instant
stamps (delta slice)"];
    StampEntry -> Coords  [label="WorldToUV", style=dashed];

    Pending  -> Combined;
    Pending  -> StampOnly;
    Uniforms -> Combined [style=dashed];
    Uniforms -> DiffOnly [style=dashed];

    Combined  -> BlitSwap [label="material = diffusion (_STAMPS_ON)"];
    StampOnly -> BlitSwap [label="material = stamp"];
    DiffOnly  -> BlitSwap [label="material = diffusion"];

    BlitSwap -> FieldA [label="read -> write"];
    BlitSwap -> FieldB [label="swap"];
    BlitSwap -> PushTex;
    DiffMat  -> BlitSwap [style=invis];
    StampMat -> BlitSwap [style=invis];

    Coords -> Bounds [label="PushGlobalBounds()"];

    PushTex -> PuffCloud;
    PushTex -> BushLeaf;
    PushTex -> Future;
    Bounds  -> PuffCloud [style=dashed];
}
@enddot

## How it works

### RT layout

Two `RenderTexture` instances (`_fieldA`, `_fieldB`) ping-pong as read/write targets. Format is `ARGBHalf` with equilibrium clear color `(1.0, 0.5, 0.5, 1.0)`:

- **R** = density (1.0 = full cloud, 0.0 = cleared)
- **G** = displacement X (0.5 = zero, biased ±0.5 range)
- **B** = displacement Y (0.5 = zero, biased ±0.5 range)

Resolution is derived from `GameDisplayConfiguration.GetOrthogonalSize()` × `DisturbanceFieldSettings.TexelsPerUnit`.

### Stamp API

- **`Stamp(source, worldPos, direction)`** — the overload most callers use: resolves the `StampProfile` configured for the `StampSource` (radius, strength, duration) and forwards to the explicit overload. `GetProfile(source)` is also exposed for callers that need to scale the profile themselves (e.g. `DisturbanceTweenExtensions`).
- **`Stamp(worldPos, radius, strength, direction, duration = 0)`** — queues a disturbance at `worldPos`. World coordinates are converted to field UV space internally. When `duration` is zero (default), the stamp is applied instantly — stamps accumulate in a pending list and are flushed in batches each frame. When `duration` is greater than zero, queues a lerp stamp that ramps from 0 to full strength over that many seconds, spreading the effect smoothly across multiple frames. Useful for pop bursts, bomb detonations, and paint splashes. Every call also reports the impact to `ImpactEventBus`.

### Diffusion tick

Runs at a configurable interval (`DiffusionTickInterval`). Each tick:
1. Semi-Lagrangian wind advection shifts the sample origin
2. Pressure gradient pushes density from high to low, filling holes directionally
3. Displacement channels decay toward 0.5 (neutral)
4. Density trends back toward 1.0 (reform)

Wind direction is set dynamically from stamp directions (opposite to the disturbance velocity), smoothed and decaying — so the reform flows from behind the moving object.

### Lerp stamp lifecycle

When `Stamp()` is called with `duration > 0`, the stamp is queued on `LerpStampScheduler` (`_lerpScheduler`) instead of flushed immediately. Each diffusion tick the scheduler advances all active lerp stamps by the elapsed time, computes the normalized progress `t ∈ [0,1]`, and emits an instant `Stamp()` with `strength * delta` (only the new progress delta since last tick). The stamp radius also expands from `0.3×` to `1.0×` of the configured radius as `t` increases — this creates an expanding shockwave shape rather than a flat-radius pop. When `t >= 1`, the stamp is removed. The pool is capped at `MaxLerpStamps` to bound memory; oldest stamps are evicted when the cap is exceeded.

### Combined pass

When a diffusion tick and pending stamps coincide in the same frame (the common case during gameplay), both operations are folded into **a single blit** via the `_STAMPS_ON` shader keyword on the diffusion shader. The diffusion fragment shader runs its normal 3×3 blur + reform + wind pipeline, then applies the stamp loop on top of the result — all in one draw call.

Three routing paths handle every frame:

| Condition | Path | Blits |
|---|---|---|
| Diffusion due + stamps pending (≤ 32) | `TickCombinedPass` | **1** |
| Stamps pending, no diffusion | `FlushPendingStamps` | 1 per batch of 32 |
| Diffusion due, no stamps | `TickDiffusion` | 1 |
| Neither | (skip) | 0 |

When stamps exceed 32 in a frame (extremely rare — e.g. simultaneous bomb + laser + paint), the overflow is flushed as standalone stamp blits before the diffusion blit.

### Batched flush (stamp-only path)

On frames where stamps arrive but diffusion is not due, all instant stamps are flushed via the standalone `DisturbanceStampBatched` shader in batches of up to 32. Pre-allocated `Vector4[]` and `float[]` arrays (`_batchCenters`, `_batchRadii`, `_batchStrengths`, `_batchDirections`) avoid per-frame allocation. The shader receives the batch count via `_StampCount` and loops internally.

### World → UV conversion

`DisturbanceFieldCoordinates.WorldToUV` maps world-space positions to UV coordinates using the field bounds rect computed at `Start()` from `GameDisplayConfiguration.GetOrthogonalSize()`. The field covers the full screen-space orthographic viewport. `WorldRadiusToUV` normalises radius against the average of bounds width and height so a world-unit radius is consistent regardless of aspect ratio.

## Consumers

| System | When it stamps | `StampSource` | Notes |
|---|---|---|---|
| `ProjectileView` | Each `FixedUpdate` in `MoveAndBounce()` | `Projectile` | Continuous wake through Puff clouds; `Duration` from config typically 0 for a sharp per-frame stamp |
| `ProjectileView` | Once, on the first free frame (muzzle exit) | `ProjectileFire` | One-shot exit force at the muzzle, along the fire heading, tagged the reserved `Projectile` palette colour |
| `BalloonFactory` | Each frame during spawn path DOTween `OnUpdate` (via `StampDisturbanceAlongPath`) | `BalloonPath` | Spawn animations disturb clouds they pass through |
| `BalloonBalancer` | Each frame during balance path DOTween `OnUpdate` (via `StampDisturbanceAlongPath`) | `BalloonPath` | Balance animations disturb clouds |
| `BalloonController` | On balloon pop | `BalloonPop` | Pop burst shockwave; `Duration > 0` creates an expanding shockwave shape |
| `RejectedBalloonEffect` | When an overflow balloon pops | `BalloonPop` | Same pop-burst profile |
| `BombItemHandler` | On detonation | `Bomb` | Large-radius burst; `Duration > 0` for smooth shockwave spread |
| `LaserItemHandler` | Along each beam segment | `Laser` | Linear disturbance along beams |
| `PaintItemHandler` | On neighbor hit and splash landing | `Paint` | Splash disturbances |
| `DisturbanceStampCheat` | Mouse drag (debug only) | — | Direct explicit-parameter `Stamp()` call for testing |

Callers use the `Stamp(StampSource, …)` overload, which resolves the source's `StampProfile` (radius, strength, duration) inside the service; the tween extension scales the profile by the moving transform's size. Whether a given stamp becomes a lerp stamp or an instant stamp is purely a config decision — set `Duration > 0` in the SO to get an expanding shockwave, leave it at 0 for a sharp single-frame stamp.

## Configuration

All tuning lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `Configuration/README.md` for field details.

## Interactions

- **`PuffCloudView`** — reads `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize` from global shader properties (set by the service each tick). Only pushes `_TimeOffset` per-instance via `MaterialPropertyBlock`. No direct reference to the service
- **`BushLeaf.shader`** — when `_RATTLE_ON` is enabled, the vertex shader samples `_DisturbanceTex` at each leaf's world position via `tex2Dlod`. Displacement vector is converted to angular rattle modulated by depth and a configurable damping curve. No C# wiring needed — reads the same global shader properties as PuffCloud
- **`GameLifetimeScope`** — registers the service as singleton with `AsImplementedInterfaces().AsSelf()`
- **`GameDisplayConfiguration`** — provides orthographic size for RT bounds computation
- **`DisturbanceFieldSettings`** — provides all tuning knobs and shader references

