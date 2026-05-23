# BalloonParty — AI Coding Instructions

> Compact reference for AI assistants. Full guide: `Assets/Source/README.md`

## Architecture: MVC
- **Model** — plain C#, `ReactiveProperty<T>`, no MonoBehaviour
- **View** — MonoBehaviour, UniRx subscriptions, MessagePipe events
- **Controller** — plain C#, VContainer `IStartable`/`ITickable`, no `transform`

## Stack
VContainer (DI), UniRx (reactive), MessagePipe (pub/sub), UniTask (async), DOTween (tweens)

## Key Rules

### Fields — order top to bottom
1. `const` 2. `static readonly` 3. `[SerializeField]` 4. `[Inject]` 5. `readonly` 6. mutable

### Methods — order top to bottom
1. Constructors 2. Unity lifecycle 3. `[Inject]` 4. Interface impls 5. public 6. protected 7. private
- Prefer passing a config/settings object over many parameters

### Formatting
- **Allman braces** — opening `{` on own line
- **Braces always required** on if/else/for/foreach/while/using/lock/fixed
- No `StartCoroutine` — use `async UniTask`

### Comments
- Only comment the *why*, never the *what*
- No block headers (`// ======`)
- No redundant comments (`// constructor`, `// inject`)
- XML docs only on non-obvious public API

### Naming
- Namespaces match folder structure (`BalloonParty.Balloon.View`)
- Cache animator params: `static readonly int Param = Animator.StringToHash("Param")`
- Cache layer masks in static fields (lazy-init in MonoBehaviours)

### Visibility
- Default `private`. Prefer `internal` over `public` within same assembly.

### Pooling
- `PoolManager` + `PoolChannel<T>`. Consumer calls `Return()`.
- Pooled objects use `CompositeDisposable`, not `AddTo(this)`
- `OnDespawned()` must kill tweens

### Configuration
- Never hardcode values from config assets
- Never duplicate config via `[SerializeField]` — inject the config SO
- Assets: `IGameConfiguration`, `BalloonsConfiguration`, `GamePalette`, `GameDisplayConfiguration`, `ItemConfiguration`

### ColorableRenderer
- `ColorableRenderer` — abstract MonoBehaviour base for `SetColor(Color)`
- `ColorableRenderer<T>` — generic, lazy-fetches renderer component
- `CompositeColorableRenderer` — forwards to array of children
- `BindColor` extensions for reactive subscriptions (in `Shared/Extensions/`)

### READMEs
- Every feature folder has a `README.md`
- Update when mechanics change
- Written for new developers, not as changelog

### Gizmos & Editor Drawing
- `GizmoDrawingHelper` (`Shared/Rendering/`) — Gizmos API for `OnDrawGizmos`
- `SceneDrawingHelper` (`Editor/`) — Handles API for `SceneView.duringSceneGui`
- Both share method signatures and coordinate conventions — decouple drawing logic from the rendering API
- Guard `GizmoDrawingHelper`, gizmo-only fields, and `OnDrawGizmos` in `#if UNITY_EDITOR`

## Audit Script
```bash
python3 Tools/style_audit.py              # full scan
python3 Tools/style_audit.py --rule X     # single rule
python3 Tools/style_audit.py --file X     # single file
python3 Tools/style_audit.py --fix        # auto-fix: allman braces, braces-required, blank lines, comments, namespace
```

## Pre-commit Hook
`Tools/pre-commit` — audits staged `.cs` files on every `git commit`. Install:
```bash
cp Tools/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

## IDE Enforcement
- `.editorconfig` — Allman braces, required braces, naming conventions, block-scoped namespaces, `var` usage. Applied by any editor.
- `BalloonParty.sln.DotSettings` — Rider/ReSharper settings for member ordering warnings, modifier defaults, formatting.
