@mainpage BalloonParty

# BalloonParty

> A balloon-popping game built with Unity, VContainer, UniRx, MessagePipe, UniTask, and DOTween.

---

## Documentation

| Page | Description |
|------|-------------|
| @subpage style_guide "Code Style Guide" | Architecture rules, code standards, and framework usage |
| @subpage architecture "Architecture Diagrams" | Graphviz system maps, data flow, and scope hierarchy |
| @subpage plans "Plans" | Working design plans and roadmaps for upcoming features |

---

## Architecture at a Glance

**Pattern:** MVC — Model (plain C#, ReactiveProperty), View (MonoBehaviour, UniRx), Controller (IStartable/ITickable, VContainer DI)

**Stack:**

| Library | Role |
|---------|------|
| VContainer | Dependency injection, lifetime scopes |
| UniRx | Reactive properties, subscriptions |
| MessagePipe | Pub/sub messaging |
| UniTask | Async/await (replaces coroutines) |
| DOTween | Tween animations |

**Key Systems:**

- **SlotGrid** — 2D grid of actor slots with interface-based occupants
- **BalloonSpawner** — Staged spawning coordinated by `GridSpawnerCoordinator`
- **BalloonBalancer** — Consolidates grid gaps, transit-aware via `BalancePathHolder`
- **ScoreController** — Streak tracking, trail orchestration, level-up detection
- **CinematicDirector** — Camera pan-in/restore sequences during level-up
- **NudgeService** — Visual knockback with per-type overrides via `NudgeOverrideResolver`
- **PoolManager** — Object pooling with `PoolChannel<T>` and `CompositeDisposable` lifecycle

---

## Quick Links

- [Namespaces](namespaces.html) — Browse by namespace
- [Classes](annotated.html) — Full class list with inheritance diagrams
- [Class Hierarchy](hierarchy.html) — Graphical inheritance tree
- [Files](files.html) — Source file browser with per-folder READMEs

