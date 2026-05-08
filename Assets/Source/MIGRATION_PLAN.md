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
    Controller/     BalloonController.cs, BalloonBalancer.cs
    Spawner/        BalloonSpawner.cs, BalloonSpawnerSettings.cs
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
  Debug/
    ICheat.cs, CheatConsoleView.cs, BalloonRemoverCheat.cs, ...

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

1. Create `Assets/Source/Shared/Messages/BalanceBalloonsMessage.cs` — empty struct signal that triggers a balance pass via MessagePipe.
2. Register in `GameLifetimeScope`:
   ```csharp
   var options = builder.RegisterMessagePipe();
   builder.RegisterMessageBroker<BalanceBalloonsMessage>(options);
   ```
3. Create `Assets/Source/Balloon/Controller/BalloonBalancer.cs` implementing `IStartable`
   - `[Inject] ISubscriber<BalanceBalloonsMessage> _subscriber`
   - `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`
   - In `Start()`, subscribe to `BalanceBalloonsMessage` and run the bubble-loop algorithm from `BalanceBalloonsSystem`: bottom-up scan for unbalanced slots, move each to its optimal empty slot, collect per-balloon movement paths
   - Animate each balloon along its path via `DOPath` (CatmullRom); kill any in-progress tween before starting a new one
   - Set `BalloonModel.IsStable.Value = true` in tween `onComplete`
4. Add `BalloonModel.View` — a back-reference to `BalloonView` set at bind time so the balancer can reach the transform without coupling to Unity from the model.
5. Any code that previously set `isBalloonsBalanceEvent` now injects `IPublisher<BalanceBalloonsMessage>` and calls `_publisher.Publish(default)`.
6. Register `BalloonBalancer` as an entry point:
   ```csharp
   builder.RegisterEntryPoint<BalloonBalancer>();
   ```
7. Add `BalloonRemoverCheat` (`Debug/`) — draw-to-remove debug tool: hold left mouse to trace a path, red circles preview which balloons will be removed, mouse up removes them from the grid and triggers a balance pass. View destruction is handled directly in the cheat until Phase 6 provides the proper pipeline.
8. ✅ **Checkpoint:** Use the "Remove Balloons" cheat → drag across balloons → release → balloons disappear and remaining ones animate into gaps.

---

## Phase 4 — Balloon Spawning & Line Management

**Goal:** Port spawner systems using a VContainer entry point and MessagePipe — replacing `BalloonLineSpawnerSystem`, `GameStartedBalloonsSpawnSystem`, and `NewBalloonLinesInstanceSystem`.

> References: `BalloonLineSpawnerSystem.cs`, `GameStartedBalloonsSpawnSystem.cs`, `NewBalloonLinesInstanceSystem.cs`

1. Create `Assets/Source/Shared/Messages/SpawnBalloonLineMessage.cs` — empty struct that triggers spawning of one balloon line.
2. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<SpawnBalloonLineMessage>(options);
   ```
3. Create `Assets/Source/Balloon/Spawner/BalloonSpawner.cs` implementing `IStartable`
   - `[Inject] SlotGrid _grid`, `[Inject] IGameConfiguration _config`, `[Inject] IObjectResolver _resolver`
   - `[Inject] ISubscriber<SpawnBalloonLineMessage> _lineSubscriber`
   - `[Inject] IPublisher<BalanceBalloonsMessage> _balancePublisher`
   - In `Start()`: subscribe to `SpawnBalloonLineMessage` → call `SpawnLine()`
   - `SpawnLine()` — finds the bottom-most empty row in each column (mirrors `BottomSlotsIndexes` logic), picks a random color from `IGameConfiguration.BalloonColors`, calls `SpawnBalloon()` for each, then publishes `BalanceBalloonsMessage`
   - `SpawnBalloon(colorName, slot)` — instantiates the balloon prefab (injected via `BalloonSpawnerSettings`), creates a `BalloonModel`, wires up a `BalloonController`, places the model in `SlotGrid`, and animates the balloon rising from below to its slot using DOTween `DOMove` + `DOScale` — start position is `slot + Vector2Int.up * 4` in grid space, mirroring the `OnLinkedView` animation in the legacy system
4. Register as an entry point:
   ```csharp
   builder.RegisterEntryPoint<BalloonSpawner>();
   ```
5. Add `SpawnBalloonLineCheat` (`Debug/`) — publishes `SpawnBalloonLineMessage` from the cheat console to spawn a new line on demand.
6. ✅ **Checkpoint:** Press Play → use the "Spawn Balloon Line" cheat → a new row of balloons rises from below and settles into the available slots.

---

## Phase 5 — Projectile & Thrower

**Goal:** Port thrower direction, rotation, loading, firing, bounce, and collision.

> References: `ThrowerDirectionSystem`, `ThrowerRotationSystem`, `ThrowLoadedProjectileSystem`, `ProjectileBounceSystem`, `ProjectileTransformSystem`, `BalloonCollisionSystem`

1. Create `Assets/Source/Shared/Messages/ProjectileDestroyedMessage.cs` — empty struct published when the projectile runs out of shields.
2. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<ProjectileDestroyedMessage>(options);
   ```
