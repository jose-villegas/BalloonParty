@page editor_bush Bush Baking Tools

# Bush Baking Tools

Editor-only tooling for baking procedural bush canopies and individual leaf
sprites into static textures. The baked output replaces the runtime SDF
shader (`Bush.shader`) with standard sprite rendering.

## Contents

| File | Purpose |
|---|---|
| `LeafVenationSimulator` | Runions auxin-based CPU simulation that generates biologically plausible vein networks. Outputs `VeinSegment` lists with hierarchical depth (0 = midrib → 3 = tertiary). Includes a rasteriser that converts segments into anti-aliased `Texture2D` assets for the leaf baking shader. |
| `BushLeafBakeSettings` | Serializable settings for leaf baking — Gielis params, SSS, hue jitter, venation, resolution. |
| `BushCanopyBakeSettings` | Serializable settings for canopy baking — resolution, slot count, variant count. |
| `BushLeafBaker` | Renders a single Gielis leaf into a `Texture2D` using `BushBakeLeaf.shader` via a temporary offscreen camera. Applies per-variant parameter jitter and venation. |
| `LeafAtlasPacker` | Bakes N leaf variants, packs them into a square grid atlas, saves as PNG, and configures sprite slicing via `ISpriteEditorDataProvider`. |
| `BushBakerWindow` | `EditorWindow` accessible via **Tools > Bush Baker**. Provides sliders for all Gielis and shading parameters, preview grid, and export buttons for leaf atlas and canopy variants. |

## Shaders (in `Assets/Shaders/BalloonParty/Grid/`)

| Shader | Purpose |
|---|---|
| `GielisSDF.cginc` | Shared include — Gielis superformula SDF, hue rotation, Poisson shadow jitter. |
| `BushBake.shader` | Full canopy baker — all slots, all 16 depth layers, SSS, full-depth shadow, AO, colour variation. |
| `BushBakeLeaf.shader` | Single leaf baker — one Gielis leaf with full shading pipeline for atlas packing. |

## Pipeline

1. `LeafVenationSimulator.Simulate()` generates vein segments for a leaf shape.
2. `LeafVenationSimulator.RasteriseVeins()` produces a vein `Texture2D`.
3. `BushLeafBaker.BakeLeaf()` sets up a temporary camera + quad with the
   `BushBakeLeaf.shader` material, assigns Gielis params + vein texture,
   renders into a `RenderTexture`, and reads back to `Texture2D`.
4. `LeafAtlasPacker.Pack()` bakes N variants, composites them into a grid
   atlas, saves the PNG, and configures sprite slicing.
5. `BushBakerWindow` provides the UI for all of the above — preview and export.

## Dependencies

- `BalloonParty.Editor` assembly (editor-only platform).
- References `BalloonParty.Runtime` for `MathUtils` constants.

