# Cheats

Runtime cheat console for testing and debugging without modifying game state manually.

Toggle the console window in Play Mode with the **backtick** (`` ` ``) key, or — on touch devices — a **3-finger tap** (three simultaneous touches; fires once on the rising edge, so it can't retrigger while held). The 3-finger tap only registers in a **development build** (or via Unity Remote): the whole cheat system compiles out otherwise (see below), and legacy `Input.touchCount` reads 0 in the editor Game view. The console GameObject is created automatically at runtime — no scene setup required. The console discovers all registered cheats automatically and organises them by section and tag.

## In-game log console (on-device logs)

`Debug.Log` output isn't visible on a device without a wired-up console. The project depends on yasirkula's [In-game Debug Console](https://github.com/yasirkula/UnityIngameDebugConsole) (`com.yasirkula.ingamedebugconsole`, a UPM git dependency in `Packages/manifest.json`) for this.

`DevLogConsole` (a `MonoBehaviour`) spawns the console prefab at startup and `DontDestroyOnLoad`s it — **mobile development builds only**. Its guard, `UNITY_EDITOR || (DEVELOPMENT_BUILD && (UNITY_ANDROID || UNITY_IOS))`, keeps the component wireable in the editor but strips it from desktop dev builds and all release builds (the serialized prefab reference goes with it, so the console never ships there). At runtime it no-ops in the editor (the editor has its own Console window). Place one `DevLogConsole` in the **Launch scene** and assign the package's `IngameDebugConsole` prefab to its field.

## Build visibility

The entire `Cheats/` system — console, `ICheat`, and all cheat implementations — is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. It compiles out completely in release builds. Registration in `GameLifetimeScope` is guarded by the same directive.

## VContainer registration

`CheatConsoleView`, `BalloonRemoverCheat`, and `DisturbanceStampCheat` are MonoBehaviours created via `RegisterComponentOnNewGameObject`. VContainer singletons are **lazy** — the GameObject is only created when resolved. Each requires `RegisterBuildCallback(resolver => resolver.Resolve<T>())` to force eager creation at scope build time.

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
| Trigger Level Up | Score | Fills all color bars to the current threshold and immediately triggers the level-up ceremony |
| Near Level Up | Score | Fills all color bars to one point below the threshold — pop one balloon of each color to complete the level naturally |
| Level Lock | Score | Toggle — locks the current level: score trails still fly on a pop but nothing sticks (`ClaimProgress` grants the visual only; both `OnTrailArrived` handlers skip their commit, so score/bars stay put), no level-up cinematic/ceremony can start (`WillLevelUp`/`CheckLevelUp` guarded), `PlayerHealthController.Damage` no-ops (hearts never drain), and `RunController.EndRun` no-ops (no loss). Sit on a level indefinitely to test it; toggle off to resume normal play. Sets the dev-only `CheatState.BlockLevelUp` flag |
| Force Game Over | Run | Calls `RunController.EndRun()` — commits the run and shows the GameOver screen |
| Restart Run | Run | Calls `RunController.RestartRun()` — resets every `IRunResettable` and returns to `Game` |
| Stamp Disturbance | Grid | Toggle on, then mouse-drag to stamp disturbances into the shared disturbance field. Uses `DisturbanceFieldService.Stamp()` at the mouse world position with drag direction |
