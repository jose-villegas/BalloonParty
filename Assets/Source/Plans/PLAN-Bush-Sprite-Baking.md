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
| **2** | Branch map baking + leaf extraction + rendering | ✅ Done |
| **3** | Wind animation (idle) | ✅ Done |
| **4** | Rattle (disturbed state) | ✅ Done |
| **5** | Visual polish (sorting, bark texture) | ⬜ Planned |

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
    │  Per-slot rendering: each slot picks its own variant by index
    │  Branch quad: Graphics.DrawMesh (queue 3000, alpha-test, opaque)
    │  Leaf quads: DrawMeshInstanced (queue 3001, alpha-blend + shadow)
    │  Two draw calls per slot. Zero runtime generation.
    │  Base ClusterView.Renderer (SpriteRenderer) is disabled.
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

## Phase 2 — Branch Map Baking + Leaf Extraction + Rendering ✅ Done

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
| 2.5 | Leaf extractor from branch map | `BushLeafExtractor.cs` | — | ✅ Done |
| 2.6 | `BushVariantData` ScriptableObject | `BushVariantData.cs` | — | ✅ Done |
| 2.7 | Editor window: branch section + export | `TexturePreviewBox.cs` | `BushBakerWindow.cs`, `BushBakerState.cs` | ✅ Done |
| 2.8 | Runtime branch shader | `BushBranch.shader` | — | ✅ Done |
| 2.8b | Runtime leaf shader | `BushLeaf.shader` | — | ✅ Done |
| 2.9 | Runtime `BushView` refactor | — | `BushView.cs`, `BushViewController.cs` | ✅ Done |
| 2.10 | `IBushSettings` extension | — | `IBushSettings.cs`, `BushSettings.cs` | ✅ Done |
| 2.11 | Integration test — bake + render | — | — | ✅ Done |

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

Static unlit alpha-test shader. Opaque where drawn (alpha = 1), `clip()`
discards empty pixels. Rendered via `Graphics.DrawMesh` at queue 3000.

- `TransparentCutout` render type, `Transparent` queue (so it renders
  in the same pass as other 2D sprites)
- No blend — opaque write. `clip(alpha - cutoff)` discards empties.
- Depth shading: `_BranchColor × (0.6 + 0.4 × depth)`
- GPU instancing enabled (SpriteRenderer `_RendererColor` pattern)

**Material:** Created at runtime by `BushView` from `IBushSettings.BranchShader`
\+ variant's `BranchMap` texture. No serialized `.mat` asset.

---

### 2.8b — Runtime Leaf Shader

**File:** `Assets/Shaders/BalloonParty/Grid/BushLeaf.shader`

Instanced unlit alpha-blend shader for `DrawMeshInstanced` leaf rendering.
Rendered at queue 3001 (after branch at 3000).

**Per-instance properties** (via `UNITY_INSTANCING_BUFFER` + MPB):
- `_UVRect` — `float4(u, v, width, height)` atlas sub-rect
- `_LeafTint` — `fixed4` per-leaf color

**Material-level uniforms** (set from `IBushSettings`):
- `_MainTex` — leaf atlas texture
- `_ShadowColor`, `_ShadowOffset`, `_ShadowSoftness` — drop shadow
- `_SpriteScale` — shrinks sprite within quad for shadow margins

**Shadow system:**
- 9-tap box blur of leaf alpha at UV offset
- Shadow direction is world-space: vertex shader inverse-rotates
  `_ShadowOffset` via `R(-θ)` from `unity_ObjectToWorld`, negated
- `_SpriteScale` works like `SpriteShadow.shader`: divides UV outward
  `(uv - 0.5) / scale + 0.5`, sprite appears smaller, margins are
  transparent. Transform scale compensated by `1/scale` in C#.
- Porter-Duff "over" compositing: shadow behind leaf
- Bounds check masks sprite outside `[0,1]`; shadow bleeds into margins

**Material:** Created at runtime by `BushView` from `IBushSettings.LeafShader`.
No serialized `.mat` asset.

---

### 2.9 — Runtime BushView Refactor

**File:** `Assets/Source/Slots/Actor/Archetype/BushView.cs`

