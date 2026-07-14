# Balloon/Type

Balloon types define hit capacity, color selection, and per-type Inspector configuration.

## Contents

| File | What it does |
|---|---|
| `BalloonType` | Enum — `Simple`, `Tough`, `Unbreakable`, `BubbleCluster` |
| `IBalloonVariant` | Interface on balloon prefab root MonoBehaviours — `Initialize(IWriteableBalloonModel)`. Type name, hit count, score, and nudge overrides are owned by `BalloonPrefabEntry` and baked into the model by `BalloonModelFactory` before `Initialize()` is called |
| `IBalloonViewBinding` (in `Balloon/View/`) | Interface for MonoBehaviours on a balloon prefab that need to react to model binding. `BalloonView` discovers all components implementing this interface via `GetComponents` and calls `Bind(IBalloonModel, CompositeDisposable)` automatically — keeping `BalloonView` agnostic of specific balloon types |
| `ColorableBalloonVariant` | Abstract `MonoBehaviour` — picks a random color from `GamePalette` filtered by `_allowedColorsMask`; sets `Color` on the model (via `IPaintable`) in `Initialize()` |
| `SimpleBalloonVariant` | Extends `ColorableBalloonVariant` — one-hit colored balloon; no additional behavior |
| `ToughBalloonVariant` | `MonoBehaviour` implementing both `IBalloonVariant` and `IBalloonViewBinding` — not colorable; `Initialize()` is a no-op. On `Bind()` subscribes to `HitsRemaining` (via `IHasDurability`) and animates `_DamageProgress` on the `BalloonParty/Balloon/ToughBalloon` shader via `MaterialPropertyBlock` using a DOTween tween |
| `SoapBubbleClusterVariant` | `[ExecuteAlways]` `MonoBehaviour` implementing both `IBalloonVariant` and `IBalloonViewBinding` — not colorable. On `Bind()` randomises spawn rotation angle and continuous rotation speed (5–12 °/s) via `transform.localRotation`, subscribes to `HitsRemaining` and pushes `_BubbleCount` (clamped to `_maxBubbles`) to the `BalloonParty/Balloon/SoapBubbleCluster` shader. Drives `_TimeOffset` every frame via `Update()` using `EditorApplication.timeSinceStartup` in edit mode and `Time.time` in play mode; calls `SceneView.RepaintAll()` in edit mode to sustain animation. Inspector field `_previewBubbleCount` allows previewing each cluster state without Play mode |
| `UnbreakableBalloonVariant` | `[ExecuteAlways]` `MonoBehaviour` implementing both `IBalloonVariant` and `IBalloonViewBinding` — not colorable. Pushes `_SphereCenter` (world position of this transform), `_SphereRadius`, and `_TimeOffset` to all quadrant `SpriteRenderer`s via `MaterialPropertyBlock` every `Update()`. The sphere is composed of 4 quarter-circle sprites rotated by −90°×n; this variant ensures all shader effects (metallic gradient, specular highlight, convex-mirror reflection, chrome rim) are coherent across the assembled sphere. `_SphereRadius` is auto-computed from the union of renderer bounds if left at zero. Ref-counts `SceneCaptureService` (`Display/`) via `Acquire()`/`Release()` on enable/disable so the scene capture only renders while an unbreakable balloon is on screen |

## How it works

During spawning, `BalloonModelFactory` builds the model from `BalloonPrefabEntry` — a `BalloonModelConfig` struct carries `TypeName`, `ScoreValue`, `HitsToPop`, and `NudgeOverrides` into the model constructor — before `BalloonFactory` calls `IBalloonVariant.Initialize(model)`. `Initialize()` is then responsible only for type-specific data: `Color` for colored types (it is a no-op on the others). This keeps per-type balance values centralized in the configuration asset rather than scattered across MonoBehaviours. Item eligibility is not a flag — it is determined structurally: `BalloonModel` implements `IHasWriteableItemSlot`; `ToughBalloonModel`, `UnbreakableBalloonModel`, and `BubbleClusterModel` do not. A `BubbleClusterModel` gets its bubble count from the config entry's `HitsToPop` (the variant's `_maxBubbles` should match it) — each surviving hit maps to one fewer visible bubble rather than a damage-progress float.

Then `BalloonView.Bind(model)` calls `Bind()` on all `IBalloonViewBinding` components found on the same GameObject. `ToughBalloonVariant` uses this to subscribe to `HitsRemaining` and drive shader damage visuals.

