# Balloon pops

Bakes the balloon pops the card reveal plays (`Assets/Source/Plans/PLAN-CardReveal.md` §5). Each pop
is a cloth simulation in Blender, rendered frame by frame, then packed into one sheet for the game.

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Tools/art/balloon_pop/bake_pops.py -- --seeds 0
python3 Tools/art/balloon_pop/pack_sheets.py
```

The first renders `out/frames/pop_00/00.png … 23.png`. The second packs them and draws the review
files (`out/` is ignored by git):

| File | What it is |
|---|---|
| `out/sheets/pop_NN.png` | The game's sheet: 24 frames of 256×256, 6 across by 4 down, read left to right and top to bottom. Greyscale with alpha, so the game tints it. The balloon's centre is the same pixel in every frame |
| `out/review/pop_NN.png` | The contact sheet: the frames at 512 px, numbered, tinted a balloon red on paper |
| `out/review/pop_NN.gif` | The pop playing at 30 fps, as the game plays it |

A seed is one pop. `--seeds 0,4,9` bakes several; re-running a seed redraws the same pop. `--detail 5
--size 256 --samples 8` gives a quick look in about 20 s; the defaults (`--detail 6 --size 512
--samples 32`) take about a minute and a quarter a pop. `--blend` also saves each pop's scene to open in
Blender.

## How a pop is made

1. **The balloon** is an icosphere shaped into an egg that gathers into the neck, modelled already
   inflated. Every pop starts from this same rest frame.
2. **The cracks** grow from a random puncture as random walks over the mesh's edges: each keeps
   roughly to its heading, wanders a little, may branch, and stops where it meets another crack or
   reaches the neck. The mesh is cut along them. A loop of cracks that would cut a piece loose is
   stitched back at one edge, so every flap stays attached to the knot.
3. **The release front** sweeps out from the puncture (a Vertex Weight Proximity modifier, keyed every
   frame). Behind it the rubber's stored stretch lets go: the cloth shrinks to about 40% of its size,
   so the flaps snap back toward the neck, which is pinned, and buckle and curl as they gather. A
   Weld after the cloth keeps each crack shut until its sides separate, so the balloon shows no seams
   before it tears.
4. **The fall.** Once the rubber has gathered, the knot drops out of the frame and the shreds trail
   after it, leaving the stage to the slip.
5. **Rendering** is Cycles, orthographic, on a transparent background, lit from the top left like the
   UI's sun, in neutral greys (a soft subsurface body and a sharp clear-coat highlight).

The simulation runs 48 frames at a slow time scale and every second one is kept: 24 frames played at
30 fps, about 0.8 s. What varies per seed: the puncture, the number of cracks (2 to 4), their length,
wander and branching, how far the rubber shrinks, how stiff it is to bend, and how fast the front
sweeps.

The tuning lives in the constants at the top of `bake_pops.py` (`TIME_SCALE` for how slow the
motion is, `FALL_START` and `FALL_ACCELERATION` for the fall, `NECK_Z` and `CRACK_STOP_Z` for the
pinned neck and where cracks stop) and in `pop_params`, the per-seed ranges.