3. Create `Assets/Source/Projectile/Model/ProjectileModel.cs` — plain C# data object: `Direction`, `Speed`, `ShieldsRemaining`, `IsFree`, `ColorName`, `ColorPopCount`, `LastHitBalloon`.
4. Create `Assets/Source/Projectile/View/ProjectileView.cs` (MonoBehaviour on the projectile prefab)
   - `[Inject]` receives `IGameConfiguration`, `IPublisher<BalanceBalloonsMessage>`, `IPublisher<ProjectileDestroyedMessage>`, `SlotGrid`
   - `FixedUpdate()` drives manual movement (direction × speed × fixedDeltaTime); checks `LimitsClockwise` for wall hits; reflects direction, clamps position, decrements shields; destroys self and publishes both messages when shields < 0
   - `OnTriggerEnter2D` resolves `BalloonView` from the collider, tracks absorbed color (exact `BalloonCollisionSystem` logic), runs DOTween neighbor-nudge sequence matching `BalloonHitNudgeAnimationSystem`
5. Create `Assets/Source/Thrower/ThrowerController.cs` (MonoBehaviour, `IStartable`, scene-placed)
   - `[Inject]` receives `IGameConfiguration`, `IObjectResolver`, `SlotGrid`, `ISubscriber<ProjectileDestroyedMessage>`
   - `[SerializeField] GameObject _projectilePrefab` — holds the projectile prefab reference
   - `Start()`: DOMove from `ThrowerSpawnPoint + Vector2.down` to `ThrowerSpawnPoint` over 1 second (matches `GameStartedThrowerSpawnSystem`), then calls `LoadProjectile()`
   - `Update()`: updates direction from mouse (matches `ThrowerDirectionSystem`), rotates transform (matches `ThrowerRotationSystem`), orbits loaded projectile around spawn point (matches `ProjectileTransformSystem`), fires on mouse-up when all balloons are stable (matches `ThrowLoadedProjectileSystem`)
   - Subscribes to `ProjectileDestroyedMessage` → reloads
