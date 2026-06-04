@page editor_bush Bush Baking Tools

# Bush Baking Tools

Editor-only tooling for baking procedural bush canopies and individual leaf
sprites into static textures. The baked output replaces the runtime SDF
shader (`Bush.shader`) with standard sprite rendering.

## Contents

| File | Purpose |
|---|---|
| `LeafVenationSimulator` | Runions auxin-based CPU simulation that generates biologically plausible vein networks. Outputs `VeinSegment` lists with hierarchical depth (0 = midrib → 3 = tertiary). Includes a rasteriser that converts segments into anti-aliased `Texture2D` assets for the leaf baking shader. |

## Shaders (in `Assets/Shaders/BalloonParty/Grid/`)

| Shader | Purpose |
|---|---|
| `GielisSDF.cginc` | Shared include — Gielis superformula SDF, hue rotation, Poisson shadow jitter. |
| `BushBake.shader` | Full canopy baker — all slots, all 16 depth layers, SSS, full-depth shadow, AO, colour variation. |
| `BushBakeLeaf.shader` | Single leaf baker — one Gielis leaf with full shading pipeline for atlas packing. |

## Pipeline

1. `LeafVenationSimulator.Simulate()` generates vein segments for a leaf shape.
2. `LeafVenationSimulator.RasteriseVeins()` produces a vein `Texture2D`.
3. The vein texture is assigned to `BushBakeLeaf.shader` via `_VeinTex`.
4. A temporary camera renders the shader into a `RenderTexture`, which is read
   back to a `Texture2D` asset.

Leaf textures are packed into a sprite atlas for runtime use. The canopy baker
(`BushBake.shader`) renders the full multi-slot bush in a single pass.

## Dependencies

- `BalloonParty.Editor` assembly (editor-only platform).
- References `BalloonParty.Runtime` for `MathUtils` constants.

