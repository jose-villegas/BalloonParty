# Unbreakable Balloon — Visual Ideation

> Metallic propulsion sphere. Visual cousin of the Laser item — tells the player
> at a glance "these two are related" before they discover that Laser (Piercing)
> is the key to destroying it.

---

## Visual Context — Current Game

The grid is populated with **glossy rubber balloons** (round, soft-lit, with a white
shine highlight and a small tied knot at bottom) in five palette colors against a
**soft light-blue sky** background. Everything reads as a bright, airy park.

Other balloon types already on the grid:
- **Soap Cluster** — translucent iridescent bubble clusters with visible hexagonal
  membrane junctions. Clearly "fragile / bubbly" read.
- **Tough Balloon** — dark opaque matte surface (black rubber or cracked stone).
  Clearly "heavy / hard to pop" read. Cracks appear as damage accumulates.

**The Laser item** sits inside a host balloon as a **dark purple/magenta circular
targeting reticle** — concentric rings with a crosshair motif. It is the most
visibly *mechanical / technical* element on the grid today. This is the visual
language the Unbreakable should echo: concentric geometry, dark metallic tone,
precision-engineered feel.

The Unbreakable must stand out from all of these while fitting the same world.
It should read as **the most foreign object on the grid** — the one thing that
clearly doesn't belong among rubber and soap — yet still feel like it was designed
for this universe (a toy, not a real machine).

---

## Core Identity

**A small hovering chrome sphere with visible exhaust / propulsion glow.**

Not a balloon at all — it's a mechanical intruder in a park full of rubber and soap.
It reads as *foreign, tough, and purposeful*. The metallic surface reflects the
surrounding world in a distorted way (environment-mapped or faked). The underside
emits a soft thruster glow that flickers subtly — the same hue family as the Laser
item's beam, creating the visual link.

---

## Visual Pillars

| Pillar | What it communicates | How |
|---|---|---|
| **Metallic / chrome** | "You can't pop this with a normal shot" | Reflective surface, specular highlight, hard edge |
| **Propulsion exhaust** | "This thing floats by force, not buoyancy" | Soft glow underneath, subtle particle wisps |
| **Laser affinity** | "The Laser can deal with this" | Exhaust color in the same dark purple/magenta family as the Laser item's reticle; surface etch lines echo the Laser's concentric-ring / crosshair motif |
| **Weight / inertia** | "Heavy, permanent" | Slow idle float (slower than any balloon), minimal wobble on nudge |

---

## Shader: `BalloonParty/Balloon/UnbreakableBalloon`

**File:** `Assets/Shaders/BalloonParty/Balloon/UnbreakableBalloon.shader`

Built on the `SpriteShineShadow` pattern — the sprite carries all the panel/seam/lens
art, and the shader adds the dynamic chrome effects on top.

### Effects