Per-slot rendering. No SpriteRenderer — uses `Graphics.DrawMesh` for
branches and `DrawMeshInstanced` for leaves. Each slot picks a variant
by cycling `i % variants.Length`. Leaf matrices include -90° rotation
from `BaseAngle` and scale compensation for `_SpriteScale`. Shadow
uniforms set on material from `IBushSettings`. See session context
for full details.

**File:** `Assets/Source/Slots/Actor/Archetype/BushViewController.cs`

Wires `IBushSettings` to the view via `SetSettings()`. Variant selection
is handled per-slot inside `BushView.RebuildSlots()`, not in the controller.

---

### 2.10 — IBushSettings Extension

**File:** `Assets/Source/Configuration/IBushSettings.cs`

```csharp
internal interface IBushSettings : IClusterViewSettings
{
    BushView BushPrefab { get; }
    BushVariantData[] BushVariants { get; }
    Shader BranchShader { get; }
    Shader LeafShader { get; }
    float BushWorldSize { get; }
    Sprite[] LeafAtlasSprites { get; }
    Color LeafShadowColor { get; }
    Vector2 LeafShadowOffset { get; }
    float LeafShadowSoftness { get; }
    float LeafSpriteScale { get; }
    float WindAmplitude { get; }
    float WindPeriod { get; }
}
```

Materials and meshes are created internally by `BushView` at runtime.
The settings expose shaders and shadow tuning parameters only.

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
| `BushLeafExtractor.cs` | `Source/Editor/Bush/` | Tip detection + spatial filtering → `LeafSlot[]` |
| `BushVariantExporter.cs` | `Source/Editor/Bush/` | Bake + extract + save PNG + create `BushVariantData` SO |
| `BushVariantData.cs` | `Source/Configuration/` | Runtime SO: branch map texture + `LeafSlotData[]` |
| `BushBranch.shader` | `Shaders/.../Grid/` | Runtime: opaque alpha-test, depth shading, GPU instancing |
| `BushLeaf.shader` | `Shaders/.../Grid/` | Runtime: instanced alpha-blend, per-instance UV rect + tint, 9-tap drop shadow, sprite scale, rotation-independent shadow direction |
| `BushView.cs` | `Slots/Actor/Archetype/` | Per-slot rendering via `Graphics.DrawMesh*`, variant cycling, leaf shadow config |
| `BushViewController.cs` | `Slots/Actor/Archetype/` | Cluster controller, wires settings to view |
| `IBushSettings.cs` | `Configuration/` | Interface: shaders, variants, shadow settings, sprite scale |
| `BushSettings.cs` | `Configuration/` | Concrete SO with all tunable fields |
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

**Current state (June 8 2026):** Phase 3 is complete. Wind idle animation
works via `BushAnimator` (ITickable) driving per-leaf pivot rotation with
sine + Perlin noise, modulated by depth. Zero allocations per frame.
Phase 2 full pipeline also works end-to-end.

**When resuming work, read these files:**
- `Assets/Source/Slots/Actor/Archetype/BushView.cs` — per-slot rendering
- `Assets/Source/Slots/Actor/Archetype/BushViewController.cs` — controller
- `Assets/Source/Slots/Actor/Archetype/BushAnimator.cs` — wind idle animation
- `Assets/Source/Slots/Actor/Cluster/ClusterView.cs` — base class
- `Assets/Source/Slots/Actor/Cluster/ClusterViewController.cs` — base controller
- `Assets/Source/Configuration/IBushSettings.cs` — settings interface
- `Assets/Source/Configuration/BushSettings.cs` — concrete SO
- `Assets/Source/Configuration/BushVariantData.cs` — variant data SO
- `Assets/Shaders/BalloonParty/Grid/BushBranch.shader` — runtime branch shader
- `Assets/Shaders/BalloonParty/Grid/BushLeaf.shader` — runtime leaf shader
- `Assets/Source/Editor/Bush/BushBakerWindow.cs` — editor window
- `Assets/Source/Editor/Bush/BushBranchBaker.cs` — branch bake pipeline
- `Assets/Source/Editor/Bush/BushLeafExtractor.cs` — leaf tip extraction
- `Assets/Source/Editor/Bush/BushVariantExporter.cs` — export pipeline
- `Assets/Shaders/BalloonParty/README.md` — shader registry
- `.github/copilot-instructions.md` — project coding conventions

**How the runtime rendering works (BushView):**

