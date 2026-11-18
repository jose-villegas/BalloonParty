@page plan_ripe_bubble_cluster Ripe Bubble Cluster

# The ripe bubble cluster: a full cluster is a bomb with a fuse

> On water, a bubble cluster that reaches the cap of 6 becomes **ripe**. It swells, and a fuse starts.
> Popped before the fuse runs out, its burst is the player's: a ripple knocks a hit off every neighbour.
> Left alone, its burst is the level's: it hands its bubbles out as foam and leaves. Each neighbouring
> balloon gets a one-bubble coat that must be popped first and washes the shot's colour; neighbouring
> clusters get the bubbles left after that.
> Planned 2026-11-18; the owner settled the open decisions the same day. **Status: building.**

---

## Owner direction (2026-11-18)

"I'm trying to come up with an idea for the bubbles in water levels to make a mechanic after they reach
the cap of 5 but I cannot think of any, water levels are kinda boring now, as the only pressure is a more
compacted board from the bubbles."

Four ideas were put forward (a fused bomb, foam that spreads, a burst that shoves the board, a cluster
that swallows the shot). The owner chose the first: "I like 1".

Standing rule that shapes it: difficulty comes from danger to the player, never from a scoring penalty.

On the two endings, once the first version was built: "Basically we could use the shader so when it
ripples basically goes from 6 to 1 in sequence forming a big single bubble and then pops. But for the
foam, it stays in warning color, and goes from 6 to zero, keeping the bubble division, giving its entries
to anything closer, until it runs out." Its two questions were answered the same day (decisions 10 and 11).

## What exists (verified 2026-11-18)

| Piece | Where | State |
|---|---|---|
| A cluster's count | `BubbleClusterModel` (`HitsRemaining`) | drops by the damage of each hit (`BalloonModelBase.EvaluateHit`); a surviving hit is `PassThrough`, scored, and breaks the streak |
| Merge | `BubbleMergeService`, `BubbleMergeRule` | wakes on `SlotGrid.MutationVersion`, a hit on a cluster, or its own last merge; a full cluster neither takes nor gives |
| The cap | `IBalloonsConfiguration.BubbleMergeMax` (5 when this was planned, 6 since: see the decisions) | the only limit on a cluster's size |
| Spawn size | `BalloonSpawner` (`StartBubblesForLevel`) | 1 bubble, plus one every 15 levels, capped at `BubbleMergeMax`: a cluster would arrive full from level 61 |
| Time on the level | `ISpawnClock.SecondsOnLevel` | advances only while the spawn clock runs: holds through pause, the level-up cinematic, game over and a doomed shot's glide |
| Hits from code | `IHitDispatcher.Dispatch` into `HitPipeline` | synchronous: score and streak, then the balloon's controller, then the message |
| Neighbours | `SlotGrid.GetNeighbors` | the six hex neighbours, no allocation |
| Colour wash | `ProjectileHitResolver.ApplyColorChange` | a type check (`IWashesProjectileColor`), made after the hit is evaluated |
| Hearts | `BalloonSpawner` (`WaveDeficitCalculator`) | a clock line with no room costs hearts; a cluster only takes up room |
| A fuse on a model | `Balloon/Reward/NukeFuse` | a model-held fuse that follows its balloon when it moves and stops on level change, game over and run reset |
| A press on the water | `DisturbanceFieldService.Stamp` | any repelling stamp reaches the water surface through `ImpactEventBus` and `WaterImpactCollector` |
| Foam on the water | `WaterRippleBuffer` | made only by crests folding in the simulation: there is no way to drop foam at a point |

Two things in the code as it stands get in the way and are fixed by the plan:

- **The solver does not see clusters as targets.** `ShotBoardGather.TryBuildTargetSnapshot` skips actors
  that are not `IHasScore`, and `BubbleClusterModel` is not one. The playtest bot aims through the solver,
  so no water comparison can be trusted until this is fixed (phase 0). Found by reading; not yet run.
- **"How many hits until it pops" is read from `HitsRemaining` in four places** (`IsTough`, the resolver's
  one-hit check, `ShotBoardGather`, `ShotSimulator`). A ripe cluster and a coated balloon both break that
  reading, so it moves behind one extension.

## The mechanic

