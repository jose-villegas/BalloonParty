@page arch_mvc MVC Pattern

# MVC Pattern

@image html mvc_architecture.svg "Model–View–Controller with MessagePipe"

## What this diagram shows

The three-layer split that governs every feature in BalloonParty. Each layer has a
strict contract about what it is allowed to touch:

- **Model** — pure C# data objects; holds `ReactiveProperty<T>` fields; zero Unity
  engine dependencies; never references a `Transform` or `MonoBehaviour`
- **View** — `MonoBehaviour` subclass; the only layer that calls Unity APIs
  (transforms, renderers, physics, UI); subscribes to model state via UniRx and
  drives visual output; publishes input events via MessagePipe
- **Controller** — pure C# class; registered with VContainer as `IStartable` /
  `ITickable`; orchestrates systems; mutates models; no `MonoBehaviour`, no `transform`

MessagePipe arrows cross layer boundaries: a **View** publishes `ActorHitMessage` on
collision; a **Controller** subscribes to it and decides what to do. This keeps Views
ignorant of game logic and Controllers ignorant of Unity rendering.

## Guidance

**Adding a new feature:**
1. Start with the model — what reactive state does this feature need?
2. Add a view only if something must be rendered or receive Unity input
3. Wire coordination in a controller — subscribe to messages, call model setters
4. If a controller needs engine interaction it cannot avoid, split into a thin
   companion View (`MonoBehaviour`) + Controller pair and share state via an injected model

**Common mistakes to avoid:**
- Business logic inside `Update()` or `OnTriggerEnter2D` — move to a Controller
- A Controller holding a `Transform` reference — extract to a View helper
- A Model publishing to MessagePipe directly — models are passive; Views publish

