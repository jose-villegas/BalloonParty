@page plan_bush_sprite_baking Bush — 2D Skeletal Plant System

# Bush — 2D Skeletal Plant System

> Procedural 2D plants with baked branch map textures and leaf sprites.
> Branches are a static textured quad; leaves rotate around their
> attachment points for wind sway and rattle. Rendered via
> `DrawMeshInstanced` — zero GameObjects, two draw calls per cluster.

---

## Status & Phase Tracker

| Phase | Description | Status |
|---|---|---|
| **0** | Cluster infrastructure (shared with Puff) | ✅ Done |
| **1** | Leaf baking — Gielis SDF + vein system | ✅ Done |
| **2** | Branch map baking + leaf extraction + rendering | 🔨 **Current** |
| **3** | Wind animation (idle) | ⬜ Next |
| **4** | Rattle (disturbed state) | ⬜ Planned |
| **5** | Visual polish (shadows, sorting, bark texture) | ⬜ Planned |

---

## Core Idea

A bush is a **2D branching shape** seen from top-down. Fractal branching
math renders into a **branch map texture** at bake time in the editor.
The texture serves dual purpose:

1. **Branch visual** — the texture *is* the branch rendering at runtime
   (one static textured quad per bush, no per-branch geometry, no animation)
2. **Leaf placement source** — alpha channel encodes depth (0 = no
   attachment, >0 = valid leaf point); RG encodes branch direction.
   The baker extracts `LeafSlot[]` from the texture and serializes
   them — zero runtime generation cost.

```
         Root (ground anchor)
           │
       ┌───┼───┐
       │   │   │         ← baked into branch map texture (static quad)
      ┌┤  ┌┤  ┌┤
      ││  ││  ││
     🍃🍃 🍃🍃 🍃🍃       ← instanced leaf quads at extracted tips
```

**Only leaves animate.** Each leaf rotates around its attachment point
(pivot) for wind idle sway and rattle. Branches stay still — from
top-down, branch movement is barely perceptible and not worth the
complexity. This eliminates all CPU/GPU animation sync concerns.

---

## Architecture

```
Bush Baker (editor-only, bake time)
    │  Fractal branching math → renders branch map texture
    │  RG = branch direction, B = reserved, A = depth (0 = empty)
    │  Extracts LeafSlot[] from texture (alpha > threshold at tips)
    │  Serializes: branch map texture + LeafSlot[] → asset
    │
    ▼
BushView : ClusterView (runtime)
    │  Loads pre-baked branch texture + pre-extracted LeafSlot[]
    │  Branch quad: static SpriteRenderer with branch map (no animation)
    │  Leaf quads: DrawMeshInstanced from LeafSlot[] matrices
    │  Two draw calls per cluster. Zero runtime generation.
    │
    ▼
BushAnimator (ITickable, runtime)
    │  Flat loop over LeafSlot[] only:
    │  Wind: rotation around pivot = sin(t + phase) × depth
    │  Rattle: distance-from-impact × depth → damped spring rotation
    │  Output: Matrix4x4[] for leaves (rotation around attachment point)
    │
    ▼
No per-leaf or per-branch GameObjects. No Transform hierarchy.
No runtime generation. Branches are static. Leaves are flat CPU math.
```

---

## Phase 0 — Cluster Infrastructure ✅ Done

Shared cluster system extracted from Puff. All files exist and work.

| File | Location | Role |
|---|---|---|
| `ClusterView.cs` | `Slots/Actor/Cluster/` | Abstract MonoBehaviour, MPB, slot positions |
| `ClusterViewController.cs` | `Slots/Actor/Cluster/` | Generic controller, subscribes to registry |
| `SlotClusterRegistry.cs` | `Slots/Actor/Cluster/` | Flood-fill clustering |
| `BushView.cs` | `Slots/Actor/Archetype/` | Concrete ClusterView subclass |
| `BushViewController.cs` | `Slots/Actor/Archetype/` | Gap-fill midpoints, wires settings |
| `BushClusterRegistry.cs` | `Slots/Actor/Archetype/` | Bush-specific registry |
| `IBushSettings.cs` | `Configuration/` | Settings interface |

---

## Phase 1 — Leaf Baking ✅ Done

Procedural Gielis leaf sprites baked offline via an editor window. The shader
renders shape, surface shading, a gradient-driven midrib (adapting to lobe
count), lateral veins, venules, and reticulate fill into a RenderTexture
that is read back to Texture2D for atlas packing.

### What exists now

| File | Location | Role |
|---|---|---|
| `BushBakeLeaf.shader` | `Shaders/.../Editor/` | Gielis SDF, dome shading, hue jitter, AA alpha, palmate midribs, lateral veins, venules, reticulate, petiole |
| `BushLeafBaker.cs` | `Source/Editor/Bush/` | Offscreen camera bake pipeline, gradient texture baking, camera sizing for petiole |
| `BushLeafBakeSettings.cs` | `Source/Editor/Bush/` | All bake parameters (shape, surface, midrib, laterals, venules, reticulate, petiole) |
| `BushBakerWindow.cs` | `Source/Editor/Bush/` | Editor window: properties panel + live preview box, export |
| `BushBakerState.cs` | `Source/Editor/Bush/` | Persisted editor state (foldouts, settings, output path, preview seed) |
| `LeafAtlasPacker.cs` | `Source/Editor/Bush/` | Packs leaf variants into sprite atlas |
| `GielisSDF.cginc` | `Shaders/.../Editor/` | Gielis superformula, GielisRadius, HueRotate, JitterGielisParams |
| `LeafVeins.cginc` | `Shaders/.../Editor/` | Fractal vein system (unused — superseded by shader-inline veins) |

### Implemented leaf features

1. ✅ **Gielis SDF shape** — superformula boundary with per-variant jitter
   on m, n1, n2, n3 via seed-driven hash
