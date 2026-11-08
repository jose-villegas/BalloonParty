# Playtest bot

Plays BalloonParty in a running Unity Editor and writes a report of how the runs went, so a tuning
change can be measured rather than guessed. It drives the Editor through the Unity CLI and its
Pipeline package (`com.unity.pipeline`), and reads the game's own telemetry for the numbers.

```bash
python3 Tools/playtest/playtest_bot.py --seconds 240 --label reload-2s
```

## Two players

| `--player` | How it plays | Knobs |
|---|---|---|
| `human` (default) | An average player. Once the cannon is ready it waits a reaction time, a floor plus an exponential wait, so its shots arrive as a Poisson process, then fires the solver's best angle plus a normal aim error | `--react-floor` (0.25 s), `--react-mean` (0.6 s, the exponential's mean), `--aim-sd` (4 degrees) |
| `best` | The best-shot solver, fired the moment the cannon is ready: an upper bound on how well the game can be played | none |

The `human` defaults are a starting guess, not fitted to real players. Change one knob at a time and
compare against a `best` session to see how much of a level's difficulty is aim and how much is pace.

It needs the Unity CLI (`unity`, on `PATH` or in `~/AppData/Local/Unity/bin`) and the Editor open
on the project with the Pipeline package installed (`unity pipeline install`). `--project` picks
the project if you run it from another checkout: the Editor plays the code of the checkout it has
open, not yours.

## What it does

1. Keeps the Editor ticking while unfocused (`editor set_auto_tick`, `set_run_in_background`) and
   enters Play mode.
2. Sends `bot_tick.cs` to the Editor once per tick (`unity command eval`, about 0.6 s each) until
   `--seconds` of wall time have passed. The first tick installs the shooter on the Editor's update
   loop: it checks the cannon every 50 ms and fires as the chosen player, so a shot's timing isn't
   rounded to the tick. The shooter aims with the game's best-shot solver (the **Fire Best Shot**
   cheat) and removes itself when Play mode ends, a newer session replaces it, or ticks stop coming
   for 10 s. Every tick presses Restart on the game-over screen, so it plays run after run.
3. Leaves Play mode, which flushes the telemetry still buffered (`--keep-playing` skips this).
4. Reads the telemetry records written since it started (`JsonLinesTelemetrySink`, under
   `Application.persistentDataPath/telemetry/`) and writes the report.

## The report

`Logs/playtest/<stamp>[-label]/` (git-ignored) gets `ticks.jsonl` (one line per tick: level, score,
hearts, shots so far) and `report.md`:

| Section | From | Says |
|---|---|---|
| Header | `ProjectileFlightConfig.asset`, the arguments | The reload and ball-cap values, and the player, the session ran with |
| Runs | The ticks | Each run's highest level, score, hearts left, shots and length |
| Levels | Telemetry `level` records | Seconds, shots, pops, pops per shot, hearts lost and max streak, per level |
| Flights | Telemetry `flight` records | How long a ball stays in the air: mean, median, middle half, 90th percentile |

## Reading it

Neither player is a real person: `best` plays far better than anyone, and `human` is a rough model.
Compare sessions with each other, not with a person's feel: if a change makes the bot's levels take
longer or cost hearts, it made the game harder. A level only writes its record when it ends, so
the level in play when the session stops is missing from the table.

## Files

| File | What it is |
|---|---|
| `playtest_bot.py` | The runner: Editor setup, the tick loop, the report |
| `bot_tick.cs` | One tick, compiled by the Pipeline package on each call, with the `__PLACEHOLDERS__` filled in by the runner. Outside `Assets/`, so not part of the Unity project; it reaches the game's internal types by reflection. The shooter's state between ticks lives in `SessionState` (`BalloonPartyBot.*`) |
