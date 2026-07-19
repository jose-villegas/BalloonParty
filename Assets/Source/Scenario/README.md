# Scenario

Views that render the play-area frame — the visible boundary the projectile bounces off.

## WallNetView

`View/WallNetView.cs` — a `MonoBehaviour` that builds the four **net-strip** meshes framing the play
area, one per edge of the logical play rectangle. It reads that rectangle from the injected
`IGameConfiguration.LimitsClockwise` (unpacked via `WallLimits`), so the visible frame is sourced from
the same rectangle the projectile billiard reflects off — no hand-syncing a separate visual frame.

Each strip is a flat quad band laid **inward** from its wall by `_stripWidth`, tessellated into a quad
grid (`_cellsPerUnitAlong` × `_cellsAcross`) fine enough to deform smoothly. The meshes are built once
in `Start` and never rewritten from C#: all motion lives in the shared net material's vertex stage, so
there is zero per-frame CPU. Per vertex the view bakes:

- **normal** = the edge's inward direction, so the shader can billow each strip along its own normal
  without any per-strip material property.
- **UV0** = `(across 0→1, along 0→1)` — normalised, for the band-edge feather and future deform maths.
- **UV1** = the net-pattern tiling (cells kept square: the across cell size sets the along tiling).

The strips render through generated child `MeshRenderer`s sharing `_netMaterial`, on the configured
sorting layer. Registered with `RegisterComponentInHierarchy<WallNetView>()` in `GameScopeRegistration`
so its `IGameConfiguration` injection resolves; the GameObject lives as a static child of the Scenario
prefab (identity transform, so mesh world coordinates map straight through).

The motion itself is **field-driven**: the net material samples the shared `_DisturbanceTex` in the
vertex stage (the `BushLeaf` precedent), so the projectile's existing wall-contact stamp billows the net
for free — no wall-hit event, no CPU sim. See `Assets/Source/Plans/PLAN-WallBorders.md` for the full
design and phasing, and `Assets/Source/Shared/Disturbance/README.md` for the field.

## Shader

`Assets/Shaders/BalloonParty/Scenario/WallNet.shader` — the shared world-space net material. Phase 1 is
a static procedural tennis-net pattern (grid lines from UV1, feathered band edges, HDR-emissive tint so
bloom carries the glow). Later phases add the disturbance-field vertex tap, idle breathing, and the
scene-light tint. Shader/visual changes are validated in-editor, not by `dotnet build`.
