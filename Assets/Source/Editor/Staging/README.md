# Staging

`SceneStage` puts a still scene together in Play mode, so a look can be judged without playing for it: a
balloon's pop at dawn and at night, a reward balloon beside the ones around it, a scenario at night, a shader
not yet merged on a live board. The scene is built to fit what's looked at: an empty board (no balloon comes
unless placed), dealt in a chosen scenario, level and seed, the obstacles that would get in the way taken off
it, the time of day set, and then only what's wanted placed on it. It holds the world still, pops a balloon or freezes its pop at a chosen
moment, and renders the camera to a PNG.

It's driven from outside, one call per Unity CLI eval, by `Tools/stage/stage.py` (see its README for the
commands). Every method returns at once with a line saying what it did, and an `error: ...` line when it
can't; nothing in it waits. What has to play out over time (a pop, a dealt board spawning) runs under
`Advance(seconds)`, which lets game time go for that long and holds it again; `Status()` says `advancing`
until then.

| Call | Does |
|---|---|
| `Begin()` | Enters Play mode, then in one step goes straight to a held game: past the launch screen (its Play button's own actions, so the button goes and the launcher hands over to the game scene, without the cloud scroll) or a fresh run after a game over, with time held from that frame and the clock and level-up stopped, so nothing plays out on its own. The board is empty: `CheatState.EmptyBoard` keeps a run's opening board and the spawn clock's lines off (a run already under way loses its balloons). Called until it answers `ready` |
| `End()` | Clears the frozen pops, leaves Play mode and gives the game back its clock, level-ups and balloons, as leaving Play mode any other way does |
| `Hold()` / `Release()` / `Advance(s)` | Game time off, on, or on for `s` seconds. Holds through `ITimeScaleClaims` (`TimeScaleSource.Capture`, as the README shots do), so the shaders' clock stops too |
| `Deal(scenario, level, seed)` | A fresh board, its ground and obstacles (no balloons, under `EmptyBoard`): the run restarted at a level, or the first level from it in a scenario (as the Jump To Scenario cheat finds it), on a run seed, so the same call deals the same board. The seed goes through `CheatState.ForcedRunSeed`, a one-shot `RunSeed` reads on the reset, and also seeds Unity's random |
| `Strip(kinds)` | Takes kinds off the board at once, with no pop: the balloons (the board clear's `ClearAll`), the clouds, bushes or enemies (deflectors, absorbers, gatekeepers) through `StaticActorSpawner.ClearStaticActors(type)`, or every obstacle |
| `SetTime(degrees)` | The light's angle (45 dawn, 135 noon, 225 dusk, 315 night), with the clock stopped |
| `List()` | Each balloon's and obstacle's type, slot and world position, and the grid's size |
| `Clear(...)` / `ClearAround(x, y, r)` | Pops the balloons in a slot rectangle or a circle, with the Remove Balloons cheat's forced pop |
| `Place(type, col, row, item, replace)` | A balloon in a slot, through the Spawn Balloon cheat's own `SpawnAt`, whole and at rest at once (its scale-in finished, its trails cleared of the line from where the pool last had it). With `replace`, what's there is popped and the new one spawned in the same frame, before anything below can float up into the slot |
| `Pop(target)` | Pops a balloon for real, its own effect and all. A target is a balloon type (the first on the board) or `col,row` |
| `FreezePop(target, seconds, options)` | The balloon's own pop effect, stopped `seconds` in from a fixed random seed, so two runs throw the same particles. Options can hide the balloon, move the effect off it, and swap the shards' sheet, first-frame range, shader (by name, or a `.shader` file compiled on the spot) or material floats, all on copies |
| `Ball(x, y, fromPoint, seconds)` | A ball in flight at a world point, held still, so a capture shows the effects cleared around it (`Plans/PLAN-EffectsLayerClearing.md`). With `fromPoint` ("x,y") and `seconds`, it flies there in a straight line over that much game time first (under `Advance`), so the clearing behind it has decayed as in a shot. Only the ball's look (the prefab built under an inactive parent and stripped of scripts and physics), put on `IProjectileRegistryWriter`'s in-flight list, which is what the clearing follows |
| `ClearEffects()` | Removes the frozen pops and the staged balls (retired from the registry), and shows the balloons the pops hid |
| `Capture(path, around, size)` | Renders the main camera straight to a texture (fresh even while the Editor is unfocused): the frame, or a square around a balloon or a world point |

A `.shader` file passed to `FreezePop` is compiled without importing it, its relative includes rewritten
to project paths from its own folder. So a shader in a worktree can be tried in the Editor, which has the
main checkout open, before it's merged; it has to compile against the main checkout's includes.

Gated on `UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE`, like the cheats it drives. Public, unlike the rest of
this assembly, so an eval script can call it without reflection.