```mermaid
stateDiagram-v2
    [*] --> Growing
    Growing --> Growing: merge or hit, below cap
    Growing --> Ripe: count reaches BubbleMergeMax
    Ripe --> Burst: any hit, folds to one bubble, the player's ripple
    Ripe --> Foamed: fuse runs out, the level's foam
    Foamed --> Left: bubbles handed out, leaves the board
    Ripe --> Gone: level change, game over, run reset
    Burst --> [*]
    Left --> [*]
    Gone --> [*]
```

### Ripening and the fuse

A cluster is ripe when its count reaches `BubbleMergeMax`: the existing cap is the threshold, with no
second number. Ripening starts a fuse of `FuseSeconds` (6) on `ISpawnClock.SecondsOnLevel`, held as a
deadline.

The fuse is in seconds on the level clock, not in spawn lines. Lines arrive anywhere from 0.55 s to 1.7 s
apart, and their rate is multiplied by things the player cannot see (up to 8 times faster on a sparse
board, 3 times faster in flight, slower in a breather). The level clock holds through exactly the moments
a timed threat would be unfair: pause, the level-up cinematic, game over.

### Popped in time: the ripple

**A ripe cluster is fragile: any hit bursts the whole cluster.** A cluster loses one bubble a hit, so
without this the first hit on a ripe 5 would make it a 4 and the choice would be between two bad rules.
If that hit defused it, the ripple could only come from a one-shot pop (a pierce, a bomb, a laser) and
the reward would be out of reach. If it stayed ripe, the ripple would cost five hits and five streak
breaks. Fragile makes the answer one accurate shot.

The burst sends one hit to each of the six neighbours through `IHitDispatcher`, so scoring, items and
chains behave as for any other hit:

- The ripple lands `BurstSeconds` (0.35) of level clock after the hit that popped the cluster. The cluster
  pops and scores at the hit, and its view spends that time folding its six bubbles into one big bubble
  and popping it; the ripple goes out as that bubble pops.
- Damage 1, a new flag `DamageFlags.Ripple`, no `DirectHit`. The ripple lands after the shot has flown
  on, so a ripple hit scores at the shot's streak as it stands and never changes it: it cannot climb it,
  break it, or restart it on the neighbour's colour. Without `DirectHit` it triggers no pop-spawn either.
- Reward balloons and balloons that deflect an ordinary hit (the unbreakable) are skipped.
- A ripe neighbour bursts in turn, `BurstSeconds` after the ripple popped it, folding first as the first
  one did, so a chain reads as a run of pops.
- The wait is on the level clock, so a pause holds it. A burst still waiting when the level completes,
  the game ends or the run resets is dropped with the fuses: the cluster has already popped and scored,
  and only its ripple is lost. The pop that completes a level therefore ripples nothing.
- A ripple strips foam coats, so popping a ripe cluster cleans the foam round it.
- A burst scores the bubbles the cluster held.

### Fuse runs out: the foam

The cluster hands its bubbles out and leaves. `RipeBubbleBurst.PlanFoam` decides where each bubble goes
(`RipeBubbleRule.PlanHandout`), in this order:

1. A **coat** to each neighbouring plain balloon (`ICoatable`: the plain balloon and its silver, gold and
   rainbow skins, item carriers included), up to `CoatLayers`.
2. Then one bubble at a time to **neighbouring clusters** with room below `BubbleMergeMax`: one per
   cluster a round, in `HexCoordinates.HexNeighborIndices` order, until the bubbles or the room run out.
   A cluster this fills to the cap ripens on the next tick and starts its own fuse.
3. Any bubbles still left pop harmlessly.

`RipeBubbleBurst.HandOut` applies the plan. It only sets state: it dispatches no hit. The cluster then
leaves the board through `IBalloonPopper.TryLeave`: its slot is freed at once, with no pop, no hit and no
score, while its view stays for `FoamLeaveSeconds` to hand the bubbles out. It stays ripe to the end, so
it keeps its warning colour. Bubbles are only moved or lost, never made, so the board's bubbles only fall.

- The first hit on a coated balloon removes the coat and passes through. The balloon is untouched, and
  an item it carries stays with it: an item goes off only when its balloon pops.
- That contact washes the shot's stolen colour, as a cluster does.
- It scores nothing and does not touch the streak. The cost is the extra hit and the lost colour.
- A piercing or beam hit pops the balloon, coat and all. That contact does not wash: the coat went with
  the balloon, so the shot steals the balloon's colour as from any pop.
