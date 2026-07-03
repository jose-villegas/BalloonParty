# Editor/ShadowBake

Bake-time drop shadows for sprite prefabs (audit remediation 5a). The runtime
`SpriteShadow`/`SpriteBlur` shaders re-blur a static image every frame (up to 10 texture
taps per pixel per layer); baking the shadow once replaces that with a single plain
sprite — and since it's offline, the blur can be as soft as it wants.

## Contents

| File | What it does |
|---|---|
| `SpriteShadowBakerEditor` | Custom inspector for `SpriteShadowBaker` (`Shared/Rendering/`) — the **Bake** button does everything below |

## Workflow

1. Drop a `SpriteShadowBaker` component anywhere on a prefab — its transform is the
   silhouette root; every enabled child `SpriteRenderer` contributes.
2. Tune shadow color, world-space offset, blur radius, quality (resolution multiplier,
   blur passes), and whether to swap materials.
3. Press **Bake**. In one pass it:
   - renders the union silhouette of the child sprites (via `CommandBuffer.DrawRenderer`
     — no camera or scene involved, so it works from a scene instance, the prefab stage,
     or the asset inspector; sliced/tiled draw modes render their real meshes),
   - blurs it offline (iterated separable box blur ≈ Gaussian; padding is derived so the
     penumbra never clips),
   - writes the sprite to `Assets/Sprites/Baked/Shadows/<prefab folder>/<prefab>_Shadow.png`
     (folder mirrors the prefab path so names can't collide), pivot at the baker's origin,
     pixels-per-unit scaled by the resolution multiplier,
   - if **Replace Shadow Materials** is on: swaps `BalloonParty/Sprite/*` materials on the
     contributing renderers for the replacement (default `Sprites-Default`), shrinking each
     renderer by its material's `_SpriteScale` so the visible size is unchanged (sliced/tiled
     via `renderer.size`; simple draw mode via `localScale`, with a logged warning to check
     children),
   - creates/updates a `BakedShadow` child (sorted just below the lowest contributing
     renderer, positioned at the shadow offset) displaying the baked sprite.

All edits go through `LoadPrefabContents`/`SaveAsPrefabAsset` — the prefab asset is
modified directly (no undo; re-bake is cheap, and materials can be reassigned by hand to
revert). Re-baking reuses the same shadow child and overwrites the same PNG.

## Notes

- The shadow color is baked into the texture (matching how the runtime shader tinted the
  shadow); tint the `BakedShadow` renderer's color for per-instance variation — e.g. give
  it a `ColorableRenderer` if it should follow the balloon color like the shader shadows did.
- Runtime ordering/lifecycle is the prefab author's job. For balloons the whole job is one
  Inspector edit: add the `BakedShadow` renderer as **element 0** of `BalloonView`'s
  *Sprite Layer Renderers* array — the per-slot band sorting then places it first (behind
  the other layers, `band+1`) and the existing spawn/`Hide()` loops toggle it with the rest.
- The component is data-only at runtime. It ships as an inert component on the prefab;
  remove it before shipping if that bothers you.
- Exclusions that must stay on live shaders: ToughBalloon's body (its Animator animates
  material properties) and anything `PaintSplashView` drives (`_SpriteScale` at runtime).
