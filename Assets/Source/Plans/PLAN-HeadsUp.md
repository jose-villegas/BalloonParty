@page plan_headsup Handwritten Heads-Ups

# Handwritten Heads-Ups

> Working plan for short, handwritten heads-ups that tell the player what just happened on
> the board, written on as if by a pencil, plus pencil lines drawn over the board.

**Status:** Phase 2 built on 2026-11-10: the four signals of §5 (`StreakGrewMessage` from `ScoreController`,
`IActiveProjectilePierce.PierceArmed`, `ThrowerReadyMessage.AfterCapWait` recorded by `ThrowerReload.WaitedAtCap`,
and `TrailEndpointKeys.Cannon` on the cannon's pivot), each EditMode-tested, plus `ProjectileBuffGainedMessage`
for the rainbow heads-up the owner added in §10; nothing consumes them until the
routers of Phase 3. `StreakGrewMessage` announces streaks from two up, so the streak style's own threshold (§6) picks
which ones to write. Two facts for the router: a pop no tracked ball caused (a late pop from a ball
already retired) grows the shared streak, so its `StreakGrewMessage.Projectile` is null and its merge key must
allow that; and at a ball cap of 1 the shot fills the only slot, so every ready has `AfterCapWait`. Phase 1, as
built: the Patrick Hand font file (`Assets/Fonts/`, with its OFL
licence), `UI/HeadsUp/WriteOnText` (the slanted soft wipe on the stock shader, EditMode-tested reveal
maths), `TooltipBanner` writing through it, the TMP font asset, and two early heads-ups written
through plain `HandwrittenHeadsUp` lines, ahead of the router and queue of Phase 3: `ReloadHeadsUp`
writes `ready` beside the cannon for as long as it's ready, and `BounceHeadsUp` writes `+n bounce` where a
ball picks up a shield and `-1 bounce` where a wall spends one. Those lines moved off the canvas into world
space on 2026-11-09 (the `HeadsUps` prefab), as §3 always had it. The pencil text shader
(`BalloonParty/Text/Pencil`: the grain and the ragged edge, on the font's own material) and the pencil tip
sprite on every heads-up line were built on 2026-11-10, awaiting the owner's in-editor look; with that,
Phase 1 is done. §10's open questions 2-4 matter from Phase 3.

---

## 1. Why

The fast-paced mode (`PLAN-FastPaced.md`) made the board busy: several balls in flight, lines
landing on a clock, items going off. Playtests found the player can't read what is happening:

| What happens | How the game says it today | The problem |
|---|---|---|
| The cannon has reloaded | The held ball slides to the muzzle; a reload sound | Easy to miss with several balls out; you can't tell it's ready without looking at the cannon |
| A colour streak grows | A small `x3` held at that colour's progress bar (`UI/Score/ProgressNotice`) | It's at the bar, far from the pop that earned it |
| A shot earns a speed tap off a wall, or starts piercing | The shot dips and winds up faster; a rising note | No text; the player doesn't learn the rule |
| A blocked spawn line costs a heart (strikethrough) | A heart trail flies to the row, scribbles across it, then the row pops (`UI/Health/HeartTrailController`) | Reads as an effect, not as "you lost a heart because this row had no room" |
| A level-up passes in play | A `Level N` line in the top tooltip (`UI/Tooltip`) | Fine, but a different look from everything else |

A single **heads-up** language fixes all of them: one or two handwritten words, written on where the
thing happened, gone a second later. It fits the game's paper look, and a pencil writing on reads as
"the game is telling you something", not as one more effect.

## 2. What the player sees

- **Heads-Ups.** A word or two in a handwriting font, written on left to right, with a small pencil
  tip moving along the line and a soft scratch sound. It holds briefly, then fades. Examples: `x4!` at
  the pop, `tap!` at the wall, `pierce!` when a shot starts piercing, `ready` by the cannon,
  `Level 5: Quick Hands` at the top.
- **Pencil lines.** The strikethrough's scribble across a blocked row becomes a pencil stroke, and a
  heads-up that matters (a big streak) gets a pencil underline.

Heads-Ups are short, never cover the aim line for long, and run on unscaled time so they keep writing
through slow-motion beats; under the pause menu they freeze with the game (§10).

## 3. Architecture

Same shape as the audio routers (`Audio/Routing/*SoundRouter`): small plain C# routers turn gameplay
messages into heads-up requests, and one service does the rest, the way the sound routers all call one
`ISoundPlayer`. No presentation message bus. One router for every event would subscribe to about 17
sources, so the rules are split by what they're about.

```mermaid
flowchart LR
    subgraph Signals["Gameplay signals"]
        S1["StreakGrew, SpeedTapMinted, PierceArmed, PierceDischarged, ProjectileBuffGained"]
        S2["ShieldGained, WallHit, DeflectChain"]
        S3["ThrowerReady, ProjectileFired"]
        S4["StrikethroughArrived, ProgressBarCompleted"]
        S5["RunReset, GameOver, level phase, navigation"]
    end
    subgraph Rules["Routers, controllers"]
        R1[CombatHeadsUpRouter]
        R2[BounceHeadsUpRouter]
        R3[CannonHeadsUpRouter]
        R4[ProgressHeadsUpRouter]
    end
    Svc["HeadsUpService: IHeadsUps, ITickable"]
    Q["HeadsUpQueue, model"]
    Pause[PauseService]
    Cfg[IHeadsUpSettings]
    Pres[IHeadsUpPresenter]
    Layer["HeadsUpLayer, view"]
    Lines["HandwrittenHeadsUp, pooled"]
    Reg[TrailEndpointRegistry]
    S1 --> R1
    S2 --> R2
    S3 --> R3
    S4 --> R4
    S5 --> Svc
    R1 -->|"Request"| Svc
    R2 -->|"Request"| Svc
    R3 -->|"Request, Release"| Svc
    R4 -->|"Request"| Svc
    Svc <-->|"Offer, Free, Clear"| Q
    Pause -.->|"IsAnyPaused"| Svc
    Cfg -.-> Q
    Cfg -.-> Layer
    Svc -->|"Show, Rewrite, Release, ClearAll, Advance"| Pres
    Pres --> Layer
    Layer --> Lines
    Layer -->|"endpoint anchors"| Reg
    Layer -.->|"Finished"| Svc
```

| Piece | Layer | What it does |
|---|---|---|
| `CombatHeadsUpRouter`, `BounceHeadsUpRouter`, `CannonHeadsUpRouter`, `ProgressHeadsUpRouter` | Controllers, `Game/HeadsUp/Routing/` | Entry points. Each turns its signals into `HeadsUpRequest`s and hands them to `IHeadsUps`; they know no thresholds, looks or limits. Tested with `IHeadsUps` substituted |
| `IHeadsUps` / `HeadsUpService` | Controller, `Game/HeadsUp/` | `Request(request)` returns a `HeadsUpHandle`; `Release(handle)` ends a held one. Owns the queue, calls the presenter, clears everything on `RunResetMessage` and navigation to the launch screen (at once; the level-up and game-over screens follow a fading clear of their own, which an instant one would cut short) and on game over and the level-up popup opening, `LevelUpPhase.Pending` (animated; not at `Completing`, the winning pop's slow-motion beat, when the last lines matter most). Takes no requests outside `NavigationState.Game`. Ticks the presenter on unscaled time, but hands it zero "live" time while `PauseService.IsAnyPaused`, so heads-ups freeze under a pause and keep going through slow-motion (§10) |
| `HeadsUpQueue` | Model, `Game/HeadsUp/` | Owned by the service, plain C#. Applies each style's argument range, then a merge key (a showing line with the same style and key is rewritten), then the style's cooldown, then the cap: `MaxConcurrent` slots, a full queue evicting its lowest-priority, oldest line for an equal or higher request and dropping a lower one. Held styles (`ready`) sit in their own lane outside the cap. Every handle carries a generation, so a late "finished" from a reused line can't free its new occupant |
| `HeadsUpRequest` | Data, `Game/HeadsUp/` | A struct: a `HeadsUpStyleId`, an `int` argument, a `HeadsUpAnchor` (a world point, or a `TrailEndpointRegistry` key), an optional merge key (an owner object and a channel string) and an optional word. No string is built per request: the view formats with `TMP_Text.SetText(format, arg)` |
| `IHeadsUpPresenter` | `Game/HeadsUp/` | `Show`, `Rewrite`, `Release`, `ClearAll(animated)`, `Advance(liveDelta, unscaledDelta)` and a `Finished` stream. Implemented by `HeadsUpLayer` |
| `HeadsUpLayer` | View, `UI/HeadsUp/` | Registered with `RegisterComponentInHierarchy<HeadsUpLayer>().As<IHeadsUpPresenter>()`. Takes pooled `HandwrittenHeadsUp` lines, resolves anchors (an endpoint line follows its anchor, re-read every frame, since the heart and the bars are on the camera-bound canvas), packs new lines clear of the ones showing (`HeadsUpPacking`, across every line) and reports each line that finished |
| `HandwrittenHeadsUp` | View, `UI/HeadsUp/` | One pooled line: a world-space `TextMeshPro` with `WriteOnText` and the pencil tip, hand-ticked through write, hold and fade by the layer rather than by DOTween, so a pause freezes it |
| `IHeadsUpSettings` | `Configuration/HeadsUp/` | `MaxConcurrent` and one entry per `HeadsUpStyleId`: format, look (`HeadsUpStyle`: colour, size, growth, weight), priority, cooldown, hold or held, offset from the anchor, argument range, and the argument the size starts growing from. The pencil itself (halo, write speed) stays on the line prefab. Added to the CLAUDE.md list of config interfaces |

"At the ball" means where the ball was when it happened: a line moving at ball speed can't be read
while it writes, or packed. A router that needs the ball's place reads it from `IProjectileRegistry` and
skips the heads-up for a ball no longer in flight.

**The early heads-ups fold in.** `BounceHeadsUp` and `ReloadHeadsUp` (Phase 1) become
`BounceHeadsUpRouter` and `CannonHeadsUpRouter` behind tests of what they do today, and the two
components go in the same change. Left apart, they would break the cap, the pause freeze and the one
home for looks.

**The top banner stays outside the queue.** `TooltipBanner` keeps its own replace-on-new behaviour
and its own handling of `BonusCardDealtMessage` (held until play resumes, since the card is dealt
behind the game-over screen). Under the queue a level-up line could be dropped in a burst. Phase 1
only gives its label the write-on reveal and the handwriting font. Its one `TooltipMessage`
publisher stays `LevelUpPopUp`.

**Board heads-ups live in world space.** A canvas position converted once (`RectAnchorMath`) wouldn't
follow the camera rig's zoom and shake, and the pencil strokes are world-space trails anyway. Only
the top banner stays on the UI canvas.

**Clearing.** All heads-ups clear on `RunResetMessage`, game over and the level-up popup opening
(not the winning pop's slow-motion beat before it), and none start off the game screen, the
way `ProgressNoticePresenter` dismisses its notices (animated when there's time, instant otherwise).

## 4. Rendering

### 4.1 Write-on text

**DECISION (owner): a ragged pencil wipe.** Of four styles demoed (a plain letter wipe, a ragged
wipe, tracing the outline then filling, true stroke order with a single-line font), the owner picked
the ragged wipe: a left-to-right wipe whose edge is soft, grainy and slightly slanted, with a pencil
grain through the letters. Real stroke order would need stroke data for every glyph (or a thin,
plotter-like single-line font) and isn't worth it: at game speed (about 0.25-0.35 s a word) a good
wipe reads as writing.

How it's built:

- TMP lays the text out once. Each reveal frame sets the vertex alpha of the glyph being written,
  from its left edge to its right (a 4-vertex gradient), with every earlier glyph opaque and every
  later one hidden, then pushes colours only (`UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32)`).
  A few hundred colour writes a frame: cheap for several heads-ups at once.
- A pooled heads-up is set up with `SetText`, `ForceMeshUpdate()` and every glyph hidden **in the same
  call**, or it flashes fully drawn for a frame. Not `maxVisibleCharacters`: it re-lays out the text
  on every step.
- The tick follows `UI/Score/ProgressNotice`: hand-ticked appear, hold and exit on unscaled time,
  writing nothing once settled.
- The pencil tip (a small sprite) follows the write head on the baseline, with a little wobble.
- The ragged edge and the grain need a small custom text shader: a copy of TMP's SDF shader with a
  noise texture that roughens the wipe's edge and multiplies a grain into the face. The plain wipe
  above is its first step and can ship on the stock shader if the custom one takes longer.
- The top banner is UGUI. A `RectMask2D` whose right padding animates, with softness, is a cheaper
  wipe there (no mesh pushes, joined-up script stays continuous), but its edge is straight. Use it only
  if the banner can't take the custom shader.
- Handwriting fonts overlap their joining strokes, so a per-glyph step can show seams. Check in the
  playtest; the font choice (§10) matters here.

### 4.2 Pencil lines

No new stroke system. Two existing pens already leave a line behind them:

- **The strikethrough** is already a pooled `FlyingTrail` ribbon that flies to the blocked row and
  scribbles across it in jittered passes (`HeartTrailController`, `StrikethroughPasses`,
  `StrikethroughJitter`), then pops the row. It gets a pencil material and a pencil-tip head. It
  owns the row geometry and the timing, so nothing else needs to know where the row is.
- **The underline** reuses the item preview's pen (`Item/Preview/HighlightTrail`): a pooled head
  sprite pulling a `TrailRenderer`, driven by a ticker, with a pencil material.

## 5. New signals

The heads-ups need four facts the game doesn't publish yet. Each has one owner that already holds the
data, and each gets EditMode tests.

| Signal | Owner | Why the existing one isn't enough |
|---|---|---|
| `StreakGrewMessage(Projectile, Color, Streak, WorldPosition)` | `ScoreController.PublishAttributionGroup`, when the ball's streak goes up | `ScorePointsGroupMessage.Multiplier` isn't the streak: night doubles it, item pops repeat it without growth, it goes quiet once a bar is full, and a multi-colour pop sends one per colour. `StreakChangedMessage` has no position |
| `IActiveProjectilePierce.PierceArmed` (an `IObservable<IProjectileModel>`) | `ActiveProjectilePierce`, on each ball's `IsPiercing` going false to true | It only exposes "any ball piercing", which stays true when a second ball arms. Counting taps double-fires after a Snipe re-arm and misses a Snipe arm with no tap |
| `ThrowerReadyMessage.AfterCapWait` | `ThrowerController`, set when the thrower had no free slot at any tick since the last ready | Working it out in the router would copy the thrower's slot rule and its resets (level-up, un-fire, a cap card) |
| `ProjectileBuffGainedMessage(Projectile, BuffId)` | `ProjectileBuffService.Apply`, when a ball takes on a kind of buff it didn't hold | Nothing announces a buff. Asking `HasBuff` each frame would poll, and a Snipe re-applying the rainbow to a ball that has it must stay quiet |

The cannon gets a `TrailEndpointRegistry` key, so the ready heads-up anchors there.

## 6. What gets a heads-up

| Event | Signal | Heads-Up | Rule |
|---|---|---|---|
| Streak grows | `StreakGrewMessage` | `x{0}!` at the pop | From a configured streak up (say 3). Merge key (ball, colour), so a growing streak rewrites itself and two balls don't overwrite each other. An underline from a higher streak (say 6) |
| Speed tap | `SpeedTapMintedMessage` | `tap!` at the wall contact | `TotalTaps <= N` only, so a long cruise doesn't spam |
| Pierce | `PierceArmed` | `pierce!` at the ball | Once per arming, a Snipe arming included (§10) |
| Cannon ready | `ThrowerReadyMessage` | `ready`, beside the cannon, held while it's ready | **Built early, on every ready** (`ReloadHeadsUp`), cleared when the ball fires or play stops, and the reload is now near-instant with nothing flying but 2 s while a ball flies, so a ready cannon under fire is a beat worth marking. If it reads as noise, narrow it to `AfterCapWait` |
| Shield picked up | `ShieldGainedMessage` | `+{n} bounce` at the shield's balloon, n counting the ball's pickups this flight | **Built early** (`BounceHeadsUp`), on every pickup |
| Wall spends a shield | `WallHitMessage` | `-1 bounce` at the wall contact, kept on screen | **Built early** (`BounceHeadsUp`), on every bounce; quiet graphite, vermilion when it leaves the last shield |
| Ricochet | `DeflectChainMessage` | `x{n} ricochet` at the deflecting balloon | **Built early** (`BounceHeadsUp`), from the second deflect in a row without a wall; every third pays `+1 bounce` (`DeflectChainController`) |
| Heart lost to a strikethrough | `StrikethroughArrivedMessage` | `-1` at the heart (`TrailEndpointKeys.Heart`) | Every time |
| Pierce discharge | `PierceDischargedMessage` | `x{n} shattered` at the wall contact (`Position`) | From 2 toughs (`ToughCount`): what the pierce paid, closing the loop `pierce!` opened |
| A colour bar fills | `ProgressBarCompletedMessage` | `{colour} done` at that colour's bar (its `TrailEndpointRegistry` endpoint, keyed by colour name) | Every time: the colour scores nothing from here, which is why its streak goes quiet |
| Rainbow picked up | `ProjectileBuffGainedMessage` with `RainbowShield` | `any colour!` at the ball | Once per pickup; a Snipe re-applying it is not a pickup |
| Level-up, bonus card | `TooltipMessage`, `BonusCardDealtMessage` | Top banner, as today | Unchanged, outside the queue |

Each row is a style in `IHeadsUpSettings`, so thresholds and wording are tuned without code.

The bar's `x3` stays. It shows the current streak, held until it changes; the pop heads-up marks the
moment it grew. They don't say the same thing.

## 7. Phases

| Phase | Builds | Done when |
|---|---|---|
| 1. Handwritten text | The Patrick Hand TMP font asset, the pencil text shader and material, `WriteOnText`, `TooltipBanner` drawing through it | The top tooltip writes on with the ragged pencil wipe |
| 2. New signals | `StreakGrewMessage`, `PierceArmed`, `ThrowerReadyMessage.AfterCapWait`, the cannon endpoint key, with tests | Each fires exactly when §5 says, under test |
| 3. Heads-Ups | In order: the request data and `IHeadsUpSettings`; `HeadsUpQueue`; `HeadsUpService`; the four routers (all testable with no scene change); then `HeadsUpLayer` and the pooled `HandwrittenHeadsUp`; then retiring `BounceHeadsUp` and `ReloadHeadsUp` | Each event in §6 shows its heads-up by the rules, a burst never shows more than `MaxConcurrent`, and heads-ups clear on reset, game over and level-up |
| 4. Pencil lines | The pencil reskin of the strikethrough trail, then the `HighlightTrail` underline | A blocked row is scribbled through in pencil, and a big streak is underlined |
| 5. Cleanup | Retire anything the heads-ups made redundant, update the READMEs | No two systems say the same thing |

Each phase follows the per-phase review protocol and needs an in-editor playtest; `dotnet build`
can't judge readability. New folders (`Game/HeadsUp/`, `UI/HeadsUp/`) get a README and `.meta` files.

## 8. Tests

- The new signals (§5): `StreakGrewMessage` only on growth, not on night doubling or item pops;
  `PierceArmed` once per arming, including a second ball arming while the first pierces;
  `AfterCapWait` only after a capped wait (`ThrowerControllerRapidFireTests`).
- `HeadsUpRouter`: each rule in §6, with the presenter substituted.
- `HeadsUpQueue`: the cap, the drop order, merge rewrites, cooldowns, freeing a slot on finish.
- The views (reveal, tip, pencil trails) are playtest-only.

## 9. Risks

| Risk | Mitigation |
|---|---|
| Heads-Ups become noise on a busy board | The queue's cap and cooldowns, thresholds in config, short words only. Playtest with rapid fire at cap 3 |
| They cover the aim line | Anchors offset away from the cannon's aim cone; heads-ups are short-lived |
| Cost on the pop hot path | The router allocates nothing per pop (an `int` argument, no strings); mesh pushes only during the reveal; pooled views. The optimizer reviews each phase |
| A canvas heads-up re-batches the whole canvas | Board heads-ups are world space; the banner sits on its own nested canvas, with no layout groups and raycasts off |
| The font lacks a glyph | TMP fallback font; keep heads-up wording plain ASCII |
| Seams in joined-up script during the wipe | Pick the font with the wipe in mind (the soft edge of the ragged wipe also hides small seams) |
| The custom shader is fiddly (TMP's SDF shader is large) | Phase 1 ships the plain wipe on the stock shader first; the shader is a drop-in material swap after |

## 10. Open questions

1. ~~**Font.**~~ **DECISION (owner): Patrick Hand** (SIL Open Font License, Google Fonts). Picked over
   Caveat, Gochi Hand and Kalam: it reads best at phone sizes, and its letters don't join, so the
   wipe shows no seams. Ships with its OFL licence text beside the `.ttf`.
2. ~~**Pierce from a Snipe.**~~ **DECISION (owner): yes.** A Snipe arming a ball writes `pierce!` like a
   tap arming does. A banked charge keeping a ball armed through its discharge is not a new arming, so it
   writes nothing (`PierceArmed` doesn't fire for it).
3. ~~**Under the pause menu.**~~ **DECISION (owner): they freeze with the game.** A heads-up mid-write,
   holding or fading stops while any pause source is held (`PauseService.IsAnyPaused`) and carries on
   where it was. Unscaled time still applies to slow-motion beats, which aren't a pause.
4. ~~**More events.**~~ **DECISION (owner): three more** (§6): `x{n} shattered` on a pierce discharge,
   `{colour} done` when a colour bar fills, and `any colour!` when a ball picks up the rainbow. Left out: an
   item going off (its effect is already the loudest thing on screen), the next line about to land (the
   danger heart already counts it down as `-N`), and coins (they fly to their counter).
