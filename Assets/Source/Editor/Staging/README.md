# Staging

`SceneStage` puts a still scene together in Play mode, so a look can be judged without playing for it: a
balloon's pop at dawn and at night, a reward balloon beside the ones around it, a shader not yet merged on a
live board. It holds the world still, clears slots, places balloons, sets the time of day, pops a balloon or
freezes its pop at a chosen moment, and renders the camera to a PNG.

It's driven from outside, one call per Unity CLI eval, by `Tools/stage/stage.py` (see its README for the
commands). Every method returns at once with a line saying what it did, and an `error: ...` line when it
can't; nothing in it waits. What has to play out over time (a pop, a restart settling) runs under
`Advance(seconds)`, which lets game time go for that long and holds it again; `Status()` says `advancing`
until then.

| Call | Does |
|---|---|
| `Begin()` | One step toward a held game: enters Play mode, taps Play on the launch screen, then stops the clock and the level-up and holds the world. Called until it answers `ready` |
| `Hold()` / `Release()` / `Advance(s)` | Game time off, on, or on for `s` seconds. Holds through `ITimeScaleClaims` (`TimeScaleSource.Capture`, as the README shots do), so the shaders' clock stops too |
| `Restart(level)` | The run again at `level`, keeping its seed (the Start From Level cheat's state) |
| `SetTime(degrees)` | The light's angle (45 dawn, 135 noon, 225 dusk, 315 night), with the clock stopped |
| `List()` | Each balloon's type, slot and world position, and the grid's size |
| `Clear(...)` / `ClearAround(x, y, r)` | Pops the balloons in a slot rectangle or a circle, with the Remove Balloons cheat's forced pop |
| `Place(type, col, row, item)` | A balloon in an empty slot, through the Spawn Balloon cheat's own `SpawnAt` |
| `Pop(target)` | Pops a balloon for real, its own effect and all. A target is a balloon type (the first on the board) or `col,row` |
| `FreezePop(target, seconds, options)` | The balloon's own pop effect, stopped `seconds` in from a fixed random seed, so two runs throw the same particles. Options can hide the balloon, move the effect off it, and swap the shards' sheet, first-frame range, shader (by name, or a `.shader` file compiled on the spot) or material floats, all on copies |
| `ClearEffects()` | Removes the frozen pops and shows the balloons they hid |
| `Capture(path, around, size)` | Renders the main camera straight to a texture (fresh even while the Editor is unfocused): the frame, or a square around a balloon or a world point |

A `.shader` file passed to `FreezePop` is compiled without importing it, its relative includes rewritten
to project paths from its own folder. So a shader in a worktree can be tried in the Editor, which has the
main checkout open, before it's merged; it has to compile against the main checkout's includes.

Gated on `UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE`, like the cheats it drives. Public, unlike the rest of
this assembly, so an eval script can call it without reflection.
