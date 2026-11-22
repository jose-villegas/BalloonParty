# Frame

Stages a shot of the game and takes it apart, for `Docs/frame-composition.md`: every render map, and the
frame redrawn one draw group at a time, at each of a run of frames over the whole shot; and one balloon under a
turning light. The scene is a
text file, so the shot can be changed and retaken in a few minutes. Nothing is left to a bot's luck: the
board, the time of day and the shot are all written down.

```bash
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --preview 400   # film the shot on one sheet
python3 Tools/frame/frame_doc.py Tools/frame/glory.json                 # capture and compose
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --compose       # compose the last capture again
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --publish       # capture, compose, copy to Docs/frame/
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --compose --publish --animations gif   # the same pictures as GIFs
python3 Tools/frame/frame_doc.py Tools/frame/glory.json Tools/frame/sky.json Tools/frame/water.json Tools/frame/paper.json --publish
```

Given several setups, the tool captures and composes each in turn, into `Logs/frame/<name>/`, and joins their
videos one after another into `Logs/frame/reel.mp4`, each frame labelled with its scenario. Only the first
setup is published.

The Unity Editor must be open and out of Play mode; `--project` picks the project it has open (default: the
main checkout). The tool drives `Tools/stage` (`SceneStage` and `FrameBreakdown` in
`Assets/Source/Editor/Staging/`). A full capture takes about fifteen minutes, or about half an hour with every frame of the shot (`detail`). It needs the Unity CLI
(`com.unity.pipeline`) and Pillow, and with `ffmpeg` on the path the animations come out a good deal smaller.

| File | What it is |
|---|---|
| `glory.json` | The scene: what the document's frame is. The meadow at dusk: silver green balloons (3 points each) the lance runs through on its way to the nuke, after the mushrooms arm it |
| `sky.json`, `water.json`, `paper.json` | The same board in the other scenarios, each at its own time of day: in the sky at day, clouds stand in for the mushrooms and a snake of toughs and toughers, their springs between them, for the bushes; in the water at night, full bubble clusters; on the bonus level at dawn, more reward balloons and items. The lance is loaded armed, and the shot is the best-scoring one that pops the nuke; only the bonus level's may pop a golden balloon on its way |
| `frame_doc.py` | Builds the scene, fires the shot, captures the frames, and calls the composer |
| `compose.py` | Lays the captured pictures out as the stills and animations the document shows |

## The setup file

