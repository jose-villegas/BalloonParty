# Cheats

Runtime cheat console for testing and debugging without modifying game state manually.

Toggle the console window in Play Mode with the **backtick** (`` ` ``) key, or — on touch devices — a **3-finger tap** (three simultaneous touches; fires once on the rising edge, so it can't retrigger while held). The 3-finger tap only registers in a build that compiles the cheat system in — a **development build**, or a release build made with cheats enabled (see below) — and legacy `Input.touchCount` reads 0 in the editor Game view. The console GameObject is created automatically at runtime — no scene setup required. The console discovers all registered cheats automatically and organises them by section and tag.

## In-game log console (on-device logs)

`Debug.Log` output isn't visible on a device without a wired-up console. The project depends on yasirkula's [In-game Debug Console](https://github.com/yasirkula/UnityIngameDebugConsole) (`com.yasirkula.ingamedebugconsole`, a UPM git dependency in `Packages/manifest.json`) for this.

`DevLogConsole` (a `MonoBehaviour`) spawns the console prefab at startup and `DontDestroyOnLoad`s it — **mobile development builds only** (plus mobile release builds made with `CHEATS_IN_RELEASE`). Its guard, `UNITY_EDITOR || ((UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE) && (UNITY_ANDROID || UNITY_IOS))`, keeps the component wireable in the editor but strips it from desktop dev builds and ordinary release builds (the serialized prefab reference goes with it, so the console never ships there). At runtime it no-ops in the editor (the editor has its own Console window). Place one `DevLogConsole` in the **Launch scene** and assign the package's `IngameDebugConsole` prefab to its field.

## Build visibility

The entire `Cheats/` system — console, `ICheat`, and all cheat implementations — is wrapped in `#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE`. It compiles out completely in ordinary release builds.

Since Unity 6.6 the **Development Build** checkbox no longer decides this: `UNITY_ENABLE_CHECKS` comes from the per-platform **Managed Code Variant** (*Project Settings ▸ Player ▸ Other Settings*). The editor always defines it; a player gets it only with the **Checked** or **Debug** variant. The default variant is Release, so a development build made by hand with the default has no cheats, no `Log.Info`/`Log.Assert` and no telemetry sink — set the variant to Checked first. **Tools → BalloonParty → Upload Release** does this itself: its dev APK builds Checked (Debug when script debugging is on), and both release APKs build Release, restoring your setting afterwards.

Registration in `GameLifetimeScope` and the `CheatState` checks sprinkled through gameplay controllers are guarded by the same directive.

### Release builds with cheats (`CHEATS_IN_RELEASE`)

**Tools → BalloonParty → Cheats In Release Builds** toggles the `CHEATS_IN_RELEASE` scripting define on the active build target (checkmark shows current state). While on, a normal release build includes the full cheat system — without paying the other development-build costs (`Log.Info`/`Log.Assert` stay stripped, no dev-build profiler/overlay overhead), so the build stays performance-representative. The define lives in ProjectSettings and **persists until toggled off** — turn it off before building anything meant to ship (the enable path logs a warning as a reminder).

## Autoplay (device playtests)

`Autoplay/` lets a checked build play itself, so `Tools/playtest/device_bot.py` can run playtest-bot sessions on
emulators or phones while the Editor does something else. It compiles only under `UNITY_ENABLE_CHECKS` (not
`CHEATS_IN_RELEASE`): its report reads the telemetry log, which only checked builds write.

- `AutoplayLaunchArgument` reads the spec the app was launched with: the intent's `autoplay` extra on
  Android (`adb shell am start -n <package>/<activity> -e autoplay 'player=human seconds=420'`), the argument
  after `-autoplay` elsewhere. With none, nothing is registered and the build plays as usual.
- `AutoplaySettings.TryParse` reads it: `player=human|best`, `seconds`, `restart=0|1`, and the human's knobs
  `floor`, `mean`, `aim`, `wild` (the playtest bot's, see `Tools/playtest/README.md`), plus an optional
  `seed`. A malformed spec is refused with a warning rather than played wrong.
- `AutoplayController` (Game scope) presses Play on the launch screen, fires as the chosen player once the
  cannon is ready (the best angle from `FireBestShotCheat.TryFindBestAngle`, plus `AutoplayDice`'s reaction
  time and aim error for a human), presses Restart on game over or ends there, and quits the app when the
  time is up.
- `AutoplayTap` presses those two buttons as a finger does. It finds the button that calls
  `LaunchPlayTrigger.Play` (or `GameOverScreen.OnRestartPressed`), from a Button's On Click or a
  `ScaleDownButton`'s On Scale Complete, raycasts the event system at its centre, and sends pointer down, up
  and click to what that hits. So every listener runs (the press animation, the scene load, the title
  animation), a covered button isn't tapped through, and the bot goes through the same screens a player does
  instead of calling into the game behind them.
- `AutoplayTickWriter` writes `persistentDataPath/autoplay/ticks.jsonl` (the Editor bot's tick fields, twice a
  second) and a `done` file at the end, which is what the runner waits for.

## VContainer registration

`CheatConsoleView`, `BalloonRemoverCheat`, `DisturbanceStampCheat`, and `LightStampCheat` are MonoBehaviours created via `RegisterComponentOnNewGameObject`. VContainer singletons are **lazy** — the GameObject is only created when resolved. Each requires `RegisterBuildCallback(resolver => resolver.Resolve<T>())` to force eager creation at scope build time.

## Adding a new cheat

1. Create a class implementing `ICheat` — give it a `Name`, a `Section`, and one or more `Tags`.
2. Inject whatever publishers or services it needs via the constructor.
3. Register it in `GameLifetimeScope`:
   ```csharp
   builder.Register<YourCheat>(Lifetime.Singleton).AsImplementedInterfaces();
   ```
That's all. The console picks it up automatically.

### Interactive cheats

A cheat that needs inputs (dropdowns, counters) also implements `ICheatControls.DrawControls()`. The console draws that IMGUI block in the cheat's row (below a plain name + ☆ header) instead of the one-shot Execute button — `SpawnBalloonCheat` is the reference. `DrawControls` is only ever called from within `OnGUI`. `Execute()` is still required by `ICheat`; make it perform the same action with the current selections so the cheat also works if surfaced as a plain button.

## Console features

- **Search** — filters cheats by name as you type
- **Tag pills** — click a tag to filter to only cheats with that tag; click again or "All" to clear
- **Sections** — cheats are grouped by their `Section` property
- **Favorites** — click ☆ next to any cheat to pin it to the top of the list
- **Thrower held while open** — opening the console `Pause`s `PauseSource.Cheat` (releases on close/teardown), so the thrower stays inert and stray fires don't disturb testing

## Current cheats

| Name | Section | What it does |
|---|---|---|
| Spawn Balloon Line | Spawning | Publishes `SpawnBalloonLineMessage` — spawns one new row of balloons |
| Spawn Balloon | Spawning | Pick a `BalloonType`, an item to hold (or None), and a count; force-spawns that many into open slots (bottom-up), assigning the item to each that can hold one. Bypasses the weighted spawner + caps by design. An interactive cheat (`ICheatControls`) — draws its own type/item grids + count stepper |
| Remove Balloons | Grid | Draw across balloons to remove them and trigger a balance pass |
| Add Shields | Projectile | Adds shields to the shot currently loaded in the thrower — bumps the reactive count directly, so the shield view plays its normal gain feedback. Interactive: draws an amount stepper |
| Fire Best Shot | Projectile | Sweeps the shot solver across the aim arc and fires the best-scoring angle through `ThrowerController.FireAt` — the Shot Solver window's "Fire Best" as a one-tap cheat, usable on device. With `IProjectileFlightConfig.AimAngleStepDegrees` quantized, the sweep samples exactly the reachable angles (`AimAngleGrid.ResolveSweepSampleCount`/`ResolveSweepAngle`) instead of a fixed lerp — sampling off-grid here would let the cheat pick, report, and fire an angle the player could never aim at |
| Trigger Level Up | Score | Fills all color bars to the current threshold and immediately triggers the level-up ceremony. Fills through the real scoring pipeline (one pop per colour carrying the whole shortfall), but under `CheatState.InstantScoreTrails` so `ScoreTrailService` confirms each group instantly instead of spawning flying pen-trails — a level's worth of points is granted with no visual flood |
| Reveal A Card | Run | Plays the card reveal (`UI/CardReveal/`) for a random card, as if a level-up had just won it, without holding the card: publishes `UpgradeCardHeldMessage` (stacks 1). Shows nothing when the reveal has no stage (no pops baked) or off the game screen until play resumes |
| Near Level Up | Score | Fills all color bars to three points below the threshold — pop one balloon of each color to complete the level naturally. Uses the same instant fill as Trigger Level Up (no pen-trail flood) |
| Award Score Pop | Score | Awards one N-point pop of a chosen colour through the real hit pipeline (streak reset first so it lands on exactly N) — the way to summon any BigScore catalog shape on demand. Interactive: point field, colour cycle button, and preset buttons per catalog denomination |
| Level Lock | Score | Toggle — locks the current level: score trails still fly on a pop but nothing sticks (`ClaimProgress` grants the visual only; both `OnTrailArrived` handlers skip their commit, so score/bars stay put), no level-up cinematic/ceremony can start (`WillLevelUp`/`CheckLevelUp` guarded), `PlayerHealthController.Damage` no-ops (hearts never drain), and `RunController.EndRun` no-ops (no loss). Sit on a level indefinitely to test it; toggle off to resume normal play. Sets the dev-only `CheatState.BlockLevelUp` flag |
| Force Game Over | Run | Calls `RunController.EndRun()` — commits the run and shows the GameOver screen |
| Restart Run | Run | Calls `RunController.RestartRun()` — resets every `IRunResettable` and returns to `Game` |
| Start From Level | Run | Sets the dev start-level override then restarts the run so it begins at the chosen level — the in-play equivalent of the Level Pacing window's "play from here". A run started this way (or by Jump To Scenario, or the window) holds the cards a run reaching that level would: `RunUpgrades` plays out every card level-up on the way, taking each hand's first card as the level-up does in play |
| Jump To Scenario | Run | Sky, Meadow or Water: restarts the run at the first level of the next stretch of that scenario after the current level, found in this run's own schedule. Keeps the run's seed through that restart (`CheatState.KeepRunSeed`, read by `RunSeed`), since a new seed lays out a different schedule. It jumps forward, so each jump lands on a harder level; pair it with Block Level Up to look around without losing hearts |
| Stamp Disturbance | Grid | Toggle on, then mouse-drag to stamp disturbances into the shared disturbance field. Uses `DisturbanceFieldService.Stamp()` at the mouse world position with drag direction |
| Place Light | Lighting | Toggle on, pick a palette colour from the dropdown, then tap the board to drop a light into the shared light field there; "Clear" removes every light placed so far |
| Scenario Grade | Lighting | Toggle — turns the per-scenario colour grade (`ScenarioGradeView`) off and on, to compare the scenes with and without it. Sets the dev-only `CheatState.ScenarioGradeOff` flag |
| Time of Day | Lighting | Scrub the day/night angle directly (drives whichever source is active) — the slider tints the arc that counts as night — and set how fast the Realtime source advances it (Freeze/1x/8x/30x presets) |
