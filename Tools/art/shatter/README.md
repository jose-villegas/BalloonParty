# Shatter sheets

Bakes the sprite sheet a balloon's shatter particles draw from: the pieces that fly off when a hard balloon
breaks. It does what used to be done by hand in GIMP: split a picture of the balloon into Voronoi pieces, give
each piece a bit of border so it reads as a plate with thickness, bake a soft shadow under it, and lay the pieces
out one to a cell for the particle system's Texture Sheet Animation.

Every shatter sheet comes from here, cut from the captures in `sources/`:

| Sheet (`Assets/Sprites/Balloons/`) | Pop | Baked with |
|---|---|---|
| `Unbreakable/unbreakable_parts.png` | `PSVFX_UnbreakablePop` | the defaults: 9 plates on 3x3 |
| `Golden/golden_parts.png` | `PSVFX_GoldenPop` | the defaults |
| `Unbreakable/tough_parts.png` | `PSVFX_ToughBalloonPop` | `--pieces 10 --grid 4x3 --diameter 0.73 --swirl 3` |
| `Unbreakable/tougher_parts.png` | `PSVFX_TougherBalloonPop` | as the tough's, from the tougher's capture |

The plates (unbreakable, golden) crack straight; the tough balloons' pieces are swirled, a rubbery tear.

## Making a sheet

1. **Capture the balloon.** In the open Editor, in Play mode, get the balloon on the board (`Tools/stage`'s
   `place`, or the Spawn Balloon cheat), then fill in `capture_balloon.cs`'s placeholders and send it:

   ```bash
   sed "s/__MODEL__/GoldenBalloonModel/; s/__RENDERERS__/Body,Shine*/; s#__OUT__#$PWD/Tools/art/shatter/sources/golden_balloon.png#" Tools/art/shatter/capture_balloon.cs > /tmp/capture.cs
   ```

   ```bash
   unity command script eval --file /tmp/capture.cs --result-only
   ```

   `__MODEL__` is the balloon's model type, or its prefab's name where two balloons share a model (the tough and
   the tougher are both `ToughBalloonModel`: `ToughBalloon`, `TougherBalloon`). It renders only the named renderers (the body and its highlights, not the baked shadow or the knot) on a
   transparent background, at 1024 px a world unit, under neutral noon light whatever the time of day: white,
   from the upper left, with no local lights. So the sheet carries no tint, and the shards' material
   (`BalloonParty/Particle/SceneLit`) lights them at runtime, at dawn, dusk or night, like the balloon beside
   them. Keep the capture in `sources/`, so the sheet can be baked again without the Editor. A balloon whose
   look depends on its state is captured as it looks on the board: the unbreakable's shell is split open,
   showing its joints, beside its neighbours.

2. **Bake the sheet.**

   ```bash
   python3 Tools/art/shatter/make_shatter_sheet.py Tools/art/shatter/sources/golden_balloon.png Assets/Sprites/Balloons/Golden/golden_parts.png --review /tmp/golden_parts_review.png
   ```

   `--pieces` (9) and `--grid` (3, or columns x rows as `4x3`) set how many shards and the cell grid, `--size`
   (2048) the sheet's width, `--diameter` (0.95) the share of a cell the balloon spans, `--swirl` (0) how far
   each piece is whirled (radians at its middle), `--seed` (7) the cut: re-running a seed gives the same shards.
   Bake a sheet a pop already draws with its grid and the balloon's size in it, so the particle system's tiles,
   start frames and start sizes still fit (the table above has each sheet's). `--review` writes the sheet over a flat green so the
   shards, their walls and shadows can be judged. Needs numpy, scipy and Pillow.

3. **Point a particle system at it.** Texture Sheet Animation in Grid mode with the same tiles, a random Start
   Frame over every shard (serialized 0 to 0.9999 of the sheet), and the sheet as the material's texture, on the
   `SceneLit` shader with its light knobs matching the balloon's material. `PSVFX_GoldenPop` is the unbreakable's pop and
   `PSVFX_TougherBalloonPop` the tough's, each with its sheet and material swapped this way; a balloon's catalogue
   entry picks its pop in `_hitVfxOverrides`. The shards' start colour stays white: the sheet carries the
   balloon's colours.

## How the shards are made

- **Split.** Seeds spread over the silhouette and evened out by a few half-steps of Lloyd relaxation (a full
  relaxation turns into a honeycomb). The pixel grid is pushed by a smooth noise field before each pixel finds
  its nearest seed, so the cracks wander a little (`CRACK_WANDER`, `CRACK_SCALE`). A piece smaller than
  `MIN_PIECE_SHARE` of the balloon is merged into its neighbour, and a cut-off bit of a piece joins the closest.
- **Swirl.** With `--swirl`, each piece is whirled about its centroid before anything else is drawn, as
  GIMP's Whirl: it turns most at the middle, easing to none at its farthest point, so its corners stay put and
  its straight edges bend into S-curves. The picture on it turns too, so a highlight swirls with the piece.
- **Border.** Each shard is extruded `WALL_DEPTH` pixels away from the light (`LIGHT_FROM`, the top left like
  the game's baked shadows), in its own colours darkened (`WALL_SHADE`) and greyed a little, so the wall matches
  the plate it came from. The fresh cracks, not the balloon's own outline, catch a thin highlight
  (`EDGE_WIDTH`, `EDGE_LIGHT`).
- **Shadow.** The shard and its wall, blurred (`SHADOW_BLUR`), offset away from the light (`SHADOW_OFFSET`)
  and laid under them at `SHADOW_ALPHA`. The shadow is baked, so it turns with the particle, as the hand-made
  sheets' did.
- **Sheet.** Largest shard first, row by row from the top left (frame 0 is the top-left cell). Each is centred
  on the plate's centroid so it spins about its middle, and clipped to its cell (the script warns if a shadow
  would be cut). The balloon spans `--diameter` (0.95) of a cell, so a particle at the system's largest
  start size draws a shard at the balloon's true size.

The pixel measures are for the balloon drawn about 650 px across (a 2048 sheet of 3x3 at 0.95) and scale with
its drawn size.
