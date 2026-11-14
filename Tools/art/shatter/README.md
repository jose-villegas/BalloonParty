# Shatter sheets

Bakes the sprite sheet a balloon's shatter particles draw from: the plate pieces that fly off when a hard balloon
breaks. It does what used to be done by hand in GIMP: split a picture of the balloon into Voronoi pieces, give
each piece a bit of border so it reads as a plate with thickness, bake a soft shadow under it, and lay the pieces
out one to a cell for the particle system's Texture Sheet Animation.

The golden reward balloon's sheet (`Assets/Sprites/Balloons/Golden/golden_parts.png`, drawn by
`PSVFX_GoldenPop`) comes from here. The unbreakable's (`unbreakable_parts.png`) is still the hand-made one.

## Making a sheet

1. **Capture the balloon.** In the open Editor, in Play mode, get the balloon on the board (the Spawn Balloon
   cheat) in daylight (the Time of Day cheat), then fill in `capture_balloon.cs`'s placeholders and send it:

   ```bash
   sed "s/__MODEL__/GoldenBalloonModel/; s/__RENDERERS__/Body,Shine*/; s#__OUT__#$PWD/Tools/art/shatter/sources/golden_balloon.png#" Tools/art/shatter/capture_balloon.cs > /tmp/capture.cs
   ```

   ```bash
   unity command script eval --file /tmp/capture.cs --result-only
   ```

   It renders only the named renderers (the body and its highlights, not the baked shadow or the knot), lit by
   the live scene light, on a transparent background, at 1024 px a world unit. Keep the capture in `sources/`,
   so the sheet can be baked again without the Editor.

2. **Bake the sheet.**

   ```bash
   python3 Tools/art/shatter/make_shatter_sheet.py Tools/art/shatter/sources/golden_balloon.png Assets/Sprites/Balloons/Golden/golden_parts.png --review /tmp/golden_parts_review.png
   ```

   `--pieces` (9) and `--grid` (3) set how many shards and the cell grid, `--size` (2048) the sheet, `--seed`
   (7) the cut: re-running a seed gives the same shards. `--review` writes the sheet over a flat green so the
   shards, their walls and shadows can be judged. Needs numpy, scipy and Pillow.

3. **Point a particle system at it.** Texture Sheet Animation in Grid mode with the same tiles, a random Start
   Frame over every shard (serialized 0 to 0.9999 of the sheet; the unbreakable's stops at two thirds), and the
   sheet as the material's texture. `PSVFX_GoldenPop` is the unbreakable's pop with its sheet and material
   swapped this way; a balloon's catalogue entry picks its pop in `_hitVfxOverrides`.

## How the shards are made

- **Split.** Seeds spread over the silhouette and evened out by a few half-steps of Lloyd relaxation (a full
  relaxation turns into a honeycomb). The pixel grid is pushed by a smooth noise field before each pixel finds
  its nearest seed, so the cracks wander a little (`CRACK_WANDER`, `CRACK_SCALE`). A piece smaller than
  `MIN_PIECE_SHARE` of the balloon is merged into its neighbour, and a cut-off bit of a piece joins the closest.
- **Border.** Each shard is extruded `WALL_DEPTH` pixels away from the light (`LIGHT_FROM`, the top left like
  the game's baked shadows), in its own colours darkened (`WALL_SHADE`) and greyed a little, so the wall matches
  the plate it came from. The fresh cracks, not the balloon's own outline, catch a thin highlight
  (`EDGE_WIDTH`, `EDGE_LIGHT`).
- **Shadow.** The shard and its wall, blurred (`SHADOW_BLUR`), offset away from the light (`SHADOW_OFFSET`)
  and laid under them at `SHADOW_ALPHA`. The shadow is baked, so it turns with the particle; the unbreakable's
  does the same.
- **Sheet.** Largest shard first, row by row from the top left (frame 0 is the top-left cell). Each is centred
  on the plate's centroid so it spins about its middle, and clipped to its cell (the script warns if a shadow
  would be cut). The balloon spans `DIAMETER_SHARE` (0.95) of a cell, so a particle at the system's largest
  start size draws a shard at the balloon's true size.

The pixel measures are for a 2048 sheet and scale with `--size`.
