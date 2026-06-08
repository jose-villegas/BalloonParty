@page editor_bush Bush Baking Tools

# Bush Baking Tools

Editor-only tooling for baking procedural bush branch maps and leaf sprite
atlases. Branch maps are generated from fractal segment math and rendered
via an offscreen camera. Leaf attachment points are extracted from terminal
branch segments. Output is serialized as `BushVariantData` ScriptableObjects
consumed at runtime by `BushView`.

## Contents

| File | Purpose |
|---|---|
| `BushBranchGenerator` | Recursive fractal generator — produces `Segment[]` in UV space (0–1). Root at centre (0.5, 0.5), branches radiate outward 360°. Deterministic via `System.Random(seed)`. |
| `BushBranchBaker` | Offscreen camera pipeline — builds a procedural mesh from segments, renders via `BushBakeBranch.shader`, reads back to `Texture2D`. |
| `BushBranchBakeSettings` | Serializable settings for branch generation — depth, angle spread, length, width, taper, colour, leaf extraction thresholds. |
| `BushLeafExtractor` | Finds terminal branch segments (whose End is not another segment's Start) and produces `LeafSlot[]` with UV positions along the segment centerline. Configurable attachment bias. |
| `BushVariantExporter` | Full export pipeline — bake branch map + extract leaves + save PNG + create `BushVariantData` SO. Also stores debug segment data for gizmo overlay. |
| `BushLeafBaker` | Renders a single Gielis leaf into a `Texture2D` using `BushBakeLeaf.shader` via a temporary offscreen camera. Applies per-variant parameter jitter. |
| `BushLeafBakeSettings` | Serializable settings for leaf baking — Gielis params, surface shading, midrib, laterals, venules, reticulate, petiole. |
| `LeafAtlasPacker` | Bakes N leaf variants, packs them into a square grid atlas, saves as PNG, and configures sprite slicing. |
| `BushBakerWindow` | `EditorWindow` via **Tools > Bush Baker**. Two tabs: Branch Map (fractal preview + export) and Leaf Atlas (Gielis preview + export). Auto-preview on parameter change. |
| `BushBakerState` | Persisted editor state — foldouts, settings, output path, preview seed. |
| `LeafVenationSimulator` | Runions auxin-based vein simulation (legacy — superseded by shader-inline veins in `BushBakeLeaf.shader`). |

## Shaders (in `Assets/Shaders/BalloonParty/Grid/Editor/`)

| Shader | Purpose |
|---|---|
| `GielisSDF.cginc` | Shared include — Gielis superformula SDF, hue rotation, parameter jitter. |
| `BushBakeLeaf.shader` | Leaf baker — Gielis SDF shape, dome shading, palmate midribs, lateral veins, venules, reticulate fill, petiole. |
| `BushBakeBranch.shader` | Branch baker — vertex colour pass-through (RG=direction, A=depth) + edge AA. |

## Pipeline

### Branch Map + Variant Export

1. `BushBranchGenerator.Generate(seed, settings)` produces fractal segments in UV space.
2. `BushBranchBaker.Bake()` builds a procedural mesh from segments, renders to RT, reads back to `Texture2D`.
3. `BushLeafExtractor.Extract()` re-generates segments from the same seed, finds terminal tips, and produces `LeafSlot[]` with UV attachment positions.
4. `BushVariantExporter.Export()` saves the branch map PNG, creates `BushVariantData` SO with leaf slot data and debug segments.

### Leaf Atlas

1. `BushLeafBaker.BakeLeaf()` renders each variant via offscreen camera + Gielis SDF material.
2. `LeafAtlasPacker.Pack()` composites variants into a grid atlas PNG and configures sprite slicing.

## Dependencies

- `BalloonParty.Editor` assembly (editor-only platform).
- References `BalloonParty.Runtime` for configuration types.

