# SpriteCombine

Editor half of `SpriteLayerCombiner` (`Shared/Rendering/`): flattens a prefab's rigid
sprite layers into one baked sprite to cut per-balloon draws and overdraw. Scoped to
the colored normal balloons.

## Workflow

1. Open the prefab, add `SpriteLayerCombiner` on the node that will host the combined
   renderer (the baked sprite's pivot lands on that transform).
2. Assign the layers to flatten. Group by tint behavior:
   - **Runtime-tinted group** (Knot + Body — same `ColorableRenderer` palette color):
     `Neutralize Tint` ON, so the bake is white and the combined renderer takes the
     runtime tint. Only group layers that share one tint.
   - **Fixed overlays** (Shine + Specular): `Neutralize Tint` OFF — authored colors
     bake in.
   One prefab can host several combiners; distinguish outputs via `Output Suffix`.
3. Bake → `Assets/Sprites/Baked/Combined/<prefab path>/<Prefab>_<suffix>.png`, pivot
   pre-positioned. Wiring is manual: assign the sprite, disable/remove the source
   layers, update `BalloonView._spriteLayerRenderers` / `_colorableRenderers`.

## Constraints

- Layers must be rigid relative to each other — anything independently animated
  (Tough's `ShinePivot`, item icons, shield overlays) stays live. The base balloon's
  `StableIdle` only animates the shared "Ballon" child, so its five layers qualify.
- Batching only materializes when the combined sprites (across all balloon prefabs)
  share **one SpriteAtlas and one material**, and tinting stays `SpriteRenderer.color`
  (a MaterialPropertyBlock would break the batch again).
- Measure with the Frame Debugger before/after — the flattening only pays off if the
  draw count actually moves.
