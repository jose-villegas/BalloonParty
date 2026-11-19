# Solver

The runtime half of the shot solver (see the authoritative rule-mirroring/approximations doc in
`Assets/Source/Editor/ShotSolver/README.md`):
the pure event-to-event `ShotSimulator` (with `ShotFlightState`/`ShotBoardSnapshot` as its state/board
data types) and its dynamic-board companions (`ShotBoardDynamics`, `ShotSimBoardActor`,
`ShotMotionMath`), its item-carrier companions (`ShotItemLayer`, `ShotSimEffectBoard`), and
`ShotBoardGather`, which snapshots the live board/thrower/config into a `ShotSolveContext`.

The sweep's sample grid (`ResolveSweepSampleCount`/`ResolveSweepAngle`) and its sibling
`ClampToReachableAngle` live in `Shared/AimAngleGrid` rather than here — that grid is a domain rule
about which angles the thrower can reach, consumed by four independent places (the live
`ThrowerController`, `FireBestShotCheat`, `ShotSolverWindow`, and the Scene-view aim fan overlay), not
solver-specific logic, so it lives in `Shared/` next to `WallLimits` for the same reason. `ShotBoardGather`
now *consumes* `AimAngleGrid` for its sweep rather than owning the grid math itself: with
`IProjectileFlightConfig.AimAngleStepDegrees` at 0 (continuous aim), a sweep lerps a fixed count
of samples across the arc, same as before quantization existed; with a non-zero step, it instead
samples exactly the step's multiples that fall inside the arc — the same absolute grid
`ThrowerController.ClampAndQuantizeAimDirection` snaps player input to — so a sweep can never
recommend an angle the player could not actually aim at. Both sweeping callers (`FireBestShotCheat`,
`ShotSolverWindow`) go through `AimAngleGrid`'s two statics rather than each lerping the arc themselves,
and both source the arc bounds from `IProjectileFlightConfig.AimAngleMinDegrees`/`AimAngleMaxDegrees` —
the same range the thrower clamps aim to — rather than a separate constant, so a sweep can never cover a
different set of angles than the player can actually reach.

`AimAngleGrid.ClampToReachableAngle` is the sibling the thrower's own clamp calls: given a raw aim
angle, it resolves the same grid-anchored "reachable" angle `ResolveSweepAngle` would sample at that
position — clamping the *rounded grid index*, not the angle itself, so a clamp-then-snap or
snap-then-clamp can't disagree with what the sweep considers reachable (see
`ThrowerController.ClampAndQuantizeAimDirection`'s own comment for why the naive orderings both
fail).

Lives in the Runtime assembly so both the editor Shot Solver window AND runtime tooling (the
Fire Best Shot cheat, development builds on device) can run the same simulation — the editor
window keeps the sweeping/bisection UI, this folder owns the physics truth.

## The mushrooms

On a meadow board the simulated ball bounces off the mushrooms, and a simulated lance cuts their caps and
flies on (`Plans/PLAN-MeadowMushrooms.md`).
`ShotBumperLayer.Gather` copies the board's bumper circles (`IBumperField`, each whole cap with its id and its wall) with the game's
bounce rule, its chain rules and the Snipe's speed buff, and every caller of `ShotBoardGather.Gather` hands
it over (the parameter has no default: a caller that left it out would aim through the mushrooms). On a
board without mushrooms it is null, and `Simulate` runs the loop it had before: no line of the mushroom
code runs, and `ShotMushroomFreeBoardTests` holds that mushrooms no shot can reach change no flight to the
bit and that a sweep over mushrooms allocates nothing.

The simulator owns no mushroom geometry and no chain rule. It calls `BumperSweep`, `MushroomChainRule` and
the `IBumperBounceRule` the live flight calls, in the live flight's order, and pays a completed chain as
the game's Snipe handler does (`ShotSimulator.PayMushroomReward`: `Arm` arms, and nothing else pays). So the Fire Best Shot
cheat, the autoplay and the playtest bot that aim with it see a chain for what it does: a flight that
completes one is a lance from the third mushroom on, and is ranked by what that lance then pops. The
ranking has no term for a lance, a chain or a cut cap by itself (`ShotRanking`: golden, hearts that count,
nuke, disco, then score).

