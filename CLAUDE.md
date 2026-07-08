# BalloonParty — Claude Code Guide

Compact, always-loaded reference for working in this repo. **Authoritative full guide:**
`Assets/Source/README.md`. Design plans: `Assets/Source/Plans/`.

Source lives under `Assets/Source/` (asmdef `BalloonParty.Runtime`); editor code under
`Assets/Source/Editor/`. Conform to the conventions below — they're enforced by
`Tools/style_audit.py` and a pre-commit hook, so violations block commits.

---

## Architecture — MVC (enforced at the type level)
- **Model** — plain C#, `ReactiveProperty<T>`, no `MonoBehaviour`, no `transform`.
- **View** — `MonoBehaviour`; the only layer touching Unity APIs. Reads model state via UniRx; publishes input via MessagePipe.
- **Controller** — plain C#, registered in VContainer (`IStartable`/`ITickable`/`IFixedTickable`), no `MonoBehaviour`.

When a controller needs engine interaction, split into a thin View (MonoBehaviour) + Controller (plain C#).

## Stack
VContainer (DI — `LifetimeScope`, `[Inject]`), UniRx (`ReactiveProperty<T>`, `.Subscribe()`),
MessagePipe (`IPublisher<T>`/`ISubscriber<T>`), UniTask (`async UniTask`), DOTween (tweens).

---

## Code rules

### Field order (top → bottom)
1. `const` · 2. `static readonly` · 3. `[SerializeField]` · 4. `[Inject]` · 5. `readonly` · 6. mutable · 7. properties
(Attribute-decorated fields group by attribute, not by readonly/mutable.)
- **Properties live in this top block, after the fields and before the constructor** — never among constructors or methods. Order: `fields → properties → constructors → methods`. A property (incl. expression-bodied getters) declared after a constructor or method is an error. Exception: each `#if`/`#else` preprocessor branch is its own context, so an editor-only property inside an `#if UNITY_EDITOR` block below the class's methods is fine (the same ordering still applies *within* that branch).

### Method order (top → bottom)
1. Constructors · 2. Unity lifecycle (`Awake`→`OnEnable`→`Start`→`Update`→…) · 3. `[Inject]` methods · 4. interface impls · 5. public · 6. protected · 7. private
- Prefer passing a config/settings object over many parameters.
- Use read-only collection interfaces for params never mutated (`IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`, `IReadOnlyCollection<T>`).

### Formatting
- **Allman braces** — opening `{` on its own line.
- **Braces always required** on `if/else/for/foreach/while/using/lock/fixed`.
- No `StartCoroutine`/`IEnumerator` — use `async UniTask`.
- One blank line max between members; file ends with exactly one newline.

### Comments
- Comment the *why*, never the *what*. No block headers (`// =====`). No redundant comments (`// constructor`). XML docs only on non-obvious public API.

### Naming & visibility
- Namespaces mirror folder structure (`BalloonParty.Balloon.View`).
- Cache animator params: `static readonly int Param = Animator.StringToHash("Param")`. Cache layer masks in static fields (lazy-init in MonoBehaviours — `NameToLayer` can't run in a static-field initializer there).
- Default `private`; prefer `internal` over `public` within the same assembly.

### Pooling
- `PoolManager` + `PoolChannel<T>`; the consumer that calls `Get()` calls `Return()`.
- Pooled `MonoBehaviour`s use `CompositeDisposable` (cleared on `OnDespawned()`), **not** `AddTo(this)`.
- `OnDespawned()` kills in-flight tweens (`DOTween.Kill(transform)` / `tweenTracker.Kill()`).

### Configuration
- Never hardcode values that live in a config asset; never duplicate config via `[SerializeField]` — inject the **read-only interface** (`IGameConfiguration`, `IBalloonsConfiguration`, `IGamePalette`, `IGameDisplayConfiguration`, `IItemConfiguration`, `IGridActorConfiguration`, `IDisturbanceFieldSettings`, `IPuffCloudSettings`, `IBushSettings`), not the concrete SO.
- Editor config lookups: use `ConfigAssetCache<T>` (`Shared/`) — never inline `FindAssets` + `LoadAssetAtPath`.

### Gizmos / editor drawing
- `GizmoDrawingHelper` (`Shared/Rendering/`, Gizmos) and `SceneDrawingHelper` (`Editor/`, Handles) share signatures/conventions.
- Guard `GizmoDrawingHelper`, gizmo-only fields, and `OnDrawGizmos` in `#if UNITY_EDITOR` (fields/`[Inject]` are NOT auto-stripped — `OnDrawGizmos` is).

### Never edit Unity project settings outside the editor
`TagManager.asset`, `ProjectSettings/*.asset` (sorting layers, tags, physics layers) — change via the Unity Editor only.

### READMEs & Plans (living docs)
- Every feature folder has a `README.md`; update it when mechanics/responsibilities change. Written for a new developer, not as a changelog.
- Plans live in `Assets/Source/Plans/`, named `PLAN-<Feature>.md`, starting with a Doxygen `@page`, registered in `Plans/Plans.md` via `@subpage`. New `.md`/`.cs` files need a Unity `.meta` (mirror an existing sibling's format).

---

## Verifying changes (no Unity runtime here)
Compile C# without opening Unity:
```bash
dotnet build BalloonParty.Runtime.csproj -nologo -clp:ErrorsOnly   # also .Editor.csproj / .Tests.EditMode.csproj
```
- **`dotnet build` does NOT compile shaders** (`.shader`/HLSL) — only Unity validates those. Flag shader edits as needing an in-editor check.
- It also can't run the game; behavior/visual changes (rendering, animation, the level-up cinematic) need an in-editor playtest. Say so rather than claiming verified.

Style audit (the same rules the pre-commit hook runs on staged `.cs`):
```bash
python3 Tools/style_audit.py            # full scan
python3 Tools/style_audit.py --file X   # one file
python3 Tools/style_audit.py --fix      # auto-fix: braces, blank lines, comments, namespace
```
Markdown plan edits don't trigger the audit.

## Workflow notes
- The pre-commit hook (`Tools/pre-commit`) runs `style_audit.py` on staged `.cs` and blocks on `[ERROR]` findings (advisory `[WARN]` ones don't block; `--strict` promotes them). CI runs the same audit + `Tools/test_style_audit.py`. Fix the check, not the code, if it misfires.
- This repo commits directly to `main` (single-developer history). Commit only when asked.
- Project status, optimization findings, and known-fragile areas (e.g. the level-up cinematic trail path) live in Claude's memory index, not here — check it for current state.