2. ✅ **Dome shading** — radial darkening at edges, controllable via Edge Shade;
   reduced along midrib axis so the central vein stays bright at the leaf
   base, creating a natural connection to the petiole
3. ✅ **Hue jitter** — per-variant hue rotation in degrees
4. ✅ **AA alpha** — smoothstep anti-aliased edge with configurable width
5. ✅ **Palmate midrib** — gradient-driven central vein(s) adapting to lobe count
   - m < 2.5: single vertical midrib (pinnate venation)
   - m ≥ 2.5: `floor(m)` midribs radiate from centre (palmate venation)
   - Each midrib direction aligns with its Gielis lobe axis
   - Multi-midrib mode clips each midrib to its forward side
   - Width parameter, gradient cross-section profile
   - Gradient baked to a 64×1 texture, sampled in the shader
6. ✅ **Lateral veins** — mirrored pairs branching from each midrib
   - Count: number of vein pairs per midrib (0–8)
   - Angle: min/max range (degrees), randomised per lateral
   - Width Ratio: lateral width as fraction of midrib width
   - Start: where along the midrib the first pair originates (-1 to 0.5)
   - Length: min/max range, randomised per vein; biased by position —
     veins near the base are longer, near the tip shorter
   - Curvature: shape-adaptive bend follows Gielis boundary contour
     (separate from venule curvature); samples boundary at vein position
   - Lateral directions computed relative to their parent midrib direction
   - Smooth fade-out toward tips + fade near leaf edge via SDF distance
   - Primary laterals emerge from midrib at full strength (no fade-in seam)
7. ✅ **Venules** — recursive fractal branching from lateral veins
   - Per Lateral count (0–4), Survival Chance (0–1) via deterministic hash
   - Length: min/max range, randomised per venule; biased by position
   - Sub-veins branch in both directions (±angle from parent lateral)
   - Half-width of parent, smooth fade-in at origin for seamless blending
   - Origins placed on the **curved** parent lateral (not the straight ray)
   - Curvature: independent from lateral curvature, also shape-adaptive
   - Each venule gets an independent random length via unique hash seeds
8. ✅ **Edge fade** — all veins (laterals + venules) fade out when close to
   the leaf boundary, regardless of their configured length; uses SDF
   distance with `smoothstep(0, -0.04, sdfDist)`
9. ✅ **Vein presence tracking** — `inout float veinPresence` accumulates
   proximity to midrib + all laterals + all venules during rendering;
   each vein contributes a soft halo (3× base width) for reticulate suppression
10. ✅ **Reticulate venation** — fine net-like mesh filling spaces between veins
    - Two sets of parallel lines at ±angle create a diamond mesh
    - Organic distortion via cheap sine noise breaks grid regularity
    - Line width varies along each line for hand-drawn feel
    - Suppressed near all veins via `veinPresence` tracker
    - Fades near leaf edge via SDF distance
    - Controls: Enabled, Density (5–60), Width, Opacity, Angle (°)
11. ✅ **Petiole** — leaf stem extending from the bottommost Gielis boundary
    - Samples `GielisRadius(π)` to find the leaf base point
    - Overlaps slightly into the leaf (2× midrib width) for seamless blending
    - Uses midrib gradient cross-section for colour; darkens toward tip
    - Tapered width: configurable -1 to 1 (flared to pointed)
    - Length, Width, Taper controls in a dedicated Petiole foldout
    - Bake camera auto-expands to fit the petiole when enabled
12. ✅ **Unified vein seed** — `_VeinSeed` uniform derived from the variant
    hash feeds into `VeinHash()`, so randomising the preview seed also
    reshuffles vein angles, lengths, and venule survival patterns
13. ✅ **Preview seed** — 🎲 button in preview box randomises the seed,
    changing shape, hue, and full vein hierarchy per click
14. ✅ **Clickable variant grid** — variant thumbnails in a HelpBox; clicking
    any variant displays it in the live preview box with a `►` selection marker
15. ✅ **Shared MinMaxSlider** — reusable `PropertyDrawerHelper.DrawMinMaxSlider`
    (rect-based) and `DrawMinMaxSliderLayout` (layout-based) in `Source/Editor/`

### Leaf feature backlog (add one at a time)

1. **Highlight** — specular-like dome highlight
2. **Edge darkening** — colour variation at leaf boundary

### Bake pipeline

The shader uses an offscreen camera rendering a quad with the Gielis SDF
material into a RenderTexture, then reads back to Texture2D. The leaf is
centred at origin pointing up (+Y). Per-variant Gielis jitter and hue shift
are applied via a hash derived from the seed and variant index. The same
hash (scaled) is passed as `_VeinSeed` so vein randomisation is coupled
to the variant identity.

The midrib gradient is baked from Unity's `Gradient` to a 64×1 RGBA texture
at bake time, passed as `_MidribGradient`. All vein levels and the petiole
reuse the same texture. Cleaned up after each bake.

When petiole is enabled, the bake camera ortho size and quad scale expand
to fit the stem below the leaf. All loops in the shader use `[loop]`
attributes to prevent Metal from attempting to unroll the triply-nested
vein hierarchy (6 midribs × 8 laterals × 4 venules). Up to 64 variants.

### Editor window layout

The Bush Baker window (`Tools > Bush Baker`) has:
- **Branch Map section** (first) — properties panel + `TexturePreviewBox`
  with 🌿/🗺 toggle and 🎲 randomiser
- **Leaf Atlas section** (second) — properties panel + `TexturePreviewBox`
  with 🎲 randomiser
- Both preview boxes support background mode cycling (▦/■/□)
- **Buttons** — "Preview All Variants" and "Export Leaf Atlas"
- **Variant grid** — thumbnails in a HelpBox container; clickable to
  display the selected variant in the live preview. Selection is cleared
  when parameters change and auto-preview re-bakes.

