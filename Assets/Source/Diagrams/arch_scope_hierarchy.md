@page arch_scope_hierarchy VContainer Scope Hierarchy

# VContainer Scope Hierarchy

@image html scope_hierarchy.svg "VContainer Scope Hierarchy"

## What this diagram shows

How VContainer scopes nest in BalloonParty. Child scopes automatically inherit all
parent registrations, so `GameLifetimeScope` is the composition root that every other
scope can resolve from.

**Current scope tree:**
- `LaunchLifetimeScope` — launcher scene; registers camera controller, display config, and `SceneCaptureService`
- `GameLifetimeScope` — game scene root; registers all gameplay systems, config SOs,
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

