@page arch_scope_hierarchy VContainer Scope Hierarchy

# VContainer Scope Hierarchy

@image html scope_hierarchy.svg "VContainer Scope Hierarchy"

## What this diagram shows

How VContainer scopes nest in BalloonParty. Child scopes automatically inherit all
parent registrations. `AppLifetimeScope` is the actual VContainer **project root** — a
persistent, `DontDestroyOnLoad` scope built once for the whole session — and parents both
`LaunchLifetimeScope` and `GameLifetimeScope`, which resolve its registrations as children.

**Current scope tree:**
- `AppLifetimeScope` — persistent app root; owns the display config, the ambient time-of-day
  owner, the single camera pipeline (there is only one camera for the whole session), and the
  camera-independent backdrop fields (disturbance, background cloud, the shared impact bus)
  - `LaunchLifetimeScope` — launcher scene; essentially empty, the app root already provides
    everything the launch screen needs
  - `GameLifetimeScope` — game scene root; registers all per-run gameplay systems, config SOs,
    MessagePipe brokers, and entry points
    - `ThrowerLifetimeScope` — thrower prefab; registers `ThrowerController` and wires the view
    - `ScoreUILifetimeScope` — score HUD canvas; injects the `ColorProgressBar` array and binds the score/level labels (`ScoreTrailService` itself lives in `GameLifetimeScope`)
    - `LevelUpLifetimeScope` — level-up popup; registers `LevelUpPopUp` and the `CinematicEndGate` ready-gate (the cinematic producers and `CinematicDirector` live in `GameLifetimeScope`)
    - `ShieldUILifetimeScope` — shield HUD; registers the shield counter labels, `ShieldCounterAnimation`, `ShieldTrailController`, and the shield trail endpoint
    - `HealthUILifetimeScope` — health HUD; binds `HealthCounterLabel` views, registers `HeartTrailController` and the heart trail endpoint
    - `DangerUILifetimeScope` — danger overlay; binds `DangerGradientView` to the danger level
    - `GameOverLifetimeScope` — game-over panel; a scope shell with no local registrations (hierarchy injection only)

**Pooled prefabs (balloons, projectiles)** do not use child scopes. Their `[Inject]`
fields are populated via `InjectingPoolChannel`, which instantiates each instance through
`IObjectResolver.Instantiate()` directly — no scope creation overhead per instance.

## Guidance

**Registering a new service:**
- Game-wide singleton → register in `GameLifetimeScope`
- Scoped to a specific UI panel → give that panel its own child `LifetimeScope`
- Scoped to a prefab with multiple injected components → use `InjectingPoolChannel` if pooled, or `CreateChildFromPrefab` for one-shots

**When to create a new child scope:**
- The component is logically self-contained (popup, HUD section, feature prefab)
- It has local registrations other systems should not see
- It needs to be opened standalone (e.g. in a test scene) without the full game running

**Never** use `Object.Instantiate` for a prefab that carries a `LifetimeScope` —
`FindParent()` races with sibling `Awake()` calls. Use `parentScope.CreateChildFromPrefab(prefab)`.

