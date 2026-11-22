# Stage

A peeking tool: puts a still scene together in the open Unity Editor, to judge a look without playing for
it. A pop at each time of day, a balloon beside the ones around it, a scenario at night, a shader not yet
merged on a live board. It drives `SceneStage` (`Assets/Source/Editor/Staging/`) through the Unity CLI, one
call per command, so a scene is a handful of commands or a short Python script.

The board starts empty, with no balloon unless one is placed: the run's own board and the lines that keep
coming are held off. The rest is built to fit what's being looked at, each part a choice: which scenario,
level and seed (the same seed deals the same obstacles), which of them to take off (the clouds, the bushes,
the enemies), the time of day, and then exactly what to put on it.

```bash
python3 Tools/stage/stage.py begin --scenario meadow --seed 7 --strip clouds,enemies --time night
python3 Tools/stage/stage.py list                     # what's on the board, by slot
python3 Tools/stage/stage.py place Golden 4,9         # a golden balloon in slot 4,9
python3 Tools/stage/stage.py setup --scenario water --strip enemies   # the water level's obstacles, its enemies off
python3 Tools/stage/stage.py freeze-pop Golden 0.2 --hide
python3 Tools/stage/stage.py capture Logs/stage/golden-night.png --around Golden
python3 Tools/stage/stage.py end                      # clear up, let the game run, leave Play mode
```

The Editor must be open on the main checkout: it only sees committed, merged code. Run from a worktree, the
tool still talks to the Editor on the main checkout (`--project` picks another). `begin` enters Play
mode itself and keeps the game ticking while the Editor is in the background. Between commands the game
is **held**: time scale 0, the clock and the level-up stopped. So the board stays exactly as it was put,
and only `advance`, `clear`, `pop --settle` and dealing a board let game time run, for as long as they need.

