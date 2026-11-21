# README shots

Retakes the pictures on the front page (`README.md`): each scenario and the bonus level in play, at morning
and at night; each item at the moment its effect is fullest; and each gift balloon at night, as a looping GIF.
Every picture is a file of its own with no label in it: the README lays them out in Markdown tables, so each
opens at its full size. Nothing is chosen by hand on a run; the script plays the same steps every time, so
after a visual change you rerun it and commit the new pictures.

```bash
python3 Tools/readme_shots/capture.py            # capture, then compose into Docs/readme/
python3 Tools/readme_shots/capture.py --compose  # compose the last capture again, no Editor needed
python3 Tools/readme_shots/capture.py --compose --raw DIR  # the same, from raw shots in another folder
python3 Tools/readme_shots/capture.py --burst    # each item at a run of moments, to retune its timing
```

The Unity Editor must be open on the main checkout (it only sees committed, merged code), on the Launcher
scene and out of Play mode. The script enters Play mode itself, waits for the driver to finish, and leaves
Play mode. A capture takes about seven minutes. It needs the Unity CLI (`com.unity.pipeline`) and Pillow.

| Part | Does |
|---|---|
| `Assets/Source/Editor/ReadmeShotCapture.cs` | The driver, run in Play mode. Taps Play on the launch screen and stops the clock, then takes the items, films the gifts and plays the scenarios (below). Holds time through `ITimeScaleClaims` (`TimeScaleSource.Capture`). Writes `moments.txt`, then `done.txt` with `ok` or the error |
| `capture.py` | Starts the driver through the CLI, waits on `done.txt` with a deadline, prints the moments the stills were taken at, and composes the pictures |

What the driver takes:

| Pictures | How |
|---|---|
| Items | For each item it restarts at level 3 (a mixed board) with the level locked, pops a balloon carrying the item in the middle of the board (Shield and Snipe arm the ball, which is then fired), and holds the world still to take the shot at the item's moment |
| Gift balloons | For each gift it restarts at level 2, swaps the middle balloon for the gift (`SceneStage.Place`) and films it at night, when its rays and its own light show best: 48 squares cut out round it from a camera render (`SceneStage.Capture`, so with no HUD), one every 0.06 s of game time |
| Scenarios | See Glory shots |

## Glory shots

A scenario's still is taken from play, not from a board that has just been dealt. For each of sky, meadow,
water and the bonus level's page, the driver jumps to the first level of a fixed run (`PlaySeed`) with that
backdrop, unlocks the level so the score counts and the bars fill, and lets a bot play it for up to 30 seconds
of game time: the solver's best shot whenever the cannon is ready, no sooner than 0.4 s after the last.

While it plays the driver watches how much is going on: a moment's *glory* is the balloons that left the
board in the last 0.4 s plus the balls in flight. Each time a moment beats every one before it (and reaches
`MinGlory`), the driver waits a quarter of a second for the burst's shapes and smoke to fill out, holds the
world, takes it at morning and at night over the level's last still, and plays on. The still left at the end
is the level's best moment. A burst that leaves fewer than `MinOnBoard` balloons (a nuke's blast nearly done)
is passed over, since a still wants a board in it, and so is anything in the first three seconds, while the
level is still being dealt. The play ends early if the level is completed or the run
is lost; a level with no burst big enough gets the board as the bot leaves it.

Every frame of the play lasts one physics step whatever the real frame took (`Time.captureDeltaTime`), so
the game runs at its own pace while a still is written. The run's seed is fixed, so every retake plays the
same levels, but the play is not the same game twice: with the seed, the step and the bot's timing all
fixed, two runs still came out different (measured 2026-11-20), so each retake picks its own moments.

## Output

| Where | What |
|---|---|
| `Logs/readme_shots/` (git-ignored) | The raw shots: `scene_<sky\|meadow\|water\|bonus>_<morning\|night>.png` and `item_<name>.png` (`item_<name>_<seconds>.png` with `--burst`) at the Game view's size, `gift_<golden\|heart\|disco\|nuke>_night_<NN>.png`, and `moments.txt`: each still taken, with its glory and when in the level |
| `Docs/readme/scene_<scenario>_<morning\|night>.jpg` | A scenario's still at the Game view's size |
| `Docs/readme/item_<name>.jpg` | An item's still at the Game view's size |
| `Docs/readme/gift_<name>.gif` | A gift balloon, 280 pixels square, 2.4 s on a loop: the film's last pictures melt into its first, so the loop has no jump. One palette for its frames and no dithering, which keeps each near 1.4 MB |

To retune an item, run `--burst`, look at its shots in `Logs/readme_shots/`, and set its moment in the
driver's `Items` table. The other numbers (how long the bot plays, what counts as glory, how long the light
takes to change, the gift film's length) are constants at the top of the driver; the GIFs' size and
frame time are at the top of `capture.py`.