| Layer | What it does | Key properties |
|---|---|---|
| **Chrome rim** | Detects the sprite's alpha edge (8-tap neighbour sampling), then sweeps a specular highlight around the contour over time. Simulates a light source rotating around a metallic surface. | `_RimColor`, `_RimWidth`, `_RimIntensity`, `_RimSweepSpeed`, `_RimSweepWidth` |
| **Diagonal shine** | Same periodic shine band as `SpriteShineShadow`. Adds a white sweep across the surface on an interval. | `_ShineWidth`, `_ShineSpeed`, `_ShineInterval` |
| **Exhaust glow** | Additive gradient below sprite center. Flickers via sine wave. Visible even beyond the sprite edge for a soft underglow. | `_ExhaustColor` (purple/magenta), `_ExhaustCenter`, `_ExhaustWidth`, `_ExhaustFalloff`, `_ExhaustFlicker`, `_ExhaustFlickerAmt` |
| **Deflect flash** | Full-surface brightness pulse driven by `_DeflectFlash` (0→1→0 via C#). | `_DeflectFlash` |
| **Shadow** | Standard offset shadow with optional soft blur. Same system as all other balloon shaders. | `_ShadowColor`, `_ShadowOffset`, `_ShadowSoftness` |

### How the chrome rim works

1. **Edge detection:** For each pixel, sample alpha in 8 neighbouring directions at
   `_RimWidth` distance. Where the centre is opaque but a neighbour is transparent,
   that's the edge. This catches ALL sprite contours — outer silhouette AND internal
   panel seam edges (if those seams have alpha variation in the sprite).

2. **Angular sweep:** Compute the angle of each edge pixel relative to sprite centre
   (`atan2`). A sweep position rotates continuously (`frac(time * speed)`). Pixels
   whose angle is near the sweep position get highlighted. `_RimSweepWidth` controls
   the arc width. The result: a bright specular glint that travels around the border
   of the sprite, exactly like light catching a chrome edge.

3. The sweep is smooth (not a hard cut) — it fades in and out via `smoothstep`, so
   the highlight reads as a soft metallic reflection, not a hard line.

---

## Approach — Sprite + Chrome Overlay Shader

The sprite carries all the art-directed detail (panels, seams, lens), and the
shader adds chrome life on top: a traveling specular rim, periodic shine sweep,
exhaust glow, deflect flash, and drop shadow.

**Why sprite-based, not fully procedural:**
- The panel geometry, seam curves, and lens housing are specific art-directed
  shapes — every Unbreakable looks identical. No per-instance variation needed
  (unlike Soap Cluster's `_BubbleCount` or Tough's `_VoronoiSeed`).
- The only dynamic elements (chrome rim, exhaust, deflect) are lightweight
  overlays on any sprite shader.
- The sprite can be iterated in Photoshop/Aseprite independently of shader tuning.

---

## C# Variant: `UnbreakableBalloonVariant`

Simpler than `SoapBubbleClusterVariant` — no per-instance geometry or bubble count:

```
Assets/Source/Balloon/Type/UnbreakableBalloonVariant.cs
```

- `[ExecuteAlways]` — runs in edit mode for live preview of exhaust flicker
- Implements `IBalloonVariant` + `IBalloonViewBinding`
- `Bind()`: no randomization needed (every Unbreakable looks the same)
- Subscribes to hit callbacks → drives `_DeflectFlash` (0→1→0 tween via DOTween
  on MaterialPropertyBlock, unscaled time)
- `Update()`: pushes `_TimeOffset` for exhaust flicker

## Sprite Art Direction — Adapting the Reference

The reference image shows a dark-grey paneled mechanical sphere with:
- **Armored plates** separated by dark seam lines with bright edge highlights
- **A central red lens/eye** in a concentric housing — the focal point
- **A side thruster nub** — implies propulsion
- **Flat-shaded cel style** with clean gradients — fits the game's art direction

**Adaptations for BalloonParty:**

| Reference element | Game adaptation |
|---|---|
| Dark grey plates | Keep, but slightly lighter/softer to sit against the light-blue sky without reading as a hole in the grid |
| Red lens | **Purple/magenta lens** — match the Laser item's reticle hue. This IS the visual link. The lens can pulse/glow subtly via the shader. |
| Dark seam lines | Keep — the seams are what make it read as "paneled / engineered" vs a plain sphere. They differentiate it from Tough (which has cracks, not seams). |
| Side thruster nub | Keep or simplify — at game scale (~0.9 units) fine detail may be lost. A simpler silhouette with 1–2 panel divisions may read better than many small plates. |
| Flat cel shading | Matches the game perfectly — Simple balloons are already flat-shaded with a single highlight band. |

**Key read at game scale:** At ~100–120 pixels on a phone screen, the player
needs to instantly distinguish Unbreakable from Tough Balloon (also dark, also
round). The differentiator is **panels + lens glow** vs **cracks + matte surface**.
The lens is the single most important detail — it's the "eye" that makes it feel
mechanical and alive.

---

// ...existing code... (## Animator section onwards)

Minimal — the shader does all the visual work:

| State | Notes |
|---|---|
| `StableIdle` | Very slow, heavy up-down float. Amplitude ~60% of Simple balloon. |
| `UnstableIdle` | Subtle lateral wobble during balancer movement. |
| `DeflectReact` | Brief recoil/shake (position only — 0.15s). Pairs with shader's `_DeflectFlash`. |

No `Pop` state in the animator — when Piercing destroys it, `BalloonView.PlayPopEffect`
handles the VFX (see below).

---

## VFX

| Effect | Description |
|---|---|
| `PSVFX_UnbreakableDeflect` | Small spark burst + metallic clang particles at contact point. Short-lived. Chromatic — silver/white sparks. |
| `PSVFX_UnbreakablePop` | Dramatic: metallic shrapnel fragments (small chrome shards) flying outward + bright flash + exhaust snuff-out (the glow dies). Should feel like "the Laser cracked through the armor." |

---

## Open Questions

1. **Exhaust color** — The Laser item's in-game reticle is dark purple/magenta.
   Match that hue exactly, or shift to a slightly desaturated / cooler variant
   to avoid reading as "this IS a Laser"? Suggestion: same hue, lower saturation,
   slight glow bloom so it reads as "powered by the same energy."

2. **Size** — Same cell size as Simple balloon? Slightly smaller (dense, compact)?
   Slightly larger (imposing)? Affects grid readability.

3. **Panel detail at game scale** — The reference has many small plates and
   sub-details. At ~100px on a phone, how much should we simplify? Suggest
   starting with 3–4 large plates + the lens, test readability, then add detail
   if it still reads too generic.

4. **Lens vs Tough differentiation** — Both are dark spheres. The lens glow is
   the key differentiator. Should the lens pulse continuously (always visible)
   or only on deflect? Continuous pulse is stronger for readability.

5. **Shadow tint** — Standard dark shadow or a faint cyan/blue shadow matching the
   exhaust? The latter would reinforce the propulsion-glow read at a glance.



