# Game/HeadsUp

Decides which handwritten heads-ups go on screen, and when. A heads-up is a short pencilled line
such as `x3!`, `-1 bounce` or `ready`, written where something just happened. This folder does not
draw them: `UI/HeadsUp/` draws them. The design is in `Plans/PLAN-HeadsUp.md` §3.

Gameplay code publishes the messages it already has (a pop growing a streak, a wall hit, the cannon
reloading). Each **router** turns those messages into heads-up requests. The **service** asks the
**queue** whether a request may show, then tells the **presenter** (the view) what to draw. A burst
of events can't flood the screen: there is a cap, and each kind of heads-up has a priority.

## Contents

| File | What it does |
|---|---|
| `HeadsUpRequest` | One ask: which style (`HeadsUpStyleId`), the number it shows (`Arg`), where (`HeadsUpAnchor`), an optional `HeadsUpMergeKey`, and an optional `Word` for the styles that write one (a colour's name) |
| `HeadsUpAnchor` | Where a line goes. Either a fixed world point (`At`), or a named HUD endpoint (`Endpoint`) such as the heart, the cannon or a colour's bar, using the keys in `TrailEndpointKeys`. Endpoint lines follow their anchor |
| `HeadsUpMergeKey` | Lets a new request rewrite a line already showing instead of adding another. Two keys match when they have the same owner (compared by reference) and the same channel. The streak uses (ball, colour), so `x3!` becomes `x4!` in place. A request without a key never merges |
| `HeadsUpHandle` | Names one showing line: its slot and that slot's generation. A slot reused for a new line gets a new generation, so an old handle stops working (it is stale) and can't touch the new line |
| `HeadsUpQueue` | The rules, as plain C# with the time passed in. In order: a request whose number is outside its style's range is dropped. One matching a showing line's style and key rewrites that line. One still inside its style's cooldown is dropped. Otherwise it takes a free slot. With no free slot, it evicts the lowest-priority line (the oldest of those) if that line's priority is lower than its own, or equal and shown at an earlier moment; else it is dropped. The "earlier moment" rule stops a burst of equals in one frame from showing and evicting each other before a stroke is drawn. A rewrite makes its line the newest, so the busiest streak isn't the first to go. Held styles (`ready`) have their own two slots outside the cap |
| `HeadsUpService` | `IHeadsUps`: what routers call. Runs each request through the queue and tells the presenter to show, rewrite or release. Frees a slot when the presenter reports the line finished. Takes no requests outside the game screen (an item or trail resolving a frame after the level-up or game-over screen took over). Clears every line at once on a run reset or on leaving for the launch screen, and with a fade on game over or when the level-up popup opens (`LevelUpPhase.Pending`, not the slow-motion beat before it). Ticks the presenter with unscaled time, but passes zero "live" time while the game is paused, so lines freeze under a pause and keep writing through slow motion. Cooldowns also count only live time |
| `IHeadsUpPresenter` | What the view implements: `Show`, `Rewrite`, `Release`, `ClearAll(animated)`, `Advance(liveDelta, unscaledDelta)`, and `Finished`, which reports each line once it has faded out |
| `Routing/CombatHeadsUpRouter` | The shot: a streak growing (`x3!` at the pop, merged per ball and colour), a speed tap off a wall, `pierce!` when the ball arms a pierce (a Snipe or a cruise tap), `x{n} shattered` when a pierce discharges, and `any colour!` when the ball picks up the rainbow. `pierce!` and `any colour!` are written where the ball is at that moment, and the line stays there; a ball not in flight (a Snipe arming the held ball) writes nothing |
| `Routing/BounceHeadsUpRouter` | The ball's bounces: `+n bounce` at a picked-up shield (n counts this ball's pickups this flight), `-1 bounce` at a wall (loud when it leaves the last shield), and `xn ricochet`. A ricochet that paid a shield writes `+1 bounce` under itself, so that shield's own pickup writes nothing, though it still counts |
| `Routing/CannonHeadsUpRouter` | `ready` beside the cannon, held from `ThrowerReadyMessage` until the ball fires |
| `Routing/ProgressHeadsUpRouter` | `-1` at the heart when a strikethrough costs one, and `{colour} done` at a colour's bar when it fills |

The settings live in `Configuration/HeadsUp/`. `IHeadsUpSettings` gives the cap (`MaxConcurrent`)
and one `HeadsUpStyleSettings` per `HeadsUpStyleId`. Each style sets the format, the look, the
priority, the cooldown, how long the line stays, whether it is held, an offset from its anchor, the
range of numbers worth writing, and the count its size starts growing from. Which numbers a style
writes (no `x2!`, taps only up to the third) is set there, not in the routers.

## Adding a heads-up

1. Add a `HeadsUpStyleId` and its default in `HeadsUpSettings.Defaults()`. `HeadsUpSettingsTests`
   fails until every id has exactly one style.
2. Request it from the router that already hears the event, or from a new router registered next to
   the others.
3. If it needs a signal that doesn't exist yet, publish one from the gameplay code. Routers may look up
   where a ball is (`IProjectileRegistry`), but shouldn't work out what happened from gameplay state.