`ClusterViewController` creates ONE `BushView` for ALL clusters, positioned
at the combined bounding box center. `ClusterView.Configure()` fills
`SlotCentersBuffer` with `(x, y, seed, radiusScale)` per slot. `BushView`
does NOT use the SpriteRenderer at all — it disables the base `Renderer`.

`BushView.RebuildSlots()` iterates `SlotCentersBuffer[0..SlotCount]` and
for each slot:
1. Picks a `BushVariantData` by `i % variants.Length` (cycling for variety)
2. Creates a branch `Material` from `BranchShader` + variant's `BranchMap`
   texture, queue 3000
3. Creates a leaf `Material` from `LeafShader` + atlas texture, queue 3001,
   with shadow uniforms (`_ShadowColor`, `_ShadowOffset`, `_ShadowSoftness`,
   `_SpriteScale`) set from `IBushSettings`
4. Builds `Matrix4x4` for branch (centered at slot world pos, scaled to
   `BushWorldSize`) and for each leaf (offset from slot pos, rotated by
   `BaseAngle - 90°`, scaled by `slot.Scale * (1/spriteScale)`)
5. Per-instance `_LeafTint` and `_UVRect` arrays via `MaterialPropertyBlock`

`LateUpdate()` loops `_slotRenderData` and calls:
- `Graphics.DrawMesh(branchQuad, matrix, material, layer)` per slot
- `Graphics.DrawMeshInstanced(leafQuad, 0, material, matrices, count, mpb)` per slot

**Leaf quad** is bottom-pivoted: vertices at `y: [0, 1]`, so the petiole
(bottom) sits at the attachment point. Branch quad is center-pivoted:
vertices at `[-0.5, 0.5]`. Both are static shared meshes with
`UploadMeshData(true)`.

**Leaf shader (BushLeaf.shader) features:**
- Per-instance `_LeafTint` (color) and `_UVRect` (atlas sub-rect) via
  `UNITY_INSTANCING_BUFFER`
- `_SpriteScale` shrinks sprite within quad via `(uv - 0.5) / scale + 0.5`
  (same pattern as `SpriteShadow.shader`). Transform scale compensated by
  `1 / spriteScale` in C# so visual size stays the same.
- 9-tap soft drop shadow: samples at `rawUV + localShadowOffset`, blurred
  by `_ShadowSoftness`, composited via Porter-Duff "over"
- Shadow direction is **world-space**: the vertex shader inverse-rotates
  `_ShadowOffset` using `R(-θ)` extracted from `unity_ObjectToWorld` column 0,
  then negates. Result passed as `localShadowOffset` interpolator.
- Bounds check masks the sprite outside `[0,1]` after scaling; shadow is
  NOT masked so it bleeds into the margins.

**Branch shader (BushBranch.shader) features:**
- `TransparentCutout` render type, `Transparent` queue, no blend
- `clip(alpha - cutoff)` discards empty pixels
- Depth shading: `_BranchColor * (0.6 + 0.4 * alpha)`
- Output alpha = 1.0 (fully opaque where drawn)
- GPU instancing enabled (SpriteRenderer pattern for `_RendererColor`)

**Key decisions made during implementation:**
1. **Top-down radial growth** — root at UV centre (0.5, 0.5), primary
   branches radiate outward 360°. Matches top-down camera.
2. **Branches are static** — no animation, only leaves rotate.
3. **RG = direction, B = reserved, A = depth** — branch map encoding.
4. **No serialized Material or Mesh assets** — shaders on settings,
   materials and meshes created at runtime by `BushView`.
5. **Both branch and leaves use `Graphics.DrawMesh*`** — avoids
   SpriteRenderer vs DrawMeshInstanced sorting conflicts.
6. **Per-slot rendering** — one branch + one leaf batch per slot, not
   per cluster. Variants cycle by index for visual variety.
7. **Leaf drop shadows** — SpriteShadow pattern with rotation-independent
   direction via inverse rotation in vertex shader.
8. **Branch shader is opaque** — `clip()` + alpha=1, no blending.

**Next steps:**
1. **Fix leaf tip detection** — extractor places leaves at branch body/fork
   positions, not actual endpoints. See investigation notes below.
2. **Phase 4** — rattle (disturbed state via `DisturbanceFieldService`)

