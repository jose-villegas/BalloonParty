@page plan_notices Handwritten Notices

# Handwritten Notices

> Working plan for short, handwritten heads-up notices that tell the player what just happened on
> the board, written on as if by a pencil, plus pencil lines drawn over the board.

**Status:** Phase 1 in progress. Built: the Patrick Hand font file (`Assets/Fonts/`, with its OFL
licence), `UI/Notices/WriteOnText` (the slanted soft wipe on the stock shader, EditMode-tested reveal
maths) and `TooltipBanner` writing through it. Left for Phase 1: the TMP font asset (made in the
editor), the pencil tip sprite, and the pencil text shader (grain and ragged edge). The other open
questions in §10 matter from Phase 2.

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

A single **notice** language fixes all of them: one or two handwritten words, written on where the
thing happened, gone a second later. It fits the game's paper look, and a pencil writing on reads as
"the game is telling you something", not as one more effect.

## 2. What the player sees

- **Notices.** A word or two in a handwriting font, written on left to right, with a small pencil
  tip moving along the line and a soft scratch sound. It holds briefly, then fades. Examples: `x4!` at
  the pop, `tap!` at the wall, `pierce!` when a shot starts piercing, `ready` by the cannon,
  `Level 5: Quick Hands` at the top.
- **Pencil lines.** The strikethrough's scribble across a blocked row becomes a pencil stroke, and a
  notice that matters (a big streak) gets a pencil underline.

Notices are short, never cover the aim line for long, and run on unscaled time so they keep writing
through slow-motion beats.

## 3. Architecture

Same shape as the audio routers (`Audio/Routing/*SoundRouter`): a plain C# router listens to gameplay
messages and calls one injected presenter interface, the way the sound routers call `ISoundPlayer`.
No presentation message bus.

```mermaid
flowchart LR
    subgraph Signals["Gameplay signals"]
        G1["StreakGrewMessage (new)"]
        G2[SpeedTapMintedMessage]
        G3["IActiveProjectilePierce.PierceArmed (new)"]
        G4["ThrowerReadyMessage.AfterCapWait (new field)"]
    end
    R["NoticeRouter (controller)"]
    Q["NoticeQueue (model)"]
    I[INoticePresenter]
    L["NoticeLayer (view)"]
    P["HandwrittenNotice (pooled, world space)"]
    G1 --> R
    G2 --> R
    G3 --> R
    G4 --> R
    R --> Q
    Q --> R
    R --> I
    I --> L
    L --> P
    L -.finished.-> R
```

| Piece | Layer | What it does |
|---|---|---|
| `NoticeRouter` | Controller, `Game/Notices/` | Entry point (`RegisterEntryPoint`, like the sound routers). Turns gameplay signals into notice requests by the rules in §6, asks `NoticeQueue` whether to show each, and calls `INoticePresenter`. Pure C#, tested with NSubstitute like the sound routers |
| `NoticeQueue` | Model, `Game/Notices/` | Owned by the router. At most `MaxConcurrent` notices, lowest priority dropped first, merge-key rewrites, per-style cooldowns. Takes the current unscaled time as an argument so it stays plain C#. Frees a slot when the presenter reports a notice finished |
| `INoticePresenter` | `Game/Notices/` | `Show(request, onFinished)`, `Rewrite(handle, arg)`, `ClearAll(animated)`. Implemented by `NoticeLayer` |
| Notice request | `Game/Notices/` | A struct: style id, an `int` argument, an anchor (a world position, or a `TrailEndpointRegistry` key) and an optional merge key. No `string`: the view formats with `TMP_Text.SetText(format, arg)`, so a notice on every pop allocates nothing |
| `NoticeLayer` | View, `UI/Notices/` | Registered with `RegisterComponentInHierarchy<NoticeLayer>().As<INoticePresenter>()`. Resolves anchors, takes `HandwrittenNotice` from `PoolManager` (prewarmed with `PrewarmAsync`) and returns it. Plays the scratch sound for notices actually shown, rate-limited |
| `HandwrittenNotice` | View, `UI/Notices/` | One notice: a world-space `TextMeshPro` on a sorting layer above the balloons, plus `WriteOnText` and the pencil tip |
| `WriteOnText` | View, `UI/Notices/` | The reveal (§4.1) as a standalone component on any `TMP_Text`, so `TooltipBanner` uses it too |
| `INoticeSettings` | `Configuration/` | Styles (format, size, colour, write speed, hold, fade, priority, cooldown), `MaxConcurrent`, thresholds, the tip sprite. Injected read-only; added to the CLAUDE.md list of config interfaces |

**The top banner stays outside the queue.** `TooltipBanner` keeps its own replace-on-new behaviour
and its own handling of `BonusCardDealtMessage` (held until play resumes, since the card is dealt
behind the game-over screen). Under the queue a level-up line could be dropped in a burst. Phase 1
only gives its label the write-on reveal and the handwriting font. Its one `TooltipMessage`
publisher stays `LevelUpPopUp`.

**Board notices live in world space.** A canvas position converted once (`RectAnchorMath`) wouldn't
follow the camera rig's zoom and shake, and the pencil strokes are world-space trails anyway. Only
the top banner stays on the UI canvas.

**Clearing.** All notices clear on `RunResetMessage`, game over and the level-up transition, the
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
  A few hundred colour writes a frame: cheap for several notices at once.
- A pooled notice is set up with `SetText`, `ForceMeshUpdate()` and every glyph hidden **in the same
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

The notices need three facts the game doesn't publish yet. Each has one owner that already holds the
data, and each gets EditMode tests.

