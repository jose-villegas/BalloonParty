@page disturbance_field Disturbance Field Service

# Disturbance Field Service

Shared screen-space disturbance field that any game system can stamp into. Puff cloud shaders and future effects sample from it.

## Contents

| File | What it does |
|---|---|
| `DisturbanceFieldService` | Plain C# `IStartable` + `ITickable` + `IDisposable` — owns a camera-sized `ARGBHalf` RT pair (density in R, displacement XY in GB). Runs one diffusion blit per tick (spatial blur + reform toward equilibrium + wind advection + pressure fill + displacement decay). Exposes `Stamp()` for instant and lerp stamps. Pending stamps are batched (up to 16 per blit pass) via `DisturbanceStampBatched.shader`. Pushes `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize` as global shader properties each tick so all consumers (cloud views, future effects) read the field without per-instance setup. Registered as a singleton in `GameLifetimeScope` |

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
        Settings  [label="DisturbanceFieldSettings\n(ScriptableObject)"];
        Display   [label="GameDisplayConfiguration\n(ScriptableObject)"];
    }

    subgraph cluster_callers {
        label="Stamp Callers";
        style=filled;
        fillcolor="#dce8f5";
        ProjView  [label="ProjectileView\n(FixedUpdate)"];
        BallSpawn [label="BalloonSpawner\n(spawn path OnUpdate)"];
        BallBal   [label="BalloonBalancer\n(balance path OnUpdate)"];
        BallCtrl  [label="BalloonController\n(on pop)"];
        BombH     [label="BombItemHandler\n(on detonation)"];
        LaserH    [label="LaserItemHandler\n(per beam segment)"];
        PaintH    [label="PaintItemHandler\n(splash landing)"];
    }

    subgraph cluster_service {
        label="DisturbanceFieldService  (IStartable / ITickable / IDisposable)";
        style=filled;
        fillcolor="#f0f0f0";

        StampEntry [label="Stamp(pos, radius, strength,\ndirection, duration)", shape=ellipse];
        Pending   [label="_pendingStamps\nList<PendingStamp>\n(duration == 0)"];
        LerpQ     [label="_activeStamps\nList<LerpStamp>\n(duration > 0)"];
        TickLerp  [label="TickLerpStamps()\nramp strength over duration", shape=ellipse];

        subgraph cluster_paths {
            label="Tick Routing (at most 1 blit per frame)";
            style=filled;
            fillcolor="#e0e8f0";
            Combined  [label="TickCombinedPass()\ndiffusion + stamps\nin one blit", shape=ellipse];
            StampOnly [label="FlushPendingStamps()\nstamp-only blit\n(no diffusion due)", shape=ellipse];
            DiffOnly  [label="TickDiffusion()\ndiffusion-only blit\n(no stamps)", shape=ellipse];
        }

        subgraph cluster_rt {
            label="RT Ping-pong (ARGBHalf)";
            style=filled;
            fillcolor="#e8e8e8";
            FieldA [label="_fieldA\nR=density  G=dispX  B=dispY"];
            FieldB [label="_fieldB\nR=density  G=dispX  B=dispY"];
        }

        DiffShader  [label="DiffusionShader\n(_STAMPS_ON keyword\nfor combined pass)", shape=ellipse, fillcolor="#ffe8cc"];
        StampShader [label="StampBatchedShader\n(fallback: stamps\nwithout diffusion)", shape=ellipse, fillcolor="#ffe8cc"];

        StampEntry -> Pending  [label="duration == 0"];
        StampEntry -> LerpQ    [label="duration > 0"];
        LerpQ   -> TickLerp;
        TickLerp -> StampEntry [label="generates\ninstant stamps\n(delta slice)"];
        Pending -> Combined   [label="stamps + diffusion\ndue this frame"];
        Pending -> StampOnly  [label="stamps only\n(diffusion not due)"];
        Combined  -> DiffShader  [label="_STAMPS_ON"];
        StampOnly -> StampShader;
        DiffOnly  -> DiffShader  [label="no keyword"];
        DiffShader  -> FieldA [label="blit write"];
        DiffShader  -> FieldB [label="blit write"];
        StampShader -> FieldA [label="blit write"];
        StampShader -> FieldB [label="blit write"];
    }

    subgraph cluster_globals {
        label="Global Shader Properties (set each tick)";
        style=filled;
        fillcolor="#e8f5dc";
        Globals [label="_DisturbanceTex\n_FieldBoundsMin\n_FieldBoundsSize"];
    }

    subgraph cluster_consumers {
        label="Consumers";
        style=filled;
        fillcolor="#f5dce8";
        PuffCloud [label="PuffCloudView\n(samples field per slot)"];
        BushLeaf  [label="BushLeaf.shader\n(vertex tex2Dlod\nper leaf, _RATTLE_ON)"];
        Future    [label="Future effects\n(any shader sampling\n_DisturbanceTex)", style=dashed];
    }

    Settings -> StampEntry [lhead=cluster_service, label="tuning knobs\n+ shader refs"];
    Display  -> StampEntry [lhead=cluster_service, label="ortho size\nfor RT bounds"];

    ProjView  -> StampEntry [label="Stamp(…, stamp.Duration)"];
    BallSpawn -> StampEntry [label="Stamp(…, stamp.Duration)"];
    BallBal   -> StampEntry [label="Stamp(…, stamp.Duration)"];
    BallCtrl  -> StampEntry [label="Stamp(…, stamp.Duration)"];
    BombH     -> StampEntry [label="Stamp(…, stamp.Duration)"];
    LaserH    -> StampEntry [label="Stamp(…, stamp.Duration)"];
    PaintH    -> StampEntry [label="Stamp(…, stamp.Duration)"];

    FieldA -> Globals [label="PushGlobalTexture()"];
    FieldB -> Globals [label="PushGlobalTexture()"];

    Globals -> PuffCloud;
    Globals -> BushLeaf;
    Globals -> Future;
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