- One coat at most (`CoatLayers`, 1). Toughs, unbreakables, reward balloons and grid actors take
  nothing. Clusters take bubbles, never a coat.

## Architecture

State sits on the models. One small controller beside `BubbleMergeService` makes the decisions, woken the
same way. The merge service itself stays as it is: it has no state and no teardown, and the fuse has both.

```mermaid
classDiagram
    class ICoatable {
        <<interface>>
        +IReadOnlyReactiveProperty~int~ Coat
    }
    class BubbleClusterModel {
        +IReadOnlyReactiveProperty~bool~ Ripe
        +IReadOnlyReactiveProperty~float~ FuseLeft
        +EvaluateHit(DamageContext) HitOutcome
        ~Ripen()
        ~SetFuseLeft(float)
        ~SetBubbles(int)
    }
    class BalloonModel {
        +IReadOnlyReactiveProperty~int~ Coat
        +EvaluateHit(DamageContext) HitOutcome
        ~TryCoat(int maxLayers) bool
    }
    class RipeBubbleRule {
        <<static>>
        +IsRipe(int bubbles, int max) bool
        +Throbs(float before, float after, float fast) bool
        +WarnAmount(float left, float fuse) float
        +RippleTakes(ISlotActor) bool
        +FoamTakes(ISlotActor, int maxLayers) bool
        +PlanHandout(int bubbles, coatRoom, clusterRoom) int
        +FoldCount(float elapsed, float burst, int from) int
        +FoamShownCount(int bubbles, float elapsed, float step) int
    }
    class RipeBubbleService {
        +Start()
        +Tick()
        +ResetRun(int)
        -OnActorHit(ActorHitMessage)
    }
    class RipeBubbleBurst {
        +Ripple(Vector2Int, DamageContext) int
        +PlanFoam(Vector2Int, int bubbles) int
        +HandOut(BubbleClusterModel, int handout)
    }
    class IBalloonPopper {
        <<interface>>
        +TryLeave(IBalloonModel, float linger) bool
    }
    class IBalloonPopPrelude {
        <<interface>>
        +TryPlayPopPrelude(Action onDone) bool
    }
    class SoapBubbleClusterVariant {
        +Bind(IBalloonModel, CompositeDisposable)
    }
    class FoamCoatOverlay {
        +Create(Transform, SpriteRenderer, IRipeBubbleSettings)$
        +Show()
        +Hide()
        +PuffAndHide()
    }
    class BalloonView {
        +HoldCoat()
        +ReleaseCoat()
    }
    class RipeBubbleBurstView
    class SoapBubbleHop {
        <<static>>
        +Place(Transform, Vector3, Vector3, float, float, Vector3, float)$
    }
    BalloonModel ..|> ICoatable
    RipeBubbleService --> RipeBubbleRule
    RipeBubbleService --> RipeBubbleBurst
    RipeBubbleService --> BubbleClusterModel : ripens and runs fuse
    RipeBubbleBurst --> IHitDispatcher : ripple hits
    RipeBubbleBurst --> BalloonModel : TryCoat
    RipeBubbleBurst --> BubbleClusterModel : one more bubble
    RipeBubbleBurst --> IBalloonPopper : the foamed cluster leaves
    SoapBubbleClusterVariant ..|> IBalloonPopPrelude
    BalloonView --> IBalloonPopPrelude : asks before the pop
    SoapBubbleClusterVariant --> BubbleClusterModel : reads Ripe, FuseLeft
    BalloonView --> ICoatable : reads Coat
    BalloonView --> FoamCoatOverlay : creates on the first coat
    RipeBubbleService ..> RipeBubbleBurstView : RipeBubbleBurstMessage
    RipeBubbleBurstView --> BalloonView : holds and releases the coat
    RipeBubbleBurstView --> SoapBubbleHop
```

