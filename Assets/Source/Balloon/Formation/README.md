# Balloon/Formation

Unbreakable balloons keep one clock, the **beat** (`BeatSeconds`, 1.7 s of play), and every
`BeatsPerCycle` (2) beats make a **cycle** (3.4 s). An unbreakable snake steps `SnakeStepsPerBeat` (2) times
a beat, on the beat and the off-beat (every 0.85 s), so it walks quicker than the tough snake. When two or more are on the board, they gather into a
**formation**. They form up on any beat, stay in place for a few cycles, then switch to a different line on
the cycle. While a formation stands, its members float in midair: the balloons below rise until they reach the
formation, and spawn lines stop under it. An unbreakable left alone, in no formation, roams: it jumps toward
the far side of the board, but only on a cycle, only after it has stood `LoneHoldCycles` (3) cycles, and only
one at a time, so the board moves to one rhythm rather than a lone unbreakable crossing it at every wave.

Several formations can stand at once. Unbreakables left over after one formation is full form a second
one, so two lines can stack into a blockade. A formation the player leaves alone calls in more
unbreakables, past the level's usual cap, and those form formations of their own.

Tough balloons (toughs and toughers) gather too, but only into one snake of their own. It starts from the
biggest group, every other tough falls in behind its tail, and it walks through the balloons in its way. See
"Tough snakes" below.

## Contents

| File | What it does |
|---|---|
| `FormationShape` | The five shapes: `Horizontal`, `DiagonalDownRight`, `DiagonalDownLeft`, `ItemCover` (three to six around a balloon carrying a bomb, a snipe or a lightning, shielding it from the cannon: below it first, then its sides, then above), and `Snake` (a line that walks the board) |
| `SnakeWalk` | Where a snake's head steps next: straight on if it can, else the gentlest open turn |
| `SnakeBody` | Where free toughs fall in behind the tough snake's tail: a line on from it that they take in one batch, the balloons on it moving out to the slots they left |
| `SlotMove` | One actor and the slot it moves to. The service hands the balancer each arrangement and each snake step as a batch of these, and every actor in a batch leaves its slot before any lands |
| `FormationPlanner` | Pure planning. For one shape it lists every spot on the board where the shape fits, works out how far the members would travel to fill it, and picks at random among the few cheapest spots. It then says which member goes to which slot. It reads the grid but never changes it |
| `FormationAssignment` | One member and the slot it goes to |
| `StandingFormation` | One standing formation: its members, its shape, the waves it has left to hold, whether a member was pierced since it formed, and whether it is a tough snake (`Armoured`) |
| `UnbreakableFormationService` | The controller. It decides when each formation forms, holds, changes and breaks up, marks members as in formation, rolls for reinforcements, and keeps the toughs' snake: starts it, takes each free tough in at its tail, and walks it through the board |
| `IFormationReinforcements` | The reinforcements the formations have called in. `BalloonSpawner` takes them at each wave's first line |
| `IToughSnakeBody` | The tough snake's members, head first, for the view that draws its springs (`Balloon/View/ToughSnakeSpringView`) |

## The clock

`BalloonBalancer` keeps the beat (`TickStepClock`): it counts seconds of play, and a pause holds it. The
rhythm comes before anything else, so a doomed shot's last glide, which freezes the rest of the board,
doesn't hold the beat: an unbreakable snake steps on its tick right through it, and a cycle or form-up that
falls due in the glide runs in the balance after it. An unbreakable snake steps `SnakeStepsPerBeat` times a beat, evenly spaced, and on a beat between cycles the free unbreakables form up (`FormOnBeat`) once
`MinMembers` of them are free, still gliding in or not, so one that arrives or is set free is red a beat at
most, not a cycle. On every cycle the balancer asks the service to `ArrangeOnCycle`, then moves one
lone unbreakable (`RelocateOneRoamer`): the one that has rested longest of those that have stood
`LoneHoldCycles`, and none while the last one to move is still on its way (`RoamRests` keeps the count).
Formations go first, so the lone one sees their slots as taken. A cycle with no unbreakable on the board
does nothing, and no balance runs for it. The beat runs with no snake too, so the snakes' steps and the
cycles keep one rhythm. The tough snake keeps its own clock (`ToughSnakeStepSeconds`, 1.5 s): it has no
shape to change and nothing to hold, so it steps on its own.

## How a formation runs