Live preview auto-updates on any parameter change via a settings hash that
includes all fields, gradient colour/alpha keys, and the preview seed.

### Session context for Phase 1

When continuing leaf baking work, read:
- `Assets/Source/Editor/Bush/BushBakerWindow.cs` — editor window
- `Assets/Source/Editor/Bush/BushLeafBaker.cs` — bake pipeline
- `Assets/Source/Editor/Bush/BushLeafBakeSettings.cs` — settings
- `Assets/Source/Editor/Bush/BushBakerState.cs` — persisted editor state
- `Assets/Shaders/BalloonParty/Grid/Editor/BushBakeLeaf.shader` — the shader
- `Assets/Shaders/BalloonParty/Grid/Editor/GielisSDF.cginc` — Gielis math
- `Assets/Source/Editor/PropertyDrawerHelper.cs` — shared MinMaxSlider
- `.github/copilot-instructions.md` — project coding conventions


---

## Phase 2 — Branch Map Baking + Leaf Extraction + Rendering 🔨 Current

### Overview

The Bush Baker (editor window, Phase 1 infrastructure) gains a second
bake pass: fractal branching math renders a **branch map texture** where
RG = branch direction, B = reserved, A = depth. The baker then extracts
leaf attachment points from the texture and serializes both the texture
and the `LeafSlot[]` array as assets. At runtime, branches are a static
textured quad; leaves are `DrawMeshInstanced` with pivot-based rotation.

---

### Task Breakdown

| # | Task | New files | Modified files | Status |
|---|---|---|---|---|
| 2.1 | Branch bake settings class | `BushBranchBakeSettings.cs` | `BushBakerState.cs` | ✅ Done |
| 2.2 | Fractal branch generator (CPU) | `BushBranchGenerator.cs` | — | ✅ Done |
| 2.3 | Branch bake shader | `BushBakeBranch.shader` | — | ✅ Done |
| 2.4 | Branch baker (offscreen pipeline) | `BushBranchBaker.cs` | — | ✅ Done |
| 2.5 | Leaf extractor from branch map | `BushLeafExtractor.cs` | — | ⬜ |
| 2.6 | `BushVariantData` ScriptableObject | `BushVariantData.cs` | — | ⬜ |
| 2.7 | Editor window: branch section + export | `TexturePreviewBox.cs` | `BushBakerWindow.cs`, `BushBakerState.cs` | ✅ Done (preview), ⬜ (export) |
| 2.8 | Runtime branch shader | `BushBranch.shader` | — | ✅ Done |
| 2.9 | Runtime `BushView` refactor | — | `BushView.cs`, `BushViewController.cs` | ⬜ |
| 2.10 | `IBushSettings` extension | — | `IBushSettings.cs`, concrete SO | ⬜ |
| 2.11 | Integration test — bake + render | — | — | ⬜ |

---

### 2.1 — Branch Bake Settings

**File:** `Assets/Source/Editor/Bush/BushBranchBakeSettings.cs`

```csharp
[Serializable]
internal class BushBranchBakeSettings
{
    [SerializeField] internal int Resolution = 256;
    [SerializeField] internal int Variants = 4;

    // Fractal generation
    [SerializeField] internal int MaxDepth = 4;
    [SerializeField] internal int BranchesPerNode = 3;
    [SerializeField] internal Vector2 AngleSpread = new(25f, 55f);
    [SerializeField] internal Vector2 LengthRange = new(0.15f, 0.35f);
    [SerializeField] internal float LengthDecay = 0.7f;
    [SerializeField] internal float TrunkLength = 0.12f;
    [SerializeField] internal float BranchWidth = 0.02f;
    [SerializeField] internal float WidthDecay = 0.6f;
    [SerializeField] internal float TipTaper = 0.3f;

    // Visual
    [SerializeField] internal Color BranchColor = new(0.35f, 0.22f, 0.10f, 1f);
    [SerializeField] internal float ColorVariation = 0.08f;

    // Leaf extraction
    [SerializeField] internal float LeafDepthThreshold = 0.6f;
    [SerializeField] internal int MaxLeavesPerVariant = 12;
    [SerializeField] internal float LeafScale = 0.08f;
    [SerializeField] internal float LeafScaleVariation = 0.3f;
}
```

**Modify:** `BushBakerState.cs` — add:
- `[SerializeField] internal BushBranchBakeSettings BranchSettings = new();`
- `[SerializeField] internal bool BranchFoldout = true;`
- `[SerializeField] internal bool BranchShapeFoldout = true;`
- `[SerializeField] internal bool BranchVisualFoldout = true;`
- `[SerializeField] internal bool BranchLeafFoldout = true;`

---

### 2.2 — Fractal Branch Generator

**File:** `Assets/Source/Editor/Bush/BushBranchGenerator.cs`

Generates a flat list of branch segments via recursive fractal math.
Output used by the bake shader (mesh vertices) and leaf extractor (tips).

```csharp
internal static class BushBranchGenerator
{
    internal struct Segment
    {
        internal Vector2 Start;        // UV space 0–1
        internal Vector2 End;          // UV space 0–1
        internal float StartWidth;     // normalised
        internal float EndWidth;       // normalised (tapered)
        internal float Depth;          // normalised 0–1 (0=trunk, 1=tips)
        internal float DirectionAngle; // radians
    }

    internal static List<Segment> Generate(int seed, BushBranchBakeSettings settings)
    {
        // Uses System.Random(seed) for determinism.
        //
        // Algorithm:
        // 1. Root at (0.5, 0.1), pointing up
        // 2. Grow trunk: short segment, full width
        // 3. At trunk tip, spawn BranchesPerNode children:
        //    angle = parentAngle ± random(AngleSpread.x, AngleSpread.y)
        //    alternating sign for spreading
        // 4. Each child: length from LengthRange × LengthDecay^depth
        //    width = parentWidth × WidthDecay, tipWidth = width × TipTaper
        // 5. Recurse until depth == MaxDepth
        // 6. Clamp all positions to [0.02, 0.98] UV bounds
        // 7. Return flat list sorted by depth (trunk first)
    }
}
```

