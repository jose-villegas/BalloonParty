@page arch_bush_system Bush System

# Bush System

@dot
digraph BushSystem {
    rankdir=TB;
    compound=true;
    node [shape=box, fontname="Helvetica", fontsize=10, style=filled, fillcolor=white];
    edge [fontname="Helvetica", fontsize=9];

    subgraph cluster_editor {
        label="Editor — Bake Time";
        style=filled;
        fillcolor="#f5f5dc";

        Generator   [label="BushBranchGenerator\n(fractal segments\nin UV space)"];
        BranchBaker [label="BushBranchBaker\n(offscreen camera\n→ branch map PNG)"];
        Extractor   [label="BushLeafExtractor\n(terminal segments\n→ LeafSlot[])"];
        LeafBaker   [label="BushLeafBaker\n(Gielis SDF\n→ leaf atlas PNG)"];
        Exporter    [label="BushVariantExporter\n(PNG + SO output)"];
        Window      [label="BushBakerWindow\n(Tools > Bush Baker)"];

        Generator   -> BranchBaker [label="Segment[]"];
        Generator   -> Extractor   [label="Segment[]"];
        BranchBaker -> Exporter    [label="Texture2D"];
        Extractor   -> Exporter    [label="LeafSlot[]"];
        LeafBaker   -> Exporter    [label="Leaf atlas", style=dashed];
        Window      -> BranchBaker [label="preview"];
        Window      -> LeafBaker   [label="preview"];
        Window      -> Exporter    [label="export"];
    }

    subgraph cluster_assets {
        label="Serialized Assets";
        style=filled;
        fillcolor="#e8f5dc";

        VariantSO [label="BushVariantData (SO)\nbranch map + LeafSlotData[]\n+ debug segments"];
        Settings  [label="BushSettings (SO)\nIBushSettings\nwind, rattle, shadow,\nsprite scale, pivot"];
        LeafMat   [label="Leaf Material\n(BushLeaf.shader)"];
    }

    Exporter -> VariantSO [label="creates"];

    subgraph cluster_runtime {
        label="Runtime — Per Frame";
        style=filled;
        fillcolor="#dce8f5";

        subgraph cluster_mvc {
            label="MVC";
            style=filled;
            fillcolor="#e0e8f0";

            Registry [label="BushClusterRegistry\n(flood-fill adjacency)"];
            CtrlView [label="BushViewController\n(gap-fill, wires settings)"];
            View     [label="BushView : ClusterView\n(static matrices,\nDrawMesh + DrawMeshInstanced)"];
        }

        subgraph cluster_gpu {
            label="GPU — Vertex Shader";
            style=filled;
            fillcolor="#ffe8cc";

            WindAnim    [label="Wind Animation\nsin + dual-sine noise\n× depth × phase"];
            RattleAnim  [label="Rattle Animation\ntex2Dlod _DisturbanceTex\n× depth × damping"];
            PivotRot    [label="Pivot-based Rotation\nscale + rotate\naround attachment"];
            ShadowHL    [label="Shadow + Highlight\ninverse-rotated\nfrom GPU angle"];
        }

        WindAnim   -> PivotRot [label="windDeg"];
        RattleAnim -> PivotRot [label="rattleDeg"];
        PivotRot   -> ShadowHL [label="cosA, sinA"];
    }

    subgraph cluster_disturbance {
        label="Shared — Disturbance Field";
        style=filled;
        fillcolor="#f5dce8";

        FieldService [label="DisturbanceFieldService\n(global _DisturbanceTex)"];
        Stampers     [label="Projectile, Balloon,\nBomb, Laser, Paint\n(stamp callers)"];

        Stampers -> FieldService [label="Stamp()"];
    }

    Settings  -> View      [label="inject"];
    VariantSO -> View      [label="variant data"];
    LeafMat   -> View      [label="material template"];
    Registry  -> CtrlView  [label="cluster events"];
    CtrlView  -> View      [label="Configure()"];

    View -> WindAnim       [label="_LeafWind\n(static MPB)", lhead=cluster_gpu];
    Settings -> WindAnim   [label="uniforms\n(freq, amp, noise)", style=dashed];
    Settings -> RattleAnim [label="uniforms\n(amp, freq, damp)", style=dashed];

    FieldService -> RattleAnim [label="_DisturbanceTex\n_FieldBoundsMin\n_FieldBoundsSize\n(global shader props)"];
}
@enddot

## What this diagram shows

The bush system's full data flow from editor bake time through runtime
rendering and GPU animation.

**Editor (bake time):**
- Fractal generator produces branch segments in UV space
- Branch baker renders segments into a branch map texture
- Leaf extractor finds terminal segments and produces attachment points
- Leaf baker renders Gielis SDF leaves into an atlas
- Exporter bundles everything into a `BushVariantData` ScriptableObject

**Runtime (per frame):**
- `BushView` sets up **static** translation-only matrices once per rebuild
- `DrawMesh` renders branch quads; `DrawMeshInstanced` renders leaf quads
- **Zero CPU animation** — all leaf motion runs on the GPU vertex shader

**GPU vertex shader (per leaf, per vertex):**
- **Wind:** sine oscillation + dual-sine noise, modulated by depth and phase
- **Rattle:** samples `_DisturbanceTex` at the leaf's world position via
  `tex2Dlod`; displacement is crossed with leaf direction for signed rotation,
  shaped by a power-curve damping parameter
- **Pivot rotation:** vertices shifted to pivot point, rotated by combined
  wind + rattle angle, scaled by animated leaf scale
- **Shadow/highlight:** offset vectors inverse-rotated using the GPU-computed
  angle, so direction stays consistent in world space

**Disturbance field interaction:**
- Any system that calls `DisturbanceFieldService.Stamp()` (projectile,
  balloon pop, bomb, laser, paint) automatically affects bush leaves
- No C# wiring between stampers and bushes — the leaf shader reads the
  same global `_DisturbanceTex` that Puff clouds use
- The field's diffusion and reform mechanics provide the rattle settle curve

## Key design decisions

1. **Static matrices** — leaf matrices are set once at `RebuildSlots` and
   never mutated. No `ITickable`, no per-frame matrix updates.
2. **GPU-only animation** — wind and rattle are pure shader features.
   `BushAnimator` was deleted — zero CPU animation cost.
3. **Shared disturbance field** — rattle reuses the same RT and global
   shader properties as Puff clouds. Adding new stampers (future effects)
   automatically affects both clouds and bushes.
4. **`multi_compile` for rattle** — `_RATTLE_ON` uses `multi_compile` (not
   `shader_feature`) so the variant survives build stripping when enabled
   at runtime via C# `EnableKeyword`.