| Command | Does |
|---|---|
| `begin [scene options]` | Play mode, then straight to a held, empty game: no Play button or ascent, no balloon unless placed, the clock and level-up stopped (a run already under way loses its balloons); then the scene options, as `setup` |
| `setup [--scenario S] [--level N] [--seed N] [--strip KINDS] [--time WHEN]` | Builds the scene, each part optional. `--scenario` (`sky`, `meadow`, `water`, or `paper`, the next bonus level), `--level` and `--seed` deal a fresh board, its ground and obstacles but no balloons: the first level in the scenario from `--level`, on that run seed, so the same options deal the same board. `--strip` takes kinds off it at once, with no pop: `balloons` (placed ones), `clouds`, `bushes`, `enemies` (deflectors, absorbers, gatekeepers), `obstacles` (everything but the balloons), or a grid actor type. `--time` as `time` |
| `status` | `off`, `launch`, `loading`, or the level, `held` / `running` / `advancing`, and the light's angle |
| `hold` / `release` / `advance S` | Game time off, on, or on for `S` seconds and then held |
| `time WHEN` | The time of day: `dawn` (45), `day` (135), `dusk` (225), `night` (315), or an angle |
| `list` | Every balloon's type, slot and world position, and the grid's size |
| `clear --slots c0,r0:c1,r1` / `clear --around x,y [--radius r]` | Pops the balloons there and lets the pops play out |
| `place TYPE col,row [--item X]` | A balloon of a `BalloonType` in that slot, holding an item; whatever was there is popped first |
| `place-actor TYPE col,row` | A bush (`Bush`) or a cloud (`Puff`) in that empty slot, as the level's deal would stand one. Not a mushroom: its caps belong to the walls the seed picks |
| `bubbles col,row [--count N]` | Fills the bubble cluster in that slot with `N` bubbles, or up to the merge cap: a full cluster, as merges make one, which the board ripens on its next tick, and its fuse starts |
| `pop TARGET [--settle S]` | Pops a balloon for real, with its own effect. `TARGET` is a type (the first on the board) or `col,row` |
| `freeze-pop TARGET SECONDS [...]` | The balloon's pop effect stopped `SECONDS` in, from a fixed seed so runs match. `--hide` hides the balloon; `--dx`/`--dy` move the effect off it; `--seed`; `--texture PNG` swaps the shards' sheet and `--start-frame F` their first-frame range (0..1 of the sheet); `--shader NAME` or `--shader-file PATH` (compiled on the spot, so a worktree's shader can be tried before it's merged) and `--set _Name=value` change their material. Swaps go on copies; no asset changes |
| `level-up-ready` | Readies a real level-up for the next shot: every balloon on the board turns the level's first colour, every other colour's bar fills, that one is left a point short, and the level-up is let through. `release`, then fire the Fire Best Shot cheat, and the level-up cinematic plays out for real, zoom and all |
| `fire DEGREES [--lance]` | Fires the ball loaded in the thrower for real, at an angle (0 = right, 90 = straight up), as the Fire Best Shot cheat does: it flies, bounces, hits and scores once game time runs (`advance`). Its answer says where it was fired from, to aim by. With `--lance` it is armed as a Snipe lance first, so it plows the toughs in its way and shatters them at the next wall |
| `cut COL,ROW DEGREES [--offset F]` / `regrow COL,ROW` | Cuts the cap off the mushroom in a slot as a lance flying at `DEGREES` would (0 = right, 90 = straight up), through the cap's middle or `F` world units to the left of it, which takes a sliver off near the edge; `regrow` grows it back now. It is the cut a lance makes in play: a ball flies through where the cap stood, and the cap grows back by itself on the fifth of the board's beats (between 6.8 and 8.5 seconds of game time), so film a bare stem within that. Let game time run (`advance`) to see the pieces fall. For a blast, set a real bomb off beside the mushrooms: `place Simple COL,ROW --item Bomb`, then `pop COL,ROW`; the caps next to it are blown off and the ones further out sway, as in play (the bomb's cloud covers them for the first 0.4 seconds) |
| `aim DEGREES` / `aim off` | Holds the thrower's aim at an angle and draws its aim line, without firing: a capture then shows the line the game predicts, its bend off a mushroom included. `fire` lets go too |
| `chain` | On a meadow board with mushrooms: the angle to pass to `fire` so the ball crosses between the walls of caps three times and earns the lance, found by flying every aim angle through the shot solver on the board as it stands; it is the middle of the widest run of angles that do. Its answer also says how many angles arm the lance, how many whole caps stand in how many walls, and where the cannon stands. A ball that is loaded as a lance already (a Sharpshooter roll) is an error: a lance bounces off no mushroom and cuts their caps, so fire it away first to film the arming |
| `best-shot THEN [--lance]` | On a board with no mushrooms: the angle to pass to `fire` whose flight pops a balloon of the kind `THEN` names (`nuke`, `golden`, `disco`, `heart`) and scores the most on its way, of those whose neighbouring angles pop it too (`--margin N`: that many on each side, default 1; a board whose balloons float on after the first pops needs more), found by flying every aim angle through the shot solver. `--spare golden` (or another kind) passes over the angles whose flight pops one of those too. With `--lance` the loaded ball is armed as a lance first; fire it without `--lance` then, as it is armed already |
| `ball x,y [--from x,y] [--seconds S] [--shields N]` | A ball in flight at a world point, held still, with the effects above the board cleared around it, as wide as its `N` shields (default 1) make the clearing. With `--from`, it flies there over `S` seconds of game time first (default 0.5), so the clearing it leaves behind has closed up as it would in a shot |
| `clear-effects` | Removes the frozen pops and the staged balls, and shows the balloons the pops hid |
| `capture PATH [--around TARGET] [--size N]` | Renders the camera to a PNG: the whole frame, or an `N`-pixel square around a balloon or a world point `x;y` |
| `maps DIR [--title T] [--no-sheet]` | Writes every render map the Game Render Maps window lists (scene capture, disturbance field, light field and its colour map, GI light buffer, background field, smoke field, and the effects maps once a ball has flown) into `DIR` as it stands this frame: `<map>-rgb.png` for its colour channels and `<map>-a.png` for its alpha as grey, at the map's own resolution, plus the frame as `frame.png` and all of them side by side as `sheet.png`. A map brighter than 1 is scaled down to fit and the sheet says by how much; maps that don't exist yet or are empty are named under the heading |
| `end` | Clears the frozen pops and leaves Play mode, giving the game back its clock, level-ups and balloons (leaving Play mode any other way does too) |

Balloons float: a cleared slot is filled from below while the pops play out, so a cleared patch doesn't
stay where it was cleared. Put balloons where they'd rest (against the stack, `list` shows it), and put
frozen effects in open space with `--dx`/`--dy`. `place` into a taken slot pops what's there and spawns in
the same frame, so nothing floats up into it first.

For a scene with several takes, import it:

```python
import sys; sys.path.insert(0, "Tools/stage")
from stage import Stage

stage = Stage()
stage.begin(scenario="sky", seed=7, strip="obstacles")
stage.place("Unbreakable", (2, 0))
for when in ("dawn", "day", "dusk", "night"):
    stage.time_of_day(when)
    stage.freeze_pop("Unbreakable", 0.15, dy=0.9)
    stage.capture(f"Logs/stage/unbreakable-{when}.png", around="Unbreakable")
    stage.clear_effects()
stage.end()
```

`contact_sheet.py` lays captures out in a labelled grid, one row per take and one column per moment:

```bash
python3 Tools/stage/contact_sheet.py out.png --cols dawn day dusk night --row "hand-made" 'Logs/stage/old-{}.png' --row baked 'Logs/stage/new-{}.png'
```

A mushroom chain, filmed: deal a meadow level (the stretch starts with 2 walls of caps and grows to 4), ask for the
angle, show the aim line, fire, and capture as game time advances:

```bash
python3 Tools/stage/stage.py begin --scenario meadow --level 7 --seed 7 --strip enemies
python3 Tools/stage/stage.py chain                  # angle 79.333 (the middle of 8 in a row; ...)
python3 Tools/stage/stage.py aim 79.333 && python3 Tools/stage/stage.py capture Logs/stage/chain-00.png
python3 Tools/stage/stage.py fire 79.333
python3 Tools/stage/stage.py advance 0.05 && python3 Tools/stage/stage.py capture Logs/stage/chain-01.png
```

To see what a look is made of, `maps` writes the render maps behind a staged frame. Take it once per state
(a light on and off, night and day) and the sheets compare:

```bash
python3 Tools/stage/stage.py maps Logs/stage/maps-night --title "Night, ring light on"
python3 Tools/stage/map_sheet.py Logs/stage/maps-night --title "Night, ring light on"   # the sheet again, from the PNGs
```

What each channel of a map means is in `Assets/Source/Editor/Maps/README.md` and the window's tooltips.

Captures under `Logs/` stay out of git. Needs the Unity CLI (`com.unity.pipeline`) and, for the contact
sheet and the map sheet, Pillow.
