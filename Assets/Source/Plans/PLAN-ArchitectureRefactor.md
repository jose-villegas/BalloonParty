@page plan_architecture_refactor Architecture Refactor

# Architecture Refactor

> A prioritized roadmap for reducing redundancy, enforcing SOLID, and shrinking the
> largest classes under `Assets/Source/`. The codebase is healthy at the macro level —
> the read/write model-interface split, polymorphic `EvaluateHit` / `PushResponse`,
> `BalloonModelFactory`, and `PressureCascade` are all sound. The problems are
> concentrated in **a handful of god-classes** and **structural copy-paste the type
> system could erase**. Every item below cites `file:line` and a concrete fix.

> Audit basis: full read-only sweep of `Assets/Source/` (June 21, 2026). No behavioural
> change is intended by any item — these are structure/maintainability refactors. Items
> touching shaders or visuals (G2, G4) require an in-editor playtest; the rest are
> `dotnet build` + `style_audit.py` verifiable.

---

## How to use this plan

Work top-down — tiers are ordered by leverage-per-risk. Each item is independently
shippable. Suggested sequencing is at the end. After each item: run
`dotnet build BalloonParty.Runtime.csproj -nologo -clp:ErrorsOnly` and
`python3 Tools/style_audit.py`, and update the affected folder READMEs.

---

## Tier 1 — Redundancy the type system should kill

Pure wins, low risk, immediate LOC reduction. Mostly mechanical.

### R1 — Collapse five duplicate `PoolChannel<T>` subclasses into one generic

**Problem.** Five channels have byte-identical `Create()` bodies (`Instantiate` →
`SetActive(false)` → return):

- `UI/Score/ScoreTrailPoolChannel.cs`
- `UI/Shields/ShieldTrailPoolChannel.cs`
- `UI/Score/ProgressNoticePoolChannel.cs`
- `Shared/Pool/EffectPoolChannel.cs`
- `Item/ItemVisualPoolChannel.cs`

**Fix.** Add `SimplePoolChannel<T>` to `Shared/Pool/` taking either a `T prefab` or a
`GameObject prefab` + `GetComponent<T>()` (the `ItemVisualPoolChannel` variant). Update
call sites (`() => new ScoreTrailPoolChannel(prefab)` → `() => new SimplePoolChannel<FlyingTrail>(prefab)`)
and delete the five files. Keep `ParticlePoolChannel` (extra `ParticleSystem.Stop`) and
`InjectingPoolChannel` distinct.

**Net:** ~5 files → 1. **Verify:** `dotnet build`.

### R2 — Share wall-reflection math between projectile and predictor *(also fixes a latent bug)*

**Problem.** `ProjectileView.ClampToLimits` ([Projectile/View/ProjectileView.cs:167-197](../Projectile/View/ProjectileView.cs))
and `PredictionTraceCalculator` ([Prediction/PredictionTraceCalculator.cs:33-70](../Prediction/PredictionTraceCalculator.cs))
both build a `reflect` vector from `IGameConfiguration.LimitsClockwise` and call
`Vector2.Reflect`, but with subtly different wall handling (the predictor treats the
bottom wall differently). The aim preview can therefore diverge from real flight.

**Fix.** Extract `WallReflector` into `Shared/` exposing
`Vector3 ReflectAt(Vector3 pos, Vector3 dir, Vector4 limits, out Vector3 normal)`. Both
the live step and the trace step call it, guaranteeing preview matches reality.

**Net:** removes a divergence class. **Verify:** `dotnet build` + in-editor aim check.

### R3 — One generic reactive binder + counter label for the UI HUD

**Problem.** The "scope + binder" triple is copy-pasted across Health / Danger / Shield:
`HealthLabelBinder` and `DangerGradientBinder` ([UI/Danger/DangerGradientBinder.cs](../UI/Danger/DangerGradientBinder.cs))
differ only in two generic types and which reactive property they read; their scopes
([UI/Health/HealthUILifetimeScope.cs:14](../UI/Health/HealthUILifetimeScope.cs) etc.)
all do `GetComponentsInChildren → RegisterInstance → RegisterEntryPoint`. Separately,
the counter labels `HealthCounterLabel` and `ShieldCounterLabel`
([UI/Shields/ShieldCounterLabel.cs:9](../UI/Shields/ShieldCounterLabel.cs)) duplicate
logic that `FormattedLabel` ([UI/GameOver/FormattedLabel.cs](../UI/GameOver/FormattedLabel.cs))
already solves.