**A lance cuts caps** (`ShotSimulator.TryCutCap`, the mirror of `ProjectileMotionResolver.CutCaps`). A
simulated ball that is piercing, however it was armed, bounces off no mushroom: the first whole cap its path
enters before its next wall is cut, there and then, and the ball flies on unturned, so a cut adds no point to
the path. The chain is not asked, and the cap that armed the ball is left alone while it still flies the
direction it left along. A cut cap leaves the layer's circles for the rest of that flight
(`ShotBumperLayer.Cut`), so the sweep, the kick and the lookahead no longer find it, and the next flight starts
from the board as gathered (`ResetForNewFlight`, which allocates nothing). `ShotMushroomTally.CapsCut` counts
them and `ShotBumperLayer.CutLog`, when a test sets it, names them. Each cut banks the simulated flight one
plain charge, as the game's does (decision K; `ShotSimulator.BankCutCharge`): `BankedPierceCharges`, with the Snipe's speed to re-arm at (the layer's `LanceSpeedBuffMultiplier`, the item configuration's own number, which a test holds against what ships),
and `ShotMushroomTally.ChargesBanked` for the tally. The ranking gives a banked charge no value. A charge is
spent where the simulator spends a pierce (`SpendPierceAtDischarge`), which is a cruise-earned pierce's
discharge: a Snipe's lance, and so a chain's, rides to the end of the simulated flight and never needs one.

**A bomb blows caps off** (`ShotSimulator.BlowCapsOff`, the mirror of `Item/Bomb/MushroomBlast.Bomb`). A bomb
the simulated flight sets off, the host's or one its blast chained into, takes the caps the game's takes: the
ones the bomb's own rule breaks outright (`BombBlast.Pierces`: the six slots round the bomb's; for a rainbow
bomb, a centre within its kill radius). `ShotItemLayer` notes each bomb it resolves and the simulator hands
them to `ShotBumperLayer.BlowOff` before it looks for the next event, and once more after the last. The caps
leave the layer for the rest of that flight, as a cut cap does, and the next flight starts from the board as
gathered. For this the layer keeps each cap's slot, taken at the gather from `IBumperField.SlotAt`; a layer
built by hand without slots loses no cap to a bomb. `ShotMushroomTally.CapsBlown` counts them and
`ShotBumperLayer.BlownLog`, when a test sets it, names them. Not modelled: the sway, which moves no ball, and
a nuke's blast, which comes after a fuse and kills every shot in flight. And the game's bomb goes off a frame
after the pop that released it, where the simulator's goes off at once: a ball that reaches a cap next to the
bomb inside that frame bounces in the game and flies through here.

**Which caps are whole** is the field's to say: `IBumperField.Bumpers` lists whole caps only, so a cap a lance
cut before the gather is not in the layer, as it is not there for the game in the same frame. What the
simulator does not model is anything that changes the board after the gather. It has no beats: a cap the game
regrows on the board's beat, seconds into a long flight, stays cut for the whole of a simulated one, and a cap
cut before the gather stays cut however long the flight is. And it flies one ball: a cap another ball in flight
cuts after the gather is still whole in the layer, so a shot solved a moment before another lance's cut bounces
off a cap the game's ball will find gone. Both are accepted: a flight is seconds long, and the next solve
gathers the board as it then stands.
And the simulator's lance is not spent when it plows a tough balloon (the approximation in
`Editor/ShotSolver/README.md`), so where the game's lance is an ordinary ball again past its next wall and
bounces off the caps left, the simulated one goes on cutting them.

## The loaded ball

A ball can already carry a grant before it fires: a Snipe pickup that armed the ball still in the
thrower, or the Sharpshooter card arming it as it loaded (`Game/Upgrades/README.md`). `Gather` takes the
loaded ball (`IProjectileRegistry.Loaded`) and keeps what it carries as the context's `LoadedSeed`
(`ShotBoardGather.SeedFor`): a piercing ball seeds as `ShotFlightSeed.Armed`, with its speed buff and a
rainbow host's grant riding the pierce, and a rainbow Shield grant seeds as ending at the next wall.
`SimulateAt` launches every angle from that seed, so the Fire Best Shot cheat, the autoplay that aims with
it, and the Shot Solver window all plan an armed ball as the lance it is. Banked Snipe charges on a held
ball aren't seeded; they matter only once its first pierce discharges.
