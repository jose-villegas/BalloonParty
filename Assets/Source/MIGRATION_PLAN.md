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
- **Async:** [UniTask](https://github.com/Cysharp/UniTask) — `async`/`await` replacing Unity coroutines for delays, polling, and frame yields
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
    PowerUps/       Bomb/, Laser/, Lightning/, Shield/   ← Phase 8
  Slots/
    SlotGrid.cs, SlotGridChangedEvent.cs
    SlotGridController.cs, SlotGridView.cs
  Projectile/
    ProjectileLifetimeScope.cs, ProjectilePoolChannel.cs
    Model/          ProjectileModel.cs
    View/           ProjectileView.cs, ProjectileShieldView.cs, ProjectileTrail.cs
    Controller/     ← reserved; logic currently lives in ProjectileView
  Thrower/
    ThrowerController.cs, ThrowerSettings.cs
  Game/
    ScoreController.cs
    GameLifetimeScope.cs   ← root VContainer scope
    GameChildLifetimeScope.cs ← abstract base for all child scopes
    GameManager.cs         ← Phase 9
  Shared/
    IGameConfiguration.cs, IReusable.cs
    Pool/           PoolChannel.cs, IPoolable.cs, PoolManager.cs,
                    PoolableParticle.cs, VfxPoolChannel.cs
    Messages/       BalanceBalloonsMessage.cs, BalloonHitMessage.cs,
                    BalloonScoredMessage.cs, ScoreLevelUpMessage.cs,
                    SpawnBalloonLineMessage.cs, ProjectileDestroyedMessage.cs,
                    ProjectileLoadedMessage.cs
    Extensions/     ← reserved for future extension methods
  UI/
    Score/          ColorProgressBar.cs, ColorProgressBarInstancer.cs,
                    ScoreNotice.cs, ScorePointTrail.cs,
                    ScoreCounterLabel.cs, LevelLabel.cs,
                    ScoreUILifetimeScope.cs   ← child VContainer scope
    LevelUp/        LevelUpPopUp.cs, LevelUpLifetimeScope.cs
    Shields/        ShieldCounterLabel.cs, ShieldCounterAnimation.cs,
                    ShieldUILifetimeScope.cs   ← child VContainer scope
    GameStart/      GameStartButton.cs
  Debug/
    ICheat.cs, CheatConsoleView.cs
    BalloonRemoverCheat.cs, SpawnBalloonLineCheat.cs,
    FireProjectileCheat.cs, TriggerLevelUpCheat.cs, NearLevelUpCheat.cs,
    ScoreCheatHelper.cs   ← shared helper for score-related cheats
  README.md              ← each feature folder contains a README.md (living documentation)

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

## Phase 6 — Balloon Hit, Destruction & Score Logic

**Goal:** Port the hit pipeline and scoring mechanics — pop VFX, grid removal, score tracking, level-up, and persistence. No UI wiring required; verify with the cheat console.

> References: `BalloonHitDestructionSystem`, `BalloonHitScoreSystem`, `BalloonAnimationController`

1. `BalloonHitMessage` — struct carrying `BalloonModel` + `WorldPosition` (position captured before the view is destroyed; used later by score trail UI)
2. `BalloonController` subscribes to `BalloonHitMessage`, filtered by its own model:
   - Calls `BalloonView.PlayPopEffect(color)` — instantiates `PSVFX_BalloonPop` at world position, sets `startColor`, auto-destroys after particle lifetime
   - Removes model from `SlotGrid`, destroys the view GO, publishes `BalanceBalloonsMessage`
3. `ScoreController` (`IStartable` + `IDisposable`):
   - Loads per-color persistent score + level progress from `PlayerPrefs` on `Start()`
   - On each `BalloonHitMessage`: increments that color's persistent score and level progress; checks if all colors meet `IGameConfiguration.PointsRequiredForLevel(level + 1)`; on level-up resets all progress, increments `Level`, publishes `ScoreLevelUpMessage`, pauses via `Time.timeScale = 0`
   - Publishes `BalloonScoredMessage` (carries `ColorName`, `WorldPosition`, `TotalScore`) after every hit — consumed by score trail UI in Phase 7a
   - Saves to `PlayerPrefs` on `Application.quitting` and focus-lost
   - Exposes `ReactiveProperty<int> TotalScore`, `ReactiveProperty<int> Level`, `GetProgress(colorName)`, `GetRequiredPoints()`
4. `SlotGrid.AllBalloonsStable()` — scans all slots, returns true when every non-null model has `IsStable.Value == true` (used by `LevelUpPopUp` in Phase 7a)
5. Register in `GameLifetimeScope`:
   ```csharp
   builder.RegisterMessageBroker<BalloonScoredMessage>(options);
   builder.RegisterMessageBroker<ScoreLevelUpMessage>(options);
   builder.RegisterEntryPoint<ScoreController>().AsSelf();
   ```
6. Update `BalloonRemoverCheat` — replace direct `Destroy` with `_hitPublisher.Publish(new BalloonHitMessage(model, worldPos))` so removal routes through the full destruction pipeline
7. ✅ **Checkpoint:** Use the "Remove Balloons" cheat → balloons pop with colored VFX → `ScoreController.TotalScore` increments (verify in debugger or cheat console) → balance animation triggers. No UI required for this checkpoint.

---

## Phase 7a — Score Feedback UI (Progress Bars, Notices, Trails, Labels)

**Goal:** Wire per-hit score feedback so every balloon pop updates the correct color bar, spawns a trail orb and notice, and reflects in the level/total-score labels.

> All C# files were written during Phase 6. This phase is **Unity Editor wiring only**.

| File | What it does |
|---|---|
| `ColorProgressBar.cs` | Per-color slider; owns `ScoreNotice` + `ScorePointTrail` pools; completion VFX |
| `ColorProgressBarInstancer.cs` | Spawns one `ColorProgressBar` per color at Start |
| `ScoreNotice.cs` | Pooled floating "+N" popup at the bar |
| `ScorePointTrail.cs` | Pooled orb that flies from balloon world position → bar position |
| `ScoreCounterLabel.cs` | Binds total-score `Text` to `ScoreController.TotalScore` |
| `LevelLabel.cs` | Binds level `Text` to `ScoreController.Level`; `_showNextLevel` toggle |

**`ScoreUILifetimeScope` registration** (code — already done):
```csharp
builder.RegisterComponentInHierarchy<ColorProgressBarInstancer>();
```

**Unity Editor steps:**

1. **`ColorProgressBar` prefab** — duplicate legacy; remove old MonoBehaviours; add new `ColorProgressBar`; wire `_graphicsToSetColor`, `_progressSlider`, `_animator`, `_completionParticleSystem`, `_noticePrefab`, `_trailPrefab`
2. **`ColorProgressBarInstancer`** — add to the Score UI Canvas root (the same GameObject that has `ScoreUILifetimeScope`); assign the `ColorProgressBar` prefab in the Inspector
3. **`ScoreCounterLabel`** — add to the total-score `Text` element
4. **`LevelLabel`** — add to the current-level `Text`; add a second instance with `_showNextLevel` ticked for the "next level" label
5. **Disable legacy** — disable: old `ColorProgressBarInstancer`, old `ScoreCounterLabel`, old `LevelLabel`, old `GameScoreController`

✅ **Checkpoint:** Hit balloons → correct color bar fills → score notice pops at bar → trail orb flies to bar → score counter and level labels update. Bars completing is visible but the level-up popup is not yet wired.

---

## Phase 7b — Level-Up Popup

**Goal:** Wire the full-screen level-up ceremony that fires when all color bars complete — game pauses, glow fills, player continues.

> `LevelUpPopUp.cs` lives in `UI/LevelUp/`. This phase requires creating `LevelUpLifetimeScope` and removing `LevelUpPopUp` from `ScoreUILifetimeScope`.

| File | What it does |
|---|---|
| `LevelUpPopUp.cs` | Waits for `AllBalloonsStable()`; triggers `"Appear"` animator; animates glow fill using `Time.unscaledDeltaTime`; `OnContinue()` restores `Time.timeScale = 1` |
| `LevelUpLifetimeScope.cs` | Child scope on the LevelUp popup root — registers only what the popup needs |

**Registration** — remove `LevelUpPopUp` from `ScoreUILifetimeScope` and create `LevelUpLifetimeScope`:
```csharp
// LevelUpLifetimeScope.cs — on the LevelUp popup root GameObject
protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterComponentInHierarchy<LevelUpPopUp>();
}
```

**Test cheats** (code — already done, registered in `GameLifetimeScope`):
- **"Trigger Level Up"** — fills all color bars to the current threshold and immediately triggers the popup. Use this to test the full ceremony without playing.
- **"Near Level Up"** — fills all bars to one point below the threshold. Pop one balloon of each color in-game to complete the level naturally and verify the transition from gameplay into the popup.

**Unity Editor steps:**

1. Create `LevelUpLifetimeScope.cs` in `UI/LevelUp/` — child `LifetimeScope` that registers `LevelUpPopUp` via `RegisterComponentInHierarchy`
2. Add `LevelUpLifetimeScope` to the LevelUp popup root GameObject in the scene; set its **Parent** to `GameLifetimeScope` (or leave auto-discovered via `EnqueueParent`)
3. Remove `builder.RegisterComponentInHierarchy<LevelUpPopUp>()` from `ScoreUILifetimeScope`
4. **`LevelUpPopUp`** — add to the popup GameObject; wire `_animator`, `_levelLabel`, `_levelGlowFill`, `_levelGlowFillParticleSystem`, and the three delay floats (`_fillAnimationDelay`, `_playParticlesDelay`, `_continueUnpauseDelay`)
5. Set the popup's **Animator Update Mode → Unscaled Time** — the game is paused at level-up so a Normal-mode Animator will freeze
6. Ensure the popup GameObject is **active** in the scene — it hides via CanvasGroup alpha, not `SetActive`; if disabled, `Start()` never runs
7. Wire the **Continue** button `OnClick` → `LevelUpPopUp.OnContinue()`
8. **Disable legacy** — disable the old `LevelUpPopUp` MonoBehaviour

✅ **Checkpoint:** All color bars complete → game pauses → level-up popup appears with previous level number → glow particle fills the circle → level number updates to the new level → Continue resumes the game and resets all bars.

---

## Phase 7c — Shield Counter HUD

**Goal:** Wire the shield counter HUD so bounce feedback is visible and driven by `ProjectileModel.ShieldsRemaining`.

### Architecture

- **`ShieldUILifetimeScope`** — child scope on the shield HUD root; registers `ShieldCounterLabel[]` (via `GetComponentsInChildren`) and `ShieldCounterAnimation`
- **`ShieldCounterAnimation`** — orchestrator; subscribes to `ProjectileLoadedMessage` and `BalanceBalloonsMessage`; binds/unbinds labels and drives animator triggers
- **`ShieldCounterLabel`** — simple view (no injection needed); exposes `Bind(IReadOnlyReactiveProperty<int>)` / `Unbind()`; multiple instances supported
- **`ProjectileLoadedMessage`** — carries `ProjectileModel` so subscribers can self-bind without coupling to `ThrowerController`

### Key decisions

- `ThrowerController` does **not** know about shield UI — decoupling is achieved via `ProjectileLoadedMessage` carrying the model
- `ShieldCounterAnimation` resets stale triggers (`ResetTrigger("Waiting")`, `ResetTrigger("Lost")`) before setting `"Ready"` — both `BalanceBalloonsMessage` and `ProjectileDestroyedMessage` fire in the same frame when the projectile is destroyed, so without reset the animator would flash through the Waiting state

### Unity Editor steps

1. Add `ShieldUILifetimeScope` to the shield HUD root GameObject (must be an ancestor of all shield UI components)
2. Add `ShieldCounterAnimation` to the Animator GameObject (child of scope root)
3. Add `ShieldCounterLabel` to each shield count `Text` element (child of scope root)
4. Disable legacy `ShieldCounterLabel` and `ShieldCounterAnimation` MonoBehaviours

✅ **Checkpoint:** Thrower loads → shield counter shows starting value → each wall bounce decrements with `"Lost"` animation → projectile destroyed and reloaded → counter resets with `"Ready"` animation.

---

## Phase 7d — Projectile Shield Visuals & Gain Logic

**Goal:** Port the visual shield orbs on the projectile and the "gain a shield on 3 same-color pops" mechanic — replacing `ProjectileBounceShieldController`, `ProjectileShieldSystem`, and `ProjectileShieldFXSystem`.

### Behaviour Summary

- The projectile carries N shield orbs (`SpriteRenderer` children) that visually represent `ShieldsRemaining`
- On load: all shields start at `scale = 0`; shields scale up to represent starting count
- On bounce (shield lost): the topmost visible shield scales down to zero; plays `PSVFX_ShieldLose` with current color
- On 3 consecutive same-color balloon pops: `ShieldsRemaining` increments; a new shield orb scales up; plays `PSVFX_ShieldGain` with current color
- Shield orbs tint to the current `ColorName` via DOTween color lerp (matches glow behavior)
- Each orb has a slightly larger scale than the previous: `Vector3.one + Vector3.right * incrementX * i + Vector3.up * incrementY * i`

### References

| Legacy file | What it does |
|---|---|
| `ProjectileBounceShieldController.cs` | View: scales shield sprites on shield count change; tints to balloon color |
| `ProjectileShieldSystem.cs` | Logic: when `ColorPopCount >= 3`, increment shield count |
| `ProjectileShieldFXSystem.cs` | VFX: spawns `PSVFX_ShieldGain`/`PSVFX_ShieldLose` particles on shield change |

### New files

| File | Responsibility |
|---|---|
| `ProjectileShieldView.cs` | MonoBehaviour on projectile prefab; `[SerializeField] List<SpriteRenderer> _shields`; observes `ShieldsRemaining` and `ColorName` via UniRx; scales/tints shields; spawns VFX |

### Implementation

1. **`ProjectileShieldView.cs`** (`Assets/Source/Projectile/View/`)
   - `[SerializeField] List<SpriteRenderer> _shields` — the ordered shield orb renderers (children of projectile)
   - `[SerializeField] float _alpha`, `_colorDuration`, `_scaleDuration`, `Vector2 _scaleIncrements`
   - `[Inject] IGameConfiguration _config` — injected by `_resolver.Instantiate()` from `ThrowerController`
   - `Bind(ProjectileModel model)`:
     - Subscribe to `model.ShieldsRemaining` → `UpdateShieldVisuals(count)`
     - Track previous count to determine gain vs loss → play `PSVFX_ShieldGain` or `PSVFX_ShieldLose`
   - `UpdateShieldVisuals(int count)`: for each shield sprite, DOScale to target (or zero if index >= count)
   - `UpdateColor(string colorName)`: DOColor all visible shields to `_config.BalloonColor(colorName)` with alpha
   - On `Awake()`: set all shields to `localScale = Vector3.zero`

3. **Shield gain logic in `ProjectileView.TrackColor()`**:
   - After incrementing `ColorPopCount`, check `if (_model.ColorPopCount >= 3)` → increment `_model.ShieldsRemaining.Value++`, reset `ColorPopCount = 0`
   - This replaces `ProjectileShieldSystem`

4. **`ProjectileView`** — resolves `ProjectileShieldView` via `GetComponent` in `Awake()`; calls `_shieldView.Bind(model)` in `Bind()`

5. **`ThrowerController`** — injects `LifetimeScope` (the parent scope auto-registered by VContainer) and calls `_parentScope.CreateChildFromPrefab(_settings.ProjectileScopePrefab)` to instantiate the projectile with a properly wired child scope. `ThrowerSettings` now holds a `ProjectileLifetimeScope` reference instead of a plain `GameObject`.

6. **Color update**: `ProjectileShieldView` subscribes to `model.ColorName` (now a `ReactiveProperty<string>`) directly

### VFX

- `PSVFX_ShieldGain` — instantiated at projectile position on shield gain, `startColor` set to balloon color
- `PSVFX_ShieldLose` — instantiated at projectile position on shield loss, `startColor` set to current color
- `PSVFX_ShieldBounce` — instantiated at bounce position (integrate into `PlayBounceEffect`)

### Model changes

- `ProjectileModel.ColorName` → convert to `ReactiveProperty<string>` so `ProjectileShieldView` can observe color changes for tinting

### Unity Editor steps

1. Add `ProjectileShieldView` to the projectile prefab
2. Wire `_shields` list to the shield orb `SpriteRenderer` children (ordered bottom to top)
3. Set `_alpha: 0.3`, `_colorDuration: 0.5`, `_scaleDuration: 1`, `_scaleIncrements: (0.5, 0.2)` (legacy values from the old prefab)
4. Assign VFX prefabs (`PSVFX_ShieldGain`, `PSVFX_ShieldLose`, `PSVFX_ShieldBounce`)

✅ **Checkpoint:** Fire projectile → shields start visible at initial count → hit 3 same-color balloons → new shield appears with gain VFX → bounce off wall → shield disappears with lose VFX → shield color matches last balloon hit.

---

## Phase 7e — Auto-Spawning Balloon Lines on Projectile Death

**Goal:** After each projectile death (from the second turn onward), automatically spawn new balloon lines — replacing `NewBalloonLinesInstanceSystem` and `GameStartedBalloonsSpawnSystem`.

> References: `NewBalloonLinesInstanceSystem.cs`, `GameStartedBalloonsSpawnSystem.cs`, `GameTurnCounterComponent.cs`

### Legacy behaviour

1. A `GameTurnCounter` component tracks the turn number (incremented each time a projectile dies in `ProjectileBounceSystem`)
2. `GameStartedBalloonsSpawnSystem` spawns `GameStartedBalloonLines` lines on game start, with `NewBalloonLinesTimeInterval` delay between each
3. `NewBalloonLinesInstanceSystem` reacts to `GameTurnCounter` changes — on turn > 1 (i.e. not the first projectile death), spawns `NewProjectileBalloonLines` lines with `NewBalloonLinesTimeInterval` delay, then triggers power-up check and balance

### New implementation

All logic lives in `BalloonSpawner`:

1. **`BalloonSpawner`** subscribes to `ProjectileDestroyedMessage` in addition to `SpawnBalloonLineMessage`
2. Tracks `_turnCount` — incremented on each `ProjectileDestroyedMessage`; skips spawning on turn ≤ 1 (first death is the initial projectile fired after game start)
3. On turn > 1: runs `SpawnLinesWithDelayAsync()` (`async UniTaskVoid` with `CancellationTokenSource`) that spawns `NewProjectileBalloonLines` lines with `NewBalloonLinesTimeInterval` delay between each, then publishes `BalanceBalloonsMessage` once after all lines
4. Uses `SpawnLineInternal()` (no per-line balance) vs `SpawnLine()` (with balance) to avoid redundant balance passes during multi-line spawning

### `SpawnBalloonLineMessage` changes

- Added `LineCount` field (default 1) so callers can request multiple delayed lines in a single message
- `GameStartButton` now publishes `new SpawnBalloonLineMessage(GameStartedBalloonLines)` — game-start lines also spawn with delays between them, matching legacy

### Async delay

`BalloonSpawner` is a plain C# class (`IStartable`), so it uses `async UniTaskVoid` with a `CancellationTokenSource` for delayed multi-line spawning. No coroutine runner dependency is needed.

### Files changed

| File | Change |
|---|---|
| `BalloonSpawner.cs` | Added `ProjectileDestroyedMessage` subscription, turn tracking, `SpawnLinesWithDelayAsync` (`async UniTaskVoid`), `SpawnLineInternal` extraction, removed `SlotGridView` coroutine runner dependency |
| `SpawnBalloonLineMessage.cs` | Added `LineCount` property |
| `GameStartButton.cs` | Publishes single message with `LineCount` instead of N separate messages |

✅ **Checkpoint:** Fire projectile → it bounces and dies → new balloon lines spawn from below with staggered timing → grid rebalances after all lines settle. First projectile death (after game start) does **not** spawn new lines.

---

## Phase 7f — Game Start

**Goal:** Replace the legacy `isGameStarted` entity flag with `GameStartButton` publishing `SpawnBalloonLineMessage`, so the game can be started independently of any Entitas system.

| File | Status |
|---|---|
| `GameStartButton.cs` | ✅ Coded |

**Unity Editor steps:**

1. **`GameStartButton`** — add to the start button GameObject in the scene
2. `GameStartButton` is already registered in `GameLifetimeScope`:
   ```csharp
   builder.RegisterComponentInHierarchy<GameStartButton>();
   ```
3. **Disable legacy** — disable the old `GameStartButton` MonoBehaviour and comment out the `isGameStarted` handling in `GameUpdateSystems` / `GameFixedUpdateSystems`

✅ **Checkpoint:** Press the start button → initial balloon lines spawn and settle → thrower slides in → game is fully playable without Entitas handling game start.

---

## Phase 7g — HUD Audit & Cleanup

**Goal:** Confirm zero active Entitas UI in the scene; remove stubs.

1. Open the scene; search for any remaining MonoBehaviours from `Source_Old/UI/` still active — `grep` for `IAny*Listener` or inspect each UI GameObject
2. For each still-active legacy component, either port it or confirm it is superseded and disable it
3. Verify `GameLifetimeScope` and `ScoreUILifetimeScope` have `RegisterComponentInHierarchy` calls for every new UI component that needs injection
4. ~~Delete `ScoreUIController.cs`~~ — already deleted
5. ✅ **Checkpoint:** No `Contexts.sharedInstance` calls execute from any UI component during Play Mode; all HUD elements update reactively.

---

## Phase 8 — Object Pooling

### Phase 8a — Generic Pool System & VFX/Trail Decoupling

**Goal:** Replace all `Instantiate`/`Destroy` patterns with a generic pooling system. Decouple VFX and trails from the projectile hierarchy so they survive projectile recycling.

### Problem

1. Projectile instances were never cleaned up — `Destroy(gameObject)` left orphaned scopes, and `IStartable` on a MonoBehaviour caused double `Start()` calls, spawning duplicate projectiles.
2. When the projectile is pooled and reactivated, `TrailRenderer` snaps to the new position, creating a visible line artifact.
3. VFX parented to the projectile get repositioned or cut short on recycle.

### Solution — Generic Pool Architecture

All pooling goes through a single pattern: `PoolChannel<TItem>` + `PoolManager`. See `Assets/Source/Shared/Pool/README.md` for full documentation.

| File | Location | Responsibility |
|---|---|---|
| `IPoolChannel` | `Shared/Pool/PoolChannel.cs` | Non-generic marker interface for type-safe dictionary storage |
| `PoolChannel<TItem>` | `Shared/Pool/PoolChannel.cs` | Abstract base implementing `IPoolChannel` — `Stack<TItem>`, `Get()`/`Return()`, abstract `Create()` |
| `IPoolable` | `Shared/Pool/IPoolable.cs` | Contract: `OnSpawned()`, `OnDespawned()` |
| `PoolManager` | `Shared/Pool/PoolManager.cs` | Injectable singleton registry; `Dictionary<string, IPoolChannel>` keyed by string (prefab name or explicit key) |
| `VfxPoolChannel` | `Shared/Pool/VfxPoolChannel.cs` | Particle pool — one channel per prefab, auto-returns via `PoolableParticle` |
| `PoolableParticle` | `Shared/Pool/PoolableParticle.cs` | `IPoolable` wrapper; auto-returns when `!IsAlive()` |
| `ProjectilePoolChannel` | `Projectile/ProjectilePoolChannel.cs` | Projectile pool — creates via `CreateChildFromPrefab` |

### Key decisions

- **`ThrowerController` no longer implements `IStartable`** — a MonoBehaviour registered via `RegisterComponentInHierarchy` gets `Start()` called by both Unity and VContainer, causing duplicate projectile spawns. Removed `IStartable`; uses Unity's `Start()` only.
- **Projectile is pooled, not destroyed** — `ProjectileView` implements `IPoolable`; on death it publishes messages but does not `Destroy`. `ThrowerController.Reload()` returns it to the pool and immediately gets it back (single-item pool).
- **`ProjectileShieldView` hides on `Awake()`, shows on first fired frame** — mirrors legacy timing where shields were added at fire time, not load time.
- **VFX are world-space orphans** — `VfxPoolChannel` instantiates particles unparented; `PoolableParticle` auto-returns after lifetime. No VFX is a child of the projectile.
- **Pool defensiveness** — `PoolChannel.Get()` skips destroyed items in the stack (Unity's `== null` check catches destroyed-but-not-GC'd objects). `PoolChannel.Return()` early-exits if the item has been destroyed. This guards against race conditions where a VFX prefab's `Stop Action: Destroy` competes with the pool's auto-return.

### Trail handling

Trail management is extracted into `ProjectileTrail`, a child component on the trail GameObject. It is **not** `IPoolable` — the projectile itself is pooled, so its children's lifecycle is managed by the projectile's `OnSpawned`/`OnDespawned`. `ProjectileTrail` exposes `Enable()` / `Disable()`:

- **`Disable()`** — `_trail.emitting = false` + `_trail.Clear()`
- **`Enable()`** — `async UniTaskVoid` that yields one frame (via `UniTask.Yield(destroyCancellationToken)`), clears the trail, then re-enables emitting (prevents snap artifact from position change)

`ProjectileView` calls `_projectileTrail.Enable()` on fire (first `FixedUpdate` frame where `IsFree` is true) and `_projectileTrail.Disable()` on death and despawn.

### Changes to existing files

| File | Change |
|---|---|
| `ProjectileView.cs` | Implements `IPoolable`; delegates trail management to `ProjectileTrail`; removed `Destroy(gameObject)` |
| `ProjectileTrail.cs` | New — `Enable()`/`Disable()` via `async UniTaskVoid`; child component on trail GameObject |
| `ProjectileShieldView.cs` | Injects `PoolManager`; uses `VfxPoolChannel` for VFX; added `Reset()` and `Show()` |
| `BalloonView.cs` | Injects `PoolManager`; uses `VfxPoolChannel` for pop VFX |
| `ThrowerController.cs` | Removed `IStartable`; uses `PoolManager` + `ProjectilePoolChannel` |
| `GameLifetimeScope.cs` | Registers `PoolManager` as singleton |

### Registration

```csharp
// GameLifetimeScope
builder.Register<PoolManager>(Lifetime.Singleton);
```

### Unity Editor steps

1. On the projectile prefab, assign the `Trail` child's `TrailRenderer` to `ProjectileView._trail`

✅ **Checkpoint:** Fire projectile → it bounces and dies → no trail line artifact on reload → shield gain/lose/bounce VFX play at correct world positions and fade independently of the projectile lifecycle. Only one projectile instance exists in the hierarchy.

---

### Phase 8b — Migrate ScorePointTrail to PoolManager

**Goal:** Replace the hand-rolled `List<ScorePointTrail>` + `IReusable` pattern in `ColorProgressBar` with the generic `PoolManager` / `PoolChannel` system, so score trails live under the `[Pool]` hierarchy instead of cluttering the scene root.

### Problem

`ColorProgressBar.SpawnTrail()` maintained its own `List<ScorePointTrail>` and used `FindAvailable()` (scanning for `IReusable.IsUsable`) to reuse instances. This duplicated pooling logic already solved by `PoolChannel<T>` and left instantiated trails at the scene root.

### Solution

| File | Change |
|---|---|
| `ScorePointTrail.cs` | Replaced `IReusable` with `IPoolable`; added `Initialize(Action<ScorePointTrail>)` callback for auto-return; removed `IsUsable` property; calls return callback on tween completion |
| `ScoreTrailPoolChannel.cs` | New — `PoolChannel<ScorePointTrail>` that instantiates from a prefab under `Container` and wires the auto-return callback |
| `ColorProgressBar.cs` | Injects `PoolManager`; removed `_trails` list and `FindAvailable` usage for trails; uses `_poolManager.GetOrRegister()` with a per-color key (`ScoreTrail_{colorName}`) so each color gets its own pool channel |

### Key decisions

- **Per-color pool keys** (`ScoreTrail_Red`, `ScoreTrail_Blue`, etc.) — each `ColorProgressBar` instance registers its own channel keyed by color name. All share the same prefab but are separated in the hierarchy for clarity.
- **Auto-return via callback** — same pattern as `PoolableParticle`: the trail calls a return delegate on tween completion, so `ColorProgressBar` never manually returns items.
- **`ScoreNotice` stays as-is** — notices are UI elements parented to the progress bar (`transform`), not world-space objects. Their reuse via `IReusable` + `FindAvailable` is appropriate for UI-local recycling.

### Unity Editor steps

None — no new serialized references. `PoolManager` is already registered as a singleton.

✅ **Checkpoint:** Pop a balloon → score trail flies from world position to progress bar → trail instance appears under `[Pool]/ScoreTrail_{color}` in hierarchy → trail auto-returns and is reused on next pop.

---

### Phase 8c — Migrate Balloon Instances to PoolManager

**Goal:** Replace `Instantiate`/`Destroy` for balloon GameObjects with the `PoolManager` / `PoolChannel` system, so popped balloons are recycled instead of destroyed and all balloon instances live under the `[Pool]` hierarchy.

### Problem

`BalloonSpawner` called `_resolver.Instantiate` for every new balloon and `BalloonController` called `Object.Destroy` on hit. With dozens of balloons spawning and dying per game, this caused constant allocation and GC pressure, and left instances scattered at the hierarchy root.

### Solution

| File | Change |
|---|---|
| `BalloonView.cs` | Implements `IPoolable`; `OnSpawned()` resets scale; `OnDespawned()` kills DOTween tweens, clears reactive subscriptions via `CompositeDisposable`, nulls model. Subscriptions use `_bindDisposables` instead of `AddTo(this)` to support rebinding on reuse. Added `RegisterDisposeOnDespawn(IDisposable)` so external subscribers (e.g. `BalloonController`) can tie their subscription lifetime to the view's pool cycle. |
| `BalloonPoolChannel.cs` | New — `PoolChannel<BalloonView>` that creates via `_resolver.Instantiate` (VContainer injection-aware) and parents under `Container` |
| `BalloonSpawner.cs` | Injects `PoolManager`; registers `BalloonPoolChannel` in `Start()`; `SpawnBalloon` gets from pool instead of instantiating |
| `BalloonController.cs` | Injects `PoolManager`; on hit calls `_poolManager.Return("Balloon", _view)` instead of `Object.Destroy`. No longer implements `IStartable` (it's manually constructed, not DI-resolved). Hit subscription is stored as `IDisposable` and disposed immediately on hit + registered with `view.RegisterDisposeOnDespawn()` as a safety net. |

### Key decisions

- **`CompositeDisposable` for reactive subscriptions** — `AddTo(this)` ties disposal to MonoBehaviour destruction, which never happens for pooled objects. `_bindDisposables` is cleared on each `Bind()` and on `OnDespawned()`, preventing subscription accumulation across reuse cycles.
- **`DOTween.Kill` on despawn** — balloons may be mid-spawn-animation when popped; killing tweens prevents callbacks from firing on a recycled view.
- **VContainer injection preserved** — `BalloonPoolChannel` uses `IObjectResolver.Instantiate` so `[Inject]` fields on `BalloonView` are populated on first creation and remain valid across pool cycles (fields are injected once at instantiation, not re-injected).
- **Pool key is a constant** (`"Balloon"`) — all balloons share one pool regardless of color since the same prefab is used; color is applied via model binding.
- **BalloonController subscription lifecycle** — `BalloonController` subscribes to `BalloonHitMessage` globally. Without disposal, old controllers (from previous pool cycles) would accumulate subscriptions forever — each holding a reference to `_view` (the recycled view). Although the `msg.Balloon != Model` guard prevents wrong execution, the subscriptions prevent GC of dead controllers and waste CPU scanning every hit message. Fix: the subscription `IDisposable` is disposed immediately when the hit fires and is also registered via `view.RegisterDisposeOnDespawn()` so it's cleaned up on pool return even if the balloon is never hit (e.g. game reset).
- **BalloonController no longer implements `IStartable`** — it's constructed manually with `new`, not resolved by VContainer, so `IStartable` was never honoured by the container. Removed to avoid confusion.

### Unity Editor steps

None — no new serialized references. `PoolManager` is already registered as a singleton.

✅ **Checkpoint:** Spawn balloons → pop them → they disappear (pop VFX plays) → balloon instances move under `[Pool]/Balloon` in hierarchy (inactive) → new spawn line reuses existing instances → no `Destroy` calls in console.

---

## Phase 9 — Balance Animation System Redo

**Goal:** Rewrite the balloon balance/nudge/spawn animation system so that tweens compose correctly — matching the legacy Entitas system's behaviour where balance appends to running animations instead of killing them.

### Legacy system analysis

The legacy code across four Entitas systems worked together:

#### Execution order (driven by Entitas reactive systems + coroutines)

1. **Projectile hits balloon** → `BalloonHitNudgeAnimationSystem` fires:
   - Nudges all neighbors (no stability check — nudges everyone).
   - Creates a `DOTween.Sequence` (push out → return to slot).
   - **Stores the sequence on the entity**: `neighborEntity.ReplaceTweenSequence(sequence)`.
   - Sets `isStableBalloon = false`; sequence `onComplete` → `isStableBalloon = true`.
   - Removes the hit balloon from the slot array: `_slots[index.x, index.y] = null`.

2. **Balloon destruction** → `BalloonHitDestructionSystem` fires:
   - Removes from slot indexer, marks entity as destroyed.

3. **New balloon lines** → `NewBalloonLinesInstanceSystem` fires (on turn counter change):
   - Marks ALL existing balloons as `isNewBalloon = false`.
   - Spawns new lines via coroutine with `WaitForSeconds` delays between lines.
   - **After ALL lines spawned**: `yield return new WaitForEndOfFrame()` × 2, THEN creates balance event.

4. **Balloon line spawner** → `BalloonLineSpawnerSystem`:
   - Creates balloon entity at first empty row per column.
   - Spawn animation: `DOMove` + `DOScale` (separate tweens, NOT in a sequence).
   - `DOMove.onComplete` → `isStableBalloon = true`.
   - **No tween sequence stored on entity** — spawn tweens are standalone.

5. **Balance** → `BalanceBalloonsSystem` fires (from balance event):
   - Single-pass `while (hasUnbalanced)` loop, bottom-to-top.
   - Does NOT skip any balloon — processes all, regardless of stability or new/old status.
   - `HandlePathTween` checks entity's stored tween sequence:
     - **If has sequence AND not complete**: `entity.tweenSequence.Value.Append(balanceTween)` — **balance is APPENDED after the running nudge**.
     - **If has sequence AND complete**: removes old sequence, creates new balance tween.
     - **If no sequence**: creates new balance tween directly.
   - Balance tween: `DOPath(CatmullRom)` with fixed `TimeForBalloonsBalance` duration.
   - `onComplete` → `isStableBalloon = true`.

#### Key insight: tween composition via entity-stored sequences

The nudge system stored its `Sequence` on the entity. When balance ran later, it checked if a sequence was still playing and **appended** the balance DOPath to it. This meant:
- A nudged balloon finished its push-out → return animation, THEN started its balance movement.
- No tween was ever killed — animations chained naturally.
- Spawn tweens (move + scale) were NOT stored in a sequence, so balance would create a new DOPath that ran in PARALLEL with the spawn tweens. Since spawn and balance both moved the balloon, this created competing move tweens — but in practice, balance only ran after `WaitForEndOfFrame` so spawn tweens had time to finish or nearly finish.

#### Weight algorithm (`CalculateWeight`)

Purpose: when a balloon has TWO empty slots above it (directly above and diagonally above), the weight determines which one it moves to. The weight of a candidate slot = count of occupied slots in the tree above it (recursive). Higher weight = more "support" above = preferred target. This biases balloons toward the side of the grid that has more balloons, creating natural clustering.

```csharp
// Legacy: Source_Old/Game/GameContextExtensions.cs
public static int CalculateWeight(this IEntity[,] slots, int i, int j)
{
    if (j == 0) return slots.IsEmpty(i, j) ? 0 : 1;
    if (j > 0)
    {
        var weight = slots.IsEmpty(i, j) ? 0 : 1;
        weight += slots.CalculateWeight(i, j - 1);
        weight += slots.CalculateWeight(i + (j % 2 == 0 ? -1 : 1), j - 1);
        return weight;
    }
    return 0;
}
```

**Difference from current code**: legacy `OptimalNextEmptySlot` initialises weight at `0` and uses `>=` comparison (`if (slotWeight >= weight)`), so the LAST candidate with equal weight wins (favours diagonal). ✅ Fixed — our code now matches legacy.

### Design for new implementation

#### `TweenTracker` — MonoBehaviour (generic, reusable)

Replaces the Entitas `TweenSequenceComponent`. Lives on any view that needs tween composition. Located in `Assets/Source/Shared/TweenTracker.cs`.

```
Fields:
  - Sequence _active (nullable)

Methods:
  - Append(Tween tween): if _active is alive and playing, Append; else create new Sequence containing the tween
  - Replace(Sequence sequence): kill _active if alive, store new sequence
  - Kill(): kill _active if alive, set null
  - bool IsPlaying: _active != null && _active.IsActive() && !_active.IsComplete()
```

#### Animation flow

**Spawn** (`BalloonSpawner.AnimateSpawn`):
- Create move + scale as standalone tweens (matching legacy — NOT stored in tracker).
- `DOMove.onComplete` → `IsStable = true`.
- Do NOT call `tracker.Append` — spawn tweens live outside the tracker.

**Nudge** (`ProjectileView.NudgeNeighbors`):
- Captures current scale, then kills standalone spawn tweens via `view.transform.DOKill()`.
- Uses slot positions (logical positions) for push direction and return, not visual `transform.position` — ensures consistent nudge regardless of in-progress animations.
- `tracker.Replace(nudgeSequence)` — kills any previous nudge and stores new one.
- Nudge sequence: push out from slot position → return to slot position.
- If balloon was mid-scale-up, creates a parallel `DOScale(Vector3.one, nudgeDuration)` for smooth scale recovery.
- `onComplete` → `IsStable = true`.

**Balance** (`BalloonBalancer.AnimatePaths`):
- Kills tracker and standalone tweens (`tracker.Kill()` + `transform.DOKill()`), captures current scale.
- Creates `DOPath(CatmullRom)` with fixed `TimeForBalloonsBalance` duration.
- `tracker.Append(balanceTween)` — creates a new tracked sequence.
- If balloon was mid-scale-up, creates a parallel `DOScale(Vector3.one, balanceDuration)` for smooth scale recovery.
- `onComplete` → `IsStable = true`.

**Despawn** (`BalloonView.OnDespawned`):
- `tracker.Kill()` — clean up tracked sequences.
- `transform.DOKill()` — clean up standalone spawn tweens.

#### Balance timing — turn-based (matches legacy)

Balance fires ONCE after the projectile is destroyed and new lines have spawned — never mid-flight. This eliminates re-balance-during-balance conflicts and keeps animation phases sequential:

1. **Hit phase** — projectile bounces and pops balloons; each pop nudges neighbors (local elastic animation only, no rebalancing).
2. **Spawn phase** — after projectile dies, new balloon lines spawn with delay.
3. **Balance phase** — single balance pass runs after all spawning is done.

`BalloonController` no longer publishes `BalanceBalloonsMessage`. Balance sources:
- `ProjectileView` — on projectile death (fallback for turn 1 / no-spawn).
- `BalloonSpawner` — after all lines spawned.

**Why turn-based over real-time**: mid-flight balance (per-hit) was attempted and caused cascading issues — competing tweens, stale balance paths, double-occupation visuals, and required complex `AppendOrReplace` logic with tween type tracking. Turn-based eliminates these by design: phases are sequential, tweens never overlap by category, and `TweenTracker` stays simple.

#### Weight algorithm fix

In `SlotGrid.OptimalNextEmptySlot`:
- ✅ `bestWeight = 0` (was `-1`)
- ✅ `if (weight >= bestWeight)` (was `>`)

This matches legacy tie-breaking: prefer the diagonal candidate when weights are equal.

#### SlotGrid defensive guard

`SlotGrid.Place` now rejects placement if the slot is already occupied (returns early with `Debug.LogError`). This prevents silent overwrites where the first balloon would become orphaned — still in the scene with a running view but invisible to the grid.

### Findings and issues resolved during implementation

1. **Competing spawn + nudge tweens** — spawn move was initially routed through the tracker; nudge's `Replace` killed it mid-flight causing jumps. Fix: spawn move stays standalone (matching legacy); nudge calls `transform.DOKill()` to explicitly take over.

2. **Scale freeze on interruption** — `transform.DOKill()` in nudge/balance killed the standalone scale tween mid-animation, leaving balloons at partial scale (visually invisible near zero). Fix: capture `localScale` before DOKill; create parallel `DOScale` if not yet at full size.

3. **Re-balance during balance** — with per-hit balance, a second balance could fire while the first was still animating, causing balloons to travel through stale intermediate positions. Fix: switched to turn-based balance (one pass after projectile death).

4. **Nudge using visual position** — nudge originally read `transform.position` for push direction. Mid-animation, this was an arbitrary point along a path, causing unnatural nudge directions. Fix: use `IndexToWorldPosition(SlotIndex)` (logical slot position) for both push direction and return target.

5. **Double-occupation via silent overwrite** — `SlotGrid.Place` overwrote without checking occupancy. If two code paths placed at the same slot, the first balloon was orphaned. Fix: guard with early return + error log.

### Files to create/modify

| File | Change |
|---|---|
| `TweenTracker.cs` | New generic MonoBehaviour in `Shared/` — `Append`, `Replace`, `Kill`, `IsPlaying` |
| `BalloonView.cs` | Reference tracker (via `GetComponent` in `Awake`); `OnDespawned` calls `tracker.Kill()` + `transform.DOKill()` for standalone spawn tweens |
| `BalloonPoolChannel.cs` | Ensure tracker component exists on pooled instances (add in `Create` if not on prefab) |
| `BalloonSpawner.cs` | `AnimateSpawn`: standalone move + scale tweens (no tracker, no SetId) — matches legacy |
| `BalloonBalancer.cs` | `AnimatePaths`: use `tracker.Append` instead of `DOTween.Kill` + new tween. Remove `DOTween.Kill(id)`. |
| `ProjectileView.cs` | `NudgeNeighbors`: use `tracker.Replace` instead of `DOTween.Kill` + new sequence |
### Files created/modified

| File | Change |
|---|---|
| `TweenTracker.cs` | New generic MonoBehaviour in `Shared/` — `Append`, `Replace`, `Kill`, `IsPlaying` |
| `BalloonView.cs` | Reference tracker (via `GetComponent` in `Awake`); `OnDespawned` calls `tracker.Kill()` + `transform.DOKill()` |
| `BalloonSpawner.cs` | `AnimateSpawn`: standalone move + scale tweens (no tracker) — matches legacy |
| `BalloonBalancer.cs` | Kills tracker + standalone tweens, creates fresh `DOPath`, parallel `DOScale` for scale recovery. Removed per-hit balance dependency. |
| `BalloonController.cs` | Removed `BalanceBalloonsMessage` publisher — balance is now turn-based (post-death only) |
| `ProjectileView.cs` | `NudgeNeighbors`: uses slot positions, kills standalone tweens, `tracker.Replace`, parallel `DOScale` for scale recovery |
| `SlotGrid.cs` | Fixed weight tie-breaking (`0` initial, `>=` compare). Added `Place` guard rejecting double-occupation. |

### Unity Editor steps

1. Add `TweenTracker` component to the balloon prefab.

✅ **Checkpoint:** Spawn balloons → they rise smoothly with scale → hit a balloon → neighbors nudge outward and return (using logical slot positions) → projectile dies → new lines spawn → balance runs ONCE → balloons float upward smoothly → mid-scale balloons finish scaling alongside movement → no snapping, no zipping, no double-occupation, no invisible balloons.

---

## Phase 11 — Configuration Migration

**Goal:** Move `GameConfiguration` out of `Source_Old` into `Source/` so it can be maintained independently.

---

## Phase 12 — Camera & Display Setup

**Goal:** Port the camera orthographic-size controller and audit the `AspectRatio` utility.

> References: `OrthogonalSizeCameraController`, `AspectRatio`

1. Create `Assets/Source/Display/OrthogonalSizeCameraController.cs` as an `IStartable` — inject `IGameConfiguration`, find `Camera.main`, read `DisplayConfiguration.GetOrthogonalSize()` and apply on start
2. Audit `AspectRatio` utility — currently unused (no callers). Confirm it can be dropped; if any future display logic needs it, port to `BalloonParty.Display` namespace.
3. ✅ **Checkpoint:** Camera orthographic size adapts correctly to different aspect ratios at startup.

---

## Phase 13 — Projectile Visuals

**Goal:** Port the projectile glow color and shield ring visuals to the new architecture.

> References: `ProjectileGlowColorController`, `ProjectileBounceShieldController`

1. Port glow color logic into the projectile view hierarchy — tint a glow sprite to match the loaded balloon color. Inject `IGameConfiguration` for color lookup; subscribe to the projectile model's color via UniRx.
2. Port shield ring scaling and color tinting — subscribe to shield count changes and balloon color. Match original DOTween eases and durations.
3. ✅ **Checkpoint:** Projectile glow matches loaded balloon color; shield rings scale in/out and tint correctly.

---

## Phase 14 — MVC Architecture Audit

**Goal:** Enforce strict MVC separation — Models and Controllers must not be `MonoBehaviour`s. Only Views interact directly with Unity engine APIs.

### Audit Targets

| Class | Current | Issue | Action |
|---|---|---|---|
| `SlotGridController` | `MonoBehaviour` | Pure logic in `Start()`, uses `[SerializeField]` for config data already in `IGameConfiguration` | Convert to `IStartable`, use `IGameConfiguration.GameStartedBalloonLines` |
| `ThrowerController` | `MonoBehaviour` | Uses `transform`, `Update()`, `GetComponentInChildren` — tightly coupled to GameObject | Split into `ThrowerController` (`IStartable` + `ITickable`) and `ThrowerView` (`MonoBehaviour`), add `ThrowerLifetimeScope` |

### Rules Going Forward

- **Model** — plain C# class. Holds reactive state (`ReactiveProperty<T>`). No Unity dependencies.
- **Controller** — plain C# class registered via `Register<T>` or `RegisterEntryPoint<T>`. Uses `IStartable`, `ITickable`, `IFixedTickable` for lifecycle. No `MonoBehaviour`, no `transform`, no `Update()`.
- **View** — `MonoBehaviour`. Owns the visual representation, reads model state via UniRx subscriptions, publishes user input as messages. May use `[Inject]` for dependencies.
- When a controller needs engine interaction (transform, physics), split into a thin `View` (MonoBehaviour) and a `Controller` (plain C# class). The controller injects the view. Use a child `LifetimeScope` on the view's GameObject to wire them together.

### Steps

1. Convert `SlotGridController` from `MonoBehaviour` to `IStartable`, use `IGameConfiguration.GameStartedBalloonLines`
2. Update `GameLifetimeScope` — change `RegisterComponentInHierarchy` to `RegisterEntryPoint` for `SlotGridController`
3. Remove the `SlotGridController` component from the scene
4. Split `ThrowerController` into `ThrowerView` (MonoBehaviour) + `ThrowerController` (`IStartable` + `ITickable`)
5. Create `ThrowerLifetimeScope` — registers `ThrowerView` and `ThrowerController` as child of `GameLifetimeScope`
6. Remove old `ThrowerController` MonoBehaviour from the Thrower GameObject, add `ThrowerView` and `ThrowerLifetimeScope` instead
7. ✅ **Checkpoint:** No controller or model class inherits `MonoBehaviour`. All engine interaction goes through Views.

---

### Phase 14 — Noted for Future Review

The following Unity API usages in controllers/models were identified during the audit. They work correctly today but violate strict MVC separation. Consider abstracting behind interfaces if the codebase grows or testing requirements demand it.

| Class | Unity API Used | Possible Abstraction |
|---|---|---|
| `ScoreController` | `PlayerPrefs.GetInt/SetInt/Save` | Extract `IScorePersistence` interface; implement with `PlayerPrefs` in a view-layer class |
| `ScoreController` | `Application.quitting`, `Application.focusChanged` | Inject an `IApplicationLifecycle` or use a thin MonoBehaviour that publishes lifecycle messages |
| `ScoreController` | `Time.timeScale = 0f` | Publish a `GamePausedMessage`; let a view-layer class set `Time.timeScale` |
| `OrthogonalSizeCameraController` | `Camera.main`, `camera.orthographicSize` | Already minimal — entire purpose is camera setup. Could become a MonoBehaviour view if needed |
| `SlotGrid` | `UnityEngine.Random.Range` | Inject `System.Random` or an `IRandom` interface for deterministic testing |
| `SlotGrid` | `Debug.LogError` | Replace with a logging abstraction or simply remove the guard (fail fast) |

---

## Phase 15 — Power-Ups

**Goal:** Port the 5 power-up controllers using MessagePipe and VContainer.

> References: `BombPowerUpController`, `LaserPowerUpController`, `LightningPowerUpController`, Shield, `BalloonsPowerUpCheckSystem`

1. Create `Assets/Source/Shared/Messages/PowerUpActivatedMessage.cs` — carries power-up type and position
2. Create `Assets/Source/Balloon/PowerUps/` — one controller per power-up, each injecting `IPublisher`/`ISubscriber` and `SlotGrid`
3. Subscribe to `BalloonHitMessage` to check power-up trigger conditions (replaces `BalloonsPowerUpCheckSystem`)
4. ✅ **Checkpoint:** Power-up triggers and visual effects work correctly.

---

## Phase 16 — Game Loop, UI & Cleanup

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
- [ ] `BalloonsPowerUpCheckSystem`, all `*PowerUpController` → replaced by power-up controllers (Phase 15)
- [ ] `GameControllerBehaviour`, `GameController`, `GameUpdateSystems`, `GameFixedUpdateSystems` → replaced by `GameManager` + `GameLifetimeScope` (Phase 16)
- [ ] All Entitas-generated code in `Assets/Generated/` → deleted (Phase 16)
- [ ] Entitas and DesperateDevs packages removed from `manifest.json` (Phase 16)

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

// GameLifetimeScope runs at -5001 so its container is ready before any child scope
// (VContainer's LifetimeScope base runs at -5000).
// Child scopes override FindParent() — VContainer's built-in hook called inside Build()
// before CreateScope runs. No Awake override or EnqueueParent needed.

// GameLifetimeScope.cs
[DefaultExecutionOrder(-5001)]
public class GameLifetimeScope : LifetimeScope { ... }

// GameChildLifetimeScope.cs  (abstract base for all UI and feature child scopes)
public abstract class GameChildLifetimeScope : LifetimeScope
{
    protected override LifetimeScope FindParent() => FindFirstObjectByType<GameLifetimeScope>();
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

### UniTask
```csharp
// Fire-and-forget async (MonoBehaviour)
private async UniTaskVoid DoSomethingAsync()
{
    await UniTask.Delay(500, cancellationToken: destroyCancellationToken);
    await UniTask.WaitUntil(() => _ready, cancellationToken: destroyCancellationToken);
    await UniTask.Yield(cancellationToken: destroyCancellationToken);
}

// Fire-and-forget async (plain C# class — manual CancellationTokenSource)
private readonly CancellationTokenSource _cts = new();
SpawnAsync(_cts.Token).Forget();

// Ignoring time scale (for paused-game UI)
await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: destroyCancellationToken);
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

### UI Scope Architecture

Each self-contained UI panel or popup owns its own child `LifetimeScope`. This is intentional: a scoped popup can resolve its dependencies from its parent scope (the game) while keeping its own registrations local. The concrete benefit is that the popup can eventually be opened in isolation — e.g. in a dedicated preview scene — without needing the full game running. It only needs to read state (via injected reactive properties or message subscribers), never to drive gameplay.

**Rule:** Any UI element that is logically self-contained (a popup, a full-screen panel, a HUD section) gets its own `LifetimeScope` on its root GameObject. Flat components that are always part of a larger panel (e.g. `ScoreCounterLabel` inside the Score HUD) stay registered in that panel's scope.

### Dynamically Instantiated Prefab Scopes

Prefabs that carry multiple MonoBehaviours needing injection (e.g. the projectile) use VContainer's `LifetimeScope.CreateChildFromPrefab()`. Place a child `GameChildLifetimeScope` on the prefab root and register its components via `RegisterComponentInHierarchy`. The spawner injects the parent `LifetimeScope` and calls `parentScope.CreateChildFromPrefab(prefab)` — this deactivates the prefab before `Instantiate`, wires the parent reference, then reactivates so `Awake()` → `Build()` runs with the correct parent. The child scope inherits all parent services (messages, config, grid) automatically.

**Do not use plain `Object.Instantiate` for prefabs with child scopes** — the scope's `FindParent()` races with sibling `Awake()` calls and may fail to find the parent. `CreateChildFromPrefab` avoids this by explicitly setting `parentReference.Object` before activation.

**Do not use `IObjectResolver.Instantiate()` on prefabs with a `LifetimeScope`** — the resolver injects fields from the parent container but does not build the child scope, leaving `RegisterComponentInHierarchy` registrations unresolved.

**When to use each pattern:**
- **`CreateChildFromPrefab`** — prefab has a `LifetimeScope` with local registrations (2+ injected MonoBehaviours, or the component set may grow independently)
- **`IObjectResolver.Instantiate()`** — prefab has no `LifetimeScope`; a single MonoBehaviour needs injection from the parent container

**Current scopes:**

| Scope | Base | GameObject | Registers |
|---|---|---|---|
| `GameLifetimeScope` | `LifetimeScope` | scene root | all game systems, messages, cheats |
| `ThrowerLifetimeScope` | `GameChildLifetimeScope` | Thrower GameObject | `ThrowerView`, `ThrowerController` |
| `ScoreUILifetimeScope` | `GameChildLifetimeScope` | Score HUD canvas root | `ColorProgressBarInstancer` |
| `LevelUpLifetimeScope` | `GameChildLifetimeScope` | LevelUp popup root | `LevelUpPopUp` |
| `ShieldUILifetimeScope` | `GameChildLifetimeScope` | Shield HUD root | `ShieldCounterLabel[]`, `ShieldCounterAnimation` |
| `ProjectileLifetimeScope` | `GameChildLifetimeScope` | Projectile prefab root | `ProjectileView`, `ProjectileShieldView` |

Future popups (power-up unlocks, game-over screen, etc.) extend `GameChildLifetimeScope` — parent is wired automatically via `FindParent()`.

**`RegisterComponentInHierarchy` scope boundary:** For scene-based `LifetimeScope`s, VContainer searches the **entire scene** for the component (not just the scope's subtree). For prefab-based scopes created via `CreateChildFromPrefab`, the search is limited to the prefab's `GetComponentInChildren`. Always ensure the component exists in the expected search space.

**Multiple instances of the same component:** When a scope's subtree contains multiple instances of a component (e.g. several `ShieldCounterLabel` on different Text elements), use `GetComponentsInChildren<T>(true)` in `Configure()` and register the array via `builder.RegisterInstance(array)`. The consumer injects `T[]`.

---

### VContainer Injection Timing

VContainer's `[Inject]` (both field injection and method injection) runs during the scope's `Build()` phase, which executes inside the scope's `Awake()`. This has important implications for MonoBehaviours registered via `RegisterComponentInHierarchy`:

- **`[Inject]` methods run before the component's own `Awake()`** — Unity has not yet called `Awake()` on the target MonoBehaviour when VContainer injects it. Do not rely on `Awake()`-initialized fields inside an `[Inject]` method. Use `GetComponent<T>()` directly if needed.
- **`Start()` runs after injection** — if the scope builds during its `Awake()`, child components' `Start()` will have all injected fields available. However, prefer `[Inject]` methods for subscription wiring rather than `Start()`, to make the dependency on injection explicit.
- **Animator triggers from the same frame:** When multiple MessagePipe messages fire in the same frame (e.g. `BalanceBalloonsMessage` + `ProjectileDestroyedMessage` from `ProjectileView`), their subscribers execute synchronously. If both set animator triggers, call `ResetTrigger` on conflicting triggers before setting the intended one.
- **`Time.timeScale = 0` freezes DOTween and physics.** Any code that pauses time must ensure that pending `OnComplete` callbacks, `WaitUntil` conditions, and Animator playback are not deadlocked. Animators that must play while paused need `updateMode = AnimatorUpdateMode.UnscaledTime`. UniTask delays and waits that must resolve while paused need `ignoreTimeScale: true`.

---

### VContainer Lazy Singletons

`RegisterComponentOnNewGameObject<T>(Lifetime.Singleton, ...)` is **lazy** — the GameObject is created only when `T` is resolved by another registration or by a `RegisterBuildCallback`. If nothing resolves `T`, the component never exists.

**Rule:** Any `RegisterComponentOnNewGameObject` registration that must exist at startup (e.g. debug tools, always-on HUD elements) needs an explicit `RegisterBuildCallback`:

```csharp
builder.RegisterComponentOnNewGameObject<MyView>(Lifetime.Singleton, "MyView");
builder.RegisterBuildCallback(resolver => resolver.Resolve<MyView>());
```

---

### Message Design

- **Carry relevant data.** If a subscriber needs access to the source object (model, position, etc.), include it in the message struct rather than forcing the subscriber to inject the producer. Example: `ProjectileLoadedMessage` carries `ProjectileModel` so shield UI can self-bind without knowing about `ThrowerController`.
- **Prefer decoupling over direct injection** between unrelated systems. A controller should not inject a UI component; instead publish a message that the UI subscribes to independently.
- **Empty structs** are fine for pure signals where no data is needed (`BalanceBalloonsMessage`, `SpawnBalloonLineMessage`).

---

### Cheat Console

A self-building runtime debug console lives in `Assets/Source/Cheats/`. Press **backtick (`)** in Play Mode to toggle it.

**Adding a cheat:**
1. Implement `ICheat` — provide `Name`, `Section`, and `Tags[]`
2. Inject whatever publishers or services it needs via the constructor
3. Register in `GameLifetimeScope`: `builder.Register<YourCheat>(Lifetime.Singleton).AsImplementedInterfaces()`

The console discovers all registered `ICheat` implementations automatically. Features: live search by name, tag filter pills, section grouping, and per-cheat favorites (★).

**Eager creation:** `CheatConsoleView` and `BalloonRemoverCheat` are MonoBehaviours created via `RegisterComponentOnNewGameObject`. VContainer's singleton lifetime is **lazy** — the GameObject is only created when resolved. Use `RegisterBuildCallback(resolver => resolver.Resolve<T>())` to force creation at scope build time. Without this, the component will never exist because nothing else resolves it.

Every phase that introduces a new triggerable behaviour should add a corresponding cheat so it can be tested in isolation without running the full game loop.

**Cheat ownership principle:** All logic specific to a cheat lives in the cheat class itself — game systems must not expose methods solely to serve cheats. A cheat drives behaviour through the same messages and public APIs that gameplay uses. Where a cheat needs to simulate a game event (e.g. a balloon being hit), it creates a temporary model and publishes the appropriate message; existing subscribers react normally. This keeps game systems ignorant of the debug layer and ensures the cheat exercises the real pipeline rather than a shortcut.

---

### Living Documentation

Each feature folder contains a `README.md` that describes what that feature covers — its gameplay purpose, how it works, and how it interacts with other systems. These are not implementation notes; they explain intent and behaviour at the feature level.

**Keep them current.** Update a folder's `README.md` whenever:
- A new mechanic is added or an existing one changes significantly
- A system's responsibility shifts (e.g. a controller absorbs logic from another)
- Interactions with other systems are added, removed, or change in character

The test for whether a README needs updating: if a new developer read only that file, would they still have an accurate picture of what the feature does and how it connects to the rest of the game?

**No migration references.** READMEs and code comments in `Assets/Source/` must not mention Entitas, the legacy codebase, or the migration effort. They describe the current architecture as if it were always the intended design.

---

## Code Quality Constraints

These constraints apply to all code generated or written during this migration.

### MVC Separation
- **Model** — plain C# class. Holds reactive state (`ReactiveProperty<T>`). No Unity dependencies, no `MonoBehaviour`.
- **Controller** — plain C# class. Registered via `Register<T>` or `RegisterEntryPoint<T>`. Uses VContainer lifecycle interfaces (`IStartable`, `ITickable`, `IFixedTickable`) instead of Unity's `Start()` / `Update()`. No `MonoBehaviour`, no `transform`.
- **View** — `MonoBehaviour`. The only layer that touches Unity APIs (transforms, renderers, UI, physics). Reads model state via UniRx subscriptions. Publishes input/events via MessagePipe.
- When a controller needs engine interaction, split into a thin **View** (MonoBehaviour) + **Controller** (plain C# class). Wire them via a child `LifetimeScope` on the view's GameObject.

### Comments
- **Only comment the *why***, never the *what* or *how* — if the code needs a comment to explain what it does, it should be renamed or refactored instead.
- **No redundant comments.** Avoid comments like `// inject dependencies`, `// constructor`, `// update position` above self-evident code.
- **No block comment headers** on every file or class (e.g. `// ====== BalloonModel ======`).
- XML doc comments (`/// <summary>`) only on public API surfaces that are non-obvious to a consumer.

### Naming & Readability
- Code must be **self-explanatory through naming, namespaces, and context**. A reader should understand intent without comments.
- Prefer longer, descriptive names over short ambiguous ones (`FindOptimalEmptySlot` over `GetSlot`).
- Namespaces must reflect folder structure (e.g. `BalloonParty.Balloon.Model`, `BalloonParty.Slots`).

### Visibility
- **Default to `private`**. Only increase visibility when there is a concrete consumer.
- Expose only intentional service methods as `public` — if nothing outside the class calls it, it stays `private`.
- Prefer `internal` over `public` when the consumer is within the same assembly but outside the class.
- Never make a field or method `public` "just in case" — widen access only when a use case demands it.

### Architecture & Reuse
- **Before writing new code, check for existing methods** in the codebase (including `Source_Old`) that can be ported, extracted, or called directly.
- **Identify commonalities** across systems early — if two controllers share a pattern, extract it into a base class or generic utility.
- **Prefer generic implementations** over copy-paste specialisations (e.g. a generic `ModelView<TModel>` base for all View MonoBehaviours rather than boilerplate per class).
- **Extension methods** over utility classes where possible — keep them in a dedicated `Extensions/` namespace.
- Keep classes **small and focused** — if a class is growing beyond one clear responsibility, split it.
- Avoid `static` state; prefer injected singleton services via VContainer.

### Formatting

- **Allman brace style** — every opening brace goes on its own line.
- **Braces are always required** for `if`, `else`, `for`, `foreach`, `while`, `using`, `lock`, and `fixed` — even single-line bodies. No braceless statements.
- These rules are enforced via `.editorconfig` and `BalloonParty.sln.DotSettings` at the project root.

### Member Ordering

> **Enforcement:** Rider's File Layout cannot reliably automate this ordering. It is enforced exclusively through **code review and periodic audits** against this specification. Every audit phase must verify member ordering compliance.

Classes follow a strict top-to-bottom ordering. Within each numbered group, sort alphabetically unless a different order is noted.

#### Fields & Properties (top of class)

Order within the class, top to bottom:

| # | Group | Example | Notes |
|---|-------|---------|-------|
| 1 | **Constants** (`const`) | `private const float PickRadius = 0.25f;` | All visibilities; `public` before `private` |
| 2 | **Static readonly fields** | `private static readonly Color DefaultColor = Color.white;` | |
| 3 | **`[SerializeField]` fields** | `[SerializeField] private SpriteRenderer _renderer;` | Grouped by `[Header]` when context warrants it |
| 4 | **`[Inject]` fields** | `[Inject] private SlotGrid _grid;` | All `[Inject]` together, alphabetical by type name |
| 5 | **Readonly instance fields** | `private readonly List<Vector3> _path = new();` | Services, config, disposables, collections |
| 6 | **Mutable instance fields** | `private bool _active;` | Always last among fields |
| 7 | **Auto-properties** | `public string Name => ...;` | After all fields |

**Key rule:** Fields decorated with an attribute (`[SerializeField]`, `[Inject]`) are grouped by that attribute, not by readonly/mutable. An `[Inject]` mutable field sits in group 4, not group 6.

#### Methods (below fields)

| # | Group | Notes |
|---|-------|-------|
| 1 | **Constructors** | Static constructors before instance constructors |
| 2 | **Unity lifecycle methods** | In lifecycle order: `Awake` → `OnEnable` → `Start` → `Update` → `FixedUpdate` → `LateUpdate` → `OnDisable` → `OnDestroy` → other callbacks |
| 3 | **`[Inject]` methods** | Immediately after lifecycle methods; only injection-related wiring |
| 4 | **Interface implementations** | `IStartable.Start()`, `ITickable.Tick()`, `IDisposable.Dispose()`, `IPoolable`, etc. |
| 5 | **Public methods** | Alphabetical |
| 6 | **Protected methods** | Alphabetical |
| 7 | **Private methods** | Alphabetical; helper methods called by a single public method may sit directly below their caller for locality |

#### Canonical Example

```csharp
public class BalloonRemoverCheat : MonoBehaviour, ICheat
{
    // 1. Constants
    private const float PickRadius = 0.25f;
    private const float PathSampleDistance = 0.05f;

    // 4. [Inject] fields (grouped by attribute)
    [Inject] private SlotGrid _grid;
    [Inject] private IPublisher<BalloonHitMessage> _hitPublisher;
    [Inject] private IPublisher<BalanceBalloonsMessage> _publisher;

    // 5. Readonly instance fields
    private readonly List<Vector3> _path = new();

    // 6. Mutable instance fields (always last among fields)
    private bool _active;
    private bool _dragging;
    private Material _lineMaterial;

    // 7. Auto-properties
    public string Name => _active ? "Remove Balloons  [ON]" : "Remove Balloons";
    public string Section => "Grid";
    public IReadOnlyList<string> Tags => new[] { "balloons", "grid" };

    // --- Methods ---

    // Unity lifecycle (in lifecycle order)
    private void Awake() { ... }
    private void Update() { ... }
    private void OnRenderObject() { ... }

    // Interface implementations
    public void Execute() { ... }

    // Private helpers (alphabetical)
    private HashSet<Vector2Int> CollectHitSlots() { ... }
    private static void DrawThickCircle(...) { ... }
    private static void DrawThickPath(...) { ... }
    private static Vector3? MouseWorldPosition() { ... }
    private void RemoveBalloonsAlongPath() { ... }
    private void SampleMousePosition() { ... }
}
```

### Async: Prefer UniTask over Coroutines

All async work in `Assets/Source/` must use **UniTask** instead of Unity coroutines. No `StartCoroutine` / `IEnumerator` patterns in new code.

- **`async UniTaskVoid`** for fire-and-forget operations (call `.Forget()` at the call site)
- **`async UniTask`** when the caller needs to `await` the result
- **`UniTask.Delay(milliseconds)`** replaces `WaitForSeconds`; use `ignoreTimeScale: true` where the game is paused
- **`UniTask.WaitUntil(() => condition)`** replaces `yield return null` polling loops
- **`UniTask.Yield()`** replaces `yield return null` for single-frame delays
- **Cancellation:** use `destroyCancellationToken` for MonoBehaviour-scoped tasks (auto-cancels on destroy). Use `CancellationTokenSource` for plain C# classes with manually controlled lifetimes.
- **Child components of pooled objects** (e.g. `ProjectileTrail`) use `destroyCancellationToken` — the pooled parent deactivates/reactivates them, but does not destroy them, so `destroyCancellationToken` only fires on actual destruction.

---

## Progress Tracker

| Phase | Description                               | Status          |
|-------|-------------------------------------------|-----------------|
| 0     | Preparation & Folder Scaffold             | ✅ Done         |
| 1     | Balloon Model + View                      | ✅ Done         |
| 2     | Slot Grid Model & Placement               | ✅ Done         |
| 3     | Balance / Movement Logic                  | ✅ Done         |
| 4     | Balloon Spawning & Line Management        | ✅ Done         |
| 5     | Projectile & Thrower                      | ✅ Done         |
| 6     | Hit, Destruction & Score Logic            | ✅ Done         |
| 7     | HUD & Score UI                            | ✅ Done         |
| 7a    | — Score Feedback (bars, notices, trails)   | ✅ Done         |
| 7b    | — Level-Up Popup                          | ✅ Done         |
| 7c    | — Shield Counter HUD                      | ✅ Done         |
| 7d    | — Projectile Shield Visuals               | ⬜ Unity wiring pending |
| 7e    | — Auto-Spawning on Projectile Death       | ✅ Done         |
| 7f    | — Game Start Button                       | ⬜ Unity wiring pending |
| 7g    | — HUD Audit & Cleanup                    | ⬜ Todo         |
| 8     | Object Pooling                            | ✅ Done         |
| 8a    | — Generic Pool System & VFX/Trail         | ✅ Done         |
| 8b    | — Migrate ScorePointTrail to PoolManager  | ✅ Done         |
| 8c    | — Migrate Balloon Instances to PoolManager| ✅ Done         |
| 9     | Balance Animation System Redo             | ✅ Done         |
| 10    | Prediction Trace                          | ✅ Done (Unity wiring pending) |
| 11    | Configuration Migration                   | ✅ Done         |
| 12    | Camera & Display Setup                    | ✅ Done         |
| 13    | Projectile Visuals (Glow + Shield Rings)  | ✅ Done         |
| 14    | MVC Architecture Audit                    | ⬜ Todo         |
| 15    | Power-Ups                                 | ⬜ Todo         |
| 16    | Game Loop, UI & Cleanup                   | ⬜ Todo         |

