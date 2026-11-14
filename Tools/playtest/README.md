# Playtest bot

Plays BalloonParty in a running Unity Editor and writes a report of how the runs went, so a tuning
change can be measured rather than guessed. It drives the Editor through the Unity CLI and its
Pipeline package (`com.unity.pipeline`), and reads the game's own telemetry for the numbers.

```bash
python3 Tools/playtest/playtest_bot.py --seconds 240 --label reload-2s
python3 Tools/playtest/playtest_bot.py --seconds 480 --skill low --label skill-low
```

## Two players

| `--player` | How it plays | Knobs |
|---|---|---|
| `human` (default) | A person, at a skill level. Once the cannon is ready it waits a reaction time, a floor plus an exponential wait, so its shots arrive as a Poisson process, then fires the solver's best angle plus a normal aim error, or now and then a wild shot at any angle, unaimed | `--skill` picks the preset; `--react-floor`, `--react-mean` (the exponential's mean), `--aim-sd` and `--wild` override one knob of it |
| `best` | The best-shot solver, fired the moment the cannon is ready: an upper bound on how well the game can be played | none |

| `--skill` | Reaction floor | Reaction mean | Aim error sd | Unaimed shots |
|---|---|---|---|---|
| `low` | 0.4 s | 1.2 s | 10 degrees | 20% |
| `average` (default) | 0.25 s | 0.6 s | 4 degrees | 5% |
| `high` | 0.15 s | 0.25 s | 1.5 degrees | none |

The presets are a starting guess, not fitted to real players. Every skill should lose sometimes; what
a tuning changes is how often each one loses and what leads to it, so run the skills against the same
build and compare. Change one knob at a time, and compare against a `best` session to see how much of
a level's difficulty is aim and how much is pace.

Two options set up the first run, for measuring what only comes late or to a player in trouble:

| Option | What it does |
|---|---|
| `--purple` | Once the first run is in play, restarts it on the first level its seed makes purple (one more colour, the golden balloon's level), through the Start From Level cheat with the seed kept, so the same roll holds. The run's first moments, before the restart, are on level 1 |
| `--hearts N` | Once that run is in play, sets the player's hearts to N, so the session starts low on hearts (where the heart balloon comes). A later run (`--on-loss restart`) starts as configured |

```bash
python3 Tools/playtest/playtest_bot.py --purple --seconds 300 --label purple
python3 Tools/playtest/playtest_bot.py --hearts 1 --seconds 420 --label one-heart
```

It needs the Unity CLI (`unity`, on `PATH` or in `~/AppData/Local/Unity/bin`) and the Editor open
on the project with the Pipeline package installed (`unity pipeline install`). `--project` picks
the project if you run it from another checkout: the Editor plays the code of the checkout it has
open, not yours.

## On a game over

`--on-loss` says what the bot presses on the game-over screen. It presses once the screen is up and
pressable (after the loss cinematic, as a finger would), and plays on until the time is up.

| `--on-loss` | Presses |
|---|---|
| `stop` (default) | Nothing: the session ends at the first game over |
| `restart` (or `--restart`) | Restart: a new run from level 1 |
| `retry` | Retry, replaying the level with the hearts refilled, while the run has a retry: the free ones (`RunConfig.RetriesBeforeAd`), then the ones behind a rewarded ad. Restart once none is left |
| `bonus` | The bonus retry while the game offers it: a rewarded ad that buys a card for the retried level. Otherwise as `retry` |

A press that stages nothing (an ad skipped or not shown: the screen comes back) steps down for that loss: a
failed bonus retry is followed by a plain retry, a failed retry by Restart. The report lists such a press but
doesn't count it.

There is no ad SDK yet (`NoAdService`), so in the Editor every ad counts as watched, at once: a
`retry` or `bonus` session is a player who always watches the ad. The report counts the ads and lists
every loss with its answer.

```bash
python3 Tools/playtest/playtest_bot.py --on-loss bonus --seconds 600 --label ads
```

`device_bot.py` only knows `--restart`: the device autoplay never retries.

## What it does

1. Keeps the Editor ticking while unfocused (`editor set_auto_tick`, `set_run_in_background`) and
   enters Play mode.
2. Sends `bot_tick.cs` to the Editor once per tick (`unity command eval`, about 0.6 s each) until
   `--seconds` of wall time have passed. The first tick installs the shooter on the Editor's update
   loop: it checks the cannon every 50 ms and fires as the chosen player, so a shot's timing isn't
   rounded to the tick. The shooter aims with the game's best-shot solver (the **Fire Best Shot**
   cheat) and removes itself when Play mode ends, a newer session replaces it, or ticks stop coming
   for 10 s. If Play mode starts on the launch screen (the Editor has the Launcher scene open), a tick
   taps its Play button the way a finger does, after 1.5 s on the screen (the device autoplay's
   `AutoplayTap`), so the press and the ascend play as they would for a player. What a game over gets is
   `--on-loss` (below); by default the session ends there, so each one is a single run: how long it lasted
   and what lost it.