6. Add `BalloonView.Model` public property — set in `Bind()` so `ProjectileView` can reach the `BalloonModel` from a trigger collider.
7. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterComponentInHierarchy<ThrowerController>().AsImplementedInterfaces().AsSelf();
   ```
8. Add `FireProjectileCheat` (`Debug/`) — calls `ThrowerController.FireImmediate()` to force-fire the loaded projectile regardless of mouse state.
9. ✅ **Checkpoint:** Thrower slides into position → loads projectile → aim with mouse → release to fire → projectile bounces off walls → hitting balloons nudges neighbors → when shields deplete, grid rebalances and thrower reloads.

---

## Phase 6 — Balloon Hit, Destruction & Score

**Goal:** Port hit/pop/score/nudge systems using MessagePipe, including the full per-color progress bar UI.

> References: `BalloonHitDestructionSystem`, `BalloonHitScoreSystem`, `BalloonHitNudgeAnimationSystem`, `BalloonAnimationController`, `ColorProgressBar`, `ColorProgressBarInstancer`, `LevelUpPopUp`, `LevelLabel`

1. `BalloonHitMessage` — struct carrying `BalloonModel` + `WorldPosition` (captured before the view is destroyed)
2. `BalloonController` injects `ISubscriber<BalloonHitMessage>`, filters by its own model:
   - Calls `BalloonView.PlayPopEffect(color)` — instantiates `PSVFX_BalloonPop` at world position, sets `startColor`, auto-destroys
   - Removes from `SlotGrid`, destroys the view GO, publishes `BalanceBalloonsMessage`
3. `ScoreController` (`IStartable` + `IDisposable`):
   - Loads per-color persistent score + level progress from `PlayerPrefs` on `Start()`
   - On each `BalloonHitMessage`: increments that color's persistent score and level progress; checks if all colors meet `IGameConfiguration.PointsRequiredForLevel(level + 1)`; on level-up resets all progress, increments `Level`, publishes `ScoreLevelUpMessage`, and pauses via `Time.timeScale = 0`
   - Publishes `BalloonScoredMessage` (carries `ColorName`, `WorldPosition`, `TotalScore`) after every hit
   - Saves to `PlayerPrefs` on `Application.quitting` and focus-lost
   - Exposes `ReactiveProperty<int> TotalScore`, `ReactiveProperty<int> Level`, `GetProgress(colorName)`, `GetRequiredPoints()`
4. Per-color progress bar UI (`Assets/Source/UI/`):
   - `ColorProgressBarInstancer` — spawns one `ColorProgressBar` per `IGameConfiguration.BalloonColors` entry at Start; injects the bar via `IObjectResolver.Inject()` then calls `bar.Setup(color, scoreController)`
   - `ColorProgressBar` — subscribes to `BalloonScoredMessage` (filtered by color) and `ScoreLevelUpMessage`; drives a `Slider`; owns its own `ScoreNotice` and `ScorePointTrail` pools; on trail arrival triggers `"TrailHit"` animator; on bar completion plays `completionParticleSystem` + `"Completed"` animator bool; on level-up resets slider max/value and VFX
   - `ScorePointTrail` — orb that `DOMove`s FROM balloon world position TO the bar's position (not a global counter)
   - `ScoreNotice` — floating "+N" popup pooled per bar; uses `Animator` + `Text`
   - `LevelLabel` — `[RequireComponent(Text)]`; subscribes to `ScoreController.Level`; `_showNextLevel` bool shows `level + 1`
   - `ScoreCounterLabel` — subscribes to `ScoreController.TotalScore`
5. `LevelUpPopUp` — waits for `SlotGrid.AllBalloonsStable()`, triggers `"Appear"` animator, animates glow fill image + particle system using `Time.unscaledDeltaTime` (game is paused); `OnContinue()` button restores `Time.timeScale = 1`
6. `SlotGrid.AllBalloonsStable()` — scans all slots, returns true when every non-null model has `IsStable.Value == true`
7. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<BalloonScoredMessage>(options);
   builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
   builder.RegisterEntryPoint<ScoreController>().AsSelf();
   builder.RegisterComponentInHierarchy<ColorProgressBarInstancer>();
   builder.RegisterComponentInHierarchy<LevelUpPopUp>();
   ```
8. ✅ **Checkpoint:** Hit balloon → colored pop VFX → correct color bar fills → orb flies to that bar → "+N" notice pops at bar → all bars full → level-up popup appears → glow fills → Continue resumes game → progress reset.

---

## Phase 7a — Score UI (Progress Bars, Notices, Trails)

**Goal:** Wire the per-color progress bar system so scoring feedback is fully visible in Play Mode.

> All C# files in this phase are **already coded**. This phase is Unity Editor wiring only.