| Type | Layer | What it does |
|---|---|---|
| `BubbleClusterModel` (new fields) | Model | `Ripe`, `FuseLeft`; while ripe, any damage pops it and it scores the count it held |
| `BalloonModel` (new field), `ICoatable` | Model | `Coat`; a coated balloon's first ordinary hit removes the coat and passes through |
| `RipeBubbleRule` | pure static | when a cluster is ripe, which neighbours a ripple hits, which take a coat, where a foam's bubbles go, and the fold's and the hand-out's schedules |
| `RipeBubbleService` | Controller (`IStartable`, `ITickable`, `IRunResettable`) | ripens clusters at the cap, runs each fuse, queues bursts `BurstSeconds` after their hit, hands a foamed cluster's bubbles out, stops on level change, game over and run reset |
| `RipeBubbleBurst` | Controller | the ripple's hits; the foam's plan (`PlanFoam`) and hand-out (`HandOut`: coats, bubbles for clusters, then the cluster leaves); stamps the water. The ripple returns the neighbours it reached, a bit per direction; the plan is the foam's message |
| `IBalloonPopper.TryLeave`, `BalloonController.Leave` | Controller | takes a balloon out of play with no pop, hit or score, and returns its view to the pool after a linger |
| `IBalloonPopPrelude` | View seam | a view binding that plays before its balloon's pop; `BalloonView.TryPlayPopPrelude` asks each binding, and the controller presents the pop when the prelude ends |
| `SoapBubbleClusterVariant` | View, on the cluster prefab | swell, faster float, throb and tint from `Ripe` and `FuseLeft`; the fold, as the cluster's `IBalloonPopPrelude` |
| `FoamCoatOverlay` | View, made in code by `BalloonView` the first time its balloon is coated | a child sprite with the soap material showing one bubble round the balloon, shown while `Coat` is above 0 and hidden as the pop starts. It fits itself on every show from the body's local transforms up to the balloon's root, so the balloon's own world scale (a spawn tween, a pool at zero scale) never enters the fit |
| `SoapBubbleShader` | shared static | the soap bubble shader's property IDs and look defaults, the float rebase, a lone bubble's look and size, used by the cluster variant, the coat and the burst view |
| `RipeBubbleBurstView` | View | the ripple ring, and the foam's hand-out: a bubble at a time hopping to each taker or drifting off |
| `SoapBubbleHop`, `SoapBubbleSprites` | View helpers | the hop's arc and the pool of loose bubbles, shared by `BubbleMergeView` and `RipeBubbleBurstView` |

New messages: `BubbleRipenedMessage`, `RipeFuseBeatMessage` (each fast throb near the end of a fuse) and
`RipeBubbleBurstMessage` (ripple or foam, and which neighbours it touched, one bit per direction in the order
`HexCoordinates.HexNeighborIndices` lists them: the ripple's hits, or the foam's coats). A foam's message
also carries its plan in one `int`, four bits a bubble in the order they leave (a coat or a cluster and
its direction, or a leftover) and the count above them, so it allocates nothing. A coat coming off needs none: it is already an
`ActorHitMessage` with `PassThrough`, which `ActorHitMessageExtensions.TookCoat` tells apart from a resist.

Points that are easy to get wrong:

- **The ripple is never dispatched from inside a hit handler.** `ProjectileHitResolver` reads the shot's
  streak straight after its own dispatch to grant shields; a ripple nested inside that dispatch would
  change it underneath. The service queues the burst when it hears the hit and drains the queue in its
  own `Tick`, with an index loop, so a chain is picked up without recursion. At most 12 clusters exist
  (`MaxBubbles`) and each pops once, so a chain is bounded.
- **The service holds models, not slots,** and checks each tick that the model is still the one in its
  slot, as `NukeFuse` does, so a cluster the balancer moves keeps its fuse.
- **A ripe cluster is not anchored.** An anchored actor can seal a pocket off from the spawn lines.
- **The wash is read before the hit is evaluated.** After it, the coat is gone. One extension,
  `WashesProjectileColor`, answers for both a cluster and a coated balloon; the resolver uses it. The
  solver does not: a coat can come off mid-flight, so its gather keeps the coat count (`ICoatable`) and the
  marker apart, and decides the wash at each simulated contact. `BalloonModel` does not take the
  `IWashesProjectileColor` marker itself: the solver reads that marker as "resists paint".
- **A ripe cluster is not tough, nor is a coated balloon.** `IsTough` treats more than one hit remaining
  as plough-through and discharge at the wall; a piercing shot must burst a ripe cluster, and pop a coated
  balloon, on contact. So `HitsToPop` counts the coat (a coated one-hit balloon takes two ordinary hits, and
  is no clean sweep) and `IsTough` does not.
- **Only a pop releases an item.** `ItemActivator` used to fire on any hit on a carrier, which was the same
  thing while a plain balloon could not survive a hit. A coat makes it survive one.