**Fix.**
1. `ReactivePropertyBinder<TView, TValue>` (`IStartable`) constructed with `TView[]`, an
   `IReadOnlyReactiveProperty<TValue>`, and a `(view, prop) => view.Bind(prop)` delegate
   — or a small `IBoundView<T> { Bind(IReadOnlyReactiveProperty<T>) }` the views implement.
2. A `RegisterBoundViews<TView,TValue>(builder, sourceSelector)` scope-builder helper.
3. Collapse `HealthCounterLabel` / `ShieldCounterLabel` into one
   `ReactiveCounterLabel` with an inspector format string + placeholder.

**Net:** ~5 files → ~2.

### R4 — Extract a base/service for item-handler boilerplate

**Problem.** The get-or-register / get-color / play / return block, the
`ContactFilter2D` + `BalloonsLayer` setup, and the `GetComponentInParent<BalloonView>`
overlap loop are duplicated across all five handlers:
[Item/Bomb/BombItemHandler.cs:130](../Item/Bomb/BombItemHandler.cs),
[Item/Laser/LaserItemHandler.cs:167](../Item/Laser/LaserItemHandler.cs),
[Item/Shield/ShieldItemHandler.cs:67](../Item/Shield/ShieldItemHandler.cs), and inline in
Lightning / Paint.

**Fix.** Introduce `BalloonItemHandlerBase` (or injected `ItemVfxPlayer` +
`BalloonOverlapQuery` services) holding `Setup`, the cached filter,
`SpawnVisual(settings, [rotation])`, and the overlap iterator. Handlers shrink to their
unique targeting logic. **Depends on / pairs with T3-OCP1 (`ItemSettings` split).**

### R5 — Add palette accessors; kill the hand-rolled name lookup

**Problem.** `foreach (palette.Colors)` / `.First(c => c.Name == name)` recurs in
[Game/Score/ScoreController.cs:66](../Game/Score/ScoreController.cs),
[UI/Score/ColorProgressBar.cs:72](../UI/Score/ColorProgressBar.cs),
[UI/LevelUp/LevelUpPopUp.cs:113](../UI/LevelUp/LevelUpPopUp.cs), and the cheats. One of
these (`ColorProgressBar.Start`) runs the `O(n)` scan on a hot path.

**Fix.** Add `PaletteEntry GetEntry(string)` and/or `IReadOnlyList<string> ColorNames`
to `IGamePalette`; replace the inline loops. Build a dictionary once in the SO.

---

## Tier 2 — God-class decomposition (LOC + SRP)

### G1 — Extract `ProjectileController` from `ProjectileView` *(highest-priority MVC violation)*

**Problem.** `OnTriggerEnter2D` ([Projectile/View/ProjectileView.cs:71-114](../Projectile/View/ProjectileView.cs))
evaluates hits, mutates `ShieldsRemaining`, reads the streak tracker, and applies
shield-gain rules — game rules inside a MonoBehaviour, contradicting the MVC contract.

**Fix.** Add a plain-C# `ProjectileController` (mirroring `BalloonController`). The View
detects the collision and publishes a raw `ProjectileCollidedMessage(balloonModel,
worldPos, direction)`; the controller evaluates the hit, applies streak/shield rules,
and publishes `ActorHitMessage` / `ShieldGainedMessage`. Movement/bounce
(`MoveAndBounce`, `ClampToLimits`) stays view-side.

### G2 — Extract `CinematicCameraRig` from `LevelUpTrailEffect` (386 LOC) *(known-fragile)*

**Problem.** Bundles score-gating, trail tracking, camera pan/zoom/clamp, time-scale
tweening, and 3-subscriber juggling. The 60-line per-frame
`PanInTick` ([Game/Cinematics/LevelUpTrailEffect.cs:195-255](../Game/Cinematics/LevelUpTrailEffect.cs))
mixes four concerns.

**Fix.** Extract `CinematicCameraRig` (capture/restore/pan/clamp, ~120 LOC out) and a
`TimeScaleController`; reduce the effect to a phase sequencer (pan-in → arrived →
dismissed → restore). Decompose `PanInTick` into `UpdateTrail(dt)` +
`UpdateCameraFollow(dt)`. **Requires an in-editor playtest of the level-up cinematic.**

### G3 — Split `BalloonSpawner` (479 LOC) into factory + placement resolver

**Problem.** Bundles pool registration, prewarm, the 13-arg controller assembly
(`SpawnBalloon` [Balloon/Spawner/BalloonSpawner.cs:281-326](../Balloon/Spawner/BalloonSpawner.cs)),
the column/pressure placement search ([363-451](../Balloon/Spawner/BalloonSpawner.cs)),
spawn animation, and scheduling. The `BalloonController` ctor takes 13 params
([Balloon/Controller/BalloonController.cs:37-67](../Balloon/Controller/BalloonController.cs)),
violating the repo's "prefer a config object over many parameters" rule.