| File | Status |
|---|---|
| `ColorProgressBar.cs` | ✅ Coded |
| `ColorProgressBarInstancer.cs` | ✅ Coded |
| `ScoreNotice.cs` | ✅ Coded |
| `ScorePointTrail.cs` | ✅ Coded |
| `ScoreCounterLabel.cs` | ✅ Coded |
| `LevelLabel.cs` | ✅ Coded |
| `LevelUpPopUp.cs` | ✅ Coded |
| `BalloonScoredMessage` | ✅ Coded |
| `ScoreLevelUpMessage` | ✅ Coded |

**Unity Editor steps:**

1. **`ColorProgressBar` prefab** — duplicate the legacy prefab; swap all legacy MonoBehaviours for the new `ColorProgressBar`; wire Inspector slots: `_graphicsToSetColor` (color-tinted images), `_progressSlider`, `_animator`, `_completionParticleSystem`, `_noticePrefab` (ScoreNotice prefab), `_trailPrefab` (ScorePointTrail prefab)
2. **`ColorProgressBarInstancer`** — place a GameObject under the UI Canvas; add `ColorProgressBarInstancer`; assign the `ColorProgressBar` prefab; VContainer auto-injects the rest
3. **`ScoreCounterLabel`** — add to the total-score `Text` element
4. **`LevelLabel`** — add to the current-level `Text`; add a second instance with `_showNextLevel` ticked on the "next level" label
5. **`LevelUpPopUp`** — add to the popup GameObject; wire `_animator`, `_levelLabel`, `_levelGlowFill`, `_levelGlowFillParticleSystem`, and the three delay floats; wire **Continue** button `OnClick` → `LevelUpPopUp.OnContinue()`; add `RegisterComponentInHierarchy<LevelUpPopUp>()` to `GameLifetimeScope`
6. **Disable legacy** — in the scene, disable: old `ColorProgressBarInstancer`, old `ScoreCounterLabel`, old `LevelLabel`, old `LevelUpPopUp`, old `GameScoreController`

✅ **Checkpoint:** Hit balloons → correct color bar fills → score notice pops at bar → trail orb flies to bar → all bars complete → level-up popup appears (game pauses) → glow fills → Continue resumes.

---

## Phase 7b — Shield Counter HUD

**Goal:** Wire the shield counter HUD so bounce feedback is visible and driven by `ProjectileModel.ShieldsRemaining`.

> All C# files in this phase are **already coded**. This phase is Unity Editor wiring + one code touch in `ThrowerController`.

| File | Status |
|---|---|
| `ShieldCounterLabel.cs` | ✅ Coded |
| `ShieldCounterAnimation.cs` | ✅ Coded |
| `ProjectileLoadedMessage` | ✅ Coded |
| `ProjectileModel.ShieldsRemaining` → `ReactiveProperty<int>` | ✅ Coded |
| `ThrowerController` publishes `ProjectileLoadedMessage` | ✅ Coded |
| `ThrowerController` injects + calls `ShieldCounterAnimation.BindProjectile` | ❌ Needs code |

**Code step — bind `ShieldCounterAnimation` from `ThrowerController`:**

Add to `ThrowerController`:
```csharp
[Inject] private ShieldCounterAnimation _shieldAnim;
```
Call after creating the model in `LoadProjectile()`:
```csharp
_shieldAnim.BindProjectile(_activeProjectile);
```
Register in `GameLifetimeScope`:
```csharp
builder.RegisterComponentInHierarchy<ShieldCounterAnimation>();
```

**Unity Editor steps:**

1. **`ShieldCounterLabel`** — add to the shield count `Text` element; VContainer auto-injects subscribers and config
2. **`ShieldCounterAnimation`** — add to the Animator GameObject; VContainer auto-injects subscribers
3. **Disable legacy** — disable old `ShieldCounterLabel` and `ShieldCounterAnimation` MonoBehaviours in the scene