1. **Cycle.** On each cycle the balancer calls `ArrangeOnCycle`, before the lone unbreakable's move.
2. **Forming.** If at least `MinMembers` unbreakables are free (in no formation), the service picks a
   shape for them. The pick is weighted at random from the shape weights. The snake's weight grows by
   `SnakeWeightPerExtraUnbreakable` of itself for each unbreakable on the board beyond `MinMembers`, so a
   crowded board snakes more often, up to `MaxUnbreakableSnakes` (2) snakes at once: with that many
   walking, the pick leaves the snake out. It leaves out the shape just held, and `ItemCover` finds no spot
   unless a bomb, a snipe or a lightning is on the board. If the picked shape fits nowhere, the service tries the next one. If nothing fits, nothing forms and the unbreakables roam as usual. A
   formation takes at most `MaxMembers` (a horizontal line, `HorizontalMaxMembers`); an item cover takes
   three to six. While `MinMembers` or more are still free after it, they form another one.
3. **Moving in.** The service hands each member's move to the balancer's relocate routine. That is the
   same routine roam jumps use, so the members glide in at their normal speed. Each member is marked
   in formation (`IWriteableFormationMember.SetInFormation`).
4. **Holding.** The formation lasts its shape's hold, counted in cycles and counting the one it formed
   on: a snake walks `SnakeHoldCycles` (3), a horizontal or diagonal line holds `LineHoldCycles` (3), an
   item cover `ItemCoverHoldCycles` (3). During that time the next cycles only count down: nothing moves
   but a snake's steps.
5. **Changing.** On the first cycle after the hold, each member first rolls `JoinSnakeChance` (0.35) to
   leave for the tail of the unbreakable snake with the most room, while it has room (`MaxMembers`): the
   joiners fly in one batch to a line of slots on from the tail, away from its body, each balloon on that
   line going to the slot a joiner left, so snakes grow from the other formations rather than new ones
   forming. Then a new shape is planned for the members left and any free unbreakables. A formation with a
   member a lance plowed (see "Plowed by a lance" below) waits for the discharge and changes on the cycle
   after it. Members that stay in the formation are never marked out, even for a
   moment. Members the new shape doesn't need turn red and roam again, each once it has rested. If no shape fits, the formation
   breaks up. Each formation keeps its own count, so they change at different waves.
6. **Breaking up.** The service watches `SlotGrid.OnChanged`. When a member leaves the grid (it was
   pierced, or the board was cleared) it drops out of its formation. If fewer than `MinMembers` are left
   in that formation, the rest are released at once. The other formations stand. The next line's balance,
   or the timed pulse, floats the released ones up. The service never moves a lone one itself; the
   balancer does, one a cycle. `ResetRun` releases everything.

## Snakes

A snake forms as a row, like a horizontal line, of at most `SnakeMaxMembers`. It sets off along the row,
east or west at random, led by the member at that end. From then on it takes `SnakeStepsPerBeat` (2) steps
a beat, on the beat and the off-beat (every 0.85 s of play), quicker than the tough snake: the balancer asks the service to `StepSnakes`, right before that
balance's race, or at once in a doomed shot's last glide. The beat is apart from the balancer's 4 s pulse,
which a pause or a doomed glide starts over; a pause only holds the beat. A snake formed or re-planned on a
cycle takes its first step on the next tick.

- The head steps one slot along the snake's heading (`HexCoordinates.Step`, six directions numbered from
  east, clockwise). A balloon there trades places with it, going round the snake to the slot the tail
  leaves (see "Walking through the board" below), so the snake steps on every tick. When that slot is off
  the board, below the row clearance, held by something it can't trade with, or would wall the board,
  `SnakeWalk` tries the gentlest turn instead: 60° right, 60° left, 120° right, 120° left. It never turns
  back.
- Each member behind the head steps into the slot the member ahead of it just left. When a member was
  popped, the one behind it closes the gap, so the snake stays one line.
- **Plowed by a lance.** A piercing shot plows through toughs and unbreakables without popping them and
  shatters them at the next wall, where each was struck (`ProjectileHitResolver.DischargePending`). Until
  then each plowed one holds its slot (`ProjectileRegistryExtensions.AwaitsDischarge`), and the snake splits
  there: the members ahead of it walk on, on the tick, and it and those behind it wait. With the head
  plowed, the tail end leads. Once the discharge pops the plowed ones, the members behind close up at the
  next step. A plowed one never joins a formation, falls in behind the tough snake, or roams meanwhile.
