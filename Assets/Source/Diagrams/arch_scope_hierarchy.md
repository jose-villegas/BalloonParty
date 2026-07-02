@page arch_scope_hierarchy VContainer Scope Hierarchy

# VContainer Scope Hierarchy

@image html scope_hierarchy.svg "VContainer Scope Hierarchy"

## What this diagram shows

How VContainer scopes nest in BalloonParty. Child scopes automatically inherit all
parent registrations, so `GameLifetimeScope` is the composition root that every other
scope can resolve from.

**Current scope tree:**
- `LaunchLifetimeScope` — launcher scene; registers camera controller and display config
- `GameLifetimeScope` — game scene root; registers all gameplay systems, config SOs,
  MessagePipe brokers, and entry points
  - `ThrowerLifetimeScope` — thrower prefab; registers `ThrowerController` and wires the view
  - `ScoreUILifetimeScope` — score HUD canvas; registers `ColorProgressBar` array and `ScoreTrailService`
  - `LevelUpLifetimeScope` — level-up popup; registers `LevelUpPopUp` and the `CinematicEndGate` ready-gate (the cinematic producers and `CinematicDirector` live in `GameLifetimeScope`)
  - `ShieldUILifetimeScope` — shield HUD; registers `ShieldCounterLabel` and `ShieldCounterAnimation`

**Pooled prefabs (balloons, projectiles)** do not use child scopes. Their `[Inject]`
fields are populated via `InjectingPoolChannel` which calls
`IObjectResolver.InjectGameObject()` directly — no scope creation overhead per instance.

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