**Key design decisions:**
- All coordinates in **UV space (0–1)** — maps directly to texture pixels
- **Top-down radial growth:** root at centre `(0.5, 0.5)`, primary branches
  radiate outward in 360° (evenly spaced + jitter), sub-branches fork from tips
- This matches the top-down camera perspective — the bush is seen from above,
  not from the side. Branches spread outward like a starburst.
- Depth = distance from centre: trunk (low alpha) near middle, tips (high
  alpha) near texture edges
- Random global rotation per seed prevents axis-aligned patterns
- Segments that exit UV bounds are clamped to `[0.03, 0.97]`

---

### 2.3 — Branch Bake Shader

**File:** `Assets/Shaders/BalloonParty/Grid/Editor/BushBakeBranch.shader`

Renders a procedural mesh (4 verts per segment) with vertex colors
encoding direction and depth. Fragment applies edge AA.

```hlsl
Shader "Hidden/BalloonParty/Grid/BushBakeBranch"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off  Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;   // RG=dir, B=0, A=depth
                float2 uv : TEXCOORD0;  // x = across width (0–1)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // AA: soft edge across stroke width
                float edge = abs(i.uv.x - 0.5) * 2.0;
                float aa = 1.0 - smoothstep(0.8, 1.0, edge);
                return fixed4(i.color.rgb, i.color.a * aa);
            }
            ENDCG
        }
    }
}
```

**Vertex data encoding (set by `BushBranchBaker.BuildSegmentMesh`):**
- Position: quad corners of the tapered segment in UV/world space
- Color.r: `cos(angle) * 0.5 + 0.5` (branch direction X)
- Color.g: `sin(angle) * 0.5 + 0.5` (branch direction Y)
- Color.b: `0` (reserved)
- Color.a: normalised depth (0=trunk, 1=tip)
- UV.x: 0 at left edge, 1 at right edge (drives AA)
- UV.y: 0 at base, 1 at tip (unused for now)

---

### 2.4 — Branch Baker

**File:** `Assets/Source/Editor/Bush/BushBranchBaker.cs`

Follows `BushLeafBaker` pattern — offscreen camera + procedural mesh:

```csharp
internal static class BushBranchBaker
{
    private const string ShaderName = "Hidden/BalloonParty/Grid/BushBakeBranch";
    private const int BakeLayer = 31;

    internal static Texture2D Bake(int seed, BushBranchBakeSettings settings)
    {
        var segments = BushBranchGenerator.Generate(seed, settings);
        var mesh = BuildSegmentMesh(segments);
        var material = new Material(Shader.Find(ShaderName));

        var rt = RenderTexture.GetTemporary(
            settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGB32);

        var cameraGo = CreateBakeCamera(rt);
        var meshGo = CreateMeshObject(mesh, material);

        cameraGo.GetComponent<Camera>().Render();
        var result = ReadbackTexture(rt, settings.Resolution);

        // Cleanup
        Object.DestroyImmediate(meshGo);
        Object.DestroyImmediate(cameraGo);
        Object.DestroyImmediate(mesh);
        Object.DestroyImmediate(material);
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}
```

**`BuildSegmentMesh` detail:**
Per segment → 4 vertices forming a tapered quad:
```
    v2 ────── v3          (tip: EndWidth)
     ╲          ╱
      ╲        ╱
       ╲      ╱
    v0 ────── v1          (base: StartWidth)
```
- `perpendicular = rotate90(normalize(End - Start))`
- `v0 = Start - perpendicular × StartWidth/2`
- `v1 = Start + perpendicular × StartWidth/2`
- `v2 = End - perpendicular × EndWidth/2`
- `v3 = End + perpendicular × EndWidth/2`
- Indices: `[0,2,1, 1,2,3]`
- Vertex colors: all 4 get same `(dirR, dirG, 0, depth)`
- UV.x: v0,v2 = 0; v1,v3 = 1

**Camera setup:**
- Ortho, size = 0.5 (total view height = 1.0 matches UV 0–1)
- Position: `(0.5, 0.5, -1)` looking forward (+Z)
- Clear: transparent black `(0,0,0,0)`
- Culling mask: `1 << BakeLayer`

---

### 2.5 — Leaf Extractor

**File:** `Assets/Source/Editor/Bush/BushLeafExtractor.cs`

Reads the baked branch map texture, finds branch tips, returns leaf data:

```csharp
internal static class BushLeafExtractor
{
    internal static LeafSlotData[] Extract(
        Texture2D branchMap,
        int seed,
        BushBranchBakeSettings branchSettings,
        int leafVariantCount)
    {
        var pixels = branchMap.GetPixels32();
        var res = branchMap.width;
        var candidates = FindTipCandidates(pixels, res, branchSettings);
        var filtered = SpatialFilter(candidates, branchSettings.MaxLeavesPerVariant, res);
        return BuildLeafSlots(filtered, seed, branchSettings, leafVariantCount, res);
    }
}
```

**Tip detection (`FindTipCandidates`):**
```
For each pixel (x, y):
    if alpha < threshold (LeafDepthThreshold × 255): skip
    Decode direction: dirX = R/255 * 2 - 1, dirY = G/255 * 2 - 1
    Sample 2–3 pixels ahead in (dirX, dirY) direction
    If ahead.alpha < current.alpha × 0.7 OR ahead is empty:
        → this pixel is a tip candidate
    Score = alpha (deeper tips preferred)
```

**Spatial filtering (`SpatialFilter`):**
```
Sort candidates by score descending (deepest tips first)
minDist = 1.0 / sqrt(MaxLeavesPerVariant) × 0.8  (in UV space)
accepted = []
For each candidate:
    if no accepted leaf within minDist:
        accept
    if accepted.count >= MaxLeavesPerVariant: break
```