- Boxed in (by toughs, other unbreakables, anchored or static actors, the edges), the snake turns round in
  the same tick and its tail leads the way back. Only boxed in at both ends does it miss a step, and it
  tries the other end on the next tick.

Members stay anchored and blue as they walk (`IFormationMember.Walking`), and the board settles around
each step in the same pulse. The snake keeps the row's limits: never in the bottom
`HorizontalBottomClearance` rows. Like any formation it changes shape after its hold (`SnakeHoldCycles`), breaks below
`MinMembers`, and calls reinforcements while untouched.

The shot solver predicts the pulse's rise but not a snake's step, so a shot that meets a step mid-flight
can play out differently from its prediction.

## Tough snakes

The board's toughs and toughers (`ToughBalloonModel`, an `IArmouredFormationMember`) are one body: a
single snake of their own, walking with the same `SnakeWalk` as an unbreakable snake but slower, on a clock
of its own: a step every `ToughSnakeStepSeconds` (1.5 s) of play, held by a pause or a doomed glide
(`BalloonBalancer`).
Toughs never join an unbreakable formation, and unbreakables never join theirs.

- **Starting as one body.** The initial fill deals the toughs side by side on one line
  (`Balloon/Spawner/README.md`), so they rise together as the snake's body. The toughs form up the moment
  `ToughSnakeMinMembers` (2) of them are on the board, without waiting for them to land: a tough still gliding in counts
  at the slot it is heading for. So the run is the snake while it rises, anchored, and the balancer, which
  leaves a formation's members be, never pulls it apart. It starts from the biggest group of touching
  toughs, so it starts where most of its body already is and not in a pocket the rest can't reach: that
  group's tough in the deepest row starts it, where it stands, the farther west on a tie, the west end of
  the run. Hits left play no part. The head is never one
  in the bottom `HorizontalBottomClearance` rows, which a snake never walks. It heads away from a free
  tough beside it, so toughs that rise side by side are its body as they stand; with none beside it, it
  heads toward the nearer edge, so its body has room behind it. Every other tough then lines up behind it
  at once (`SnakeBody`): a line of slots on from the head, following the toughs where they stand and
  otherwise bending as little as it can. A balloon on that line moves out to a slot one of the toughs
  left. The balancer asks every frame (`ToughsDueToGather`), so the snake forms the moment it can, not at
  a wave start or on a beat, and even while a level's transition holds the board, when the next level's
  toughs rise (`BalloonBalancer.FormToughsInTheLevelTransition`; only the form-up runs then, no step and no
  balance). The balance a line calls forms them up too, without waiting for the next frame's tick, so a
  tough or tougher a line brings flies from its entry straight to its place behind the tail. It takes its
  first step the moment its head has landed
  (`ToughSnakeLanded`), so the toughs are moving as soon as they arrive at a level's start.
- **Falling in at the tail.** A tough that rises later falls in behind the tail the moment it appears,
  the same way, gliding straight to its place. Where nothing on from the tail can take it (toughs,
  unbreakables, anchored actors and anything static block the line) but the head has room, it falls in at
  the head instead: the snake turns round, as it does in a dead end, and the old head's end is its tail.
  Where neither end can take it, it waits until something on the board changes and tries again, unless
  more toughs are free than the snake holds (a snake boxed in a pocket among bushes): then it starts over
  from the biggest group.
- **Closing up.** When a member pops, the members behind it close up at once, each into the slot of the
  one ahead, so the body stays one line between steps (a balloon in the first one's way goes round to the
  slot the tail leaves). One with nowhere to go waits for the next step, whose trail closes the gap.
- **Walking through the board.** A board in play is nearly full, so a snake that only steps into empty
  slots would hardly move. Every snake, the tough one and the unbreakables', walks through balloons
  instead: its head may step into a slot
  held by a balloon that moves and is held by nothing (`SnakeWalk.CanPassThrough`), and that balloon
  goes round the snake to the slot the tail leaves. When a tough joins in the same step, the tail's slot
  is the joiner's, so the balloon takes the joiner's slot instead. Toughs and unbreakables (free or in a
  formation), anchored actors and anything static still stand in its way, and then it turns as any snake
  does. The balancer carries out each step as one batch (`SlotMove`): every mover leaves its slot before
  any lands, so the head and the balloon it passes can trade slots.
