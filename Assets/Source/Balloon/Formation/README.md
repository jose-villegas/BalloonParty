# Balloon/Formation

Unbreakable balloons normally roam: once per wave each one jumps toward the far side of the board. When
three or more are on the board, they gather into a **formation** instead. They line up, stay in place for
a few waves, then switch to a different line. While a formation stands, its members float in midair: the
balloons below rise until they reach the formation, and spawn lines stop under it.

Several formations can stand at once. Unbreakables left over after one formation is full form a second
one, so two lines can stack into a blockade. A formation the player leaves alone calls in more
unbreakables, past the level's usual cap, and those form formations of their own.

## Contents

| File | What it does |
|---|---|
| `FormationShape` | The four shapes: `Horizontal`, `DiagonalDownRight`, `DiagonalDownLeft`, and `ItemCover` (a row right under a balloon that carries an item, so shots from below can't reach the item directly) |
| `FormationPlanner` | Pure planning. For one shape it lists every spot on the board where the shape fits, works out how far the members would travel to fill it, and picks at random among the few cheapest spots. It then says which member goes to which slot. It reads the grid but never changes it |
| `FormationAssignment` | One member and the slot it goes to |
| `StandingFormation` | One standing formation: its members, its shape, the waves it has left to hold, and whether a member was pierced since it formed |
| `UnbreakableFormationService` | The controller. It decides when each formation forms, holds, changes and breaks up, marks members as in formation, and rolls for reinforcements |
| `IFormationReinforcements` | The reinforcements the formations have called in. `BalloonSpawner` takes them at each wave's first line |

## How a formation runs

1. **Wave start.** `BalloonBalancer.Balance(atWaveStart: true)` calls `ArrangeAtWaveStart` before the
   usual roam pre-pass. Roamers therefore see the formation's slots as already taken.
2. **Forming.** If at least `MinMembers` unbreakables are free (in no formation), the service picks a
   shape for them. The pick is weighted at random from the shape weights. It leaves out the shape just
   held, and leaves out `ItemCover` unless an item is on the board. If the picked shape fits nowhere, the
   service tries the next one. If nothing fits, nothing forms and the unbreakables roam as usual. A
   formation takes at most `MaxMembers`; while `MinMembers` or more are still free after it, they form
   another one.
3. **Moving in.** The service hands each member's move to the balancer's relocate routine. That is the
   same routine roam jumps use, so the members glide in at their normal speed. Each member is marked
   in formation (`IWriteableFormationMember.SetInFormation`).
4. **Holding.** The formation lasts `HoldWaves` waves, counting the one it formed in. During that time
   the next wave starts only count down: nothing moves.
5. **Changing.** At the first wave start after the hold, a new shape is planned for that formation's
   members and any free unbreakables. Members that stay in the formation are never marked out, even for a
   moment. Members the new shape doesn't need turn red and roam again. If no shape fits, the formation
   breaks up. Each formation keeps its own count, so they change at different waves.
6. **Breaking up.** The service watches `SlotGrid.OnChanged`. When a member leaves the grid (it was
   pierced, or the board was cleared) it drops out of its formation. If fewer than `MinMembers` are left
   in that formation, the rest are released at once. The other formations stand. The next line's balance,
   or the timed pulse, floats the released ones up. `ResetRun` releases everything.

## Reinforcements

A formation the player doesn't break calls for help. At each wave start, every formation that has had no
member pierced since it formed rolls `ReinforcementChance`. Each success calls in one more unbreakable.
The wave a formation forms in doesn't roll, so the player always gets a wave to answer it. A formation
keeps its record through shape changes: once pierced, it never calls again.

The service only counts the calls (`IFormationReinforcements`). `BalloonSpawner` takes them right after
the formations arrange, since both run at the wave's first line, and adds that many unbreakables to the
line. They enter like any other balloon. They don't count against the level's own unbreakable cap or wave
quota. Instead, at most `ReinforcementCeiling` of them are on the board at once (`ReinforcementPlan`).
Calls past the ceiling, or past the line's open places, are dropped. On the board they are ordinary
unbreakables: they roam, and once enough are free they form a formation of their own.

## Rules a formation keeps

- **No pushing.** A formation slot must be empty or already hold one of the unbreakables it is planning
  with. A formation never pushes another balloon aside, and never plans over another formation's
  members.
- **Spawn room.** It never uses the bottom `BottomClearance` rows, so spawn lines always have room to
  enter.
- **Members keep their slot.** A member already standing on a formation slot keeps it: assignment
  takes the nearest pairs first, so its distance-0 pair always wins. As a result, no member ever has to
  move into a slot another member still holds.
- **The covered item isn't held.** `ItemCover` covers the item where it sits when the formation forms.
  The item's balloon is not anchored, so if it pops or rises away the cover stays where it is until the
  next change.

## Anchoring

A member in formation reports `IBalanceAnchor.IsAnchored` (from `Slots/Actor`). Everything that moves
balloons leaves an anchored actor in its slot:
- the balance race (`BalancePlanner`) and `BalloonBalancer.HasPossibleMove`,
- the roam pre-pass, and the model's own `TryPickRelocation`,
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
- `Enabled`
- `MinMembers` (3) and `MaxMembers` (5)
- `HoldWaves` (3)
- `BottomClearance` (2)
- `SpotChoices` (3)
- `ReinforcementChance` (0.35) and `ReinforcementCeiling` (4)
- the shape weights: horizontal 1, each diagonal 1, item cover 2

The design and its open risks are in `Plans/PLAN-UnbreakableFormations.md`.
