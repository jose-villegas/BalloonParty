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
| `out/sheets/pop_NN.png` | The game's sheet, 2048×3072: the front layer's 48 frames of 256×256 in the top half, 8 across by 6 down, read left to right and top to bottom, and the back layer's in the bottom half in the same order. The balloon's centre is the same pixel in every frame. The rim's texels, the rubber seen edge on, take the surface data of the solid rubber just inside (where a pixel mixes faces turned toward the camera and away, the back layer's flipped normals jump between lit and shadow and the occlusion dips, which the game's cel steps would draw as a sawtooth round the outline); a thin flap more than two texels from solid rubber keeps its own. The empty texels around the rubber hold the data bled outward (coverage stays 0), so the game's filtering doesn't bend the edge's normals. Then the coverage is blurred a little (`EDGE_BLUR`), so the outline is soft at the size the game draws it and the two layers' outlines meet without a seam. Copy it to `Assets/Sprites/UI/CardReveal/` and run Tools ▸ BalloonParty ▸ CardReveal ▸ Build Card Reveal, which imports it as linear data and lists it in the settings |
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

A seed is one pop. The game has eight: seeds 0, 12, 18, 19, 20, 24, 25 and 27. Each is punctured on the
balloon's front, where the pushpin can reach it, and its falling rubber stays inside the frame, but for a few
thin strands the border fade takes; most seeds' falls trail whole flaps out past the frame's top or sides,
which would show as a straight cut (measure a new one before keeping it). `--seeds 0,4,9` bakes several; re-running a seed redraws the same pop. `--detail 5
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

The simulation runs 48 frames at a slow time scale and every one is rendered: 1.6 s at the scene's
30 fps, and the game plays them slower still, at 24 fps (2 s), as a slow-motion beat that stays smooth.
(`--frames 24` renders every second one, for a quick look; `pack_sheets.py` packs 48.) What varies per
seed: the puncture, the number of cracks (2 to 4), their length, wander and branching, how far the
rubber shrinks, how stiff it is to bend, and how fast the front sweeps (5 to 8 frames: a hard snap).
The bake ends by printing the settings `CardRevealSettings` needs to match it: the fall, and each
pop's puncture in its first frame (`Pop Punctures`, where the game's pushpin pricks), with the side
of the balloon it is on.

The tuning lives in the constants at the top of `bake_pops.py` (`TIME_SCALE` for how slow the
motion is, `FALL_START`, `FALL_ACCELERATION` and the `RECENTRE_*` for the fall, which the game's settings must match, `NECK_Z` and `CRACK_STOP_Z` for the
pinned neck and where cracks stop) and in `pop_params`, the per-seed ranges.

## The pushpin

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P Tools/art/balloon_pop/bake_pin.py
```

`bake_pin.py` bakes the pushpin that pricks the card reveal's balloon, straight to
`Assets/Sprites/UI/CardReveal/pin.png` (`-- --out PATH` to write it elsewhere). It takes the pops'
surface material and scene from `bake_pops.py`, so the pin is surface data too, lit in game by the
same shader as the rubber. A map pin's head and needle are turned on a lathe from the profiles at the
script's top, about a third of the balloon's height, and held at the angle it jabs in at: leant
toward the camera (`TILT_TOWARD`) and to the right (`LEAN`). The game tints the head and the needle
apart, so each is rendered on its own, the needle cut where the head covers it, into the two cells of
the sheet's top half (512×512: the head on the left, the needle on the right; the bottom half, a pop
sheet's back layer, is empty), bled outward as the pops are. It takes a few seconds, and prints the
settings `CardRevealSettings` needs to match it: where the needle's tip is in a cell (`TIP`), the
cell's size in pop frame heights (`PIN_VIEW` over the pops' `ORTHO_SCALE`) and the lean.

## The shreds

```bash
python3 Tools/art/balloon_pop/make_shreds.py
```

`make_shreds.py` draws the scraps the reveal flings far out as the balloon tears (the frames can't
carry rubber that far): four torn, curled strips on a 256×256 sheet, lit and shadowed along their
curl, in near-white greys so the particles' colour tints them. Seeded, written straight to
`Assets/Sprites/UI/CardReveal/shreds.png`.
