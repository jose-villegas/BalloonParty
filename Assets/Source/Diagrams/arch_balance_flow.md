@page arch_balance_flow Balance Flow

# Balance Flow

@image html balance_flow.svg "Balance Algorithm — Per-Actor Transit Tracking"

## What this diagram shows

How `BalloonBalancer` moves balloons up to fill grid gaps, and how it keeps concurrent
spawn animations aware of in-progress movement. Balance keeps the stack packed against
the board's top edge after every pop, spawn, and shot — closing gaps immediately so
downstream systems (spawn placement, pressure propagation) can assume a compact board
rather than scattered holes.

**Core algorithm — a priority race, not a single scan:**
`Balance()` delegates the actual decision-making to `BalancePlanner.Plan`. Each pass
snapshots every currently unbalanced actor (a slot whose support above is missing — see
"Motion direction" below) and sorts them by `IBalanceInfluence.BalancePriority`: higher
priority acts first within the pass and wins any slot two actors would otherwise both
want. The planner mutates the grid (`Remove`/`Place`) for every move as it happens, so
later candidates in the same pass — and every candidate in the next pass — see the
already-updated occupancy. Passes repeat until one produces no move at all, with a
`maxPasses` backstop and a per-actor revisit guard that breaks any cycle (an actor with
`OmnidirectionalBalance` true and `MaxBalanceSteps` unlimited — today, BubbleCluster — can
have a side move tie its up move and ping-pong between the same two slots forever without
the guard). Because settling can take several passes, one actor's final path can be a
chain of hops across multiple slots, not a single jump.

Heavy actors are metered by `MaxBalanceSteps`: each planned hop counts against a shared
`turnSteps` budget that spans every balance run between projectile deaths (pre-spawn,
post-spawn, and any in-flight pulses), so a heavy balloon visibly rises one slot per turn
instead of teleporting clear across the board in a single settle.

Planning only touches the grid — no views, tweens, or `IsStable` flips happen mid-plan.
Once `Plan` returns its list of moves, `ApplyBalanceMoves` walks them in execution order:
reserving transit slots in `BalancePathHolder`, flipping `IsStable` to false, and
recording each actor's next waypoint. (`IsStable` is a reactive flag each dynamic actor
exposes — false for the duration of any in-flight balance/spawn/pressure move, true once
its tween completes; the view drives its Animator bool off it.) `AnimatePaths` then starts
one DOTween CatmullRom path per actor across its recorded waypoints.

**Motion direction — buoyancy, not gravity:**
Balloons rise. `GridBalanceQuery.IsUnbalanced` flags a slot as unsupported when either of
the two hex slots above it is empty: straight up, and the parity-shifted diagonal (the
standard offset-row hex stagger — one column left on an even row, one column right on an
odd row), both at row − 1. The two "up" neighbours (`to.y == from.y - 1`) are always
legal, shove-free candidates; a balloon settles into the best empty slot above it,
nestling under whatever is already occupied there. Side and down moves only ever happen
under an active shove (see `PressurePropagation` below) or for an actor explicitly marked
`OmnidirectionalBalance`.
Read this as buoyancy — balloons drift upward looking for support — never as gravity or
"falling."

**Scoring:**
`GridBalanceQuery.OptimalNextEmptySlot` forwards to
`MoveWeightEvaluator.OptimalBalanceMove`, which scores each up-neighbour by recursively
summing the occupied cells in the fan between that candidate and the board's top row —
the more filled cells sit above the candidate (nearer the ceiling), the higher it scores
— plus that actor's own `IBalanceInfluence.WeightBias` — an offset subtypes use to shape
their own formations (e.g. a preference for or against clustering with same-type
neighbours). This is what makes a mover nestle under an existing cluster instead of
drifting toward an open column, consistent with the buoyancy direction above. When
the straight-up and diagonal-up candidates tie, the diagonal wins (`>=` comparison); this
biases settling slightly toward the grid's center over time, which reads more naturally
than strict left/right alignment.

**When there's no free up-move — `PressurePropagation`:**
If a column is jammed solid and a new balloon needs a slot, `BalloonBalancer.TryRelievePressure`
runs a directed, weighted search that shoves through occupied movable actors until one can
vacate into a genuinely empty slot, favoring cheaper (lighter) chains. This is a distinct
resolve path from the priority race above, used specifically to open room for a spawn.

**Entry points — when a balance run happens:**
- A death-frame pass: `ProjectileView` publishes `BalanceBalloonsMessage` when the shot
  dies. This is deferred one frame (`RequestBalance` → `BalanceNextFrameAsync`).
- The pre-spawn pass: `BalloonSpawner` calls `Balance(relocateRoamers: true)` directly,
  synchronously, before placing new lines. Because a direct call services any pending
  deferred request immediately, this coalesces with the death-frame publish above into one
  sweep. `relocateRoamers: true` also runs the `IPreBalanceRelocatable` pre-pass once per
  turn — each roamer (an `IPreBalanceRelocatable` implementer; today only Unbreakable)
  picks its own destination among already-settled empty slots, in `BalancePriority` order,
  before the balance race resolves everything around it — rather than being swept toward
  the nearest gap like an ordinary actor.
- The post-spawn pass: after all lines are placed, `BalloonSpawner` publishes
  `BalanceBalloonsMessage` again — deferred to the next frame — to settle any gap the fresh
  spawn just created.
- In-flight rebalance pulses: while a shot is airborne, `BalloonBalancer.Tick` fires a
  deferred balance every `FlightRebalanceInterval` seconds, but only when
  `HasPossibleMove()` finds something to do — most flights, the board is already settled
  and no pulse is scheduled.

**Transit tracking — `BalancePathHolder`:**
`ApplyBalanceMoves` reserves every slot along an actor's full move chain — its original
slot, every intermediate hop from earlier passes, and its final slot — all under that
actor's identity in `BalancePathHolder`. While reserved, `ComputePath` treats those slots
as occupied-in-transit and emits a warning if a spawn path crosses them. The whole chain is
released together, in the tween's `OnComplete`, via `BalancePathHolder.Release(actor)`.

## Guidance

**Why a race instead of a single ordered scan:**
Different actor types need to visibly behave differently under the same gap — a normal
balloon rises immediately, a heavy one should lag, and some types want first claim on a
contested slot. Sorting each pass by `BalancePriority` and letting higher-priority actors
move (and thus occupy contested slots) before lower-priority ones gives every actor type a
lever over its own timing without hardcoding per-type logic into the planner.

**Why the step budget is per-turn, not per-run:**
A `BalloonBalancer.Balance()` call can run several times between one projectile's death
and the next shot (pre-spawn, post-spawn, flight pulses). If `MaxBalanceSteps` reset on
every one of those runs, a "slow" heavy actor would actually take one step per run and
read as just as fast as anything else, only choppier. Keeping the budget in `_turnSteps`
across the whole turn is what makes a step cap actually read as "moves slower."

**`BalancePathHolder` is your read point for in-flight balance moves:**
If you're writing a system that needs to know whether a given slot is currently being
vacated or occupied by a balance animation, query `BalancePathHolder.IsInTransit(slot)`
before assuming a slot is stably empty.

