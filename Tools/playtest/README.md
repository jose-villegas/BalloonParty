# Playtest bot

Plays BalloonParty in a running Unity Editor and writes a report of how the runs went, so a tuning
change can be measured rather than guessed. It drives the Editor through the Unity CLI and its
Pipeline package (`com.unity.pipeline`), and reads the game's own telemetry for the numbers.

```bash
python3 Tools/playtest/playtest_bot.py --seconds 240 --label reload-2s
```

It needs the Unity CLI (`unity`, on `PATH` or in `~/AppData/Local/Unity/bin`) and the Editor open
on the project with the Pipeline package installed (`unity pipeline install`). `--project` picks
the project if you run it from another checkout: the Editor plays the code of the checkout it has
open, not yours.

## What it does

1. Keeps the Editor ticking while unfocused (`editor set_auto_tick`, `set_run_in_background`) and
   enters Play mode.
2. Sends `bot_tick.cs` to the Editor once per tick (`unity command eval`), until `--seconds` of wall
   time have passed. A tick fires the game's best-shot solver (the **Fire Best Shot** cheat) when
   the cannon is ready, and presses Restart on the game-over screen, so it plays run after run.
3. Leaves Play mode, which flushes the telemetry still buffered (`--keep-playing` skips this).
4. Reads the telemetry records written since it started (`JsonLinesTelemetrySink`, under
   `Application.persistentDataPath/telemetry/`) and writes the report.

## The report

`Logs/playtest/<stamp>[-label]/` (git-ignored) gets `ticks.jsonl` (one line per tick: level, score,
hearts, whether it fired) and `report.md`:

| Section | From | Says |
|---|---|---|
| Header | `ProjectileFlightConfig.asset` | The reload and ball-cap values the session ran with |
| Runs | The ticks | Each run's highest level, score, hearts left, shots and length |
| Levels | Telemetry `level` records | Seconds, shots, pops, pops per shot, hearts lost and max streak, per level |
| Flights | Telemetry `flight` records | How long a ball stays in the air: mean, median, middle half, 90th percentile |

## Reading it

The bot aims with the solver, so it plays better than most people, and it only acts once per tick
(about 0.7 s), so it waits on a ready cannon longer than a person would. Compare sessions with each
other, not with a person's feel: if a change makes the bot's levels take longer or cost hearts, it
made the game harder. A level only writes its record when it ends, so the level in play when the
session stops is missing from the table.

## Files

| File | What it is |
|---|---|
| `playtest_bot.py` | The runner: Editor setup, the tick loop, the report |
| `bot_tick.cs` | One tick, compiled by the Pipeline package on each call. Outside `Assets/`, so not part of the Unity project; it reaches the game's internal types by reflection |