**Open questions / things to tune:**
- Whether `BushWorldSize` should be per-variant or global.
- The B channel in branch map is reserved for future use.

---

### Leaf Attachment Investigation (June 8 2026)

**Problem:** Leaves appear to float — not visually connected to branch tips.

**Verified correct (via gizmo debug):**
- **Coordinate chain** — attachment positions from the baked variant data
  match the branch map rendering positions. Gizmo yellow dots land ON the
  branches, confirming UV → local → world math is correct.
- **Pivot math** — `TRS_pos = attachmentPos + rot * (0, -(pivotOffset+0.5)*scale, 0)`.
  The pivot (at quad local `y = pivotOffset + 0.5`) always cancels back to
  `attachmentPos` in world space. Verified algebraically and via gizmo overlay.
  Changing `LeafPivotOffset` repositions the quad around a fixed pivot.
- **Sprite scale** — shader centers at UV (0.5, 0.5); quad center at local
  (0, 0.5); no lateral shift. Shadow uses same centering. All symmetric.
- **Wind rotation** — `pivotShift` uses the animated `rot`, so the pivot
  stays at `attachmentPos` even as the leaf swings. Verified algebraically.

**Verified NOT the cause:**
- Sprite scale (`_SpriteScale`) — symmetric centering, no X/Y drift
- `scaleCompensation` (1/spriteScale) — uniform scaling from TRS origin
- Shadow offset — operates in UV space, doesn't shift geometry
- Texture coordinate conventions — `GetPixels32` bottom-left origin matches
  quad UV (0,0) at bottom-left vertex and bake camera at (0.5, 0.5)

**Root cause: tip detection quality.** The `FindTipCandidates` + `IsTip`
check accepts branch body and fork pixels as valid tips. The `IsTip` method
samples 2–3 pixels ahead and requires alpha to drop — but at forks, both
branches have high alpha ahead, so forks pass the check. The gizmos confirm
attachment dots are ON branches but NOT at endpoints.

**Changes made during investigation:**
- `IBushSettings` / `BushSettings`: `Shader LeafShader` → `Material LeafMaterial`
  (prevents shader variant stripping in mobile builds)
- `BushLeaf.shader`: `fixed4` → `float4` in instancing buffer (mobile UBO compat),
  `unity_ObjectToWorld` → `UNITY_MATRIX_M` (per-instance matrix portability)
- `BushView`: instancing fallback via per-leaf `DrawMesh` when
  `SystemInfo.supportsInstancing` is false
- `BushView`: depth-tiered rendering — inner leaves (queue 2999) behind
  branches (queue 3000), outer leaves (queue 3001) in front. Configured
  via `LeafDepthSplit` threshold.
- `BushView`: `LeafPivotOffset` range changed to [-0.5, 0.5], default 0.
  Sprite center (0.5, 0.5) = quad center aligned with attachment point.
- `BushView`: debug gizmo (`_debugLeafPivots`) — yellow=attachment,
  red=TRS origin, cyan=sprite center, white line=quad extent
- `BushAnimator`: reads `LeafPivotOffset` from settings each frame for
  live play-mode tuning
- `BushLeafExtractor`: added `IsTip` ahead-check (samples 2–3 pixels in
  branch direction, rejects if alpha stays high)
- `BushLeaf.shader`: shadow wrap-around fix — `SampleShadowAlpha` masks
  samples outside `[0,1]` raw UV bounds

**Still open:**
- Tip detection must be improved to find actual branch endpoints, not
  branch body or fork pixels. Consider walking forward from the candidate
  pixel along the direction until alpha drops to zero, then using THAT
  position as the attachment point.

---

## Phase 3 — Wind Animation (Idle) ✅ Done — GPU Vertex Shader

Leaf-only animation. Branches are static. No DOTween. No CPU matrix math.

Wind animation runs **entirely on the GPU vertex shader**. Per-leaf rotation
around the attachment pivot is computed from per-instance data (phase, depth,
baseAngle, scale) and material-level wind uniforms. Matrices are static
translation-only — set once at setup, never updated per frame.

### Implementation

**`BushLeaf.shader`** vertex shader:
- Per-instance `_LeafWind` float4(phase, depth, baseAngle, scale)
- Material uniforms: `_WindFrequency`, `_WindAmplitude`, `_WindNoiseAmplitude`,
  `_WindScalePulse`, `_PivotOffset`
