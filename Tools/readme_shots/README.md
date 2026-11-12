# README shots

Retakes the stills on the front page (`README.md`): each scenario at morning and at night, and each item
at the moment its effect is fullest. Nothing is chosen by hand on a run; the script plays the same steps
every time, so after a visual change you rerun it and commit the new grids.

```bash
python3 Tools/readme_shots/capture.py            # capture, then compose into Docs/readme/
python3 Tools/readme_shots/capture.py --compose  # compose the last capture again, no Editor needed
python3 Tools/readme_shots/capture.py --burst    # each item at a run of moments, to retune its timing
```

The Unity Editor must be open on the main checkout (it only sees committed, merged code) and out of Play
mode. The script enters Play mode itself, waits for the driver to finish, and leaves Play mode. It needs
the Unity CLI (`com.unity.pipeline`) and Pillow.

| Part | Does |
|---|---|
| `Assets/Source/Editor/ReadmeShotCapture.cs` | The driver, run in Play mode. Taps Play on the launch screen, stops the clock and the level-up, then for each item restarts at level 3 (a mixed board), pops a balloon carrying it in the middle of the board (Shield and Snipe arm the ball, which is then fired), and holds the world still to take the shot. For each scenario it jumps there, sets the clock to morning and to night, and takes the board. Holds time through `ITimeScaleClaims` (`TimeScaleSource.Capture`). Writes `done.txt` with `ok` or the error |
| `capture.py` | Starts the driver through the CLI, waits on `done.txt` with a deadline, and composes the shots |

Output:

| Where | What |
|---|---|
| `Logs/readme_shots/` (git-ignored) | The raw shots, at the Game view's size: `scene_<sky\|meadow\|water>_<morning\|night>.png`, `item_<name>.png` (`item_<name>_<seconds>.png` with `--burst`) |
| `Docs/readme/scenarios.jpg` | Sky, Meadow and Water across, morning above night |
| `Docs/readme/items.jpg` | The six items, three across, each labelled |

To retune an item, run `--burst`, look at its shots in `Logs/readme_shots/`, and set its moment in the
driver's `Items` table. The other timings (how long a jump settles, how long the light takes to change)
are constants at the top of the driver.