**Building leaf slots (`BuildLeafSlots`):**
```
For each accepted tip at (px, py):
    position = ((px / res) - 0.5, (py / res) - 0.5) × bushWorldSize
    angle = atan2(dirY, dirX)
    depth = alpha / 255.0
    phase = hash(seed, index)  // deterministic
    scale = LeafScale × (1 + (hash - 0.5) × 2 × ScaleVariation)
    variant = hash(seed, index + 1000) % leafVariantCount
    tint = baseColor with hue shifted by hash × variation
```

---

### 2.6 — BushVariantData ScriptableObject

**File:** `Assets/Source/Configuration/BushVariantData.cs`
(Runtime assembly — referenced at runtime by BushView)

```csharp
namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "BalloonParty/Bush Variant Data")]
    internal class BushVariantData : ScriptableObject
    {
        [SerializeField] private Texture2D _branchMap;
        [SerializeField] private LeafSlotData[] _leafSlots;
        [SerializeField] private Vector2 _boundsSize;

        internal Texture2D BranchMap => _branchMap;
        internal IReadOnlyList<LeafSlotData> LeafSlots => _leafSlots;
        internal Vector2 BoundsSize => _boundsSize;

#if UNITY_EDITOR
        internal void SetBakeData(
            Texture2D branchMap, LeafSlotData[] leafSlots, Vector2 boundsSize)
        {
            _branchMap = branchMap;
            _leafSlots = leafSlots;
            _boundsSize = boundsSize;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    [System.Serializable]
    internal struct LeafSlotData
    {
        public Vector2 Position;      // local-space (centred at origin)
        public float BaseAngle;       // radians
        public float Depth;           // 0–1
        public float PhaseOffset;     // wind desync
        public float Scale;           // world-space leaf size
        public int SpriteVariant;     // index into leaf atlas
        public Color32 Tint;          // per-leaf hue variation
    }
}
```

---

### 2.7 — Editor Window: Branch Section + Export

**Modify:** `Assets/Source/Editor/Bush/BushBakerWindow.cs`

Add `DrawBranchSection()` between `DrawSharedSettings()` and
`DrawLeafSection()` in `OnGUI()`.

**Branch section layout:**
```
▼ Branch Map
  Resolution: [256]
  Variants: [4]
  ▼ Fractal Shape
    Max Depth: [4]
    Branches Per Node: [3]
    Angle Spread: [25°–55°]    (MinMaxSlider)
    Length: [0.15–0.35]        (MinMaxSlider)
    Length Decay: [0.7]
    Trunk Length: [0.12]
    Width: [0.02]
    Width Decay: [0.6]
    Tip Taper: [0.3]
  ▼ Visual
    Color: [■]
    Color Variation: [0.08]
  ▼ Leaf Placement
    Depth Threshold: [0.6]
    Max Leaves: [12]
    Leaf Scale: [0.08]
    Scale Variation: [0.3]

  [Preview Branch Map]

  ┌─────────────────────────────────────────────┐
  │ Branch Preview          [🌿] [▦] [🎲]      │  ← TexturePreviewBox
  │                                             │
  │       (branch map preview, auto-updates)    │
  │                                             │
  └─────────────────────────────────────────────┘
```

**Preview toolbar buttons (via TexturePreviewBox):**
- **▦ / ■ / □** — background mode cycle (checkerboard, black, white)
- **🌿 / 🗺** — toggle runtime visual (brown depth-shaded) vs raw map (RG+A encoded)
- **🎲** — randomise seed

**Runtime visual preview:** CPU-side pixel transform replicating the
`BushBranch.shader` formula: `color × (0.6 + 0.4 × depth)`. Built from
the raw map on toggle without re-baking.

**Buttons (below preview):**
- **"Preview Branch Map"** — force re-bake (also triggered by auto-preview)
- **"Preview with Leaves"** — (TODO) bake + extract → overlay leaf positions
- **"Export Bush Variant"** — (TODO) full export pipeline

**Auto-preview:** Hash-check on every Repaint. Any branch setting or seed
change triggers a re-bake. Raw map is cached; display texture rebuilds
on mode toggle without re-baking.

**Modify:** `BushBakerState.cs` — ✅ Done: foldout booleans and
`BushBranchBakeSettings BranchSettings` added.

---

### 2.8 — Runtime Branch Shader

**File:** `Assets/Shaders/BalloonParty/Grid/BushBranch.shader`

Static unlit alpha-test shader. Follows project GPU instancing pattern.

```hlsl
Shader "BalloonParty/Grid/BushBranch"
{
    Properties
    {
        _MainTex ("Branch Map", 2D) = "white" {}
        _BranchColor ("Branch Color", Color) = (0.35, 0.22, 0.10, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.01
        _RendererColor ("Renderer Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off  ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _BranchColor;
            float _AlphaCutoff;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 map = tex2D(_MainTex, i.uv);
                clip(map.a - _AlphaCutoff);
                fixed3 col = _BranchColor.rgb * (0.6 + 0.4 * map.a);
                return fixed4(col * _RendererColor.rgb, map.a);
            }
            ENDCG
        }
    }
}
```

**Material:** `Assets/Materials/Bush/BushBranchMaterial.mat`
- GPU instancing **enabled** (no MPB, static)
- Register in `Assets/Shaders/BalloonParty/README.md`
- Register in `Assets/Materials/README.md`

---

### 2.9 — Runtime BushView Refactor

**Modify:** `Assets/Source/Slots/Actor/Archetype/BushView.cs`