✅ **Checkpoint:** Thrower loads → shield counter shows starting value → each wall bounce decrements with `"Lost"` animation → projectile destroyed and reloaded → counter resets with `"Ready"` animation.

---

## Phase 7c — Game Start

**Goal:** Replace the legacy `isGameStarted` entity flag with `GameStartButton` publishing `SpawnBalloonLineMessage`, so the game can be started independently of any Entitas system.

| File | Status |
|---|---|
| `GameStartButton.cs` | ✅ Coded |

**Unity Editor steps:**

1. **`GameStartButton`** — add to the start button GameObject in the scene
2. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterComponentInHierarchy<GameStartButton>();
   ```
3. **Disable legacy** — disable the old `GameStartButton` MonoBehaviour and comment out the `isGameStarted` handling in `GameUpdateSystems` / `GameFixedUpdateSystems`

✅ **Checkpoint:** Press the start button → initial balloon lines spawn and settle → thrower slides in → game is fully playable without Entitas handling game start.

---

## Phase 7d — HUD Audit & Cleanup

**Goal:** Confirm zero active Entitas UI in the scene; remove stubs.

1. Open the scene; search for any remaining MonoBehaviours from `Source_Old/UI/` still active — `grep` for `IAny*Listener` or inspect each UI GameObject
2. For each still-active legacy component, either port it or confirm it is superseded and disable it
3. Verify `GameLifetimeScope` has a `RegisterComponentInHierarchy` call for every new UI component that needs injection
4. Delete `ScoreUIController.cs` (already a comment-only stub)
5. ✅ **Checkpoint:** No `Contexts.sharedInstance` calls execute from any UI component during Play Mode; all HUD elements update reactively.

---

## Phase 8 — Power-Ups

**Goal:** Port the 5 power-up controllers using MessagePipe and VContainer.

> References: `BombPowerUpController`, `LaserPowerUpController`, `LightningPowerUpController`, Shield, `BalloonsPowerUpCheckSystem`

1. Create `Assets/Source/Shared/Messages/PowerUpActivatedMessage.cs` — carries power-up type and position
2. Create `Assets/Source/Balloon/PowerUps/` — one controller per power-up, each injecting `IPublisher`/`ISubscriber` and `SlotGrid`
3. Subscribe to `BalloonHitMessage` to check power-up trigger conditions (replaces `BalloonsPowerUpCheckSystem`)
4. ✅ **Checkpoint:** Power-up triggers and visual effects work correctly.

---

## Phase 9 — Game Loop, UI & Cleanup

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

### UI Code — Copy-Forward Strategy

Most UI MonoBehaviours in `Source_Old/UI/` have **no direct Entitas dependency** beyond a `Start()` that fetches `Contexts.sharedInstance`. They are safe to port almost verbatim:

1. Copy the file into `Assets/Source/UI/`.
2. Remove the `Contexts.sharedInstance` fetch and any `IGroup<GameEntity>` / listener interface.
3. Replace the data source with an `[Inject]` reference — either a `ReactiveProperty<T>` from a controller (e.g. `ScoreController.TotalScore`) or an `ISubscriber<T>` for event-driven updates.
4. Use `.Subscribe(...).AddTo(this)` (UniRx) instead of the Entitas listener callback.
5. **Always run an error check immediately after copying or editing a UI file.** The tooling sometimes silently appends a duplicate class body at the end of the file, which causes cryptic compile errors. If errors appear on lines well past the closing `}` of the class, truncate the file at the first `}` that closes the namespace.

Files that are already pure visual with no Entitas coupling (`ShieldCounterAnimation`, `ColorProgressBar`, `AspectRatio`, etc.) can be copied with **zero logic changes** — just update the namespace and inject any config they need.

---

### Coexistence Strategy (Phases 1–8)

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
- [ ] `BalloonsPowerUpCheckSystem`, all `*PowerUpController` → replaced by power-up controllers (Phase 8)
- [ ] `GameControllerBehaviour`, `GameController`, `GameUpdateSystems`, `GameFixedUpdateSystems` → replaced by `GameManager` + `GameLifetimeScope` (Phase 9)
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

### Animation Fidelity

All tween animations must reproduce the original behaviour **exactly** — same ease, same duration source, same start/end values. Do not substitute eases or durations unless there is an explicit design reason to change them.

When porting an animation:
1. Read the original tween call in `Source_Old` before writing any new code.
2. Match the ease type — if the original omits `SetEase`, use DOTween's default (`InOutQuad`); do not add `OutBack`, `OutBounce`, or any other ease unless it was there originally.
3. Use the same configuration fields for timing (`IGameConfiguration.BalloonSpawnAnimationDurationRange`, `TimeForBalloonsBalance`, etc.) — never substitute hardcoded values.
4. Match the start conditions (e.g. scale starts at `Vector3.zero`, position offset is `+Vector2Int.up * 4` rows in grid space).

---

### `IGameConfiguration` as the Single Source of Truth

All game data — balloon colors, slot dimensions, timing values, spawn counts — lives in the `GameConfiguration` ScriptableObject and is accessed exclusively through `IGameConfiguration`. Rules:

- **Never hardcode** color names, slot counts, or timing values in new code. Always read from `IGameConfiguration`.
- **Never use `[SerializeField]`** to duplicate data that already exists in `GameConfiguration` (e.g. no `_colors` array on a spawner — use `_config.BalloonColors`).
- `IGameConfiguration` is registered once as a singleton in `GameLifetimeScope` and injected wherever needed — no `Contexts.sharedInstance` access in new code.
- When porting a legacy system, check what configuration fields it reads and ensure they are present on `BalloonParty.Configuration.IGameConfiguration` before writing the new implementation.

---

### Cheat Console

A self-building runtime debug console lives in `Assets/Source/Debug/`. Press **backtick (`)** in Play Mode to toggle it.

