# Scenario

The shared cloud field and the views that render the play-area frame — the visible boundary the
projectile bounces off.

## CloudFieldService

`CloudFieldService.cs` — owns the shared cloud field: an RT (`_CloudDensityTex`) blitted once per frame
from the `CloudFieldDensity` material, with the disturbance field baked in so clouds part around a wall
hit or a pop. It publishes the RT plus its world bounds as global shader properties, so every consumer
reads the SAME clouds with a single tap rather than each running its own noise. It is the ONE cloud
generator in the project; consumers only sample, never roll their own — currently the `BackgroundCloud`
backdrop, sprite drop-shadows (`SpriteLightDriven`), the GI light smear (`ScreenSpaceLightSmear`), and the
wall net below.

It is a **plain-C# DI singleton** — `IStartable/ITickable/IDisposable`, no `MonoBehaviour` and no scene
GameObject — exactly like its siblings `DisturbanceFieldService` / `SceneLightFieldService`. Its bounds
come from `IGameDisplayConfiguration` via the shared `DisturbanceFieldCoordinates` (origin-centered, so it
aligns with the disturbance/light fields and needs no camera — this is what makes it boot identically from
the Launcher's additive load and from Game directly). Its tuning lives on `CloudFieldSettings`
(`ICloudFieldSettings`: the density blit material + resolution + transition parallax), wired into
`GameLifetimeScope` and registered with `builder.Register<CloudFieldService>(Lifetime.Singleton).AsImplementedInterfaces()`
in `GameScopeRegistration`. The GPU-side tap is `Assets/Shaders/BalloonParty/Include/CloudField.cginc`;
generation is `CloudFieldGen.cginc` + the `CloudFieldDensity` blit shader. The analogue of
`DisturbanceFieldService` / `SceneLightFieldService` for clouds — see `Shared/Disturbance/README.md`.

## WallNetView

`View/WallNetView.cs` — a `MonoBehaviour` that builds the four **net-strip** meshes framing the play
area, one per edge of the logical play rectangle. It reads that rectangle from the injected
`IGameConfiguration.LimitsClockwise` (unpacked via `WallLimits`), so the visible frame is sourced from
the same rectangle the projectile billiard reflects off — no hand-syncing a separate visual frame.

Each strip is a flat quad band laid **outward** from its wall by `_stripWidth` (away from the play area,
so its reveal never covers the balloons), tessellated into a quad grid (`_cellsPerUnitAlong` ×
`_cellsAcross`) fine enough to deform smoothly. The meshes are built once in `Start` and never rewritten
from C#: all motion lives in the shared net material's vertex stage, so there is zero per-frame CPU. Per
vertex the view bakes:

- **normal** = the edge's outward direction, so the shader can un-extrude each strip to a thin line at
  rest and billow it back out along that same direction.
- **UV0** = `(across 0→1, along 0→1)` — normalised, for the band-edge feather and the unfurl/billow maths.
- **UV1** = the net-pattern tiling (cells kept square: the across cell size sets the along tiling).

The strips render through generated child `MeshRenderer`s sharing `_netMaterial`, on the configured
sorting layer. Registered with `RegisterComponentInHierarchy<WallNetView>()` in `GameScopeRegistration`
so its `IGameConfiguration` injection resolves; the GameObject lives as a static child of the Scenario
prefab (identity transform, so mesh world coordinates map straight through).

The net's motion and look now read two shared fields, not just one:

- **Disturbance** — the material samples the shared `_DisturbanceTex` in the vertex stage (the `BushLeaf`
  precedent): the projectile's existing wall-contact stamp unfurls the strip from a thin resting line to
  its full depth and billows it along the impact direction, and the same disturbance amount shifts the
  net's colour toward a "struck" gradient and pushes its visibility toward `_DisturbVisibilityTarget` —
  no wall-hit event, no CPU sim.
- **Cloud field** — the net's baseline visibility and idle breathing both read `CloudFieldService`'s
  shared field via `CloudField.cginc`: it fades in only where there's cloud (so it reads as part of the
  clouds rather than a separate frame) and its idle undulation is phase-warped by the cloud noise instead
  of a rigid marching sine.

`WallNetView` also drives a `_TransitionFade` on the material, independent of the fields above: the net
starts hidden, fades in on the first shot (`ProjectileFiredMessage`), and fades back out on a Game
transition (`ScoreLevelUpMessage`, `RunResetMessage`) until the next run's first shot.

See `Assets/Source/Plans/PLAN-WallBorders.md` for the full design and phasing, and
`Assets/Source/Shared/Disturbance/README.md` for the disturbance field.

## Shader

`Assets/Shaders/BalloonParty/Scenario/WallNet.shader` — the shared world-space net material: a
procedural tennis-net pattern (grid lines from UV1, feathered band edges, HDR-emissive tint so bloom
carries the glow) driven by the disturbance-field unfurl/billow/colour-gradient, the cloud-field
visibility/breathing, and the scene-light tint described above. Shader/visual changes are validated
in-editor, not by `dotnet build`.
