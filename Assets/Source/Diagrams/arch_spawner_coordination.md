@page arch_spawner_coordination Spawner Coordination

# Spawner Coordination

@image html spawner_coordination.svg "Spawner Coordination — Staged GridSpawner Pipeline"

## What this diagram shows

How `GridSpawnerCoordinator` sequences all grid population work at scene start,
ensuring static actors exist before balloons try to find empty slots.

**Gate:** The coordinator first awaits `NavigationReadyGate(Game)` — an `IReadyGate`
that resolves when `Navigation.Current == NavigationState.Game`. Nothing spawns until
the scene is fully active and the player has tapped Play.

**Staged pipeline:**
Spawners register themselves by implementing `IGridSpawner` and declaring a
`SpawnStage` priority. The coordinator groups registered spawners by stage (ascending)
and runs each group with `UniTask.WhenAll` — spawners within the same stage run in
parallel, stages are sequential.

```
SpawnStage.StaticActors  (0)  →  StaticActorSpawner.SpawnAsync
SpawnStage.DynamicActors (50) →  (GridSpawner — procedural placement, Phase 8.3)
SpawnStage.BalloonActors (100)→  BalloonSpawner.SpawnAsync
```

**Pre-warming overlap:** `BalloonSpawner.Start()` begins pool pre-warming
asynchronously and stores the task. `SpawnAsync` awaits it before populating the
grid — so pre-warm and navigation waiting overlap rather than serialise.

## Guidance

**Adding a new grid spawner:**
1. Implement `IGridSpawner` — provide `SpawnStage` and `SpawnAsync(CancellationToken)`
2. Register in `GameLifetimeScope` with `.As<IGridSpawner>()`
3. Choose the stage: actors that new balloons must avoid → `StaticActors` (0);
   procedurally placed actors → `DynamicActors` (50); balloons → `BalloonActors` (100)

**Parallelism within a stage:**
All spawners at the same stage run simultaneously via `UniTask.WhenAll`. If your
spawner depends on another spawner's output, assign it a higher stage number, not the
same stage.

**`StaticActorSpawner.Start()` vs `SpawnAsync`:**
Pool registration happens in `Start()` (synchronous, before the gate opens).
`SpawnAsync` only handles grid placement. This separation lets the pool be ready
immediately while placement waits for navigation.

