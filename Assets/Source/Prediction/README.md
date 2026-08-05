# Prediction

Prediction trace system — draws a dotted line showing the projectile's predicted trajectory while the player is aiming.

## Architecture

### PredictionTraceCalculator

Pure C# class (no MonoBehaviour) that takes an origin, direction, and reusable `List<Vector3>`, then fills it with world-space trace points by stepping forward and reflecting off walls **and off balloons that would deflect the shot**. Bounces off left, right, and top limits (from `LimitsClockwise`). A top-wall hit terminates further bounces.

**Deflections come from `IDeflectorField`** (implemented by `SlotGridDeflectorField`, which walks the grid). An actor qualifies when it is `IDeflectsShots` and `DeflectsOrdinaryHit` — Tough with more than one hit left, Unbreakable, the static Deflector, a Gatekeeper with hits left — and its view still has an active collider and a non-zero `ContactRadius`, so an actor mid-despawn, or a static with no collider authored, never deflects the line.

The field walks the grid rather than the balloon registry because deflecting is a capability, not a family: statics deflect too, and the grid is the one place holding every actor regardless of type. A new deflecting archetype needs no change to the field.

The **drawn** corner sits on the deflector's own skin, while the flight continues from the combined-radius contact where the shot's centre really turns. Those differ by the shot's radius, and a line whose corner floats short of the balloon reads as a bend that happened too early — the leg after it is offset by that same radius, which is invisible over its length. Only the polyline moves: `PredictionTraceProvider` (and so `TraceHitMarker`) still receives the true contacts.

The geometry is the **view's** `ContactCenter` and `ContactRadius` (both on `ISlotActorView`), not the slot's, because that is what `BalloonController.Deflect` hands the real projectile: balloons drift between slots while the board settles, and that is exactly when a player is lining a shot up. `ContactCenter` is the collider, not the pivot, so an offset collider deflects where it actually sits. Both the trace and the real deflection call `Shared/CircleContact.TryFindEntry` — one analytic ray-circle solver, so the drawn line cannot drift from the flight it predicts.

Walls and deflections share **one budget** (`IPredictionTraceConfig.MaxReflections`, currently 1). They cost the shot differently — a wall spends a shield and a deflection does not, since `ProjectileMotionResolver` decrements only on a wall — but this budget is about how much the telegraph gives away, not about what the shot can afford, and to the player both are the same event: the line turned.

The leg *leaving* the last allowed reflection is still drawn; the one after it is not. So at 1 the player sees where the shot turns once and where that takes it, and works the rest out. Expected to rise with progression.

Within a step the nearest deflector wins, and the test runs against the step *already clipped by any wall*, so a balloon beyond a wall cannot steal a bounce the wall reaches first. The contact is clamped inside the walls for the same reason the real deflection clamps it: an edge-column balloon can sit within its own radius of a wall.

> **Accuracy caveat.** A circle deflection is very sensitive to where it is struck, so near a balloon's edge a pixel of aim movement swings the outgoing leg hard — the dispersion `ProjectileMotionResolver` already warns about, where error amplifies ×10-20 per deflection. The line is truthful; whether it reads as *precise* or as *jittery* is a playtest question. If it reads badly, draw the post-deflection leg differently (shorter, dimmer, dashed) rather than making it lie.

```mermaid
sequenceDiagram
    participant Ctl as ThrowerController Tick
    participant Calc as PredictionTraceCalculator
    participant Field as SlotGridDeflectorField
    participant Grid as SlotGrid
    participant View as ISlotActorView
    Ctl->>Calc: Calculate origin, direction, shot radius
    Calc->>Field: CollectDeflectors into a reused buffer
    loop every slot, columns times rows
        Field->>Grid: At slot, cast to IDeflectsShots
        Field->>View: ContactCenter, ContactRadius, HasActiveCollider
    end
    Field-->>Calc: list of DeflectorCircle
    loop each segment, one shared reflection budget
        Calc->>Calc: clip the step by the walls first
        Calc->>Calc: nearest circle within the clipped step wins
    end
    Calc-->>Ctl: polyline whose corner sits on the deflector skin
```

The per-frame grid walk is deliberate and must not be cached, because the geometry is read from views
that drift as the board settles. `SlotGridDeflectorField`'s own XML doc explains why — point at it
rather than repeating it here.