Remove entirely:
- `_leafPrefab`, `_canopyRenderers`, `_leafSprites`, `_canopyGapScales`
- `_leafPool`, `SetLeafPool()`, `ClearSprites()`
- `SpawnCanopySprite()`, `SpawnLeafSprite()`
- `UpdateCanopyScales()`, `UpdateLeafTransformsLive()`, `ApplyLeafTransforms()`
- `PhyllotaxisCenter()`

Replace with:
```csharp
internal class BushView : ClusterView
{
    [SerializeField] private SpriteRenderer _branchRenderer;

    private BushVariantData _variantData;
    private IBushSettings _settings;
    private Matrix4x4[] _leafMatrices;
    private int _leafCount;

    internal BushVariantData VariantData => _variantData;
    internal Matrix4x4[] LeafMatrices => _leafMatrices;
    internal int LeafCount => _leafCount;

    internal void SetVariantData(BushVariantData data, IBushSettings settings)
    {
        _variantData = data;
        _settings = settings;
    }

    protected override void OnConfigured(MaterialPropertyBlock block)
    {
        // Disable legacy base renderer
        if (Renderer != null)
        {
            Renderer.enabled = false;
        }

        ConfigureBranch();
        ConfigureLeaves();
    }

    private void ConfigureBranch()
    {
        if (_branchRenderer == null || _variantData == null)
        {
            return;
        }

        _branchRenderer.enabled = true;
        _branchRenderer.material = _settings.BranchMaterial;
        _branchRenderer.material.mainTexture = _variantData.BranchMap;

        // Size the branch quad to bush world size
        var size = _settings.BushWorldSize;
        _branchRenderer.transform.localScale = new Vector3(size, size, 1f);

        _branchRenderer.sortingLayerID = _settings.SortingLayerId;
        _branchRenderer.sortingOrder = _settings.SortingOrderOffset;
    }

    private void ConfigureLeaves()
    {
        if (_variantData == null)
        {
            return;
        }

        var slots = _variantData.LeafSlots;
        _leafCount = slots.Count;
        _leafMatrices = new Matrix4x4[_leafCount];

        // Initial matrices at rest positions
        var worldOffset = (Vector2)transform.position;
        for (var i = 0; i < _leafCount; i++)
        {
            var slot = slots[i];
            var worldPos = worldOffset + slot.Position;
            _leafMatrices[i] = Matrix4x4.TRS(
                new Vector3(worldPos.x, worldPos.y, 0f),
                Quaternion.Euler(0f, 0f, slot.BaseAngle * Mathf.Rad2Deg),
                Vector3.one * slot.Scale);
        }
    }

    private void LateUpdate()
    {
        if (_leafCount == 0 || _settings == null)
        {
            return;
        }

        Graphics.DrawMeshInstanced(
            _settings.LeafQuadMesh,
            0,
            _settings.LeafMaterial,
            _leafMatrices,
            _leafCount);
    }
}
```

**Modify:** `Assets/Source/Slots/Actor/Archetype/BushViewController.cs`

Remove:
- `PoolManager` injection and `LeafPoolKey`
- `LeafSpritePoolChannel` creation in `OnViewCreated`
- `view.SetLeafPool(...)` call

Replace `OnViewCreated`:
```csharp
protected override void OnViewCreated(BushView view)
{
    _bushView = view;
    // Pick variant by seed from cluster position
    var variants = _settings.BushVariants;
    if (variants != null && variants.Length > 0)
    {
        var hash = view.transform.position.GetHashCode();
        var variant = variants[Mathf.Abs(hash) % variants.Length];
        view.SetVariantData(variant, _settings);
    }
}
```

---

### 2.10 — IBushSettings Extension

**Modify:** `Assets/Source/Configuration/IBushSettings.cs`

```csharp
internal interface IBushSettings : IClusterViewSettings
{
    // --- Phase 2 (new) ---
    BushVariantData[] BushVariants { get; }
    Material BranchMaterial { get; }
    Material LeafMaterial { get; }
    Mesh LeafQuadMesh { get; }
    float BushWorldSize { get; }

    // --- Phase 1 (kept) ---
    Sprite[] LeafAtlasSprites { get; }

    // --- Phase 3 (kept for future) ---
    float WindAmplitude { get; }
    float WindPeriod { get; }

    // --- Existing (kept) ---
    BushView BushPrefab { get; }
    float SlotRadius { get; }
    int SortingLayerId { get; }
    int SortingOrderOffset { get; }
}
```

**Deprecate / remove** from interface (old approach):
- `CanopyVariants`, `CanopyDiameter`, `BranchSpread`
- `RuffleLeafCount`, `RuffleRadius`, `RuffleRotationAmplitude`
- `RuffleScaleAmplitude`, `RufflePositionAmplitude`
- `RuffleDuration`, `RuffleStaggerPerUnit`
- `LeafSpriteSize`

**Modify concrete SO** implementing `IBushSettings` — add serialized
fields for new properties, remove deprecated ones.

---

### 2.11 — Integration Test

Manual verification checklist:

1. **Bake pipeline:**
   - Open `Tools > Bush Baker` → Branch Map section visible
   - Configure parameters, click "Preview Branch Map"
   - ✓ Fractal branches visible with tapering and depth shading
   - ✓ Changing seed produces different structures
   - Click "Preview with Leaves"
   - ✓ Leaf markers appear at branch tips, not at forks or trunk
   - Click "Export Bush Variant"
   - ✓ `BushVariantData` SO created in output folder
   - ✓ Branch map PNG saved with correct alpha channel
   - ✓ `LeafSlots` array populated in the SO

2. **Runtime rendering:**
   - Assign exported variant(s) to bush settings SO
   - Enter Play mode with bush cluster on grid
   - ✓ Branch map quad visible, correctly sized
   - ✓ Leaves render via `DrawMeshInstanced` at tip positions
   - ✓ Leaf sprites use correct atlas variants
   - ✓ 2 draw calls per cluster (verify in Frame Debugger)
   - ✓ No `LeafSpriteView` GameObjects in hierarchy
   - ✓ No runtime allocation (Profiler: zero GC in BushView)

