# BalloonParty — Code Style Guide

> This document is the authoritative reference for architecture, code standards, and framework usage across `Assets/Source/`. All new code must conform to the rules defined here.

---

## Architecture — MVC

The codebase follows a strict MVC split enforced at the type level:

| Layer | Type | Rules |
|---|---|---|
| **Model** | Plain C# class | Holds reactive state (`ReactiveProperty<T>`). No Unity dependencies, no `MonoBehaviour`. |
| **View** | `MonoBehaviour` | The only layer that touches Unity APIs (transforms, renderers, UI, physics). Reads model state via UniRx subscriptions. Publishes input/events via MessagePipe. |
| **Controller** | Plain C# class | Registered via `Register<T>` or `RegisterEntryPoint<T>`. Uses VContainer lifecycle interfaces (`IStartable`, `ITickable`, `IFixedTickable`) instead of Unity's `Start()` / `Update()`. No `MonoBehaviour`, no `transform`. |

When a controller needs engine interaction, split into a thin **View** (MonoBehaviour) + **Controller** (plain C# class). Wire them via a child `LifetimeScope` on the view's GameObject.

---

## Stack

| Library | Role |
|---|---|
| [VContainer](https://vcontainer.hadashikick.jp/) | Dependency injection — `LifetimeScope`, `[Inject]`, `IStartable` / `ITickable` |
| [UniRx](https://github.com/neuecc/UniRx) | Reactive state — `ReactiveProperty<T>`, `Subject<T>`, `.Subscribe()` |
| [MessagePipe](https://github.com/Cysharp/MessagePipe) | Pub/sub messaging — `IPublisher<T>` / `ISubscriber<T>` |
| [UniTask](https://github.com/Cysharp/UniTask) | Async — `async UniTask`, `async UniTaskVoid`, `UniTask.Delay`, `UniTask.Yield` |
| [DOTween](http://dotween.demigiant.com/) | Tweening — animations, sequences, paths |

---

## Dependency Injection — VContainer

### Scopes

Each self-contained feature or prefab owns its own child `LifetimeScope`. Child scopes inherit all parent registrations automatically.

```csharp
// GameLifetimeScope — composition root, runs at -5001
[DefaultExecutionOrder(-5001)]
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<SlotGrid>(Lifetime.Singleton);
        builder.RegisterInstance<IGameConfiguration>(_gameConfigSO);
        builder.RegisterEntryPoint<BalloonBalancer>();

        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<BalloonHitMessage>(options);
    }
}

```

**Current scope hierarchy:**

| Scope | Base | Lives on |
|---|---|---|
| `LaunchLifetimeScope` | `LifetimeScope` | Launcher scene root |
| `GameLifetimeScope` | `LifetimeScope` | Game scene root |
| `ThrowerLifetimeScope` | `LifetimeScope` | Thrower GameObject |
| `ScoreUILifetimeScope` | `LifetimeScope` | Score HUD canvas root |
| `LevelUpLifetimeScope` | `LifetimeScope` | LevelUp popup root |
| `ShieldUILifetimeScope` | `LifetimeScope` | Shield HUD root |

> **Note:** Balloon and projectile prefabs no longer use child scopes. Their `[Inject]` fields are populated via `InjectingPoolChannel` (flat `IObjectResolver.InjectGameObject()` without container creation). `BalloonLifetimeScope`, `ProjectileLifetimeScope`, and `ItemViewScope` components remain on prefabs with `autoRun = false` — they can be removed from prefabs when convenient.

**Configuration assets registered in `GameLifetimeScope`:**

| Asset | Type | Injected as |
|---|---|---|
| `GameConfiguration` | `ScriptableObject` | `IGameConfiguration` (interface) |
| `BalloonsConfiguration` | `ScriptableObject` | `BalloonsConfiguration` (concrete) |
| `GamePalette` | `ScriptableObject` | `GamePalette` (concrete) |
| `GameDisplayConfiguration` | `ScriptableObject` | `GameDisplayConfiguration` (concrete) |
| `ItemConfiguration` | `ScriptableObject` | `ItemConfiguration` (concrete) |
| `ScorePointTrail` | Prefab instance | `ScorePointTrail` (concrete) |

### Instantiating prefabs with child scopes

| Scenario | Method |
|---|---|
| Prefab has a `LifetimeScope` with local registrations | `parentScope.CreateChildFromPrefab(prefab)` |
| Prefab has no `LifetimeScope`; single component needs injection | `resolver.Instantiate(prefab)` |

Never use plain `Object.Instantiate` for prefabs with a `LifetimeScope` — the scope's `FindParent()` races with sibling `Awake()` calls and may fail.

### Eager creation for `RegisterComponentOnNewGameObject`

VContainer singletons are lazy — the GameObject is only created when resolved. Use `RegisterBuildCallback` to force creation at scope build time:

```csharp
builder.RegisterBuildCallback(resolver => resolver.Resolve<CheatConsoleView>());
```

### Injection timing

`[Inject]` runs during `Build()` — inside the scope's `Awake()` — **before** Unity calls `Awake()` on the target. Do not rely on `Awake()`-initialized fields inside an `[Inject]` method.

---

## Reactive Programming — UniRx

```csharp
// Observe model state — auto-updates view on change
model.Color.Subscribe(c => _renderer.color = _config.BalloonColor(c)).AddTo(this);

// Pooled views: use CompositeDisposable instead of AddTo(this)
// AddTo(this) ties disposal to MonoBehaviour destruction, which never fires for pooled objects.
private readonly CompositeDisposable _bindDisposables = new();

void Bind(IBalloonModel model)
{
    _bindDisposables.Clear();
    model.Color.Subscribe(UpdateColor).AddTo(_bindDisposables);
}

void OnDespawned() => _bindDisposables.Clear();
```

---

## Messaging — MessagePipe

Use MessagePipe for communication between systems that should not hold direct references to one another.

```csharp
// Publisher
[Inject] private IPublisher<BalloonHitMessage> _publisher;
_publisher.Publish(new BalloonHitMessage(model, worldPos));

// Subscriber
[Inject] private ISubscriber<BalloonHitMessage> _subscriber;
_subscriber.Subscribe(msg => OnHit(msg)).AddTo(_disposable);
```

### Message design rules

- **Carry relevant data.** If a subscriber needs the source object (model, position, etc.), include it in the message struct rather than forcing the subscriber to inject the producer.
- **Prefer decoupling over direct injection** between unrelated systems. A controller should not inject a UI component; instead publish a message that the UI subscribes to independently.
- **Empty structs are fine** for pure signals where no data is needed.
- **Messages always carry the read-only model interface** — never the writable one. Subscribers that need write access downcast explicitly at their own call site.

---

## Async — UniTask

All async work uses UniTask. No `StartCoroutine` / `IEnumerator` in new code.

```csharp
// Fire-and-forget (MonoBehaviour)
private async UniTaskVoid DoSomethingAsync()
{
    await UniTask.Delay(500, cancellationToken: destroyCancellationToken);
    await UniTask.WaitUntil(() => _ready, cancellationToken: destroyCancellationToken);
    await UniTask.Yield(cancellationToken: destroyCancellationToken);
}
SomeAsync().Forget(); // required at call site

// Fire-and-forget (plain C# class — manual CancellationTokenSource)
private readonly CancellationTokenSource _cts = new();
SpawnAsync(_cts.Token).Forget();

// Paused-game delays (Time.timeScale = 0)
await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: destroyCancellationToken);
```

**Cancellation:**
- MonoBehaviours: use `destroyCancellationToken` — auto-cancels on destroy.
- Plain C# classes: use a `CancellationTokenSource` with manually controlled lifetime.
- Child components of pooled objects: `destroyCancellationToken` fires on actual destruction only (not on pool deactivation), so it is safe to use on pool-child MonoBehaviours.

---

## Navigation State

App-wide navigation is tracked via a static `Navigation` class (in `Shared/GameState/`) holding a `ReactiveProperty<NavigationState>`. Any system can observe or transition the current state without DI wiring across scene boundaries.

```csharp
using BalloonParty.Shared.GameState;

// Observe
Navigation.Current.Where(s => s == NavigationState.Game).Subscribe(...);

// Transition
Navigation.TransitionTo(NavigationState.LevelUp);
```

### States

| State | Meaning | Set by |
|---|---|---|
| `Launch` | App startup, Launcher UI visible | Default (initial value) |
| `Game` | Active gameplay, thrower and balloons active | `NavigationTrigger` on the Launch play button |
| `LevelUp` | Level-up popup visible, game paused | `ScoreController` on level threshold |

### Scene preloading flow

1. **Launcher `Start`** — `SceneTransition` loads the Game scene additively with rendering suppressed (layer-based camera isolation + `SuppressRendering`). VContainer resolves, pools pre-warm asynchronously.
2. **Player taps Play** — `NavigationTrigger.Transition()` sets state to `Game`. `SceneTransition.Load()` restores rendering and unloads the Launcher.
3. **`BalloonSpawner`** — awaits `NavigationState.Game` after pre-warming, then populates the grid with spawn animations.
4. **`ThrowerController`** — observes state reactively; plays entrance animation on `Game`, blocks input during `LevelUp`.

### Editor standalone play

`EditorNavigationBootstrap` (editor-only) auto-transitions to `Game` when playing a scene directly, but only if the scene is the active scene (inert during additive preloading).

## Cinematic State

A static `Cinematic` class (in `Shared/GameState/`) tracks whether a cinematic sequence is playing. Mirrors the `Navigation` pattern — static reactive property, no DI required. Services that need to pause/resume work during cinematics implement `ICinematicAware` and register with `Cinematic`.

```csharp
using BalloonParty.Shared.GameState;

// Query
if (Cinematic.IsPlaying) return;

// Observe
Cinematic.Current.Where(s => s == CinematicState.None).Subscribe(...);

// Control (from the cinematic owner)
Cinematic.Begin(CinematicState.LevelUpTrail);
Cinematic.End();
```

### ICinematicAware

Services that should react to cinematic state changes implement `ICinematicAware`:

```csharp
internal class MyService : ICinematicAware
{
    public void Start() => Cinematic.Register(this);
    public void Dispose() => Cinematic.Unregister(this);

    public void OnCinematicBegin(CinematicState state) { /* pause work */ }
    public void OnCinematicEnd() { /* resume work */ }
}
```

`Cinematic.Begin` / `End` call all registered listeners synchronously, so the cinematic owner can immediately follow up (e.g., resume a specific trail).

### States

| State | Meaning | Set by |
|---|---|---|
| `None` | No cinematic active | Default; `CinematicDirector.EndCinematic` |
| `LevelUpTrail` | Level-up trail cinematic — slow-mo, zoom, camera pan | `CinematicDirector.BeginCinematic` (called by `LevelUpTrailEffect` via scene tick) |

---

## Object Pooling

All pooling goes through `PoolManager` (singleton) and `PoolChannel<TItem>`. See `Shared/Pool/README.md` for full documentation.

Key rules:
- **The consumer that calls `Get()` is responsible for calling `Return()`** — items never self-return.
- Items signal completion via a callback passed into their `Play()` or `Setup()` call; the consumer bundles `Return()` into that callback.
- For effect VFX use `EffectView` / `EffectPoolChannel`. For particle-only effects use `PoolableParticle` / `ParticlePoolChannel`.
- Pooled `MonoBehaviour`s use `CompositeDisposable` instead of `AddTo(this)` for subscriptions — cleared on `OnDespawned()`.
- `OnDespawned()` must call `DOTween.Kill(transform)` and `tweenTracker.Kill()` to clean up any in-flight tweens before the instance is reused.

---

## Configuration — Single Source of Truth

Game data is split across focused ScriptableObjects, each registered as a singleton in `GameLifetimeScope` and injected wherever needed.

| Asset | Interface / Type | What it holds |
|---|---|---|
| `GameConfiguration` | `IGameConfiguration` | Projectile settings, slot dimensions, timing values, prediction trace params, score trail timing, score points scatter delay, points formula |
| `BalloonsConfiguration` | `BalloonsConfiguration` | Balloon prefab entries (weight, cap, nudge overrides, pop VFX), spawn line counts, animation range, balance delay, global nudge defaults |
| `GamePalette` | `GamePalette` | Named color entries — the single source for all balloon colors |
| `GameDisplayConfiguration` | `GameDisplayConfiguration` | Aspect-ratio → orthographic-size lookup for camera sizing |
| `ItemConfiguration` | `ItemConfiguration` | Per-item tuning — one `ItemSettings` entry per `ItemType`: activation frequency, weight, max cap, physics params, effect prefab, damage (damaging types only), paint flight curves/duration/arc/scale |

Rules:
- **Never hardcode** values that exist in a configuration asset.
- **Never duplicate** configuration data via `[SerializeField]` on individual systems. If a system needs a value, it injects the relevant configuration object.
- `IGameConfiguration` is the read-only interface for `GameConfiguration`; all other SOs are injected by their concrete type.
- New configuration fields are added to the appropriate asset type. If a new domain of settings grows large enough to stand alone, extract it into its own ScriptableObject rather than bloating an existing one.

---

## Animation

All tween animations must reproduce the authored values **exactly**:

1. Read the target tween values from `IGameConfiguration` — never hardcode durations or distances.
2. Match the ease type. If no `SetEase` is specified, DOTween's default (`InOutQuad`) applies — do not add eases that weren't there.
3. `Time.timeScale = 0` freezes DOTween and physics. Animators that must play while paused need `updateMode = AnimatorUpdateMode.UnscaledTime`. UniTask delays that must resolve during pause need `ignoreTimeScale: true`.

---

## Unity Project Settings

**Never edit project settings files (`TagManager.asset`, `ProjectSettings/*.asset`) outside the Unity Editor.** These files contain internal IDs and cross-references that Unity manages. All changes to sorting layers, tags, and physics layers must be made through the Unity Editor's Project Settings window.

---

## Code Quality Constraints

### Comments

- **Only comment the *why***, never the *what* or *how*. If the code needs a comment to explain what it does, rename or refactor instead.
- **No redundant comments** — avoid `// inject dependencies`, `// constructor`, `// update position`.
- **No block comment headers** (e.g. `// ====== BalloonModel ======`).
- XML doc comments (`/// <summary>`) only on public API surfaces that are non-obvious to a consumer.

### Naming & Readability

- Code must be **self-explanatory through naming, namespaces, and context**.
- Prefer longer, descriptive names over short ambiguous ones (`FindOptimalEmptySlot` > `GetSlot`).
- Namespaces must reflect folder structure (`BalloonParty.Balloon.Model`, `BalloonParty.Slots`, etc.).
- Animator parameter names are cached as `private static readonly int Param = Animator.StringToHash("Param")` — never pass magic strings to `SetTrigger` / `SetBool`.
- Physics layer masks are cached as `private static readonly int Layer = LayerMask.NameToLayer("Name")` — never looked up per-frame. **Exception:** in `MonoBehaviour` subclasses, `NameToLayer` / `GetMask` cannot be called from a static field initializer (it runs in the static constructor, before the engine is ready). Use a lazy-init sentinel instead — initialize to `-1` in the field declaration and resolve once in `Awake`:

  ```csharp
  // MonoBehaviour — lazy-init (only safe option)
  private static int BalloonsLayer = -1;
  private void Awake()
  {
      if (BalloonsLayer == -1) BalloonsLayer = LayerMask.NameToLayer("Balloons");
  }

  // Plain C# class — static readonly is fine, no MonoBehaviour static-constructor restriction
  private static readonly int BalloonsLayer = LayerMask.NameToLayer("Balloons");
  ```

### Visibility

- **Default to `private`.** Only increase visibility when there is a concrete consumer.
- Expose only intentional service methods as `public`. Nothing is `public` "just in case".
- Prefer `internal` over `public` when the consumer is within the same assembly but outside the class.

### Architecture & Reuse

- **Before writing new code, check for existing methods** in the codebase that can be ported, extracted, or called directly.
- **Identify commonalities early** — if two controllers share a pattern, extract it.
- **Prefer generic implementations** over copy-paste specialisations.
- **Extension methods** over utility classes where possible — keep them in `Shared/Extensions/`.
- Keep classes **small and focused**. If a class is growing beyond one clear responsibility, split it.
- Avoid `static` state; prefer injected singleton services via VContainer.

### Formatting

- **Allman brace style** — every opening brace on its own line.
- **Braces are always required** for `if`, `else`, `for`, `foreach`, `while`, `using`, `lock`, and `fixed` — even single-line bodies.

---

## Member Ordering

Classes follow a strict top-to-bottom ordering. Within each numbered group, sort **alphabetically** unless noted.

### Fields & Properties

| # | Group | Example |
|---|---|---|
| 1 | **Constants** (`const`) | `private const float PickRadius = 0.25f;` |
| 2 | **Static readonly fields** | `private static readonly Color DefaultColor = Color.white;` |
| 3 | **`[SerializeField]` fields** | `[SerializeField] private SpriteRenderer _renderer;` — grouped by `[Header]` where useful |
| 4 | **`[Inject]` fields** | `[Inject] private SlotGrid _grid;` — all together, alphabetical by type name |
| 5 | **Readonly instance fields** | `private readonly List<Vector3> _path = new();` |
| 6 | **Mutable instance fields** | `private bool _active;` — always last among fields |
| 7 | **Auto-properties / expression-body properties** | `public string Name => "...";` |

**Key rule:** Fields decorated with an attribute (`[SerializeField]`, `[Inject]`) group by attribute, not by readonly/mutable.

### Methods

| # | Group | Notes |
|---|---|---|
| 1 | **Constructors** | Static constructors before instance constructors |
| 2 | **Unity lifecycle** | `Awake` → `OnEnable` → `Start` → `Update` → `FixedUpdate` → `LateUpdate` → `OnDisable` → `OnDestroy` → other callbacks |
| 3 | **`[Inject]` methods** | Immediately after lifecycle; injection wiring only |
| 4 | **Interface implementations** | `IStartable.Start()`, `ITickable.Tick()`, `IDisposable.Dispose()`, `IPoolable`, etc. |
| 5 | **Public methods** | Alphabetical |
| 6 | **Protected methods** | Alphabetical |
| 7 | **Private methods** | Alphabetical; helpers called only by one method may sit directly below their caller |

### Canonical Example

```csharp
public class BalloonRemoverCheat : MonoBehaviour, ICheat
{
    // 1. Constants
    private const float PickRadius = 0.25f;

    // 2. Static readonly fields
    private static readonly int CircleSegments = 32;

    // 4. [Inject] fields
    [Inject] private SlotGrid _grid;
    [Inject] private IPublisher<BalloonHitMessage> _hitPublisher;

    // 5. Readonly instance fields
    private readonly List<Vector3> _path = new();

    // 6. Mutable instance fields
    private bool _active;
    private bool _dragging;

    // 7. Auto-properties
    public string Name => _active ? "Remove Balloons  [ON]" : "Remove Balloons";
    public string Section => "Grid";

    // Unity lifecycle
    private void Awake() { ... }
    private void Update() { ... }

    // Interface implementations
    public void Execute() { ... }

    // Private helpers (alphabetical)
    private HashSet<Vector2Int> CollectHitSlots() { ... }
    private void RemoveBalloonsAlongPath() { ... }
}
```

---

## Model Interface Pattern — Read/Write Separation

Every model exposes two interfaces to enforce read/write separation at the type level:

| Type | Purpose | Typical consumer |
|---|---|---|
| `IModel` (e.g. `IBalloonModel`) | **Read-only** — `IReadOnlyReactiveProperty<T>` and read-only plain properties | Views, score controllers, messages |
| `IWriteableModel` (e.g. `IWriteableBalloonModel`) | **Mutable** — extends `IModel`; re-declares reactive properties with `new` to expose `ReactiveProperty<T>` | Controllers, spawners, any mutating code |
| `Model` (e.g. `BalloonModel`) | **Concrete class** — implements `IWriteableModel` | Construction sites only |

Rules:
1. **Use the narrowest interface.** Pass `IModel` unless mutation is required.
2. **Messages always carry the read-only interface.** If a subscriber needs write access, it downcasts explicitly.
3. **No view references on models.** Models must not hold back-references to views or `Transform`s. Reach the view via `SlotGrid.ViewAt(slotIndex)`.

```csharp
public interface IBalloonModel
{
    IReadOnlyReactiveProperty<string> Color { get; }
    IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
    IReadOnlyReactiveProperty<bool> IsStable { get; }
}

public interface IWriteableBalloonModel : IBalloonModel
{
    new ReactiveProperty<string> Color { get; }
    new ReactiveProperty<Vector2Int> SlotIndex { get; }
    new ReactiveProperty<bool> IsStable { get; }
}

public class BalloonModel : IWriteableBalloonModel
{
    public ReactiveProperty<string> Color { get; } = new();
    public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
    public ReactiveProperty<bool> IsStable { get; } = new(true);

    IReadOnlyReactiveProperty<string> IBalloonModel.Color => Color;
    IReadOnlyReactiveProperty<Vector2Int> IBalloonModel.SlotIndex => SlotIndex;
    IReadOnlyReactiveProperty<bool> IBalloonModel.IsStable => IsStable;
}
```

---

## UI Scope Architecture

Each self-contained UI panel or popup owns its own child `LifetimeScope`. Benefits: the popup resolves its dependencies from the game scope, keeps its registrations local, and can be opened in a standalone scene without the full game running.

**Rule:** Any UI element that is logically self-contained (a popup, a full-screen panel, a HUD section) gets its own `LifetimeScope`. Flat components that are always part of a larger panel stay registered in that panel's scope.

### Dynamically instantiated prefab scopes

Prefabs carrying multiple MonoBehaviours that need injection (e.g. projectile, balloon) use `LifetimeScope.CreateChildFromPrefab()`. The spawner injects the parent `LifetimeScope` and calls `parentScope.CreateChildFromPrefab(prefab)` — this deactivates the prefab before `Instantiate`, sets the parent reference, then reactivates so `Awake()` → `Build()` runs with the correct parent.

`RegisterComponentInHierarchy` scope boundary: for scene-based scopes, Unity searches the **entire scene**. For prefab-based scopes created via `CreateChildFromPrefab`, the search is limited to `GetComponentsInChildren`. Always confirm the component exists in the expected search space.

---

## Cheat Console

A runtime debug console in `Cheats/`. Press **backtick (`)** to toggle. The entire system is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and compiles out in release builds.

**Adding a cheat:**
1. Implement `ICheat` — provide `Name`, `Section`, and `Tags[]`.
2. Inject whatever publishers or services it needs via the constructor.
3. Register in `GameLifetimeScope`: `builder.Register<YourCheat>(Lifetime.Singleton).AsImplementedInterfaces()`.

**Cheat ownership rule:** All logic specific to a cheat lives in the cheat class. Game systems must not expose methods solely to serve cheats. Cheats drive behaviour through the same messages and public APIs that gameplay uses.

---

## Gizmos & Editor Drawing

Two parallel drawing helpers provide identical method signatures and coordinate conventions across editor and runtime contexts:

| Helper | Location | API | Used by |
|---|---|---|---|
| `SceneDrawingHelper` | `Editor/` | `Handles` | Custom editors, `[InitializeOnLoad]` scene overlays |
| `GizmoDrawingHelper` | `Shared/Rendering/` | `Gizmos` | Any `MonoBehaviour.OnDrawGizmos` callback |

Both expose `DrawWorldRect(center, width, height, outlineColor, fillColor)` and `DrawWorldRectFromLimits(top, right, bottom, left, outlineColor, fillColor)` using the clockwise `Vector4` convention (top → right → bottom → left).

### Build-stripping rules

All gizmo-related code must be guarded with `#if UNITY_EDITOR` to compile out in builds:

1. **`GizmoDrawingHelper` itself** is wrapped entirely in `#if UNITY_EDITOR` / `#endif`. It lives in the runtime assembly (`Shared/Rendering/`) so runtime MonoBehaviours can reference it, but it compiles out completely in builds.

2. **Consumer MonoBehaviours** must wrap gizmo-only fields (`static readonly` colors, `[Inject]` dependencies) **and** the `OnDrawGizmos` method in `#if UNITY_EDITOR`. `OnDrawGizmos` is stripped automatically in builds, but fields and `[Inject]` decorations are **not** — they remain in the build, cause DI resolution overhead, and hold references that would otherwise not exist.

```csharp
public class SlotGridView : MonoBehaviour
{
#if UNITY_EDITOR
    private static readonly Color EmptySlotColor = new(1f, 1f, 1f, 0.2f);

    [Inject] private IGameConfiguration _config;

    private void OnDrawGizmos()
    {
        // draw using _config and EmptySlotColor
    }
#endif
}
```

This ensures:
- No `[Inject]` resolution in builds for gizmo-only dependencies.
- No `static readonly` allocations for gizmo-only colors.
- The class compiles as an empty (or minimal) MonoBehaviour in release.

Fields shared between gizmo drawing **and** runtime logic (e.g. `SlotGrid _grid` used by both gameplay and `OnDrawGizmos`) should **not** be guarded — they are needed regardless.

---

## Living Documentation

Each feature folder contains a `README.md` describing its gameplay purpose, how it works, and how it interacts with other systems.

**Keep them current.** Update a folder's README whenever:
- A new mechanic is added or an existing one changes significantly.
- A system's responsibility shifts.
- Interactions with other systems are added, removed, or change in character.

**No implementation notes.** READMEs explain the current architecture as if it were always the intended design. They are written for a new developer reading the folder for the first time, not as a changelog.

---

## Enforcement Tooling

The style guide is enforced at multiple layers so violations are caught as early as possible:

| Layer | Tool | What it enforces |
|---|---|---|
| **IDE (real-time)** | `.editorconfig` | Allman braces, required braces, naming conventions (`_camelCase` fields, `PascalCase` members, `I`-prefixed interfaces), block-scoped namespaces, `var` usage, accessibility modifiers |
| **IDE (real-time)** | `BalloonParty.sln.DotSettings` | Rider/ReSharper-specific: member ordering warnings, modifier defaults, blank line rules, wrapping |
| **Git (per commit)** | `Tools/pre-commit` | Runs `style_audit.py` on staged `.cs` files; blocks commit on violations |
| **Manual / CI** | `Tools/style_audit.py` | Member ordering, block comment headers, redundant comments, `StartCoroutine` usage, magic strings (uncached animator/layer params), `AddTo(this)` in poolable classes, namespace mismatches, missing READMEs |

### Quick reference

```bash
# Full audit
python3 Tools/style_audit.py

# Single rule or file
python3 Tools/style_audit.py --rule braces
python3 Tools/style_audit.py --file BalloonView

# Auto-fix namespaces
python3 Tools/style_audit.py --fix

# Install pre-commit hook
cp Tools/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```