- **Keeping its shape.** The snake never re-orders: each member walks into the slot the one ahead of it
  left, a tough that's hit keeps its place, and one that joins stays at the tail. Boxed in, the snake
  turns round like any snake, its tail leading, and walks on in the same order.
- **Walking until the last pops.** The tough snake has no other shape to change to, so it never holds or
  re-plans, and it doesn't break below `MinMembers`: it walks on with whoever is left. When its last
  member pops it ends, and the toughs on the board start another at once. It calls no reinforcements.
- **No wall from toughs.** A tough gives way to enough hits, so it never counts toward the wall across the
  board (`ShotWall.Walls`), and the tough snake's step skips that check.

Members are anchored (`IBalanceAnchor`) while in their snake, exactly as unbreakables are. Its members
look like any tough. As a trial (`IBalloonsConfiguration.ToughSnakeSprings`, on; the Tough Snake Springs
cheat flips it in play), a spring joins each pair of neighbours in the snake, tied to their knots
(`Balloon/View/ToughSnakeSpringView`). Each is a line of points joined by springs between the two knots, the
way the launch screen's spring line is simulated (`Shared/Math/SpringChain`): a little longer than the gap
between neighbouring slots, so it sags while the pair stands side by side, stretches as the one ahead steps
away and swings as the one behind catches up. A spring lasts as long as the same pair stands next to each
other; a pop or a joiner ties a new one.

## Reinforcements

A formation the player doesn't break calls for help. Reinforcements still count waves, not cycles, since
they arrive with a wave's first line: at each wave start (`RollReinforcementsAtWaveStart`), every formation
that has had no member pierced since it formed rolls `ReinforcementChance`, eased while the player is in need
(`Game/Assist/ThreatEase`: half as likely on the last heart). Each success calls in one more unbreakable.
A formation sits out the roll at the end of the wave it formed in, so the player always gets a whole wave to
answer it. A formation
keeps its record through shape changes: once pierced, it never calls again.

The service only counts the calls (`IFormationReinforcements`). `BalloonSpawner` takes them right after
the formations arrange, since both run at the wave's first line, and adds that many unbreakables to the
line. They enter like any other balloon. They don't count against the level's own unbreakable cap or wave
quota. Instead, at most `ReinforcementCeiling` of them are on the board at once (`ReinforcementPlan`).
Calls past the ceiling, or past the line's open places, are dropped. On the board they are ordinary
unbreakables: they roam, and once two are free they form a formation of their own.

## Rules a formation keeps

- **Room on a full board.** A formation slot must be empty, already hold one of the unbreakables it is
  planning with, or hold a balloon that can trade places (one that moves and nothing holds:
  `SnakeWalk.CanPassThrough`). That balloon goes to the nearest slot a member left, in the same batch, so a
  full board doesn't keep the free unbreakables apart and red. Each balloon moved counts as
  `FormationPlanner.DisplacedBalloonCost` (3) slots of travel, so where there is room the formation takes it
  instead. A formation never plans over another formation's members, toughs, or anything anchored or
  static.
- **Spawn room.** It never uses the bottom `BottomClearance` rows, so spawn lines always have room to
  enter.
- **No wall across the board.** Every formation keeps its own row short, but two of them, or one and a
  roamer, could still meet: a row of three beside a snake of four fills a seven-wide row, and two lines one
  row apart can touch end to end. So nothing that moves an unbreakable may link the board's unbreakables
  (and deflectors) from one side wall to the other (`Slots/Grid/ShotWall`). The planner drops every spot
  that would, a snake's head turns away from a step that would, and `BalloonBalancer`'s lone move drops
  the landing slots that would (`DropWallClosingSlots`). A board some other way walled already (the balance
  race floating unbreakables up, say) is no reason to freeze them, so then the check stands aside.
- **Rows stay up and short.** A horizontal line never uses the bottom `HorizontalBottomClearance` rows,
  and takes at most `HorizontalMaxMembers`: close to the cannon, or across the board, a wall shuts the
  player out. Diagonals leave lanes open, so they keep `BottomClearance` and `MaxMembers`. The
  unbreakables a row leaves out are free to form their own.
