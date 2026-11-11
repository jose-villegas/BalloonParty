@page plan_card_reveal The Card Reveal

# The Card Reveal

> Working plan for showing a won upgrade card: during the level-to-level transition a realistic
> balloon pops and leaves behind a slip of paper, like the one inside a fortune cookie, with the card
> handwritten on it.

**Status:** Phase 1 built (the controller, its queue and the pop pick). Phase 2 built: the bake
script makes pop 0. Phase 4 started early with pop 0 alone, to see it in game before the other 15
are baked: `CardRevealView` plays it on the game canvas, lit live by the scene's light, with a
stand-in slip (a rounded panel with the title pencilled on). The cheat Reveal A Card shows one on
demand. Not built yet: the real slip and its icon, the flutter-out, and loading one sheet at a time.

The pops are 16 separate physics simulations, baked in Blender by
a script in this repo (§5), so the reveal never pops the same way twice in a row.

---

## 1. Why

Winning a card is the run's only source of power (`Game/Upgrades/README.md`), yet today it shows as
one line of the tooltip, `Level N: Card`, which is easy to miss in a busy board. The pick screen that
showed cards was switched off because it stopped play (`UI/LevelUp/README.md`). The reveal has to be
loud enough to notice, and still never stop play.

A popping balloon is the game's own verb. Here it pays out a prize instead of points: the pop
uncovers a fortune-cookie slip with what the player won. Hand-drawn frames can't make rubber tear
and snap back convincingly; a cloth simulation can, so the pops are simulated and baked.

## 2. What the player sees

1. On a level-up that deals a card, as the transition to the next level starts, a balloon drifts up
   into the top third of the screen, the slip already inside it as a faint shadow.
2. It pops: one of 16 baked simulations, chosen at random. The rubber splits from a puncture, the
   flaps snap back and curl, and the shreds fall away, in slow motion, the way high-speed footage
   shows it.
3. The slip is left where the balloon was, curled at the ends, settling with a slight sway.
4. The card's title is handwritten on the slip in pencil (`UI/HeadsUp/WriteOnText`), its icon
   beside it, and the description below in a smaller hand.
5. The slip holds for a moment, then flutters out toward the weapon it changed: the cannon for
   Extra Ball, Faster Reload and Shield, the board for More Items.

About 2.5 s in all, on unscaled time like the heads-ups, so slow-motion beats don't stretch it.

## 3. Rules