| Key | What it says |
|---|---|
| `scene` | `scenario` (`sky`, `meadow`, `water`, `paper`), `level` (the first level of that scenario from there is dealt), `seed`, `time` (the light's angle, or `dawn`, `day`, `dusk`, `night`), and `strip` (kinds to take off the board: `clouds`, `bushes`, `enemies`, `obstacles`) |
| `legend` | One entry per character of the board: `type` (a `BalloonType`: `Simple`, `Tough`, `Unbreakable`, `Golden`, `Heart`, `Disco`, `Nuke`, ...), and for a balloon that takes them a `color` (`Red`, `Green`, `Blue`, `Purple`) and an `item` (`Shield`, `Laser`, `Lightning`, `Paint`, `Bomb`, `Snipe`). An entry with `actor` (`Bush`, `Puff`) instead stands that in its slot before any balloon is placed, beside the ones the level deals. A `BubbleCluster` with `"full": true` is filled to the merge cap, so it ripens |
| `board` | Rows of text, the board's top row first, one character a slot (the grid is 6 by 11). A space or a dot is an empty slot |
| `shot` | `{"angle": 90}` fires at an angle (0 = right, 90 = straight up), `"lance": true` arms the ball first. `{"chain": true}` asks the game's shot solver for the angle the meadow's mushrooms pay with the lance, and `"then": "nuke"` (or `golden`, `disco`, `heart`) for one whose lance goes on to pop that balloon. `{"best": true, "then": "nuke"}` asks it for the angle that pops that balloon and scores the most on its way (`Tools/stage`'s `best-shot`), with `"lance": true` armed as a lance first, and with `"spare": "golden"` none whose flight pops a golden balloon |
| `moment` | Which frame is taken apart: `{"after": "blast", "frames": 9}` is nine frames into the nuke's blast, found by watching the nuke, since when a shot reaches it differs from deal to deal. For a shot with no nuke, a number of frames after the shot |
| `timeline` | Which frames of the shot are captured; the animations run over them. With a nuke: `flight_every` (a frame every so many from the shot to the hit that lights the fuse, and `flight_after` frames past it), `fuse_every` (a few through the fuse's four seconds), `blast_every` for `blast_frames` (the blast, from its first frame; `moment` must be one of them), `after_every` for `after_frames` (the cloud). Without one: `every` and `frames`. `detail` is a finer cadence for the video: with `"detail": 1` every frame of the shot is captured, the fuse's four seconds included (about 460 of them, a quarter of an hour), so the video runs with no cut and stays smooth when it is slowed, while the animations keep to the frames on the cadences above |
| `closeup` | `around` (a balloon type, `col,row`, or a world point `x;y`) and `size` in pixels: the glory frame's build-up again, cut close. The place is fixed before the shot, so the square stays put if the balloon is popped |
| `light_study` | `around` and `size` as above, and `step` in degrees: that balloon captured as the light goes round, and built up at dawn, day, dusk and night, on a board of its own: the shot is fired on one built afresh after it, as a preview fires on, since the study's game time lets the formations move on |

Three things about a board that the text can't pin down:

- **A level's clouds and bushes take slots of their own.** A slot one of them holds is left alone (the tool
  says which; `strip` takes them off first), and if one stands in the way of the shot the setup asks for,
  the board is dealt again, up to six times. The mushrooms stay where the level puts them. The Editor is
  paused from the deal on and every second of settling is stepped a frame at a time, and the board is dealt
  twice (a game just come into Play mode draws random numbers the second deal no longer does), so a meadow
  setup builds the same board and the same shot every time. A board with moving parts of its own (the
  water's merging clusters) still comes out a little differently from build to build, so the best shot is
  asked for on each.
- **Balloons float.** A balloon with nothing above it rises until it rests, and unbreakables gather into
  their formations, so the board in the picture is the text after the game has settled it.
- **The shot plays for real.** What it pops on its way is popped, and an item it sets off goes off; a lance
  takes a golden balloon with it. The real flight can also part from the one the solver flew: if the shot
  never lights the nuke's fuse, the scene is staged and fired again, up to four times.

## What a capture holds

`Logs/frame/<name>/` (git-ignored):

| Where | What |
|---|---|
| `capture.json` | Each captured frame's part of the shot (`flight`, `fuse`, `blast`, `after`) and label (`shot +24`, `blast +9`), and which one is the glory frame |
| `frames/NNN/` | One captured frame. `<map>-rgb.png` and `<map>-a.png` for every render map (`Tools/stage`'s `maps`), `frame.png` (the main camera), `screen.png` (the Game view, HUD and all), `steps/` (the build-up), and at the glory frame `closeup/` |
| `frames/NNN/steps/` | The frame with one more draw group in each picture, named for the group: `backdrop.png`, `specks.png`, `shadows.png`, `bodies.png`, `shines.png`, `board.png`, `effects.png`, `light.png` (the light quad), all with post-processing off; `post.png` with it on; and `flat.png`, everything drawn under plain white light with the local lights off, the art alone. What the HUD's camera draws (the interface, the score shapes and trails) is in none of them: `screen.png` is that last step |
| `light/` | `angle-NNN.png`, the close-up at each angle of the light, and `dawn/`, `day/`, `dusk/`, `night/`, its build-up at each |
| `preview.png`, `preview/` | `--preview`'s sheet and its frames |
| `doc/` | The composed pictures, which `--publish` copies to `Docs/frame/` |

The build-up is made by `FrameBreakdown.Write`: with the game held, it switches renderers off, renders the
main camera, and switches them back, once per group, all inside one call, so the game never draws a frame
with something missing. A renderer's group comes from its sorting layer, its layer, its shader (the two
full-screen quads, the speck field) and, for a balloon's parts, its name in the prefab. A canvas drawn in the
scene (the card reveal's ribbons) is switched with the rest of the board. The pictures before
`post.png` are written before the tone map, so anything brighter than white is clipped in them.

Beside each step the composer writes what it adds: the difference from the step before, three times brighter
(`ADDED_GAIN` in `compose.py`).

Frames are stepped with `SceneStage.Step`: the Editor is paused and stepped one frame at a time, each 1/50 s
of game time and of real time alike (the capture measures it and writes it down as `frame_seconds`). The pause
matters: a capture takes seconds, and what the game animates on real time (an item's idle spin, the shaders'
clock, a score trail) would run through every one of them if only game time were held. Every count in the
setup's `moment` and `timeline` is in those frames.

Every picture is a file of its own with no text in it, and the document lays them out in Markdown tables,
which name them: so each opens at its full size, and a table can change without a recompose. The stills are of
the glory frame: `frame.jpg` (the Game view), `step-<group>.jpg` and `added-<group>.jpg` (a step and what it
adds), `map-<map>.jpg` with `map-<map>-alpha.jpg` where the alpha says anything, `lens-*.jpg` and `flight.jpg`
(the frame of the flight with the most under the lens), `balloon-<group>.jpg` (the close-up's build-up) and
`balloon-<time>-<part>.jpg` (one balloon at each time of day). The animations run over the whole shot under
the same names, and `balloon-turning` is the balloon under the turning light. A map is enlarged by a whole
number, so its texels stay square blocks.

`--animations` says what the animations are written as: `webp` (the default: animated WebP, full colour,
about a third of the size), `gif` (200 colours, plays on every page host), or `both`. `--publish` points the
document at the type published (at the WebP ones for `both`). With ffmpeg on the path the composer also writes
`composition.mp4`: one high-quality video of one labelled sheet, the frame, its steps (each over what it
adds) and every map with its size, played once at the shot's own speed and then again 2.5 times slower
(`VIDEO_SLOW` in `compose.py`; `--no-video` leaves it out). At some eighty megabytes it is not published: it
stays in `Logs/frame/<name>/doc/`.

A map that is missing in some frames (the effects map only exists while something is alive under a flying
ball's lens) is black there, and a map's alpha is written only if it ever varies. In an animation each frame
is shown for a time that goes by its part of the shot (the flight and the blast a little slower than they
were, the fuse in a few beats).