**Fix.**
1. `BalloonFactory` owns `SpawnBalloon` + `AnimateSpawn` and the controller wiring.
2. `BalloonPlacementResolver` (plain C#, grid + balancer) owns the placement search.
3. Bundle the bus/grid/pool deps into a `BalloonControllerContext`; per-instance args
   (`model, view, poolKey, onReturned, hitVfxOverrides`) stay as call args.

`RejectedBalloonEffect` ([Balloon/Spawner/RejectedBalloonEffect.cs:137](../Balloon/Spawner/RejectedBalloonEffect.cs))
shares the spawn-animation setup — route it through the same `BalloonFactory` helper.

### G4 — Split `DisturbanceFieldService` (493 LOC)

**Problem.** Bundles RT lifecycle + double-buffer, material/keyword management,
coordinate math, simulation state, stamp queueing, and lerp-stamp scheduling.

**Fix.** Extract `DisturbanceFieldResources` (RT + material lifecycle),
`DisturbanceFieldCoordinates` (bounds + UV conversions), and `LerpStampScheduler`. Dedup
the two identical GPU stamp-upload tails
([Shared/Disturbance/DisturbanceFieldService.cs:231-236](../Shared/Disturbance/DisturbanceFieldService.cs)
vs [298-303](../Shared/Disturbance/DisturbanceFieldService.cs)) into
`UploadStampArrays(mat, count)`, and the blit/swap/reset tail into `BlitAndSwap(mat)`.
**Shader-adjacent — needs in-editor validation.**

### G5 — Extract `HexCoordinates` + `GridBalanceQuery` from `SlotGrid` (327 LOC)

**Problem.** Storage is the legitimate core, but static hex math
([Slots/Grid/SlotGrid.cs:238-262](../Slots/Grid/SlotGrid.cs)), recursive weighting/balance
([169-205](../Slots/Grid/SlotGrid.cs), [295-317](../Slots/Grid/SlotGrid.cs)), and
pathfinding are separable concerns. `ComputePath`
([133-147](../Slots/Grid/SlotGrid.cs)) is half dead "Phase 9 not implemented"
warnings that fire every step.

**Fix.** Extract `HexCoordinates` (static neighbour/world-position math) and
`GridBalanceQuery` (`IsUnbalanced`, `OptimalNextEmptySlot`, `CalculateWeight`, the memo).
Remove or debug-gate the Phase-9 warnings. `SlotGrid` keeps
`Place/Remove/At/ViewAt/IsEmpty/OnChanged`.

### G6 — Lower-urgency seams already present

- `ColorProgressBar` (265 LOC, 10 injected deps): extract a `ProgressNoticePresenter`
  (owns `_activeNotices`, the two pool keys, spawn/dismiss) and a rect-math helper for
  `WorldToAnchoredPosition` / `Center` / `RandomPosition`.
- `ChainLightningView` (379 LOC): the geometry statics are already self-contained —
  move them to `ChainLightningGeometry`; break up the 78-line `PlayAsync`
  ([Item/Lightning/ChainLightningView.cs:224-302](../Item/Lightning/ChainLightningView.cs)).
- `SlotClusterRegistry` (340 LOC): decompose `OnActorPlaced`
  ([Slots/Actor/Cluster/SlotClusterRegistry.cs:102-159](../Slots/Actor/Cluster/SlotClusterRegistry.cs))
  into `CreateSingletonCluster` / `GrowCluster` / `MergeClusters`; extract
  `ComputeWorldBounds` and the shared rebuild loop.

---

## Tier 3 — OCP & DIP (open for extension, depend on abstractions)

### OCP1 — Break up the `ItemSettings` god-config *(root cause of R4)*

`ItemSettings` ([Configuration/ItemSettings.cs:12-73](../Configuration/ItemSettings.cs))
packs Bomb / Laser / Lightning / Paint fields into one flat struct — a new item forces
editing the shared class, and every handler sees fields meaningless to it
(`LaserItemHandler` can read `PaintBlobSpinSpeed`). Move per-item params into typed
sub-settings (`BombSettings`, `LaserSettings`, …) behind a small interface, or a
`[SerializeReference] ItemBehaviour` per entry. Keep only the genuinely shared fields on
`ItemSettings` (`Type`, `TurnCheckEvery`, `Weight`, `MaximumAllowed`, `VisualPrefab`,
`ActivationEffectPrefab`, `Damage`, `Flags`).

### OCP2 — Replace `StaticActorSpawner`'s parallel type switches

`CreateModel` and `ModelMatches`
([Slots/Actor/StaticActorSpawner.cs:182-200](../Slots/Actor/StaticActorSpawner.cs)) both
`switch (GridActorType)`; a new actor type edits two methods. Move construction into the
config entry (a factory `Func` or registered map) and store the type on the model/entry.
The `GetStrategy` switch is the acceptable pattern to generalise toward.

### OCP3 — Express the effect "prepare" step as an interface

Lightning/Paint handlers downcast the pooled `EffectView` to concrete types
([Item/Lightning/LightningItemHandler.cs:101](../Item/Lightning/LightningItemHandler.cs),
[Item/Paint/PaintItemHandler.cs:117](../Item/Paint/PaintItemHandler.cs)) → runtime
`InvalidCast` on prefab misconfig. Define `IPreparedEffect<TData>` (or per-effect
`IChainEffect` / `ISplashEffect`) and resolve through a guarded `as` + `Debug.LogError`.

### DIP1 — Inject read interfaces, not concrete controllers

Views inject concrete controllers purely to read one reactive property: `SpaceDanger`,
`HealthLabelBinder`, `DangerGradientBinder`, and `ColorProgressBar`
([UI/Score/ColorProgressBar.cs:46-48](../UI/Score/ColorProgressBar.cs)) inject concrete
`PlayerHealthController` / `ScoreController` / `ColorStreakTracker` / `ScoreTrailService`.
Introduce read interfaces (`IPlayerHealth { IReadOnlyReactiveProperty<int> Current }`,
`IDangerLevel`, `IScoreQuery`) and inject those, matching the pattern `RunController`
already uses with `IRunMeta` / `IRunScore` / `INavigation`.

---

## Tier 4 — Quick consistency wins

- **Route navigation through the seam.** `ScoreController`
  ([Game/Score/ScoreController.cs:154](../Game/Score/ScoreController.cs)) calls the static
  `Navigation.TransitionTo` directly; inject `INavigation` like `RunController` does.
- **Keep engine APIs in the View.** `ThrowerController`
  ([Thrower/ThrowerController.cs:146](../Thrower/ThrowerController.cs)) polls `Input` and
  `Camera.main`; move pointer polling + screen→world conversion into `ThrowerView`.
- **Consolidate math helpers.** `MathUtils` (`Shared/`) and `VectorMathHelper`
  (`Shared/Animation/`) are both stateless pure-math statics; move into
  `Shared/Extensions/` per the repo's "extension methods over utility classes" rule.
- **`TrailSpawner.Spawn` / `SpawnUnscaled`** ([Shared/Pool/TrailSpawner.cs:59](../Shared/Pool/TrailSpawner.cs))
  are identical but one bool — collapse to `Spawn(..., bool useUnscaledTime = false)` and
  fold `FlyingTrail.Setup`'s two overloads to one taking `Color?`.
- **Likely-dead scopes.** Confirm and delete `ProjectileLifetimeScope` /
  `BalloonLifetimeScope` (README §DI Note: prefabs no longer use child scopes).
- **`ToughBalloonModel` / `BalloonModelBase`** duplicate the durability-decrement logic
  ([Balloon/Model/BalloonModelBase.cs:54-59](../Balloon/Model/BalloonModelBase.cs) vs
  ToughBalloonModel) — have the base decrement once and delegate the survive-outcome to a
  `protected virtual HitOutcome SurviveOutcome`.

---

## Suggested sequencing

1. **Tier 1** first — pure wins, low risk; R1/R5 are nearly mechanical, R2 also fixes a
   latent aim-preview divergence.
2. **G1 + G3** — the `ProjectileController` extraction and `BalloonSpawner` split are the
   highest-value SRP fixes and unblock testability.
3. **G2** with an in-editor playtest, given the cinematic's fragility.
4. **OCP1 + R4 together** — the `ItemSettings` split and the item-handler base are one
   coordinated change.
5. **G4, G5, G6, Tier 3 remainder, Tier 4** as capacity allows.

## Verification matrix

| Item | `dotnet build` | `style_audit.py` | In-editor playtest |
|---|---|---|---|
| R1, R3, R5, G3, G5, OCP1–3, DIP1, T4 | ✅ | ✅ | — |
| R2, G1 | ✅ | ✅ | aim / hit behaviour |
| R4 | ✅ | ✅ | item VFX spawn |
| G2 | ✅ | ✅ | level-up cinematic (fragile) |
| G4 | ✅ | ✅ | disturbance field (shader) |