- **A cover is a shield against the cannon.** `ItemCover` stands around a balloon carrying a bomb, a snipe
  or a lightning (the items that hit hardest; the others are left open). It takes three to six of the free
  unbreakables, whatever `MaxMembers` says, and fills the item's neighbours in order: the two below it,
  toward the cannon, then its two sides, and the two above only once those are taken. Six close the ring.
  With an odd count it tries the left and the right side both and keeps the cheaper. A neighbour off the
  board is shielded by the edge, so the cover skips it; a corner with fewer than three neighbours on the
  board gets none. A slot it takes in `BottomClearance` rules the spot out. Its place is the item's, so it
  never looks for a spot of its own.
- **Members keep their slot.** A member already standing on a formation slot keeps it: assignment
  takes the nearest pairs first, so its distance-0 pair always wins. As a result, no member ever has to
  move into a slot another member still holds.
- **A full ring holds the item.** Six leave the item's balloon nowhere to move. A smaller cover leaves
  the side above it open, so the item can float up out of it. If the item moves or pops, the cover stays
  where it is until the next change.
- **A cover's slots take the balloons' places.** A balloon is no shield, since a shot pops it, so a
  neighbour holding one trades it out to a slot a member left, as for every shape.

## Anchoring

A member in formation reports `IBalanceAnchor.IsAnchored` (from `Slots/Actor`). Everything that moves
balloons leaves an anchored actor in its slot:
- the balance race (`BalancePlanner`) and `BalloonBalancer.HasPossibleMove`,
- the lone move, and the model's own `TryPickRelocation`,
- `PressurePropagation`, where an anchored actor blocks the shove the way a static actor does,
- the shot solver. `ShotBoardGather` copies the flag into `BalanceProfile.Anchored`, and
  `ShotSimDynamicActor` reports it, so the solver's predicted pulses leave anchored balloons in place too.

## Look

`Balloon/Type/UnbreakableFormationLook` turns a member blue once it has arrived in its slot (in formation
and `IsStable`). The core glow, the inner rim and the exhaust trail all change, and a `GlitterSwirl`
halo fades in behind the sphere. The halo doesn't sway or spin with its balloon: the service gives every
member its formation's heading (`IFormationMember.FormationHeading`, a world direction), and the look turns
the halo so its swirl drifts that way. A line heads along its slots, and an item cover heads up at the
item. The unbreakable's periodic burst is tagged blue too, so its specks
match. All the blue comes from the palette's `UnbreakableFormation` entry. See `Balloon/Type/README.md`.

## Settings

The settings are `IUnbreakableFormationSettings`, nested in `BalloonsConfiguration` under
"Unbreakable Formations":
- `Enabled` (all formations, tough snakes too)
- `ToughSnakes` (on: the toughs walk as one snake. `ToughSnakeMinMembers` (2) toughs on the board start it, it steps
  every `ToughSnakeStepSeconds`, and it keeps out of the bottom `HorizontalBottomClearance` rows)
- `MinMembers` (2) and `MaxMembers` (5)
- the clock: `BeatSeconds` (1.7: seconds of play between beats), `BeatsPerCycle` (2, so a cycle is 3.4 s)
  and `SnakeStepsPerBeat` (2: an unbreakable snake steps on the beat and the off-beat, every 0.85 s)
- the holds, in cycles: `SnakeHoldCycles` (3), `LineHoldCycles` (3, horizontal and diagonal),
  `ItemCoverHoldCycles` (3), and `LoneHoldCycles` (3, a lone unbreakable's rest between moves)
- `BottomClearance` (2) and `HorizontalBottomClearance` (4, for horizontal lines)
- `HorizontalMaxMembers` (3, for horizontal lines; an item cover takes three to six)
- `SnakeMaxMembers` (4)
- `ToughSnakeStepSeconds` (1.5: seconds of play between the tough snake's steps, its own clock apart from the beat)
- `SpotChoices` (3)
- `ReinforcementChance` (0.35, eased in need by `Game/Assist/ThreatEase`) and `ReinforcementCeiling` (4)
- the shape weights: horizontal 1, each diagonal 1, item cover 2, snake 0.25 (so a snake is the rarest pick)
- `MaxUnbreakableSnakes` (2: a formation that would make a third picks another shape; 0 sets no cap)
- `JoinSnakeChance` (0.35: each member of a formation changing shape flies to a snake's tail instead)
- `SnakeWeightPerExtraUnbreakable` (0.25: the snake's weight 1.25x with one unbreakable past `MinMembers`, 1.5x
  with two)

The design and its open risks are in `Plans/PLAN-UnbreakableFormations.md`.
