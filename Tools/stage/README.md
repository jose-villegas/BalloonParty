# Stage

A peeking tool: puts a still scene together in the open Unity Editor, to judge a look without playing for
it. A pop at each time of day, a balloon beside the ones around it, a scenario at night, a shader not yet
merged on a live board. It drives `SceneStage` (`Assets/Source/Editor/Staging/`) through the Unity CLI, one
call per command, so a scene is a handful of commands or a short Python script.

The scene is built to fit what's being looked at, each part a choice: which scenario, level and seed (the
same seed deals the same board), which kinds to take off the board (the balloons, the clouds, the bushes,
the enemies), the time of day, and then exactly what to put on it.

```bash
python3 Tools/stage/stage.py begin --scenario meadow --seed 7 --strip clouds,enemies --time night
python3 Tools/stage/stage.py list                     # what's on the board, by slot
python3 Tools/stage/stage.py place Golden 4,9         # a golden balloon in slot 4,9
python3 Tools/stage/stage.py setup --strip balloons   # an empty board, the scenario's bushes kept
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
| `begin [scene options]` | Play mode, then straight to a held game (no Play button, no ascent), the clock and level-up stopped; then the scene options, as `setup` |
| `setup [--scenario S] [--level N] [--seed N] [--strip KINDS] [--time WHEN]` | Builds the scene, each part optional. `--scenario` (`sky`, `meadow`, `water`), `--level` and `--seed` deal a fresh board: the first level in the scenario from `--level`, on that run seed, so the same options deal the same board. `--strip` takes kinds off it at once, with no pop: `balloons`, `clouds`, `bushes`, `enemies` (deflectors, absorbers, gatekeepers), `obstacles` (everything but the balloons), or a grid actor type. `--time` as `time` |
| `status` | `off`, `launch`, `loading`, or the level, `held` / `running` / `advancing`, and the light's angle |
| `hold` / `release` / `advance S` | Game time off, on, or on for `S` seconds and then held |
| `time WHEN` | The time of day: `dawn` (45), `day` (135), `dusk` (225), `night` (315), or an angle |
| `list` | Every balloon's type, slot and world position, and the grid's size |
| `clear --slots c0,r0:c1,r1` / `clear --around x,y [--radius r]` | Pops the balloons there and lets the pops play out |
| `place TYPE col,row [--item X]` | A balloon of a `BalloonType` in that slot, holding an item; whatever was there is popped first |
| `pop TARGET [--settle S]` | Pops a balloon for real, with its own effect. `TARGET` is a type (the first on the board) or `col,row` |
| `freeze-pop TARGET SECONDS [...]` | The balloon's pop effect stopped `SECONDS` in, from a fixed seed so runs match. `--hide` hides the balloon; `--dx`/`--dy` move the effect off it; `--seed`; `--texture PNG` swaps the shards' sheet and `--start-frame F` their first-frame range (0..1 of the sheet); `--shader NAME` or `--shader-file PATH` (compiled on the spot, so a worktree's shader can be tried before it's merged) and `--set _Name=value` change their material. Swaps go on copies; no asset changes |
| `clear-effects` | Removes the frozen pops and shows the balloons they hid |
| `capture PATH [--around TARGET] [--size N]` | Renders the camera to a PNG: the whole frame, or an `N`-pixel square around a balloon or a world point `x;y` |
| `end` | Clears the frozen pops, lets the game run and leaves Play mode |

Balloons float: a cleared slot is filled from below while the pops play out, so a cleared patch doesn't
stay where it was cleared. Put balloons where they'd rest (against the stack, `list` shows it), and put
frozen effects in open space with `--dx`/`--dy`. `place` into a taken slot pops what's there and spawns in
the same frame, so nothing floats up into it first.

For a scene with several takes, import it:

```python
import sys; sys.path.insert(0, "Tools/stage")
from stage import Stage

stage = Stage()
stage.begin(scenario="sky", seed=7, strip=("balloons", "obstacles"))
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

Captures under `Logs/` stay out of git. Needs the Unity CLI (`com.unity.pipeline`) and, for the contact
sheet, Pillow.
