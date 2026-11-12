# Balloon pops

Bakes the balloon pops the card reveal plays (`Assets/Source/Plans/PLAN-CardReveal.md` §5). Each pop
is a cloth simulation in Blender, rendered frame by frame, then packed into one sheet for the game.

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Tools/art/balloon_pop/bake_pops.py -- --seeds 0
python3 Tools/art/balloon_pop/pack_sheets.py
```

The first renders `out/frames/pop_00/00_front.png … 23_back.png`. The second packs them and draws
the review files (`out/` is ignored by git):

| File | What it is |
|---|---|
| `out/sheets/pop_NN.png` | The game's sheet, 2048×3072: the front layer's 48 frames of 256×256 in the top half, 8 across by 6 down, read left to right and top to bottom, and the back layer's in the bottom half in the same order. The balloon's centre is the same pixel in every frame. The empty texels around the rubber hold its surface data bled outward (coverage stays 0), so the game's filtering doesn't bend the edge's normals. Copy it to `Assets/Sprites/UI/CardReveal/` and run Tools ▸ BalloonParty ▸ CardReveal ▸ Build Card Reveal, which imports it as linear data and lists it in the settings |
| `out/review/pop_NN.png` | The contact sheet: the frames at 512 px, numbered, lit from the top left and tinted a balloon red, with a stand-in slip between the layers, on paper |
| `out/review/pop_NN.gif` | The pop playing at 24 fps, in slow motion as the game plays it, under three lights side by side: the day's sun from the top left, a light from the right, and a low orange dusk |

The frames are not pictures but the rubber's surface, for the game to light with the scene's light
(`BalloonParty/Sprite/LitFlipbook`): red and green are the normal's x and y as the camera sees it
(0.5 is flat on to the camera), blue the ambient occlusion, alpha the coverage. The inside of the
rubber is turned to face the camera, so a flap seen from behind lights like its outside. Each frame
comes in two layers, the rubber in front of the balloon's centre and the rubber behind it (split by
the camera's clip planes), so the game can draw the card's ribbons between them and they read as
inside the balloon. `pack_sheets.py` lights the review files with a smooth light (the constants at
its top), enough to judge the tear and the timing; the game cel shades the rubber, so judge its look
in game.

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
4. **The fall.** From the tear the knot falls (`FALL_START`, `FALL_ACCELERATION`), and the cloth
   falls with it under gravity, switched on at the tear and as strong as the knot's fall
   (`CLOTH_FALL_PER_GRAVITY`, measured), so the shreds trail it only as the air holds them back. The
   camera follows the knot, so it stays in the frame, and eases lower once the rubber gathers so the
   rubber stays inside it (`RECENTRE`). The game moves the frames down by the camera's path:
   `CardRevealSettings`' fall and recentre settings mirror these constants (their comments say how).
5. **Rendering** is Cycles, orthographic, on a transparent background, with no colour transform
   (`Raw`), so the values written are the data: an emission shader outputs the normal and the
   occlusion, and there are no lights.

The simulation runs 48 frames at a slow time scale and every one is rendered (`--frames 24` keeps every
second one): 1.6 s at the scene's 30 fps, and the game plays them slower still, at 24 fps (2 s), as a
slow-motion beat that stays smooth. What varies per seed: the puncture, the number of cracks (2 to 4), their length,
wander and branching, how far the rubber shrinks, how stiff it is to bend, and how fast the front
sweeps.

The tuning lives in the constants at the top of `bake_pops.py` (`TIME_SCALE` for how slow the
motion is, `FALL_START`, `FALL_ACCELERATION` and the `RECENTRE_*` for the fall, which the game's settings must match, `NECK_Z` and `CRACK_STOP_Z` for the
pinned neck and where cracks stop) and in `pop_params`, the per-seed ranges.