The `_allowedColorsMask` on `ColorableBalloonVariant` is a bitmask over `GamePalette.Colors` shown in the Inspector as per-color checkboxes via `PaletteColorMaskAttribute`. `PickColor()` builds a list of allowed color names from the bitmask and picks uniformly at random. The picked color name is written to the model via an `IPaintable` cast (`model is IPaintable colorable`).

Each hit reaches the owning `BalloonController.HandleHit` directly — `HitPipeline` routes it via `BalloonControllerRegistry` (balloons do not subscribe to the `ActorHitMessage` bus). The controller switches on `msg.Outcome`:

- **`Pop`** — balloon destroyed: plays VFX, removes from grid, returns to pool
- **`Deflect`** — balloon survived and deflected the projectile; publishes `BalloonDeflectedMessage` and `NudgeMessage(Deflect)`
- **`PassThrough`** — balloon survived; projectile continues (Bubble Cluster pass-through). Plays the pass-through VFX.
- **`Absorb`** — projectile destroyed by actor (handled upstream in `ProjectileHitResolver`)

## Tough balloon shader

`ToughBalloonVariant` drives the `BalloonParty/Balloon/ToughBalloon` shader via `MaterialPropertyBlock` (no material instance allocation — safe for pooled objects):

- **`_DamageProgress`** (`0` pristine → `1` critical) — animated via `DOVirtual.Float` over `_crackAnimDuration` seconds using `Ease.OutCubic`. When a second hit arrives before the tween completes, the new tween starts from the current animated value so transitions chain smoothly.
- **`_VoronoiSeed`** — set to a random `Vector2` at bind time so each balloon instance has a unique crack pattern.

> **GPU instancing is disabled** on `ToughBalloonMaterial` because `_DamageProgress` and `_VoronoiSeed` are set per-instance via `MaterialPropertyBlock`. Instancing batching discards MPB values not declared in the shader's instancing buffer. See `Assets/Shaders/BalloonParty/README.md` for the full instancing policy.

The shader applies a `_DamageCurve` power exponent to `_DamageProgress` before driving all visual effects, controlling how damage distributes perceptually across the hit range (values `> 1` concentrate the visual impact on later hits, recommended for 3-hit balloons). Effects driven by this curve:

- **Ash tint** — base colour transitions from deep black to the configured ash tint
- **Rim** — subsurface edge fringe thins and dims with damage
- **Voronoi cracks** — spherically projected crack lines grow from invisible hairlines to full splits; both cell density (`_VoronoiScale + _VoronoiScaleDamageBoost`) and edge warp (`_SphereWarp + _SphereWarpDamageBoost`) accelerate quadratically with `dmgVis²` for a dramatic late-stage surface collapse

The shader properties are organised into labelled Inspector groups: **Damage**, **Surface**, **Rim**, **Cracks / Base**, **Cracks / Sphere Projection**, and **Cracks / Instance** (seed hidden at runtime).

## Soap Cluster shader

`SoapBubbleClusterVariant` drives the `BalloonParty/Balloon/SoapBubbleCluster` shader via `MaterialPropertyBlock`. The shader is fully procedural — no sprite or texture required.

- **`_BubbleCount`** (1–5) — set on each `HitsRemaining` change. Controls which per-count layout is active: 5 = regular pentagon, 4 = square, 3 = equilateral triangle, 2 = horizontal pair, 1 = single centred bubble. The cluster shape changes discretely on each hit.
- **`_TimeOffset`** — pushed every `Update()` frame as `Time.time * floatSpeed + instancePhase`. The shader uses this as its sole time input (does not use `_Time.y`, which is frozen in edit mode). Drives the cluster breathe oscillation, per-bubble micro-float, and iridescence hue drift.

Rotation is applied at the transform level (`transform.localRotation` on the renderer) rather than in the shader. `Bind()` sets a random initial angle; `Update()` accumulates rotation at a random per-instance speed (5–12 °/s). The specular highlight and shadow are computed in world space by the shader, so they stay fixed regardless of how much the cluster has spun.

