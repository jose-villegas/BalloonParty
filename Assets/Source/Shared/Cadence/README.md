# Cadence

Coordinates the timing of periodic RT blits across the field services, so their heavy
GPU work doesn't all land on the same frame.

## Contents

| File | What it does |
|---|---|
| `ICadencedEffect` | Interface implemented by any service that blits to a render texture on a timer. Exposes `BlitWeight` (how many blits it performs per cycle) and `ApplyPhaseOffset(offset01)` (sets its cadence accumulator's starting point) |
| `EffectCadenceCoordinator` | `IStartable` — at scope start, collects every registered `ICadencedEffect` and assigns each a one-time phase offset so their cadence timers don't all tick over together |

## Why

Several shared field services (background clouds, disturbance ripples, scene light,
scene capture, paint trails) each refresh their render texture on their own timer rather
than every frame — but if two or more of those timers land on the same frame, their
blits stack up. On tile-based mobile GPUs (Adreno/Mali) each blit forces a tile flush to
DRAM (roughly 0.3ms), so a frame with several coinciding flushes is visibly more
expensive than a frame with none. The coordinator staggers the timers apart so that
worst case drops from 10+ flushes on one frame to 2-3.

## Phase offsets

`EffectCadenceCoordinator` runs once, at scope start. It sorts every registered
`ICadencedEffect` by `BlitWeight` descending, then calls `ApplyPhaseOffset` on each with
a value in `[0, 1)`:

- The two heaviest effects get maximum separation — `0.0` and `0.5`.
- The rest are spread to fill the remaining gaps evenly.

Each effect multiplies its offset by its own cadence interval and uses that as the
starting value of its internal accumulator, so its first (and every subsequent) blit
fires shifted in time relative to the others. This is a one-time assignment at startup,
not an ongoing budget — nothing re-balances the phases later if an accumulator drifts.

`BlitWeight` is just a priority signal for that initial ordering (how much this effect's
coincidence would cost), not a literal cap on blits per frame.

## Current implementors

| Service | `BlitWeight` |
|---|---|
| `SceneCaptureService` (`Display/`) | 3 |
| `SceneLightFieldService` (`Shared/SceneLight/`) | 2 |
| `DisturbanceFieldService` (`Shared/Disturbance/`) | 2 |
| `BackgroundFieldService` (`Scenario/`) | 1 |
| `PaintingFieldService` (`Scenario/`) | 1 |

## Known gap

`ScreenSpaceLightService` (`Display/`) performs 2 blits of its own each time it rebuilds
(see `Diagrams/arch_screen_space_light.md`), but it does not implement `ICadencedEffect`
— it rebuilds off `SceneCaptureService.ContentVersion` instead of its own cadence timer,
so the coordinator has no weight for it and can't place it relative to the others. See
`PLAN-PerformanceRecovery.md` item G1 for the proposed fix (register it, then evolve the
coordinator from one-time phase assignment into an active per-frame blit budget).