**Adding a cheat:**
1. Implement `ICheat` — provide `Name`, `Section`, and `Tags[]`
2. Inject the publishers or services it needs via constructor
3. Register in `GameLifetimeScope`: `builder.Register<YourCheat>(Lifetime.Singleton).AsImplementedInterfaces()`

The console discovers all registered `ICheat` implementations automatically. Features: live search by name, tag filter pills, section grouping, and per-cheat favorites (★).

Every phase that introduces a new triggerable behaviour should add a corresponding cheat so it can be tested in isolation without running the full game loop.

---

### Living Documentation

Each feature folder contains a `README.md` that describes what that feature covers — its gameplay purpose, how it works, and how it interacts with other systems. These are not implementation notes; they explain intent and behaviour at the feature level.

**Keep them current.** Update a folder's `README.md` whenever:
- A new mechanic is added or an existing one changes significantly
- A system's responsibility shifts (e.g. a controller absorbs logic from another)
- Interactions with other systems are added, removed, or change in character

The test for whether a README needs updating: if a new developer read only that file, would they still have an accurate picture of what the feature does and how it connects to the rest of the game?

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

| Phase | Description                               | Status    |
|-------|-------------------------------------------|-----------|
| 0     | Preparation & Folder Scaffold             | ✅ Done   |
| 1     | Balloon Model + View                      | ✅ Done   |
| 2     | Slot Grid Model & Placement               | ✅ Done   |
| 3     | Balance / Movement Logic                  | ✅ Done   |
| 4     | Balloon Spawning & Line Management        | ✅ Done   |
| 5     | Projectile & Thrower                      | ✅ Done   |
| 6     | Hit, Destruction & Score                  | ✅ Done   |
| 7a    | Score UI — Progress Bars, Notices, Trails | ⬜ Wiring |
| 7b    | Shield Counter HUD                        | ⬜ Code + Wiring |
| 7c    | Game Start                                | ⬜ Wiring |
| 7d    | HUD Audit & Cleanup                       | ⬜ Todo   |
| 8     | Power-Ups                                 | ⬜ Todo   |
| 9     | Game Loop, UI & Cleanup                   | ⬜ Todo   |