Cluster animation layers (inside the shader, no C# involvement):
- **Breathe** — slow sinusoidal scale of all bubble positions from the cluster centre; primary driver of junction seam interchange as inter-bubble distances oscillate
- **Per-bubble micro-float** — independent sine term per bubble on a unique phase; makes the cluster feel loosely coupled rather than rigid
- **Iridescent rim** — hue derived from the radial angle around each bubble's rim + slow `_IridescenceSpeed` drift → rainbow soap-film appearance
- **Plateau junction** — Voronoi boundary line running through the cluster interior (the true wall between adjacent bubbles) + overlap zone fill for density depth
- **Specular** — fixed-direction highlight computed in unrotated UV; does not spin with the cluster
- **Shadow** (`_SHADOW_ON`) — projects rim + seams only (no interior fill); direction fixed in unrotated UV space; `_ShadowFilmWidth` / `_ShadowSeamWidth` independently control rim/seam shadow thickness

> **GPU instancing is disabled** on the Soap Cluster material — `_BubbleCount` and `_TimeOffset` are set per-instance via `MaterialPropertyBlock`.

## Unbreakable balloon shader

The `BalloonParty/Balloon/UnbreakableBalloon` shader renders the chrome metallic sphere. The balloon is composed of 4 quarter-circle sprites rotated by −90°×n; all four share `_SphereCenter` (world position via MPB) so every effect is coherent across the assembled sphere.

- **Metallic radial gradient** — replaces the sprite RGB with a `_MetalCenterColor` → `_MetalEdgeColor` gradient controlled by `pow(sphereDist, _MetalFalloff)`. The sprite's luminance is preserved as a detail mask (via `smoothstep` with `_MetalDetailStrength` threshold) so the bolt pattern stays visible while the gradient controls the full brightness range — bright silver at centre, near-black at the rim.
- **Specular highlight** — position and streak angle both derive from the global `_SceneLightDir`. The hotspot is placed at `_SceneLightDir * _SpecularDistance` (sphere-local). The anisotropic streak runs perpendicular to the light direction; `_SpecularBend` (negative values) bows it away from the light. Per-material knobs: `_SpecularDistance`, `_SpecularSize`, `_SpecularStretch`, `_SpecularSharpness`, `_SpecularIntensity`, color. Creates a brushed-metal elongated highlight for 3D depth instead of a circular dot.
- **Convex-mirror reflection** — samples `_SceneCaptureTex`, a global scene texture rendered by `SceneCaptureService` (`Display/`) from a secondary camera at the main camera's framing (no GrabPass). Each pixel derives its sphere surface normal from `spherePos`; the surface tangent component offsets the capture UV by `spherePos * (1 − nz) * _ReflectionSpread`, compressing the surroundings into the sphere like a real convex mirror. Fresnel-weighted blend makes edges more reflective.
- **Chrome rim** — two additive layers on the alpha-edge band (detected via 8-tap `EdgeMask`). The **static rim** (`_RimColor`, `_RimIntensity`) is always visible and outlines the sphere edge; `_RimWidth = 0` disables both layers. The **rim sweep** (`_RimSweepColor`, `_RimSweepIntensity`) is an angular gradient (like the diagonal shine) that rotates around the sphere at `_RimSweepSpeed`, rendered on top of the static rim with its own colour.
- **Diagonal shine** — periodic bright band sweeping across the surface, same as other balloon shaders.
- **Deflect flash** — `_DeflectFlash` (0–1) additive white overlay for hit feedback.

> **GPU instancing is disabled** — `_SphereCenter` and `_TimeOffset` are set per-instance via `MaterialPropertyBlock`.

## Interactions

- **BalloonFactory / BalloonModelFactory** — build the model from `BalloonPrefabEntry` before calling `Initialize()`; model class is chosen from `BalloonType`: `Simple` → `BalloonModel`, `BubbleCluster` → `BubbleClusterModel`, `Tough` → `ToughBalloonModel`, `Unbreakable` → `UnbreakableBalloonModel`
- **BalloonView** — auto-discovers and calls `IBalloonViewBinding.Bind()` on all components on the same GameObject
- **BalloonController** — receives each hit's pre-computed outcome via `HitPipeline` → `BalloonControllerRegistry` routing
- **GamePalette** — injected into `ColorableBalloonVariant` to resolve allowed color names
- **PaletteColorMaskAttribute** — drives the Inspector bitmask drawer for color filtering
- **`BalloonParty/Balloon/ToughBalloon` shader** — receives `_DamageProgress` and `_VoronoiSeed` per-instance via `MaterialPropertyBlock`
- **`BalloonParty/Balloon/SoapBubbleCluster` shader** — receives `_BubbleCount` and `_TimeOffset` per-instance via `MaterialPropertyBlock`
- **`BalloonParty/Balloon/UnbreakableBalloon` shader** — receives `_SphereCenter` and `_TimeOffset` per-instance via `MaterialPropertyBlock`; specular highlight, metallic gradient, and convex-mirror reflection are all sphere-coherent across the 4 quadrant sprites
- **SceneCaptureService** (`Display/`) — renders the `_SceneCaptureTex` the reflection samples; the variant `Acquire()`s it while enabled so the capture camera only runs when needed