3. Leaves Play mode, which flushes the telemetry still buffered (`--keep-playing` skips this).
4. Reads the telemetry records written since it started (`JsonLinesTelemetrySink`, under
   `Application.persistentDataPath/telemetry/`) and writes the report.

## The report

`Logs/playtest/<stamp>[-label]/` (git-ignored) gets `ticks.jsonl` (one line per tick: level, score,
hearts, shots so far, board fill, balls in flight, seconds left in the spawn clock's
breather, and the reward balloons placed so far) and `report.md`:

| Section | From | Says |
|---|---|---|
| Header | `ProjectileFlightConfig.asset`, the arguments | The reload and ball-cap values, and the player, the session ran with |
| Reward balloons | The ticks, the telemetry | How many golden and heart balloons were placed in the session, counted on the shooter's 50 ms loop (the reward service's on-board flags), and popped (the telemetry's pops by balloon type) |
| Runs | The ticks | Each run's outcome (game over or still playing), highest level, score, hearts left, shots and length, its retries (how many were bonus retries, how many asked for an ad), and how many runs ended in a game over. A retry doesn't end the run |
| Losses | The ticks | With `--on-loss retry` or `bonus`: every game over, the level it came on and how the bot answered it |
| Hearts lost | The ticks | Every heart lost: run, level, seconds into the level, hearts left, and the lead-up (the 10 s before it): board fill just before and on average, shots per 10 s, balls in flight |
| Levels | Telemetry `level` records | Seconds, shots, pops, pops per shot, hearts lost and max streak, per level |
| Board fill | The ticks | Per level, the share of open slots holding a balloon while playing (`IBoardFill.Occupancy`): mean, median, 90th percentile, max, and how often it ran at 0.9 or more. A heart is lost only when a new line finds no room, so this is how close each level came to costing one |
| Release and tension | The ticks | Release is play inside a spawn-clock breather (`ISpawnClock.BreatherSecondsLeft` above 0: a big clear slowed the lines), tension is play between breathers. How many releases, the share of play spent in them, how long release and tension stretches last (tension ended by a release counted apart from tension a level-up cut), and the board fill in each, overall and per level. Missing when the ticks carry no breather field |
| Flights | Telemetry `flight` records | How long a ball stays in the air: mean, median, middle half, 90th percentile |

## Reading it

Neither player is a real person: `best` plays far better than anyone, and `human` is a rough model.
Compare sessions with each other, not with a person's feel: if a change makes the bot's levels take
longer or cost hearts, it made the game harder. A level only writes its record when it ends, so
the level in play when the session stops is missing from the table.

## On emulators and phones

`device_bot.py` runs the same players on Android devices instead, several sessions at once, one per device. A
checked build plays itself when launched with an autoplay spec (`Assets/Source/Cheats/README.md`, Autoplay):
the runner installs the APK, launches it with the player's knobs, waits for the session's `done` file, pulls
its ticks and telemetry with adb and writes the same report, one folder per session.

```bash
python3 Tools/playtest/device_bot.py --boot 3 --apk Builds/local/BalloonParty-autoplay.apk --seconds 420 --label meadow average high best
```

- It needs a checked APK (Managed Code Variant Checked or Debug). Unity 6.6 builds arm64 only, so the
  emulator runs it through the Google APIs image's ARM translation.
- The emulator and its Android 15 Google APIs x86_64 image live in `%LOCALAPPDATA%\Android\Sdk` (installed
  with `sdkmanager` from Unity's bundled command-line tools), with the AVD `bp-autoplay` (1080x2400, 4 cores,
  3 GB). `--boot N` starts emulators until N run, each on its own AVD: `bp-autoplay` on port 5554, then
  `bp-autoplay-2`, `-3`, ... on 5556, 5558, ..., made on first use as copies of the first's config over the
  same system image. (Two read-only instances of one AVD would share its disk, but the second crashes as it
  boots on emulator 37.2.) They need hardware acceleration (WHPX, on with Hyper-V), and about 3 GB of free
  memory each. Each instance's output goes to `Logs/playtest/emulator-<port>.log`.
- With no `--serial`, it plays on every running emulator and never on a connected phone.
- An emulator renders on the host GPU, but slower than a phone: a slow frame stretches game time against
  wall time. Compare emulator sessions with each other, not with the Editor's.

## Files

| File | What it is |
|---|---|
| `playtest_bot.py` | The runner: Editor setup, the tick loop, the report |
| `device_bot.py` | The device runner: emulators, adb, one session per device in parallel, the same report |
| `bot_tick.cs` | One tick, compiled by the Pipeline package on each call, with the `__PLACEHOLDERS__` filled in by the runner. Outside `Assets/`, so not part of the Unity project; it reaches the game's internal types by reflection. The shooter's state between ticks lives in `SessionState` (`BalloonPartyBot.*`) |