- **The fold is a prelude, not a cluster case in the controller.** `BalloonController` asks its view for a
  pop prelude; with none, the pop is presented at once as before. With one, the balloon leaves the grid
  and the registry at once, and the pop is presented when the prelude calls back. The model popped at the
  hit, so the rules and the score never wait on the view.
- **The foam's message goes out before the models change,** so the burst view can hold what each taker
  shows (the coat, the cluster's count) until its bubble lands; the cluster's view is still at its slot.
- **A foamed cluster leaves, it does not pop.** `TryLeave` frees the slot with no hit, so nothing scores,
  nothing fires, and the merge service sees only a grid change.

```mermaid
sequenceDiagram
    participant Res as ProjectileHitResolver
    participant Pipe as HitPipeline
    participant Ripe as RipeBubbleService
    participant Burst as RipeBubbleBurst
    participant Grid as SlotGrid
    Res->>Pipe: Dispatch hit on ripe cluster, outcome Pop
    Pipe->>Ripe: ActorHitMessage
    Ripe->>Ripe: enqueue burst at slot, no dispatch here
    Note over Ripe: a Tick BurstSeconds later, outside the resolver call stack
    Ripe->>Burst: Ripple at slot with the shot as Projectile
    Burst->>Grid: GetNeighbors of slot
    loop each neighbour that RippleTakes
        Burst->>Pipe: Dispatch Damage 1, flags Ripple
        Pipe->>Ripe: ActorHitMessage, a ripe neighbour popped
        Ripe->>Ripe: enqueue chain burst, due BurstSeconds later
    end
    Burst->>Burst: stamp disturbance
    Burst-->>Ripe: which neighbours it hit
    Ripe->>Ripe: publish RipeBubbleBurstMessage
```

### The coat: why it is state on the balloon

| Option | Verdict |
|---|---|
| A `Coat` count on `BalloonModel`, checked in its `EvaluateHit` | **Chosen.** The balloon keeps its identity: slot, registry handle, item, rainbow paint, type counts. One override, one overlay the view makes in code |
| A coat on every balloon model, through the base class | Touches four models to coat actors that should take none |
| Swap the neighbour for a "coated" actor | Loses the item, the paint and the pool counts |
| A service that intercepts hits outside the model | Hits are evaluated inside the model by six or more callers; the check has to live there |

Nothing is saved mid-level and models are built new for each spawn, so ripe state and coats need no
reset or save work of their own.

## Presentation

All of it is done from C# with the soap shader's existing properties; nothing here edits a shader.

- **Ripe** (`SoapBubbleClusterVariant`): the cluster swells to `SwellScale` over `SwellSeconds`. The swell is
  a factor the variant keeps, so a merge's wobble or a throb punches from the swollen size and settles back
  to it. The float runs `FloatSpeedup` times faster, with `_TimeOffset` rebased
  (`SoapBubbleShader.RebaseTimeOffset`) so the bubbles do not jump. The fuse throbs once a second, then
  twice a second in its last `ThrobFastSeconds` (`RipeBubbleRule.Throbs`), each a punch of
  `ThrobStrength`. The cluster fills with `WarnTint` (`RipeBubbleRule.WarnAmount`): `WarnStart` of the way
  the moment it ripens, so the player sees at once that it has turned, then evenly to full as the fuse runs
  out. It spins up with the same amount, to `SpinSpeedup` times its rest spin, its angle rebased as the
  float's offset is. The fill is the soap shader's flush (`_Flush`, set per renderer), the one shader
  addition of this plan besides the sixth layout: a clear film can't carry a tint, so the renderer's own
  colour showed nothing. Binding another model or despawning puts scale, flush, spin and float speed back.
- **Spiral:** a ripe cluster sheds soap bubbles from a point that turns round it
  (`RipeBubbleBurstView`), one every `SpiralInterval`; each flies straight out, growing, and fades at
  `SpiralReach`, so together they draw a spiral arm winding out of the cluster. The ripening and each beat
  of the fuse (`RipeFuseBeatMessage`, now sent for every throb with a `Fast` flag) feed the source a little
  longer than the wait for the next beat, so the arm is unbroken while the fuse runs and stops when the
  cluster bursts or leaves the board. The source follows the cluster's view, and its bubbles are placed from where
  the cluster is now, so the arm travels with a cluster the board moves. In the fuse's last seconds it
  sheds `SpiralFastSpeedup` times faster.
  Owner, on the first version (a ring of five bubbles thrown on each throb): "the spiral doesn't read",
  with a picture of one arm of many bubbles growing outward; and "color shift early, so the user knows
  it's going to happen, the pulse a bit larger, so it reads as danger".
- **Fold** (`SoapBubbleClusterVariant`, the cluster's pop prelude): the hit on a ripe cluster steps its
  shown count down a bubble at a time, 6 to 1, over `FoldShare` (0.75) of `BurstSeconds`, keeping the
  flush. A scale makes up for each smaller layout (`RipeBubbleRule.FoldScale`), so the bubbles draw
  together into one big bubble about the cluster's outline, growing `FoldGrowth` (0.1) as it goes. It
  holds that size for the rest of `BurstSeconds`, then the pop plays as any balloon's does.
- **Ripple:** the water takes the `BubbleRipple` stamp. `RipeBubbleBurstView` draws a ring over it: one
  soap bubble in the coat's material, under the balloons, that grows from about the folded bubble's size
  to `RingReach` slot spacings and fades out over `RingSeconds`.
- **Foam:** the cluster keeps its warning colour and empties a bubble at a time, every `FoamStepSeconds`
  (0.12), keeping the bubble division. Each bubble that leaves goes where the plan says: a coat hops to its
  balloon on the bubble merge's arc (`SoapBubbleHop`, timed by `IBubbleMergeCueSettings`) and the coat
  shows as it lands (`BalloonView.HoldCoat` until then); a cluster's bubble hops in as a merge's does, and
  the cluster's count, held until then, rises by one; a leftover drifts a little way out and puffs as a
  coat does. The cluster's view is hidden as its last bubble leaves.
- **Coat:** a child renderer `BalloonView` makes in code the first time its balloon is coated, using the
  soap bubble material with one bubble, centred on the body and scaled to `CoatScale` times its radius,
  sorted over the balloon and its item. A hit that takes it off puffs it away: it swells to
  `CoatPuffScale` and fades over `CoatPuffSeconds`. Binding, a pop and despawning hide it at once.
- **Audio** (`CombatSoundRouter`): every cue is the plain balloon pop, pitched and scaled, so it sits in
  the colour pops' key without moving the ambient one. Ripening is an octave up and quiet. Each fast throb
  is the disco wave's tick. The hit on a ripe cluster plays a fifth up at half volume in place of its pop,
  as the fold starts. The player's burst, a fourth down and loud, plays when the folded bubble pops and the
  ripple goes out, and the ripple's own pops play at their colours where the shot's ramp stands, without
  climbing it. Foam is an octave down and soft, once as the fuse runs out. A coat
  taken off is a light pop a fourth up, where any other pass-through or absorb plays the resist.
- **Telemetry** (per level, `GameplayMetricsService`): `clusters_ripened`, `ripple_bursts`,
  `ripple_neighbours_hit`, `fuses_run_out`, `coats_applied` and `coats_removed`. A coat hit counts as a
  coat removed, not as an absorb.
- A digit counting the fuse down, and foam drawn on the water's surface, are both shader edits. Neither
  is in the plan.

All of it needs a look and a listen in the Editor.

## Settings

`IRipeBubbleSettings`, a block on `IBalloonsConfiguration` beside `BubbleMergeMax`.

| Setting | Default | Meaning |
|---|---|---|
| `FuseSeconds` | 6 | seconds of level clock from ripe to foam; 0 turns ripeness off |
| `BurstSeconds` | 0.35 | level clock from a ripe cluster's pop to its ripple, the fold's time; a chain waits as long at each link |
| `RippleDamage` | 1 | the hit each neighbour takes |
| `FoamStepSeconds` | 0.12 | the gap between the bubbles a foaming cluster hands out |
| `CoatLayers` | 1 | the most coats a balloon holds |
| `CoatMaterial`, `CoatSprite` | `SoapClusterMaterial`, the cluster prefab's sprite | what the coat is drawn with |
| `CoatScale` | 1.15 | the coat bubble's radius over the balloon body's |
| `SwellScale`, `SwellSeconds` | 1.12, 0.25 | a ripe cluster's size over its rest size, and how fast it gets there |
| `ThrobStrength`, `ThrobSeconds` | 0.22, 0.3 | each throb's punch and how long it takes to settle |
| `ThrobFastSeconds` | 2 | the end of the fuse that throbs twice a second, and is heard |
| `WarnTint`, `WarnStart` | (1, 0.55, 0.4), 0.35 | the colour a ripe cluster fills with, and how far it has filled the moment it ripens |
| `SpinSpeedup` | 14 | a ripe cluster's spin over its rest spin at the end of its fuse |
| `SpiralInterval`, `SpiralSeconds`, `SpiralReach` | 0.045, 1.4, 1.3 | seconds between the spiral's bubbles, how long each lives, how far it gets (slot spacings) |
| `SpiralTurnsPerSecond`, `SpiralFastSpeedup`, `SpiralBubbleRadius` | 1, 1.6, 0.14 | how fast the arm turns, how much faster it sheds in the last seconds, a bubble's size at the arm's end |
| `FloatSpeedup` | 1.6 | a ripe cluster's float speed over its rest speed |
| `CoatPuffSeconds`, `CoatPuffScale` | 0.12, 1.3 | how long a coat taken off takes to puff away, and how big it gets |
| `RingSeconds`, `RingReach` | 0.35, 1 | how long the ripple's ring takes, and how far it spreads in slot spacings |
| `RingSortingLayerName`, `RingSortingOrder` | `Balloons`, -1 | where the ring draws: under the balloons |

The foam's hops reuse `IBubbleMergeCueSettings` (`HopSeconds`, `ArcHeight`, `BubbleScale`,
`SortingOrderOffset`), so the two kinds of hopping bubble stay alike; they leave one per
`FoamStepSeconds` rather than the merge's `HopStagger`. The fold's share of `BurstSeconds` and its growth
(`FoldShare`, `FoldGrowth`) are constants in `RipeBubbleRule`, a leftover's drift is a constant in
`RipeBubbleBurstView`, and the sounds' pitches and volumes are constants in `CombatSoundRouter`.

No water-only switch is needed: clusters spawn only on water, and the board clears on every level change.

## Owner decisions (2026-11-18)

Eight decisions were put to the owner, each with a recommendation. The answer: "Go with your
recommendations, build the ripe cluster".

1. **A hit on a ripe cluster:** it bursts whole. (The other ways: it un-ripens, or it stays ripe and
   needs all 5 hits.)
2. **The fuse's clock:** seconds of level clock, 6 to start, not spawn lines.
3. *(Replaced by decision 10.)* **A cluster whose fuse ran out:** spent to 1 bubble and stays. Leaving the
   board would trade five hits of cluster for at most six one-hit coats, which is close to no danger at
   all; re-arming at the cap would make a foam fountain.
4. *(Replaced by decision 11.)* **Foam on a neighbouring cluster:** nothing. One more bubble would ripen
   clusters with foam and could run away.
5. **A ripple reaching a ripe neighbour:** it bursts too.
6. **A hit on a coat:** washes the colour and nothing else: no points, no streak break.
7. **Clusters arriving from the lines:** at most one below the cap, so a cluster always ripens through a
   merge and none arrives ripe from level 61.
8. **A ripe burst's score:** the bubbles it held (6, with the cap below). Before this a piercing pop of any cluster scored 1,
   less than chipping it down.
9. **The cap is 6, not 5** (the same day, after seeing it staged): "Cap the bubbles to 6, it makes more sense
   as it basically covers all its neighbors". A full cluster is a ring of six, one bubble toward each
   neighbouring slot (the shader's sixth layout), so the ripple and the foam reach exactly what the
   cluster shows it touches. Clusters arrive from the lines with at most 5.

The endings were then reworked on the owner's direction (quoted at the top), with two questions put to the
owner:

10. **May the cluster disappear after foaming?** "Yes". It hands every bubble out and leaves the board with
    no hit and no score, in its warning colour to the end. This replaces decision 3.
11. **Where do its bubbles go?** A suggestion was put: coats to the plain neighbours first, then one bubble
    at a time to neighbouring clusters up to the cap, then any left over pop harmlessly. The answer: "Go
    with your suggestion". This replaces decision 4. A cluster the foam fills ripens and starts its own
    fuse, but bubbles are only moved or lost, so the board's bubbles only fall.
12. **The ripple** follows the direction as it stands: the ripe cluster folds 6 to 1 into one big bubble
    and pops, and the ripple goes out then, `BurstSeconds` after the hit, for a chained burst too.

## Phases

Each phase can be played and checked on its own.

| Phase | What | Check | Risk |
|---|---|---|---|
| 0 | Clusters become `IHasScore`, so the solver sees them | the solver window shows clusters as targets | the bot's water baselines move |
| 1 | Ripe state, the fuse, the fragile burst, `IsTough`, the spawn cap, settings. Expiry only spends the cluster for now; the swell shows ripeness | ripe clusters swell, pop in one hit, shrink when the fuse runs out; the fuse holds through pause and level-up | whether 6 s is fair |
| 2 | The ripple: `DamageFlags.Ripple`, the queue, chains, the water stamp | a crowd round a ripe cluster is thinned; two ripe neighbours chain | score, streak and items firing through chained hits |
| 3 | The coat: `ICoatable`, the balloon's `EvaluateHit`, the resolver's wash and one-hit checks, foam on expiry, the overlay | an ignored ripe cluster leaves coated balloons that take two hits and wash the shot | it touches the resolver, the core hit path |
| 4 | The solver knows coats and ripe clusters; bot runs on water with and without ripeness | `RipeClusterMirrorTests`; bot reports | the solver drifting from the game. **Solver built**, its known gaps in the ShotSolver README; the bot runs are still to do |
| 5 | Presentation: float, throb, tint, ring, foam hops, coat puff, audio, telemetry counters | a look and a listen in the Editor. **Built**; not yet seen or heard | none beyond taste |
| 6 | The endings reworked (decisions 10 to 12): the fold and the delayed ripple, the foam's hand-out and the cluster leaving | a ripe pop folds into one bubble before its ring; an ignored one empties a bubble at a time into its neighbours and is gone. **Built**; not yet seen or heard | the pop prelude touches every pop's path; clusters fed by foam ripening in a run |

## Tests

- `RipeBubbleRule`: the ripe threshold; which neighbours a ripple hits and which take foam (rewards,
  deflectors, clusters, already coated); fuse left from the deadline; the hand-out plan (coats first, then
  clusters a round at a time up to the cap, then leftovers); the fold's and the hand-out's schedules.
- `BubbleClusterModel` when ripe: ordinary, piercing and indirect damage all pop it; it scores what it held.
- `BalloonModel` with a coat: an ordinary hit removes the coat and the balloon survives; piercing and beam
  pop it; indirect damage removes the coat.
- `RipeBubbleService`: ripens at the cap; the fuse fires foam once; stops on level change, game over and
  reset; follows a moved cluster; a burst heard during a dispatch is not dispatched inside it; two ripe
  neighbours each burst once; the ripple waits `BurstSeconds`, a chain as long at each link, and game over
  during the wait drops it; the foam coats six, or coats three and leaves three over, fills a cluster to
  the cap (which ripens a tick later), coats before it gives to clusters, gives a full cluster nothing,
  and the cluster leaves.
- `ProjectileHitResolver`: a coated balloon washes the shot and does not count as a one-hit pop; a ripe
  cluster is not ploughed through; a coated rainbow's coat hit carries no streak.
- `ItemActivator`: a coat hit on a carrier releases nothing; the pop releases the item.
- `FoamCoatOverlay`: the fit does not change with the balloon's world scale, survives a zero scale, and
  follows the body on every show.
- The solver: `RipeClusterMirrorTests` play the same shot through the game's score, streak, resolver and
  ripple and through `ShotSimulator`, and compare score, colour and streak.
- Presentation: the throb schedule and the warning curve (`RipeBubbleRuleTests`); the float rebase, the hop's
  path, the burst message's direction bits and its hand-out entries (`RipeBubblePresentationTests`); the
  fast beats the service announces; the router's cue for a coat hit, a resist, a ripple's pops and a ripe
  cluster's fold (`CombatSoundRouterTests`); a cluster leaving the board wakes the merge
  (`BubbleMergeServiceTests`); the
  six counters (`GameplayMetricsServiceTests`, `MetricCatalogFoldTests`).
- Existing: `BubbleClusterModelTests`, `ProjectileHitResolverTests`, the solver's `EffectBoardMirrorTests`.

## What to watch

A ripe cluster in the middle of a crowd cannot merge or move out of the way, so the fuse partly rewards
how the board happens to lie. One-hit bursts and a 6 s fuse should keep that fair; the bot runs in phase 4
are where to check it.