3. **Edge cases:**
   - ✓ Multiple clusters with different variants
   - ✓ Single-slot cluster renders correctly
   - ✓ Gap-fill midpoint slots still work (branch quad per slot)

---

### Implementation Order (recommended)

```
2.1  Settings class             ✅ Done
2.2  Generator                  ✅ Done
2.3  Bake shader                ✅ Done
2.4  Baker pipeline             ✅ Done
2.7a Window preview + toolbar   ✅ Done (TexturePreviewBox component)
2.8  Runtime shader             ✅ Done
 ─── iterate on 2.2 parameters using preview ───
2.5  Leaf extractor             ← NEXT: reads texture from 2.4
2.7b Window "with leaves"       ← visual feedback for extraction
2.6  Variant SO                 ← data container
2.7c Window export button       ← saves SO to disk
 ─── bake pipeline complete ───
2.10 Settings extension         ← add variant refs to config
2.9  BushView refactor          ← runtime rendering
2.11 Integration test           ← end-to-end validation
```

---

### What exists now (Phase 2)

| File | Location | Role |
|---|---|---|
| `BushBranchBakeSettings.cs` | `Source/Editor/Bush/` | All fractal generation + visual + extraction parameters |
| `BushBranchGenerator.cs` | `Source/Editor/Bush/` | Recursive fractal → flat `Segment[]` in UV space |
| `BushBakeBranch.shader` | `Shaders/.../Editor/` | Bake shader: vertex color pass-through + edge AA |
| `BushBranchBaker.cs` | `Source/Editor/Bush/` | Offscreen camera pipeline: mesh from segments, render, readback |
| `BushBranch.shader` | `Shaders/.../Grid/` | Runtime shader: static alpha-test, depth shading, GPU instancing |
| `TexturePreviewBox.cs` | `Source/Editor/` | Reusable preview component: background modes + extensible toolbar |

### Shared editor components created

**`TexturePreviewBox`** (`Assets/Source/Editor/TexturePreviewBox.cs`):
- Self-contained preview box with HelpBox container, title, toolbar
- Background mode cycling: checkerboard → black → white
- Extra toolbar buttons via `Func<Rect, float, float>` callback (right-to-left)
- Static helper `DrawToolbarButton(rightEdge, y, label, width, onClick)`
- Used by both branch preview (🌿/🗺 toggle + 🎲) and leaf preview (🎲)

---

### Session context for Phase 2

**Current state (June 8 2026):** Tasks 2.1–2.4, 2.7a, and 2.8 are
complete. The Bush Baker window shows a live branch map preview with
auto-update. The generator produces radial top-down fractal branching.
Next task is 2.5 (leaf extractor).

**Key decisions made during implementation:**
1. **Top-down radial growth** — root at UV centre (0.5, 0.5), primary
   branches radiate outward 360°. NOT side-view (trunk-at-bottom growing
   up). This matches the game's top-down camera.
2. **Branches are static** — no animation on branches, only leaves rotate
   around their attachment pivot. Eliminates CPU/GPU sync complexity.
3. **RG = direction, B = reserved, A = depth** — the branch map texture
   encodes data for leaf extraction; at runtime the shader ignores RG
   and just uses alpha for depth shading.
4. **TexturePreviewBox** is a shared reusable component for any editor
   preview (background modes + extensible toolbar via callback).
5. **Runtime visual preview** is a CPU-side pixel transform matching the
   shader formula, avoiding a second render pass. The raw map is cached;
   toggling mode rebuilds the display texture without re-baking.
6. **Bake pipeline** follows the same offscreen-camera pattern as the
   existing leaf baker: procedural mesh → temp camera → ReadPixels.
   The branch map uses a procedural mesh (4 verts/segment) with vertex
   colors encoding the RG+A data, whereas the leaf baker uses a material
   on a quad.

**When resuming work, read these files:**
- `Assets/Source/Editor/Bush/BushBakerWindow.cs` — editor window
- `Assets/Source/Editor/Bush/BushBranchBaker.cs` — branch bake pipeline
- `Assets/Source/Editor/Bush/BushBranchGenerator.cs` — fractal generator (radial)
- `Assets/Source/Editor/Bush/BushBranchBakeSettings.cs` — branch settings
- `Assets/Source/Editor/Bush/BushBakerState.cs` — persisted state
- `Assets/Source/Editor/Bush/BushLeafBaker.cs` — leaf bake pipeline (pattern)
- `Assets/Source/Editor/Bush/LeafAtlasPacker.cs` — export pipeline pattern
- `Assets/Source/Editor/TexturePreviewBox.cs` — reusable preview component
- `Assets/Shaders/BalloonParty/Grid/Editor/BushBakeBranch.shader` — bake shader
- `Assets/Shaders/BalloonParty/Grid/BushBranch.shader` — runtime shader
- `Assets/Source/Slots/Actor/Archetype/BushView.cs` — runtime view (to refactor)
- `Assets/Source/Slots/Actor/Archetype/BushViewController.cs` — controller
- `Assets/Source/Slots/Actor/Cluster/ClusterView.cs` — base class
- `Assets/Source/Configuration/IBushSettings.cs` — settings (to extend)
- `.github/copilot-instructions.md` — project coding conventions

**Next steps (in order):**
1. **2.5 Leaf extractor** — scan baked texture for tip pixels (high alpha,
   no higher-alpha ahead in direction), spatial filter, output `LeafSlotData[]`
2. **2.7b "Preview with Leaves"** — overlay extracted leaf positions on branch
   map in the preview box
3. **2.6 BushVariantData SO** — runtime data container for branch map + leaf slots
4. **2.7c Export button** — save PNG + create SO per variant
5. **2.10 IBushSettings** — add `BushVariants[]`, `BranchMaterial`, `LeafMaterial`,
   `LeafQuadMesh`, `BushWorldSize`