- Sine oscillation + dual-sine organic noise (replaces CPU Perlin):
  `sin(t*0.3 + phase*17.3) * sin(t*0.7 + phase*31.1)` — cheap, no texture
- Pivot-based rotation: shift vertex to pivot (y = pivotOffset + 0.5),
  rotate by animated angle, scale, then translate via static matrix
- Shadow/highlight inverse rotation uses the GPU-computed cosA/sinA

**`BushView`** changes:
- Matrices are static translation-only: `TRS(worldPos, identity, one)`
- `_LeafWind` per-instance data set on `MaterialPropertyBlock`
- Wind uniforms set on leaf material at creation time
- No per-frame matrix updates — `LateUpdate` just draws

**`BushAnimator`** changes:
- Wind loop removed entirely — empty `Tick()`
- Kept as shell for Phase 4 rattle (damped spring state is CPU-only)

### Key design decisions

1. **GPU wind eliminates CPU Tick loop** — zero per-frame CPU cost for wind
   on ~96 leaves. Matrices never change after setup.
2. **Dual-sine noise replaces Perlin** — `sin(a)*sin(b)` at irrational
   frequency ratios approximates organic drift without a texture lookup or
   CPU `PerlinNoise`. Visually indistinguishable at leaf-sway amplitudes.
3. **Phase 4 rattle stays CPU** — damped spring physics is sequential/stateful.
   Rattle will add a `_RattleAngle` per-instance float updated by `BushAnimator`
   only when a disturbance is active. Most frames it's 0.
4. **Static matrices** — set once in `RebuildSlots`, uploaded every frame
   by `DrawMeshInstanced` but never mutated. Future: could use a persistent
   compute buffer to avoid re-upload entirely.

---

## Phase 4 — Rattle (Disturbed State) ✅ Done — GPU Vertex Shader

Rattle runs entirely on the GPU by sampling the global disturbance field
texture (`_DisturbanceTex`) in the vertex shader. Zero CPU cost — no
`BushAnimator` needed at all. Both wind and rattle are pure shader features.

### How it works

The `DisturbanceFieldService` maintains a global render texture encoding
density (R) and displacement direction (GB) at each world position. The
leaf vertex shader samples this texture at the leaf's world position:

1. Compute leaf world position from the static translation matrix
2. Convert to disturbance field UV: `(worldPos - _FieldBoundsMin) / _FieldBoundsSize`
3. `tex2Dlod` sample (vertex shader) → displacement vector from GB channels
4. Cross product of displacement with leaf direction → signed rattle angle
5. Modulated by `_RattleAmplitude × depth` and a fast oscillation
   `sin(t × _RattleFrequency + phase)` for visible shaking
6. Added to wind angle before the pivot rotation

### Why this works without CPU spring physics

The disturbance field already handles the "spring decay" — stamps create
displacement, diffusion spreads it, and reform attenuates it back to zero.
The leaf shader just reads the current displacement magnitude as rattle
strength. The temporal behavior (impact → shake → settle) comes from the
field's own physics, not from per-leaf spring state.

### Settings (`IBushSettings` / `BushSettings`)

- `RattleEnabled` — bool, toggles `_RATTLE_ON` shader keyword
- `RattleAmplitude` — max rotation in degrees at full displacement (default 15°)
- `RattleFrequency` — oscillation speed during rattle (default 12 Hz)

### `BushAnimator` removed

With both wind and rattle on the GPU, `BushAnimator.cs` has been deleted.
No CPU animation code exists for bushes. The DI registration in
`GameLifetimeScope` has been removed.
---

## Phase 5 — Visual Polish

- **Ground shadow** — simple dark ellipse sprite under root
- **Sorting refinement** — leaf matrix order controls painter's algorithm
- **Leaf overlap** — randomise sort within tip clusters
- **Branch map variants** — multiple baked branch textures per bush type
  for visual variety across clusters

---

## Performance Budget

| Metric | Per slot | Per cluster (4 slots) | 3 clusters |
|---|---|---|---|
| GameObjects | 0 leaves, 0 branches | 0 leaves, 0 branches | 0 |
| Draw calls | 2 (branch DrawMesh + leaf DrawMeshInstanced) | 8 | 24 |
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