- **`Stamp(worldPos, radius, strength, direction, duration = 0)`** — queues a disturbance at `worldPos`. World coordinates are converted to field UV space internally. When `duration` is zero (default), the stamp is applied instantly — stamps accumulate in a pending list and are flushed in batches each frame. When `duration` is greater than zero, queues a lerp stamp that ramps from 0 to full strength over that many seconds, spreading the effect smoothly across multiple frames. Useful for pop bursts, bomb detonations, and paint splashes.

### Diffusion tick

Runs at a configurable interval (`DiffusionTickInterval`). Each tick:
1. Semi-Lagrangian wind advection shifts the sample origin
2. Pressure gradient pushes density from high to low, filling holes directionally
3. Displacement channels decay toward 0.5 (neutral)
4. Density trends back toward 1.0 (reform)

Wind direction is set dynamically from stamp directions (opposite to the disturbance velocity), smoothed and decaying — so the reform flows from behind the moving object.

### Lerp stamp lifecycle

When `Stamp()` is called with `duration > 0`, a `LerpStamp` is queued instead of flushed immediately. Each tick, `TickLerpStamps` advances all active lerp stamps by `dt`, computes the normalized progress `t ∈ [0,1]`, and calls `Stamp()` with `strength * delta` (only the new progress delta since last tick). The stamp radius also expands from `0.3×` to `1.0×` of the configured radius as `t` increases — this creates an expanding shockwave shape rather than a flat-radius pop. When `t >= 1`, the stamp is removed. The pool is capped at `MaxLerpStamps` to bound memory; oldest stamps are evicted when the cap is exceeded.

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

`WorldToFieldUV` maps world-space positions to UV coordinates using the field bounds rect computed at `Start()` from `GameDisplayConfiguration.GetOrthogonalSize()`. The field covers the full screen-space orthographic viewport. `WorldRadiusToFieldUV` normalises radius against the average of bounds width and height so a world-unit radius is consistent regardless of aspect ratio.

## Consumers

| System | When it stamps | `StampSource` | Notes |
|---|---|---|---|
| `ProjectileView` | Each `FixedUpdate` in `MoveAndBounce()` | `Projectile` | Continuous wake through Puff clouds; `Duration` from config typically 0 for a sharp per-frame stamp |
| `BalloonSpawner` | Each frame during spawn path DOTween `OnUpdate` | `BalloonPath` | Spawn animations disturb clouds they pass through |
| `BalloonBalancer` | Each frame during balance path DOTween `OnUpdate` | `BalloonPath` | Balance animations disturb clouds |
| `BalloonController` | On balloon pop | `BalloonPop` | Pop burst shockwave; `Duration > 0` creates an expanding shockwave shape |
| `BombItemHandler` | On detonation | `Bomb` | Large-radius burst; `Duration > 0` for smooth shockwave spread |
| `LaserItemHandler` | Along each beam segment | `Laser` | Linear disturbance along beams |
| `PaintItemHandler` | On neighbor hit and splash landing | `Paint` | Splash disturbances |
| `DisturbanceStampCheat` | Mouse drag (debug only) | — | Direct `Stamp()` call for testing |

All callers pass `stamp.Duration` directly from their `StampProfile` via `DisturbanceFieldSettings.GetProfile(StampSource)`. Whether a given stamp becomes a lerp stamp or an instant stamp is purely a config decision — set `Duration > 0` in the SO to get an expanding shockwave, leave it at 0 for a sharp single-frame stamp.

## Configuration

All tuning lives on `DisturbanceFieldSettings` SO (in `Configuration/`). See `Configuration/README.md` for field details.

## Interactions

- **`PuffCloudView`** — reads `_DisturbanceTex`, `_FieldBoundsMin`, `_FieldBoundsSize` from global shader properties (set by the service each tick). Only pushes `_TimeOffset` per-instance via `MaterialPropertyBlock`. No direct reference to the service
- **`BushLeaf.shader`** — when `_RATTLE_ON` is enabled, the vertex shader samples `_DisturbanceTex` at each leaf's world position via `tex2Dlod`. Displacement vector is converted to angular rattle modulated by depth and a configurable damping curve. No C# wiring needed — reads the same global shader properties as PuffCloud
- **`GameLifetimeScope`** — registers the service as singleton with `AsImplementedInterfaces().AsSelf()`
- **`GameDisplayConfiguration`** — provides orthographic size for RT bounds computation
- **`DisturbanceFieldSettings`** — provides all tuning knobs and shader references