6. **2.9 BushView refactor** — static branch SpriteRenderer + `DrawMeshInstanced`
7. **2.11 Integration test** — verify in play mode

**Key conventions:**
- Branch material: GPU instancing **enabled** (no MPB, static)
- Leaf material: GPU instancing **enabled** (`DrawMeshInstanced` + buffer)
- Editor-only code in `BalloonParty.Editor` namespace / assembly
- `BushVariantData` + `LeafSlotData` in runtime assembly (`BalloonParty.Configuration`)
- Allman braces, `internal` visibility, no `StartCoroutine`
- Read-only collection interfaces for non-mutated parameters
- Shared editor helpers in `Assets/Source/Editor/` (not in Bush subfolder)

**Open questions / things to tune:**
- Branch density and spread need visual iteration once preview is running
  in Unity. Parameters may need range adjustments after seeing results.
- Leaf extractor tip detection heuristic may need tuning — the "look ahead
  in branch direction" approach depends on RG encoding being accurate at tips.
- Whether `BushWorldSize` should come from `IBushSettings` or from the
  `BushVariantData` SO (per-variant vs global).
- The B channel is reserved — potential uses: branch thickness (for
  variable-width rendering), branch type/age, or per-branch color variation.

---

## Phase 3 — Wind Animation (Idle)

Leaf-only animation. Branches are static. No DOTween.

Each leaf **rotates around its attachment point** (the pivot extracted
from the branch map). The rotation angle oscillates for wind sway.

### Per-leaf rotation each frame (BushAnimator.Tick)

```
windRotation = sin(time × frequency + phaseOffset) × amplitude × depth
             + Mathf.PerlinNoise(time × 0.3f, phaseOffset) × noiseAmplitude × depth
currentAngle = baseAngle + windRotation
matrix = TRS(leaf.position, currentAngle, leaf.scale)
```

- **Frequency** — global wind speed
- **Phase offset** — unique per leaf (prevents sync)
- **Depth** — 0–1: leaves near trunk barely rotate, tip leaves sway most
- **Perlin** — slow organic drift layered on top of sine
- Optional subtle scale pulse (0.95–1.05) for flutter

---

## Phase 4 — Rattle (Disturbed State)

Triggered by projectile proximity via `DisturbanceFieldService`.
Leaf-only — branches stay static.

1. Impact detected → record impact position + initial strength
2. Each leaf: `rattleStrength = falloff(dist to impact) × depth`
3. Per-leaf damped spring: `vel += -stiffness × angle - damping × vel`
4. Blend: `finalAngle = baseAngle + windRotation + rattleAngle`
5. Spring decays to zero → pure wind resumes

Leaves near the impact and at higher depth rattle most. Leaves far
away or near the root barely react. The rotation is always around
the attachment point (pivot), so leaves shake in place naturally.

No DOTween. No graph traversal. Simple spring physics per leaf.
---

## Phase 5 — Visual Polish

- **Ground shadow** — simple dark ellipse sprite under root
- **Sorting refinement** — leaf matrix order controls painter's algorithm
- **Leaf overlap** — randomise sort within tip clusters
- **Branch map variants** — multiple baked branch textures per bush type
  for visual variety across clusters

---

## Performance Budget

| Metric | Per bush | Per cluster (4 slots) | 3 clusters |
|---|---|---|---|
| GameObjects | 0 leaves, 0 branches | 0 leaves, 0 branches | 0 |
| Draw calls | 2 (static branch quad + leaf instanced) | 2 | 6 |
| CPU matrices/frame | ~8 leaves only | ~32 leaves only | ~96 |
| Branch CPU cost | 0 (static texture) | 0 | 0 |
| Branch GPU cost | 1 tex fetch (static) | same | same |
| Texture memory | ~256KB (256×256 RGBA) | ~256KB (shared) | ~768KB |

Comparison with previous approaches:

| | SDF shader | SpriteRenderer/leaf | Baked branch map |
|---|---|---|---|
| Fragment cost | ~100 SDF evals | 1 tex fetch | 1 tex fetch |
| Draw calls/cluster | 1 (heavy) | ~8-12 | **2** |
| Leaf GameObjects | 0 | ~24-45 | **0** |
| Branch animation | SDF warp (GPU) | N/A | **None (static)** |
| Leaf animation | N/A | DOTween tweens | **CPU pivot rotation** |
| Runtime generation | per cluster | per cluster | **0** (pre-baked) |

---

## What we salvage / discard

### Salvage

| Asset | Reused for |
|---|---|
| Leaf baker (`BushBakeLeaf.shader`, `BushLeafBaker.cs`) | Baked leaf sprites with veins |
| Gielis SDF (`GielisSDF.cginc`) | Leaf shape variety |
| Baker window (`BushBakerWindow.cs`) | Extended with branch map baking |
| Bake pipeline pattern (`BushLeafBaker.cs`) | Same offscreen-camera pattern for branch map |
| Cluster system (`ClusterView`, `ClusterViewController`) | Slot management |
| `DisturbanceFieldService` | Rattle triggers |
| GPU instancing pattern | Leaf material |

### Discard

| Asset | Reason |
|---|---|
| Canopy baker (`BushBake.shader`, deleted) | Skeleton replaces canopy |
| Phyllotaxis layout | Branching tree replaces it |
| Runions venation (`LeafVenationSimulator.cs`) | Shader-inline veins replace it |
| `LeafVeins.cginc` | Superseded by inline midrib + lateral veins in `BushBakeLeaf.shader` |
| Runtime SDF (`Bush.shader`) | Pivot away from SDF |
| Per-leaf SpriteRenderer + LeafSpriteView | `DrawMeshInstanced` replaces |
| DOTween wind (BushRuffleController) | Spring physics replaces |