| Rule | Why |
|---|---|
| Never pauses or slows play | The pick screen was switched off for stopping play |
| Plays on the UI canvas, over the board, never inside the level-up cinematic's trail path | The trail path is a known fragile area; the reveal only listens to the level-up, it doesn't hook into it |
| Replaces the `Level N: Card` tooltip on card level-ups with `Level N` | One announcement of the card, not two. The level number still shows, as on level-ups without a card |
| Also plays for a bonus card at the start of a retry (the ad's or one bought with coins, `PLAN-Coins.md`) | The same prize, the same way. Replaces the `BonusCardDealtMessage` tooltip |
| A pop is picked at random, never one of the last two shown | 16 sims are there for variety; repeating the last one would waste it |
| The balloon is tinted from the palette and lit by the scene, not baked in one colour or one light | The sims are baked as surface data (the normal and the occlusion, not a picture) and lit at runtime by the scene's light, so the reveal follows the level's colours and the time of day as the board's balloons do |
| The slip is inside the balloon, not behind it | Each frame is baked in two layers, the rubber in front of the balloon's centre and the rubber behind it, and the slip is drawn between them. While the balloon moves the layers part slightly (parallax), so the slip reads as inside |
| Text, never baked into the art | The card's title and description come from `IUpgradeCardSettings`, so a new card needs no new art beyond its icon |
| A second card won while a slip is showing waits its turn | Rare (cards come every 2 levels), but a retry's bonus card can meet a level-up's |

## 4. Architecture

| Piece | Where | Responsibility |
|---|---|---|
| `CardRevealController` (built) | `Game/CardReveal/` | Plain C#, beside the other game-side services (`Game/HeadsUp/`, `Game/Coins/`). Subscribes `UpgradeCardHeldMessage` (published by `RunUpgrades` when it holds the pick, at `Transitioning`) and `BonusCardDealtMessage`; queues reveals, picks the pop, and exposes the one to play (`ICardReveals.Current`, a `RevealedCard`). The view attaches itself as the stage; with none attached nothing is queued and the tooltip keeps announcing cards |
| `CardRevealView` (built, with a stand-in slip) | `UI/CardReveal/` | `MonoBehaviour` on the game canvas, built there by `CardRevealBuilder`. Attaches the stage when the settings hold a pop. Plays the chosen pop on two `RawImage`s (the back and front layers, the slip between them), stepping the frame through each one's UV rect on unscaled time, then writes the title on the slip through `WriteOnText`. Still to come: the icon, the description and the flutter-out |
| `BalloonParty/Sprite/LitFlipbook` (built) | `Assets/Shaders/BalloonParty/Sprite/` | Lights a layer's surface data with the scene's light (`SceneLight.cginc`): a wrapped diffuse, the occlusion, a highlight, and thin-latex transmission, so a face turned from the light glows deeper instead of going dark. The tint is the `RawImage`'s colour, re-graded by the time of day like the balloons' bodies |
| `PopSheetLoader` | `UI/CardReveal/` | Loads only the chosen pop's sheet when the level-up turns `Pending` (before the transition, so the pop starts on time) and releases it after. The 16 sheets never sit in memory together. Not needed while there is one pop: the settings reference the sheets directly, so all of them load with the game scene |
| `ICardRevealSettings` (built) | `Configuration/CardReveal/`, its own `CardRevealSettings` asset (with none assigned, a default with no pops, and the reveal stays off) | The pop sheets, how many recent pops to avoid, the sheet layout and frame rate, and how the reveal plays (the rise, the beat before the pop, the frame the writing starts on, the hold, the fade, the parallax). Later: slip sway, and per card kind the icon and where the slip flies to |
| `LevelUpPopUp`, `TooltipBanner` (changed) | `UI/LevelUp/`, `UI/Tooltip/` | Leave the card to the reveal while a stage is attached (`ICardReveals.HasStage`): the level-up line drops the card's name, the bonus card line isn't shown |

The flipbook was planned as a sprite swap. It became a UV rect on a `RawImage` once the frames were
lighting data rather than pictures: a frame looks like nothing until the shader lights it, so
checking frames one by one in the Editor gained nothing, and a UV rect needs no sliced sprites.

## 5. How the pops are made

Two scripts under `Tools/art/balloon_pop/` (built; its README has the commands): `bake_pops.py`
drives Blender headless and renders each pop's frames, and `pack_sheets.py` packs them into the
game's sheets and draws a contact sheet and an animated preview to review them by. Everything is
seeded, so a run redraws the same set, and a seed can be swapped to replace one pop.

| Step | How |
|---|---|
| The balloon | A balloon mesh modelled once in the script, already inflated (an egg gathered into the neck), and a static neck and knot. The same rest frame starts every sim, so the drift-up looks the same whatever pop follows |
| The puncture | A random point on the body. Cracks grow from it across the mesh, branching, and the mesh is cut along them; a loop that would cut a piece loose is stitched back at one edge. Seeded per sim: where it starts (side, top, near the knot), how many cracks, how far they run and wander |
| The tear | A release front sweeps out from the puncture, and behind it the rubber's stored stretch lets go (cloth shrinking to about 40%): each flap snaps back toward the pinned neck and buckles and curls, with self-collision so flaps don't pass through each other. A Weld keeps each crack shut until it opens, so the rest frame shows no seams. How far it shrinks and how stiff it bends vary per sim |
| The fall | Once the rubber has gathered at the knot, the knot drops out of the frame with the shreds trailing, so the last frames are empty and the slip has the stage |
| Timing | The sim runs 48 frames at a slow time scale and every second one is kept: 24 frames played at 30 fps, about 0.8 s. Roughly: 2 frames whole, 7 tearing, 8 gathering, 7 falling |
| Rendering | Cycles on the CPU (no GPU needed headless), orthographic camera, transparent background, no colour transform. Not a lit picture but the surface as data: red and green the normal's x and y as the camera sees it, blue the ambient occlusion, alpha the coverage, with the inside of the rubber turned to face the camera. Each frame is rendered twice, split at the balloon's centre by the camera's clip planes: the front layer and the back layer. A whole pop renders in about a minute |
| Lighting | In game, by `BalloonParty/Sprite/LitFlipbook` with the scene's light (§4). `pack_sheets.py` lights its review files with the same model, under three lights, to judge the bake without opening Unity |
| Packing | Each sim's 24 frames, rendered at 512 px and scaled to 256×256: the front layer's in a 6×4 grid in the top half of the sheet, the back layer's in the bottom half (1536×2048). Imported as linear data. The balloon's centre is the same pixel in every frame |
| The slip | Rendered by the same script with a paper material, so it matches the balloon's lighting: a narrow, curled strip, off-white, with a faint fold |

Blender's cloth made the tear read as rubber (the flaps snap back rather than sag), so the fallback
of a membrane solver in the script itself wasn't needed.

**Budget.** 16 sheets of 1536×2048 (two layers), compressed (ASTC 6×6 on phones) at about 1.4 MB
each: about 22 MB in the build, twice the single-layer plan, and only one in memory at a time
(`PopSheetLoader`). Normals in ASTC may band; if they do on a phone, 4×4 for these sheets is the
first fix to try. The sheets stay out of the
sprite atlases, like the other baked art (`Sprites/README.md`), under `Assets/Sprites/UI/CardReveal/`.

## 6. Assets

| Asset | Size | Made by |
|---|---|---|
| 16 balloon pops | 16 sheets, 1536×2048 (24 frames of 256×256, two layers) | The bake script (§5) |
| Fortune-cookie slip, and its shadow | About 640×180 | The bake script, so it's lit like the balloon |
| Card icons | 4 × 192×192 | Drawn in the UI style, shared with the coin shop (`PLAN-Coins.md` §7) |

## 7. Phases

| Phase | What | Needs art |
|---|---|---|
| 1 (done) | `CardRevealController` and its queue, the pick that avoids the last two pops, the tooltip hand-off, EditMode tests | No |
| 2 (built, in review) | The bake script: the balloon, one pop, rendered and packed. The owner reviews that one pop from the contact sheet before the other 15 are baked | Makes it |
| 3 | The other 15 pops and the slip, reviewed together on the contact sheet | Makes it |
| 4 (started) | `CardRevealView` and `LitFlipbook`: the pop, lit and in two layers, and the write-on, with pop 0 and a stand-in slip (built). Still to come: `PopSheetLoader`, the real slip with its icon, and the flutter-out | Yes: phases 2 and 3 |
| 5 | A playtest pass on a phone: timing, how well the slip reads over a busy board, and whether 256 px frames are sharp enough | — |

## 8. Open questions

- Does the slip fly to the weapon it changed, or just fade? Flying ties the card to its effect, but
  it crosses the board.
- Should a card that stacks (a second Shield) say so on the slip, e.g. `Shield ×2`?
- Are 256 px frames enough? The balloon fills about a quarter of the screen width, about 270 px on a
  1080 px phone. 384 px frames would be sharper, at about twice the build size.
