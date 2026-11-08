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

### Logging
- Use `Log.Info` / `.Warn` / `.Error` / `.Assert` (`Shared/Diagnostics/Log.cs`) — prefer over raw `Debug.Log` in runtime code. `Info` and `Assert` auto-strip from release builds via `[Conditional]`; no `#if` guards needed. Editor-only tooling (windows, bakers, migration tools) may use `Debug.Log` directly since it never ships.

### Naming & visibility
- Namespaces mirror folder structure (`BalloonParty.Balloon.View`).
- Cache animator params: `static readonly int Param = Animator.StringToHash("Param")`. Cache layer masks in static fields (lazy-init in MonoBehaviours — `NameToLayer` can't run in a static-field initializer there).
- Default `private`; prefer `internal` over `public` within the same assembly.

### Pooling
- `PoolManager` + `PoolChannel<T>`; the consumer that calls `Get()` calls `Return()`.
- Pooled `MonoBehaviour`s use `CompositeDisposable` (cleared on `OnDespawned()`), **not** `AddTo(this)`.
- `OnDespawned()` kills in-flight tweens (`DOTween.Kill(transform)` / `tweenTracker.Kill()`).

### Configuration
- Never hardcode values that live in a config asset; never duplicate config via `[SerializeField]` — inject the **read-only interface** (`IGameConfiguration`, `IBalloonsConfiguration`, `IGamePalette`, `IGameDisplayConfiguration`, `IItemConfiguration`, `IGridActorConfiguration`, `IDisturbanceFieldSettings`, `IPuffCloudSettings`, `IBushSettings`, `IShieldFieldSettings`, `IBackgroundFieldSettings`, `ISmokeFieldSettings`, `IShieldChainSettings`), not the concrete SO.
- Editor config lookups: use `EditorAssetCache<T>` (editor-only, in the `com.balloonparty.editorui` package; formerly `ConfigAssetCache<T>` in `Shared/`) — never inline `FindAssets` + `LoadAssetAtPath`. It lives in an editor assembly, so a runtime `#if UNITY_EDITOR` block **can't** reference it (a runtime assembly can't depend on an editor one) — those still fall back to inline lookups.

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
- **`dotnet build` defines `UNITY_EDITOR`** (and `UNITY_ENABLE_CHECKS`/`UNITY_INCLUDE_INSTRUMENTATION`, which the editor always defines), so `#if !UNITY_EDITOR` (device-only) code is silently skipped. To compile-check it, rebuild with `-p:DefineConstants` overridden to strip `UNITY_EDITOR` (escape `;` as `%3B`, add `-p:BuildProjectReferences=false`); strip `UNITY_ENABLE_CHECKS` too to check a Release-variant player, where the cheats and `Log.Info` compile out.
- **Dev-only code gates on `UNITY_ENABLE_CHECKS`**, never `DEVELOPMENT_BUILD` (deprecated in Unity 6.6, removed in 6.8). It follows the per-platform Managed Code Variant (Checked/Debug), not the Development Build checkbox.
- It also can't run the game; behavior/visual changes (rendering, animation, the level-up cinematic) need an in-editor playtest. Say so rather than claiming verified.
- When the user's Editor is open, the Unity CLI can drive it (`unity status`, `unity command …`, via the `com.unity.pipeline` package). It has the **main checkout** open, so it only sees committed-and-merged code. `Tools/playtest/playtest_bot.py` plays runs with the best-shot solver and writes a metrics report (levels, shots, pops, hearts, flight times) to `Logs/playtest/` — see `Tools/playtest/README.md`. A bot plays well, not like a person: use it for comparisons between tunings.

Style audit (the same rules the pre-commit hook runs on staged `.cs`):
```bash
python3 Tools/style_audit.py            # full scan
python3 Tools/style_audit.py --file X   # one file
python3 Tools/style_audit.py --fix      # auto-fix: braces, blank lines, comments, namespace
```
Markdown edits don't trigger the C# audit — but they do trigger Mermaid validation.

Mermaid diagrams (plans, feature READMEs, `ARCHITECTURE.md`) are validated by rendering:
```bash
node Tools/validate-mermaid.mjs          # all of Assets/ + README.md + .claude/agents
node Tools/validate-mermaid.mjs path.md  # one file or directory
```
Needs `npm install -g @mermaid-js/mermaid-cli`. Load the `mermaid` skill before authoring
a diagram — a broken one fails silently on both GitHub and the Doxygen site.

## Workflow notes
- The pre-commit hook (`Tools/pre-commit`) runs `style_audit.py` on staged `.cs` (blocks on `[ERROR]`; advisory `[WARN]` doesn't block, `--strict` promotes it) and `validate-mermaid.mjs` on staged `.md` that contain diagrams. Enable it with `git config core.hooksPath Tools`. A missing interpreter skips its check with a warning rather than blocking. CI runs the same audit + `Tools/test_style_audit.py`. Fix the check, not the code, if it misfires.
- This repo commits directly to `main` (single-developer history). Commit only when asked.
- Project status, optimization findings, and known-fragile areas (e.g. the level-up cinematic trail path) live in Claude's memory index, not here — check it for current state.
