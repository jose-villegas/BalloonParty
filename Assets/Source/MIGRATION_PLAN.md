# BalloonParty — ECS → MVC Migration Plan

> Created: 2026-05-08  
> Context: Migrating from Entitas ECS (`Assets/Source_Old`) to a plain MVC architecture (`Assets/Source`) using MonoBehaviours, **UniRx** for reactive programming, and **VContainer** for dependency injection. Each phase ends with a **testable Unity Editor checkpoint**.

---

## Current Architecture

- **Framework:** Entitas ECS
- **Legacy folder:** `Assets/Source_Old` (renamed from `Assets/Source`)
- **Key patterns:** `ReactiveSystem`, `IComponent`, `Contexts`, `GameEntity`, `GameMatcher`

---

## Target Architecture

- **Pattern:** MVC (Model → pure C# class, View → MonoBehaviour, Controller → mediator)
- **Reactive layer:** [UniRx](https://github.com/neuecc/UniRx) — `ReactiveProperty<T>`, `Subject<T>`, `.Subscribe()` replacing plain C# events and Entitas reactive collectors
- **Dependency Injection:** [VContainer](https://vcontainer.hadashikick.jp/) — `[Inject]` attributes, `LifetimeScope`, `IContainerBuilder` replacing manual wiring and `Contexts.sharedInstance`
- **Messaging / Signals:** [MessagePipe](https://github.com/Cysharp/MessagePipe) — VContainer's recommended pub/sub companion, replaces Zenject's SignalBus
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
| Event entities (`isBalloonsBalanceEvent`) | `IPublisher<T>` / `ISubscriber<T>` (MessagePipe) |

## How VContainer Replaces Manual Wiring

| Old Pattern | VContainer Equivalent |
|---|---|
| `Contexts.sharedInstance` | `[Inject] IGameConfiguration _config` |
| `new SomeSystem(contexts)` in GameController | `builder.RegisterEntryPoint<GameManager>()` in `LifetimeScope` |
| Prefab instantiation with manual linking | `builder.RegisterFactory<BalloonController>(...)` |
| `GetComponent<>` coupling | `[Inject]` on fields/constructor |
| Entitas `IInitializeSystem` / `IExecuteSystem` | `IStartable` / `ITickable` (VContainer entry points) |

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
    GameLifetimeScope.cs   ← root VContainer scope
  Shared/
    IGameConfiguration.cs
    Messages/       BalanceBalloonsMessage.cs, BalloonHitMessage.cs, ...

Assets/Source_Old/   ← legacy Entitas, untouched until Phase 8
```

> **Prefab naming:** New prefabs replace old ones directly — no suffixes (e.g. `BalloonPrefab`, not `BalloonPrefab_MVC`).

---

## Phase 0 — Preparation & New Folder Scaffold

**Goal:** Rename legacy code, install new packages, set up new folder structure — keep game running unchanged.

1. Rename `Assets/Source` → `Assets/Source_Old` in the Unity Editor **Project window** (preserves `.meta` GUIDs).
2. Install packages:
   - **UniRx** — OpenUPM: `openupm add com.neuecc.unirx`
   - **VContainer** — OpenUPM: `openupm add jp.hadashikick.vcontainer`
   - **MessagePipe** — OpenUPM: `openupm add net.cysharp.messagepipe`  
     *(VContainer has first-class MessagePipe integration via `RegisterMessagePipe()` + `RegisterMessageBroker<T>()`)*
3. Add an `[Obsolete]` banner comment to key entry-point files in `Source_Old`:
   - `GameController.cs`
   - `GameUpdateSystems.cs`
   - `GameFixedUpdateSystems.cs`
4. Create the new `Assets/Source/` folder tree (see structure above).
5. Create `Assets/Source/Game/GameLifetimeScope.cs` — a `LifetimeScope` (VContainer's composition root) attached to a root GameObject in the main scene.
6. ✅ **Checkpoint:** Project compiles and runs in Play Mode — no runtime behaviour changed.

---

## Phase 1 — Balloon Model + View *(First Testable Milestone)*

**Goal:** A standalone `BalloonPrefab` backed by a reactive `BalloonModel`, wired via VContainer — no Entitas dependency.

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
- Receives `IGameConfiguration` via `[Inject]`.

### BalloonController (`Assets/Source/Balloon/Controller/BalloonController.cs`)
- Plain C# mediator — receives `BalloonModel` and `BalloonView` via **`[Inject]`**.
- Calls `view.Bind(model)` on construction / `IStartable.Start()`.

### Registration in `GameLifetimeScope`
```csharp
builder.Register<BalloonModel>(Lifetime.Transient);
builder.RegisterComponentInHierarchy<BalloonView>();
builder.Register<BalloonController>(Lifetime.Transient);
```

### Steps
1. Create `BalloonModel.cs`, `BalloonView.cs`, `BalloonController.cs` as above.
2. Create `BalloonPrefab`: add `BalloonView` MonoBehaviour. This prefab replaces the legacy one — no suffix needed.
3. ✅ **Checkpoint:** Drag prefab into scene → set `Color` in Inspector → Press Play → balloon renders and reacts to model changes automatically via UniRx.

---

## Phase 2 — Slot Grid Model & Balloon Placement

**Goal:** Replace `SlotsIndexer` (Entitas component) with a reactive `SlotGrid` injectable service.

1. Create `Assets/Source/Slots/SlotGrid.cs`
   - Wraps `BalloonModel[,]`
   - Exposes `Subject<SlotGridChangedEvent> OnChanged` (UniRx) — fires on any `Place`/`Remove`
   - Methods: `Place`, `Remove`, `IsEmpty`, `IsUnbalanced`, `OptimalNextEmptySlot`
   - Registered as a **singleton** in `GameLifetimeScope`:
     ```csharp
     builder.Register<SlotGrid>(Lifetime.Singleton);
     ```
2. Create `Assets/Source/Slots/SlotGridView.cs`
   - MonoBehaviour subscribing to `SlotGrid.OnChanged` to redraw Gizmos
3. Create `Assets/Source/Slots/SlotGridController.cs`
   - `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`
   - Spawns `BalloonController` instances into the grid via injected factory
4. ✅ **Checkpoint:** `SlotGridController` in scene → configure rows/cols → Press Play → balloons populate grid positions visually.

---

## Phase 3 — Balance / Movement Logic

**Goal:** Port `BalanceBalloonsSystem.cs` (ReactiveSystem + DOTween paths) to a reactive controller — no Entitas collector needed.

> Reference: `Assets/Source_Old/Balloon/BalanceBalloonsSystem.cs`

1. Create `Assets/Source/Shared/Messages/BalanceBalloonsMessage.cs` — empty struct used as a MessagePipe message.
2. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessagePipe();
   builder.RegisterMessageBroker<BalanceBalloonsMessage>();
   ```
3. Create `Assets/Source/Balloon/Controller/BalloonBalancer.cs` implementing `IStartable`
   - `[Inject] ISubscriber<BalanceBalloonsMessage> _subscriber`
   - `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`
   - In `Start()`:
     ```csharp
     _subscriber.Subscribe(_ => BalanceBalloons()).AddTo(_disposable);
     ```
   - `BalanceBalloons()` runs the same bubble-loop algorithm from the old system
   - Updates `BalloonModel.SlotIndex.Value` and fires DOTween path animations on `BalloonView.transform`
   - Sets `BalloonModel.IsStable.Value = true` in tween `onComplete`
4. Any code that previously set `isBalloonsBalanceEvent` now injects `IPublisher<BalanceBalloonsMessage>` and calls `_publisher.Publish(default)`
5. Register `BalloonBalancer` as an entry point:
   ```csharp
   builder.RegisterEntryPoint<BalloonBalancer>();
   ```
6. ✅ **Checkpoint:** Remove a balloon via a debug button → `BalanceBalloonsMessage` published → remaining balloons animate to fill gaps.

---

## Phase 4 — Balloon Spawning & Line Management

**Goal:** Port spawner systems using a VContainer factory.

> References: `BalloonLineSpawnerSystem.cs`, `GameStartedBalloonsSpawnSystem.cs`, `NewBalloonLinesInstanceSystem.cs`

1. Create `Assets/Source/Balloon/Controller/BalloonSpawner.cs`
   - `[Inject] IObjectResolver _resolver`, `[Inject] SlotGrid _grid`
   - `[Inject] IPublisher<BalanceBalloonsMessage> _publisher`
   - API: `BalloonController Spawn(string color, Vector2Int slotIndex)`
   - Publishes `BalanceBalloonsMessage` after placing new balloon lines
2. Register in `GameLifetimeScope`:
   ```csharp
   builder.Register<BalloonSpawner>(Lifetime.Singleton);
   ```
3. ✅ **Checkpoint:** `BalloonSpawner` populates the initial grid on game start.

---

## Phase 5 — Projectile & Thrower

**Goal:** Port thrower direction, rotation, loading, firing, bounce, and collision.

> References: `ThrowerDirectionSystem`, `ThrowerRotationSystem`, `ThrowLoadedProjectileSystem`, `ProjectileBounceSystem`

1. Create `Assets/Source/Shared/Messages/ProjectileFiredMessage.cs`
2. Create `ProjectileModel.cs` with `ReactiveProperty<Vector2> Velocity`, `ReactiveProperty<bool> IsFree`
3. Create `ProjectileView.cs` — subscribes to model, handles `OnTriggerEnter2D` directly (replaces `TriggerReporterController`)
4. Create `Assets/Source/Thrower/ThrowerController.cs` implementing `ITickable`
   - `[Inject] IPublisher<ProjectileFiredMessage> _publisher`, `[Inject] IGameConfiguration _config`
   - Uses `Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0))` for input stream
5. Create `ProjectileController.cs` implementing `IStartable`
   - `[Inject] ISubscriber<ProjectileFiredMessage> _subscriber`
   - Drives movement and bounce via `Observable.EveryFixedUpdate()`
6. Register entry points in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<ProjectileFiredMessage>();
   builder.RegisterEntryPoint<ThrowerController>();
   builder.RegisterEntryPoint<ProjectileController>();
   ```
7. ✅ **Checkpoint:** Thrower fires projectile → bounces off walls → hits balloon → destruction triggered.

---

## Phase 6 — Balloon Hit, Destruction & Score

**Goal:** Port hit/pop/score/nudge systems using MessagePipe.

> References: `BalloonHitDestructionSystem`, `BalloonHitScoreSystem`, `BalloonHitNudgeAnimationSystem`, `BalloonAnimationController`

1. Create `Assets/Source/Shared/Messages/BalloonHitMessage.cs` — carries `BalloonModel` reference
2. `BalloonController` injects `ISubscriber<BalloonHitMessage>`, filters by its own model:
   - Triggers nudge animation on `BalloonView`, then destruction
3. Create `Assets/Source/Game/ScoreController.cs` implementing `IStartable`
   - `[Inject] ISubscriber<BalloonHitMessage> _subscriber`
   - Subscribes and increments `ReactiveProperty<int> Score`
   - UI binds to `Score` directly via UniRx: `scoreController.Score.SubscribeToText(scoreLabel).AddTo(this)`
4. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<BalloonHitMessage>();
   builder.RegisterEntryPoint<ScoreController>();
   ```
5. ✅ **Checkpoint:** Full loop: hit → pop → score UI updates reactively → balance animation triggers.

---

## Phase 7 — Power-Ups

**Goal:** Port the 5 power-up controllers using MessagePipe and VContainer.

> References: `BombPowerUpController`, `LaserPowerUpController`, `LightningPowerUpController`, Shield, `BalloonsPowerUpCheckSystem`

1. Create `Assets/Source/Shared/Messages/PowerUpActivatedMessage.cs` — carries power-up type and position
2. Create `Assets/Source/Balloon/PowerUps/` — one controller per power-up, each injecting `IPublisher`/`ISubscriber` and `SlotGrid`
3. Subscribe to `BalloonHitMessage` to check power-up trigger conditions (replaces `BalloonsPowerUpCheckSystem`)
4. ✅ **Checkpoint:** Power-up triggers and visual effects work correctly.

---

## Phase 8 — Game Loop, UI & Cleanup

**Goal:** Replace `GameControllerBehaviour` entry point; retire `Source_Old`.

1. Create `Assets/Source/Game/GameManager.cs` implementing `IStartable`, `ITickable`
   - Registered via `builder.RegisterEntryPoint<GameManager>()` — VContainer calls `Start()` and `Tick()` automatically
   - Owns orchestration of `SlotGrid`, `BalloonSpawner`, `ThrowerController`, `ScoreController`
2. Bind all UI to reactive properties:
   ```csharp
   scoreController.Score.SubscribeToText(scoreLabel).AddTo(this);
   ```
3. Remove `GameControllerBehaviour` from root scene object → `GameLifetimeScope` becomes the sole composition root
4. ✅ **Checkpoint:** Full game loop runs end-to-end without any Entitas references.
5. After full verification → delete `Assets/Source_Old`.

---

## Coexistence Strategy (Phases 1–7)

The goal is **full removal** of Entitas and `Assets/Source_Old` by Phase 8. Every decision during the transition must serve that end — nothing in `Source_Old` should be extended or improved; it is read-only reference material.

### Principles

- **One system at a time.** For each phase, the new MVC system goes live and its Entitas counterpart is disabled (commented out or removed from `GameUpdateSystems` / `GameFixedUpdateSystems`). Never run both versions of the same system simultaneously.
- **Scene stays runnable throughout.** After disabling a legacy system, the new MVC replacement must cover its full behaviour before moving to the next phase. The game must be playable at every phase boundary.
- **`IGameConfiguration` is shared via VContainer.** The existing `GameConfiguration` ScriptableObject already implements the new `BalloonParty.Configuration.IGameConfiguration` — register it once in `GameLifetimeScope` and never access it through `Contexts.sharedInstance` in new code.
- **No new code in `Source_Old`.** Bug fixes in legacy systems are only allowed if they unblock a migration step. Every fix applied to `Source_Old` should immediately inform the equivalent new implementation.
- **Entitas context access is a hard boundary.** New code in `Assets/Source` must never reference `Contexts`, `GameEntity`, `GameMatcher`, or any Entitas type. If temporary data needs to flow from a legacy system to a new one during transition, use a MessagePipe message as the bridge — not a shared Entitas entity.

### Removal Checklist (to complete before Phase 8)

Each item must be ticked before `Source_Old` and the Entitas package can be deleted:

- [ ] `SloIndexerSystem` → replaced by `SlotGrid` (Phase 2)
- [ ] `BalanceBalloonsSystem` → replaced by `BalloonBalancer` (Phase 3)
- [ ] `GameStartedBalloonsSpawnSystem`, `BalloonLineSpawnerSystem`, `NewBalloonLinesInstanceSystem` → replaced by `BalloonSpawner` (Phase 4)
- [ ] `AssetInstancingSystem` → replaced by VContainer factory in `BalloonSpawner` (Phase 4)
- [ ] `ThrowerDirectionSystem`, `ThrowerRotationSystem`, `ThrowLoadedProjectileSystem` → replaced by `ThrowerController` (Phase 5)
- [ ] `FreeProjectileMovementSystem`, `ProjectileBounceSystem`, `ProjectileTransformSystem` → replaced by `ProjectileController` (Phase 5)
- [ ] `BalloonCollisionSystem`, `TriggerReporterController`, `Cleanup2DTriggersSystem` → replaced by `OnTriggerEnter2D` on `ProjectileView` (Phase 5)
- [ ] `BalloonHitDestructionSystem`, `BalloonHitNudgeAnimationSystem`, `BalloonHitScoreSystem` → replaced by `BalloonController` + `ScoreController` (Phase 6)
- [ ] `BalloonsPowerUpCheckSystem`, all `*PowerUpController` → replaced by power-up controllers (Phase 7)
- [ ] `GameControllerBehaviour`, `GameController`, `GameUpdateSystems`, `GameFixedUpdateSystems` → replaced by `GameManager` + `GameLifetimeScope` (Phase 8)
- [ ] All Entitas-generated code in `Assets/Generated/` → deleted (Phase 8)
- [ ] Entitas and DesperateDevs packages removed from `manifest.json` (Phase 8)

---

## Key Library Patterns Quick Reference

### UniRx
```csharp
// Reactive model property — auto-updates view on change
model.Color.Subscribe(c => spriteRenderer.color = colorMap[c]).AddTo(this);

// MessagePipe message as observable stream
_subscriber.Subscribe(_ => BalanceBalloons()).AddTo(_disposable);

// Input as stream
Observable.EveryUpdate()
          .Where(_ => Input.GetMouseButtonDown(0))
          .Subscribe(_ => Fire())
          .AddTo(this);
```

### VContainer
```csharp
// GameLifetimeScope.cs
protected override void Configure(IContainerBuilder builder)
{
    // Singleton service
    builder.Register<SlotGrid>(Lifetime.Singleton);

    // ScriptableObject instance
    builder.RegisterInstance<IGameConfiguration>(gameConfigSO);

    // Entry points (IStartable / ITickable)
    builder.RegisterEntryPoint<GameManager>();
    builder.RegisterEntryPoint<BalloonBalancer>();

    // MessagePipe
    builder.RegisterMessagePipe();
    builder.RegisterMessageBroker<BalanceBalloonsMessage>();
    builder.RegisterMessageBroker<BalloonHitMessage>();
}
```

### MessagePipe
```csharp
// Publisher (fires message)
[Inject] IPublisher<BalanceBalloonsMessage> _publisher;
_publisher.Publish(default);

// Subscriber (reacts to message)
[Inject] ISubscriber<BalanceBalloonsMessage> _subscriber;
_subscriber.Subscribe(_ => BalanceBalloons()).AddTo(_disposable);
```

### Injection
```csharp
[Inject] private IGameConfiguration _config;
[Inject] private IPublisher<BalloonHitMessage> _publisher;
[Inject] private ISubscriber<BalloonHitMessage> _subscriber;
```

---

## Code Quality Constraints

These constraints apply to all code generated or written during this migration.

### Comments
- **Only comment the *why***, never the *what* or *how* — if the code needs a comment to explain what it does, it should be renamed or refactored instead.
- **No redundant comments.** Avoid comments like `// inject dependencies`, `// constructor`, `// update position` above self-evident code.
- **No block comment headers** on every file or class (e.g. `// ====== BalloonModel ======`).
- XML doc comments (`/// <summary>`) only on public API surfaces that are non-obvious to a consumer.

### Naming & Readability
- Code must be **self-explanatory through naming, namespaces, and context**. A reader should understand intent without comments.
- Prefer longer, descriptive names over short ambiguous ones (`FindOptimalEmptySlot` over `GetSlot`).
- Namespaces must reflect folder structure (e.g. `BalloonParty.Balloon.Model`, `BalloonParty.Slots`).

### Architecture & Reuse
- **Before writing new code, check for existing methods** in the codebase (including `Source_Old`) that can be ported, extracted, or called directly.
- **Identify commonalities** across systems early — if two controllers share a pattern, extract it into a base class or generic utility.
- **Prefer generic implementations** over copy-paste specialisations (e.g. a generic `ModelView<TModel>` base for all View MonoBehaviours rather than boilerplate per class).
- **Extension methods** over utility classes where possible — keep them in a dedicated `Extensions/` namespace.
- Keep classes **small and focused** — if a class is growing beyond one clear responsibility, split it.
- Avoid `static` state; prefer injected singleton services via VContainer.

---

## Progress Tracker

| Phase | Description                        | Status  |
|-------|------------------------------------|---------|
| 0     | Preparation & Folder Scaffold      | ✅ Done |
| 1     | Balloon Model + View               | ✅ Done |
| 2     | Slot Grid Model & Placement        | ✅ Done |
| 3     | Balance / Movement Logic           | ⬜ Todo |
| 4     | Balloon Spawning & Line Management | ⬜ Todo |
| 5     | Projectile & Thrower               | ⬜ Todo |
| 6     | Hit, Destruction & Score           | ⬜ Todo |
| 7     | Power-Ups                          | ⬜ Todo |
| 8     | Game Loop, UI & Cleanup            | ⬜ Todo |