| Signal | Owner | Why the existing one isn't enough |
|---|---|---|
| `StreakGrewMessage(Projectile, Color, Streak, WorldPosition)` | `ScoreController.PublishAttributionGroup`, when the ball's streak goes up | `ScorePointsGroupMessage.Multiplier` isn't the streak: night doubles it, item pops repeat it without growth, it goes quiet once a bar is full, and a multi-colour pop sends one per colour. `StreakChangedMessage` has no position |
| `IActiveProjectilePierce.PierceArmed` (an `IObservable<IProjectileModel>`) | `ActiveProjectilePierce`, on each ball's `IsPiercing` going false to true | It only exposes "any ball piercing", which stays true when a second ball arms. Counting taps double-fires after a Snipe re-arm and misses a Snipe arm with no tap |
| `ThrowerReadyMessage.AfterCapWait` | `ThrowerController`, set when the thrower had no free slot at any tick since the last ready | Working it out in the router would copy the thrower's slot rule and its resets (level-up, un-fire, a cap card) |

The cannon gets a `TrailEndpointRegistry` key, so the ready notice anchors there.

## 6. What gets a notice

| Event | Signal | Notice | Rule |
|---|---|---|---|
| Streak grows | `StreakGrewMessage` | `x{0}!` at the pop | From a configured streak up (say 3). Merge key (ball, colour), so a growing streak rewrites itself and two balls don't overwrite each other. An underline from a higher streak (say 6) |
| Speed tap | `SpeedTapMintedMessage` | `tap!` at the wall contact | `TotalTaps <= N` only, so a long cruise doesn't spam |
| Pierce | `PierceArmed` | `pierce!` at the ball | Once per arming. Snipe arming included or not: §10 |
| Cannon ready | `ThrowerReadyMessage` with `AfterCapWait` | `ready` by the cannon | Only after the player waited on the cap. Every reload would be noise: the default is 3 balls with a 0.5 s reload |
| Heart lost to a strikethrough | `StrikethroughArrivedMessage` | `-1` at the heart (`TrailEndpointKeys.Heart`) | Every time |
| Level-up, bonus card | `TooltipMessage`, `BonusCardDealtMessage` | Top banner, as today | Unchanged, outside the queue |

Each row is a style in `INoticeSettings`, so thresholds and wording are tuned without code.

The bar's `x3` stays. It shows the current streak, held until it changes; the pop notice marks the
moment it grew. They don't say the same thing.

## 7. Phases

| Phase | Builds | Done when |
|---|---|---|
| 1. Handwritten text | The Patrick Hand TMP font asset, the pencil text shader and material, `WriteOnText`, `TooltipBanner` drawing through it | The top tooltip writes on with the ragged pencil wipe |
| 2. New signals | `StreakGrewMessage`, `PierceArmed`, `ThrowerReadyMessage.AfterCapWait`, the cannon endpoint key, with tests | Each fires exactly when §5 says, under test |
| 3. Notices | `NoticeQueue`, `NoticeRouter`, `INoticePresenter`, `NoticeLayer`, `HandwrittenNotice`, `INoticeSettings` | Each event in §6 shows its notice by the rules, a burst never shows more than `MaxConcurrent`, and notices clear on reset, game over and level-up |
| 4. Pencil lines | The pencil reskin of the strikethrough trail, then the `HighlightTrail` underline | A blocked row is scribbled through in pencil, and a big streak is underlined |
| 5. Cleanup | Retire anything the notices made redundant, update the READMEs | No two systems say the same thing |

Each phase follows the per-phase review protocol and needs an in-editor playtest; `dotnet build`
can't judge readability. New folders (`Game/Notices/`, `UI/Notices/`) get a README and `.meta` files.

## 8. Tests

- The new signals (§5): `StreakGrewMessage` only on growth, not on night doubling or item pops;
  `PierceArmed` once per arming, including a second ball arming while the first pierces;
  `AfterCapWait` only after a capped wait (`ThrowerControllerRapidFireTests`).
- `NoticeRouter`: each rule in §6, with the presenter substituted.
- `NoticeQueue`: the cap, the drop order, merge rewrites, cooldowns, freeing a slot on finish.
- The views (reveal, tip, pencil trails) are playtest-only.

## 9. Risks

| Risk | Mitigation |
|---|---|
| Notices become noise on a busy board | The queue's cap and cooldowns, thresholds in config, short words only. Playtest with rapid fire at cap 3 |
| They cover the aim line | Anchors offset away from the cannon's aim cone; notices are short-lived |
| Cost on the pop hot path | The router allocates nothing per pop (an `int` argument, no strings); mesh pushes only during the reveal; pooled views. The optimizer reviews each phase |
| A canvas notice re-batches the whole canvas | Board notices are world space; the banner sits on its own nested canvas, with no layout groups and raycasts off |
| The font lacks a glyph | TMP fallback font; keep notice wording plain ASCII |
| Seams in joined-up script during the wipe | Pick the font with the wipe in mind (the soft edge of the ragged wipe also hides small seams) |
| The custom shader is fiddly (TMP's SDF shader is large) | Phase 1 ships the plain wipe on the stock shader first; the shader is a drop-in material swap after |

## 10. Open questions

1. ~~**Font.**~~ **DECISION (owner): Patrick Hand** (SIL Open Font License, Google Fonts). Picked over
   Caveat, Gochi Hand and Kalam: it reads best at phone sizes, and its letters don't join, so the
   wipe shows no seams. Ships with its OFL licence text beside the `.ttf`.
2. **Pierce from a Snipe.** Does a Snipe arming a ball get the `pierce!` notice too, or only a tap
   arming?
3. **Under the pause menu.** Do notices already writing keep going, or freeze with the game?
4. **More events.** Anything else worth a notice: an item going off (`Bomb!`), the board running low,
   the next line about to land?