### PredictionTraceView

MonoBehaviour with a `LineRenderer`. Call `SetTrace(points)` to update, `SetColor(color)` to set `startColor`/`endColor`, or `Clear()` to hide. Attach to the Thrower prefab alongside a `LineRenderer`. For the smoke + glitter look, use the `BalloonParty/Display/TraceGlitterLine` material shader (SightSmoke's drifting-noise alpha eat plus GlitterSwirl's orbiting specks in one pass) and set the LineRenderer's texture mode to **Tile** so the pattern density stays constant over any aim length — the config-driven trace colour reaches the shader through the renderer's start/end vertex colours.

### PredictionTraceProvider

Plain C# game-scope singleton (registered in `GameScopeRegistration.RegisterCoreServices`) that mirrors the same-frame trace for readers outside the Thrower's own view chain — the house pattern is `ProjectilePositionProvider` (`Projectile/`). `SetTrace(points)` copies into an internal preallocated `List<Vector3>` (never aliases the caller's mutable buffer), bumps an `int Version`, and sets `IsActive`; `Clear()` sets `IsActive` false and bumps `Version`. Readers poll `IsActive`/`Version`/`Points` instead of subscribing, so many pooled readers can cheaply skip work on frames where nothing changed.

### TraceHitMarker

MonoBehaviour view for a circular actor (e.g. a balloon) that shows a marker where the aim trace crosses its circle. Each `LateUpdate`, it reads `PredictionTraceProvider` and finds which segment of the trace polyline most directly strikes the actor's circle via `TraceHitGeometry.TryFindSurfaceHit` (line-circle intersection scored by centrality — the segment whose perpendicular foot is closest to the centre wins, not necessarily the first in travel order; this matters near wall bounces where a pre-bounce segment can graze the circle while the post-bounce segment strikes dead-centre; pure, allocation-free, edit-mode tested in `Assets/Tests/EditMode/Prediction/TraceHitGeometryTests.cs`). The marker shows only when an intersection exists; it's positioned at `origin + hitDirection * _markerOffset` with hitDirection pointing at that surface entry, translated only — never rotated or scaled. Optionally (assign `_markerRenderer`), the sprite's alpha scales with the crossing's **centrality** (1 = line through the centre, 0 = tangential one-touch graze): direct aims read strong, grazes fade toward `_minIntensity`, under the sprite's authored alpha as the ceiling; its RGB mirrors the trace line's configured colour. Work is skipped whenever the provider's `Version` is unchanged and the actor hasn't moved past a small epsilon since the last evaluation. Visibility toggles the marker GameObject only on change, not every frame. `OnEnable` force-hides and invalidates the cache, since pooled instances are reused by toggling the whole prefab's GameObject (`PoolChannel<T>.Get`/`Return`) rather than by any dedicated reset callback.

### Integration

`ThrowerController` owns a `PredictionTraceCalculator`; `ThrowerView` finds the `PredictionTraceView` via `GetComponentInChildren` in `Awake` and exposes `SetTrace`/`SetTraceColor`/`ClearTrace`. Each `Tick`, while the player is aiming and the projectile hasn't been fired, the controller calculates the trace, pushes it through the view, and mirrors it into `PredictionTraceProvider` for any `TraceHitMarker` readers. On fire, release, or reload, both the view and the provider are cleared. The line's color comes from `IPredictionTraceConfig.LineColor`, pushed once in `ThrowerController.Start`. The line deliberately casts NO scene-light — an aim telegraph relighting the actors it crosses read as noise (a light-field version was tried and removed; see branch backup/gi-normals-spherize for the era).

## Unity Setup

1. Add a child GameObject to the Thrower prefab
2. Add `LineRenderer` + `PredictionTraceView` components
3. Configure the `LineRenderer` material and width; color is driven at runtime from `IPredictionTraceConfig.LineColor`
4. For a hit marker on a circular actor prefab (e.g. a balloon): add a small child sprite (e.g. "HitMarker"), add `TraceHitMarker` to the actor, and assign `_marker` (the child sprite's `Transform`), `_circleRadius` (the actor's world-unit circle radius), and `_markerOffset` (distance from the actor origin the marker sits at)

