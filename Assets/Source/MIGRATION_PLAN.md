# BalloonParty — ECS → MVC Migration Plan

> Created: 2026-05-08  
> Context: Migrating from Entitas ECS (`Assets/Source_Old`) to a plain MVC architecture (`Assets/Source`) using MonoBehaviours, **UniRx** for reactive programming, and **Extenject (Zenject)** for dependency injection. Each phase ends with a **testable Unity Editor checkpoint**.

---

## Current Architecture

- **Framework:** Entitas ECS
- **Legacy folder:** `Assets/Source_Old` (renamed from `Assets/Source`)
- **Key patterns:** `ReactiveSystem`, `IComponent`, `Contexts`, `GameEntity`, `GameMatcher`

---

## Target Architecture

- **Pattern:** MVC (Model → pure C# class, View → MonoBehaviour, Controller → mediator)
- **Reactive layer:** [UniRx](https://github.com/neuecc/UniRx) — `ReactiveProperty<T>`, `Subject<T>`, `.Subscribe()` replacing plain C# events and Entitas reactive collectors
- **Dependency Injection:** [Extenject (Zenject)](https://github.com/Mathijs-Bakker/Extenject) — `[Inject]` attributes, `Installer` classes, `GameObjectContext` / `SceneContext` replacing manual wiring and `Contexts.sharedInstance`
- **New folder:** `Assets/Source/`
- **No Entitas dependency** in new code

---

## How UniRx Replaces Entitas Reactivity

| Entitas Pattern | UniRx Equivalent |
|---|---|
| `ReactiveSystem` triggered by component change | `ReactiveProperty<T>` observed via `.Subscribe()` |
| `ICollector` / `GameMatcher` | `Subject<T>` or `Observable.Merge(...)` |
| `entity.isStableBalloon = true` (flag flip) | `model.IsStable.Value = true` (ReactiveProperty) |
| `IGroup<GameEntity>` filter | `.Where(...)` operator on observable stream |
| Event entities (`isBalloonsBalanceEvent`) | `Subject<Unit>` signal bus |

## How Extenject Replaces Manual Wiring

| Old Pattern | Extenject Equivalent |
|---|---|
| `Contexts.sharedInstance` | `[Inject] IGameConfiguration _config` |
| `new SomeSystem(contexts)` in GameController | Bound in a `GameInstaller : MonoInstaller` |
| Prefab instantiation with manual linking | `IFactory<BalloonController>` or `MemoryPool<BalloonView>` |
| `GetComponent<>` coupling | `[Inject]` on fields/constructor |

---

## Final Folder Structure (Target)

```
Assets/Source/
  Balloon/
    Model/          BalloonModel.cs
    View/           BalloonView.cs
    Controller/     BalloonController.cs, BalloonBalancer.cs, BalloonSpawner.cs
    PowerUps/       Bomb/, Laser/, Lightning/, Shield/
  Slots/
    SlotGrid.cs, SlotGridController.cs, SlotGridView.cs
  Projectile/
    Model/, View/, Controller/
  Thrower/
    ThrowerController.cs
  Game/
    GameManager.cs, ScoreController.cs
  Shared/
    IGameConfiguration.cs
    SignalBus/      BalloonsBalanceSignal.cs, BalloonHitSignal.cs, ...
  Installers/
    GameInstaller.cs
    BalloonInstaller.cs
    ProjectileInstaller.cs

Assets/Source_Old/   ← legacy Entitas, untouched until Phase 8
```

> **Prefab naming:** New prefabs replace old ones directly — no suffixes (e.g. `BalloonPrefab`, not `BalloonPrefab_MVC`).

---

## Phase 0 — Preparation & New Folder Scaffold

**Goal:** Rename legacy code, install new packages, set up new folder structure — keep game running unchanged.

1. Rename `Assets/Source` → `Assets/Source_Old` in the Unity Editor **Project window** (preserves `.meta` GUIDs).
2. Install packages:
   - **UniRx** — OpenUPM: `openupm add com.neuecc.unirx`
   - **Extenject** — import from the [Extenject releases](https://github.com/Mathijs-Bakker/Extenject/releases) `.unitypackage`
3. Add an `[Obsolete]` banner comment to key entry-point files in `Source_Old`:
   - `GameController.cs`
   - `GameUpdateSystems.cs`
   - `GameFixedUpdateSystems.cs`
4. Create the new `Assets/Source/` folder tree (see structure above).
5. Add a `SceneContext` GameObject to the main scene (Extenject) — this will be the root DI container.
6. ✅ **Checkpoint:** Project compiles and runs in Play Mode — no runtime behaviour changed.

---

## Phase 1 — Balloon Model + View *(First Testable Milestone)*

**Goal:** A standalone `BalloonPrefab` backed by a reactive `BalloonModel`, wired via Extenject — no Entitas dependency.

### BalloonModel (`Assets/Source/Balloon/Model/BalloonModel.cs`)
- Pure C# class with **UniRx** `ReactiveProperty<T>` fields:
  ```csharp
  public ReactiveProperty<string> Color { get; } = new ReactiveProperty<string>();
  public ReactiveProperty<Vector2Int> SlotIndex { get; } = new ReactiveProperty<Vector2Int>();
  public ReactiveProperty<bool> IsStable { get; } = new ReactiveProperty<bool>(true);
  public float SlotWeight;
  ```
- No `IComponent`, no Entitas imports.

### BalloonView (`Assets/Source/Balloon/View/BalloonView.cs`)
- MonoBehaviour with `[SerializeField]` references: `SpriteRenderer`, shadow renderer.
- `public void Bind(BalloonModel model)` — subscribes to `model.Color` and `model.SlotIndex` using `.Subscribe(...).AddTo(this)`.
- Port color-application logic from `Source_Old/Balloon/BalloonColorController.cs` here.
- Inject `IGameConfiguration` via `[Inject]` instead of `Contexts.sharedInstance`.

### BalloonController (`Assets/Source/Balloon/Controller/BalloonController.cs`)
- Plain C# mediator — receives `BalloonModel` and `BalloonView` via **`[Inject]`**.
- Calls `view.Bind(model)` on construction.

### BalloonInstaller (`Assets/Source/Installers/BalloonInstaller.cs`)
- `MonoInstaller` that binds `BalloonModel` and `BalloonView` for the prefab's `GameObjectContext`.
- Pulls `IGameConfiguration` from the scene-level container.

### Steps
1. Create `BalloonModel.cs`, `BalloonView.cs`, `BalloonController.cs` as above.
2. Create `BalloonPrefab`: add `GameObjectContext` + `BalloonInstaller`, `BalloonView`, `BalloonController` MonoBehaviours. This prefab replaces the legacy one — no suffix needed.
3. ✅ **Checkpoint:** Drag prefab into scene → set `Color` in Inspector → Press Play → balloon renders and reacts to model changes automatically via UniRx.

---

## Phase 2 — Slot Grid Model & Balloon Placement

**Goal:** Replace `SlotsIndexer` (Entitas component) with a reactive `SlotGrid` injectable service.

1. Create `Assets/Source/Slots/SlotGrid.cs`
   - Wraps `BalloonModel[,]`
   - Exposes `Subject<SlotGridChangedEvent> OnChanged` (UniRx) — fires on any `Place`/`Remove`
   - Methods: `Place`, `Remove`, `IsEmpty`, `IsUnbalanced`, `OptimalNextEmptySlot`
   - Bound as a **singleton** in `GameInstaller`: `Container.Bind<SlotGrid>().AsSingle()`
2. Create `Assets/Source/Slots/SlotGridView.cs`
   - MonoBehaviour subscribing to `SlotGrid.OnChanged` to redraw Gizmos
3. Create `Assets/Source/Slots/SlotGridController.cs`
   - `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`
   - Spawns `BalloonController` instances into the grid using a Zenject `IFactory<BalloonController>`
4. ✅ **Checkpoint:** `SlotGridController` in scene → configure rows/cols → Press Play → balloons populate grid positions visually.

---

## Phase 3 — Balance / Movement Logic

**Goal:** Port `BalanceBalloonsSystem.cs` (ReactiveSystem + DOTween paths) to a reactive controller — no Entitas collector needed.

> Reference: `Assets/Source_Old/Balloon/BalanceBalloonsSystem.cs`

1. Create `Assets/Source/Shared/SignalBus/BalanceBalloonsSignal.cs` — empty struct used as a Zenject signal.
2. Register in `GameInstaller`:
   ```csharp
   Container.DeclareSignal<BalanceBalloonsSignal>();
   ```
3. Create `Assets/Source/Balloon/Controller/BalloonBalancer.cs`
   - `[Inject] SignalBus _signalBus`, `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`
   - In `Initialize()`:
     ```csharp
     _signalBus.GetStream<BalanceBalloonsSignal>()
               .Subscribe(_ => BalanceBalloons())
               .AddTo(this);
     ```
   - `BalanceBalloons()` runs the same bubble-loop algorithm from the old system
   - Updates `BalloonModel.SlotIndex.Value` and fires DOTween path animations on `BalloonView.transform`
   - Sets `BalloonModel.IsStable.Value = true` in tween `onComplete`
4. Any code that previously set `isBalloonsBalanceEvent` now calls `_signalBus.Fire<BalanceBalloonsSignal>()`
5. ✅ **Checkpoint:** Remove a balloon via a debug button → `BalanceBalloonsSignal` fires → remaining balloons animate to fill gaps.

---

## Phase 4 — Balloon Spawning & Line Management

**Goal:** Port spawner systems using a Zenject factory + memory pool.

> References: `BalloonLineSpawnerSystem.cs`, `GameStartedBalloonsSpawnSystem.cs`, `NewBalloonLinesInstanceSystem.cs`

1. Create `Assets/Source/Balloon/Controller/BalloonSpawner.cs`
   - `[Inject] IFactory<BalloonController> _factory`, `[Inject] SlotGrid _grid`, `[Inject] SignalBus _signalBus`
   - API: `BalloonController Spawn(string color, Vector2Int slotIndex)`
   - Fires `BalanceBalloonsSignal` after placing new balloon lines
2. Bind factory in `BalloonInstaller`:
   ```csharp
   Container.BindFactory<BalloonController, BalloonController.Factory>()
       .FromSubContainerResolve().ByNewPrefab(balloonPrefab);
   ```
3. ✅ **Checkpoint:** `BalloonSpawner` populates the initial grid on game start.

---

## Phase 5 — Projectile & Thrower

**Goal:** Port thrower direction, rotation, loading, firing, bounce, and collision.

> References: `ThrowerDirectionSystem`, `ThrowerRotationSystem`, `ThrowLoadedProjectileSystem`, `ProjectileBounceSystem`

1. Create `ProjectileModel.cs` with `ReactiveProperty<Vector2> Velocity`, `ReactiveProperty<bool> IsFree`
2. Create `ProjectileView.cs` — subscribes to model, handles `OnTriggerEnter2D` directly (replaces `TriggerReporterController`)
3. Create `Assets/Source/Thrower/ThrowerController.cs`
   - `[Inject] SignalBus _signalBus`, `[Inject] IGameConfiguration _config`
   - Uses `Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0))` for input stream
   - Fires `ProjectileFiredSignal` via SignalBus
4. Create `ProjectileController.cs`
   - Subscribes to `ProjectileFiredSignal`, drives movement and bounce via `Observable.EveryFixedUpdate()`
5. Bind in `ProjectileInstaller`; use `MemoryPool<ProjectileView>` for object reuse
6. ✅ **Checkpoint:** Thrower fires projectile → bounces off walls → hits balloon → destruction triggered.

---

## Phase 6 — Balloon Hit, Destruction & Score

**Goal:** Port hit/pop/score/nudge systems using signal bus.

> References: `BalloonHitDestructionSystem`, `BalloonHitScoreSystem`, `BalloonHitNudgeAnimationSystem`, `BalloonAnimationController`

1. Create `Assets/Source/Shared/SignalBus/BalloonHitSignal.cs` — carries `BalloonModel` reference
2. `BalloonController` subscribes to `BalloonHitSignal` filtered by its own model:
   - Triggers nudge animation on `BalloonView`, then destruction
3. Create `Assets/Source/Game/ScoreController.cs`
   - `[Inject] SignalBus _signalBus`
   - `_signalBus.GetStream<BalloonHitSignal>().Subscribe(s => AddScore(s.Model)).AddTo(this)`
   - Maintains `ReactiveProperty<int> Score` — UI binds to this directly via UniRx
4. ✅ **Checkpoint:** Full loop: hit → pop → score UI updates reactively → balance animation triggers.

---

## Phase 7 — Power-Ups

**Goal:** Port the 5 power-up controllers using signals and DI.

> References: `BombPowerUpController`, `LaserPowerUpController`, `LightningPowerUpController`, Shield, `BalloonsPowerUpCheckSystem`

1. Create `Assets/Source/Shared/SignalBus/PowerUpActivatedSignal.cs` — carries power-up type and position
2. Create `Assets/Source/Balloon/PowerUps/` — one controller per power-up, each `[Inject]`ing `SignalBus` and `SlotGrid`
3. Subscribe to `BalloonHitSignal` to check power-up trigger conditions (replaces `BalloonsPowerUpCheckSystem`)
4. ✅ **Checkpoint:** Power-up triggers and visual effects work correctly.

---

## Phase 8 — Game Loop, UI & Cleanup

**Goal:** Replace `GameControllerBehaviour` entry point; retire `Source_Old`.

1. Create `Assets/Source/Game/GameManager.cs` implementing `IInitializable`, `ITickable` (Zenject interfaces)
   - Bound in `GameInstaller` — Zenject calls `Initialize()` and `Tick()` automatically, no manual `Update` needed
   - Owns orchestration of `SlotGrid`, `BalloonSpawner`, `ThrowerController`, `ScoreController`
2. Bind all UI to reactive properties:
   ```csharp
   scoreController.Score.SubscribeToText(scoreLabel).AddTo(this);
   ```
3. Remove `GameControllerBehaviour` from root scene object → replace with `SceneContext` + `GameInstaller`
4. ✅ **Checkpoint:** Full game loop runs end-to-end without any Entitas references.
5. After full verification → delete `Assets/Source_Old`.

---

## Coexistence Strategy (Phases 1–7)

- **DI containers are separate:** `SceneContext` (Extenject) and `GameControllerBehaviour` (Entitas) can both live in the scene without conflict.
- Add a `bool UseLegacy` flag on `GameManager` to toggle which path drives the game loop per phase — no need for a separate scene fork.
- `IGameConfiguration` ScriptableObject bound once in `GameInstaller` and shared by both old and new code during transition:
  ```csharp
  Container.Bind<IGameConfiguration>().FromScriptableObjectResource("GameConfiguration").AsSingle();
  ```
- **SignalBus as a seam:** ECS systems not yet ported can fire Zenject signals via a thin bridge adapter, letting MVC controllers react immediately without waiting for the full system to be migrated.

---

## Key Library Patterns Quick Reference

### UniRx
```csharp
// Reactive model property — auto-updates view on change
model.Color.Subscribe(c => spriteRenderer.color = colorMap[c]).AddTo(this);

// Zenject signal as observable stream
signalBus.GetStream<BalanceBalloonsSignal>()
         .Subscribe(_ => BalanceBalloons())
         .AddTo(this);

// Input as stream
Observable.EveryUpdate()
          .Where(_ => Input.GetMouseButtonDown(0))
          .Subscribe(_ => Fire())
          .AddTo(this);
```

### Extenject
```csharp
// Scene-level singleton
Container.Bind<SlotGrid>().AsSingle();

// Signal declaration + direct binding
Container.DeclareSignal<BalloonHitSignal>();
Container.BindSignal<BalloonHitSignal>()
         .ToMethod<ScoreController>(x => x.OnBalloonHit)
         .FromResolve();

// Injection
[Inject] private IGameConfiguration _config;
[Inject] private SignalBus _signalBus;
```

---

## Progress Tracker

| Phase | Description                        | Status  |
|-------|------------------------------------|---------|
| 0     | Preparation & Folder Scaffold      | ⬜ Todo |
| 1     | Balloon Model + View               | ⬜ Todo |
| 2     | Slot Grid Model & Placement        | ⬜ Todo |
| 3     | Balance / Movement Logic           | ⬜ Todo |
| 4     | Balloon Spawning & Line Management | ⬜ Todo |
| 5     | Projectile & Thrower               | ⬜ Todo |
| 6     | Hit, Destruction & Score           | ⬜ Todo |
| 7     | Power-Ups                          | ⬜ Todo |
| 8     | Game Loop, UI & Cleanup            | ⬜ Todo |

