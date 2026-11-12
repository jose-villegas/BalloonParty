@page plan_card_reveal The Card Reveal

# The Card Reveal

> Working plan for showing a won upgrade card: during the level-to-level transition a realistic
> balloon pops, and two pieces of ribbon fly off it, one with the card's name and one with what it
> does, drifting across the screen while the level's ascent plays.

**Status:** Phase 1 built (the controller, its queue and the pop pick). Phase 2 built: the bake
script makes pop 0. Phase 4 built with pop 0 alone, to see it in game before the other 15 are baked:
`CardRevealView` plays it on a World Space canvas of its own during the level-up, lit live by the scene's light field and the screen-space light like the board, and
the ribbons fly with the ascent. The cheat Reveal A Card shows one on demand. Not built yet: a card
icon, and loading one sheet at a time.

The pops are 16 separate physics simulations, baked in Blender by
a script in this repo (§5), so the reveal never pops the same way twice in a row.

---

## 1. Why

Winning a card is the run's only source of power (`Game/Upgrades/README.md`), yet today it shows as
one line of the tooltip, `Level N: Card`, which is easy to miss in a busy board. The pick screen that
showed cards was switched off because it stopped play (`UI/LevelUp/README.md`). The reveal has to be
loud enough to notice, and still never stop play.

A popping balloon is the game's own verb. Here it pays out a prize instead of points: the pop sets
free the ribbons that say what the player won. Hand-drawn frames can't make rubber tear and snap
back convincingly; a cloth simulation can, so the pops are simulated and baked.

## 2. What the player sees

1. On a level-up that deals a card, as the level-up starts, a silver balloon (none of the board's colours)
   floats up to a little above the screen's middle, bobbing and swaying to rest, two rolled-up ribbons inside it as a faint shadow.
2. A red pushpin comes in and jabs it, and it pops: one of 16 baked simulations, chosen at random.
   The rubber splits from where the pin went in, the flaps snap back and curl, and the shreds fall
   away, in slow motion, the way high-speed footage shows it, while scraps fly far out and flutter
   down and the pin is thrown back out.
3. As the rubber opens, the two ribbons burst out: the card's title on the upper one, what it does
   on the lower one (`Reload 15% faster`), in the tooltip's pencil hand. The ribbons ripple, and the
   writing ripples with them.
4. They drift and sway while the level's ascent plays, the old board falling away under them.
5. Just before the next level settles, they fly off, up and outward, and fade.

On a level-up the ribbons are gone 0.82 of the way through the ascent and a second more (about 4.3 s
into its 4 s, a moment after it ends; the ascent starts just after the reveal). A retry's bonus card has
no ascent, so its ribbons fly a set time instead. All on unscaled time like the heads-ups, so
slow-motion beats don't stretch it.

## 3. Rules

| Rule | Why |
|---|---|
| Never pauses or slows play | The pick screen was switched off for stopping play |
| Plays on the UI canvas, over the board, never inside the level-up cinematic's trail path | The trail path is a known fragile area; the reveal only listens to the level-up, it doesn't hook into it |
| Replaces the `Level N: Card` tooltip on card level-ups with `Level N` | One announcement of the card, not two. The level number still shows, as on level-ups without a card |
| Also plays for a bonus card at the start of a retry (the ad's or one bought with coins, `PLAN-Coins.md`) | The same prize, the same way. Replaces the `BonusCardDealtMessage` tooltip |
| A pop is picked at random, never one of the last two shown | 16 sims are there for variety; repeating the last one would waste it |
| The balloon is tinted at runtime and lit by the scene, not baked in one colour or one light | The sims are baked as surface data (the normal and the occlusion, not a picture) and lit at runtime by the scene's light, so the reveal takes any colour (`BalloonColor`, silver) and follows the time of day as the board's balloons do |
| The ribbons are inside the balloon, not behind it | Each frame is baked in two layers, the rubber in front of the balloon's centre and the rubber behind it, and the ribbons are drawn between them. While the balloon moves the layers part slightly (parallax), so they read as inside |
| A level-up's reveal plays with the ascent, and is over about as the next level settles | The ascent is the level-up's quiet stretch: nothing to aim at, the screen moving. The ribbons stay a second past most of it, so the card is read, and leave as the new board settles |
| Text, never baked into the art | The card's title and description come from `IUpgradeCardSettings`, so a new card needs no new art |
| A second card won while a reveal plays waits its turn | Rare (cards come every 2 levels), but a retry's bonus card can meet a level-up's |

## 4. Architecture

| Piece | Where | Responsibility |
|---|---|---|
| `CardRevealController` (built) | `Game/CardReveal/` | Plain C#, beside the other game-side services (`Game/HeadsUp/`, `Game/Coins/`). Subscribes `UpgradeCardHeldMessage` (published by `RunUpgrades` when it holds the pick, at `Transitioning`) and `BonusCardDealtMessage`; queues reveals, picks the pop, and exposes the one to play (`ICardReveals.Current`, a `RevealedCard`). Starts one in play or during a level-up, whose navigation stays on `LevelUp` through the ascent. The view attaches itself as the stage; with none attached nothing is queued and the tooltip keeps announcing cards |
| `CardRevealView` (built) | `UI/CardReveal/` | `MonoBehaviour` on `CardRevealCanvas`, a World Space canvas fitted to the camera in the Game scene, built by `CardRevealBuilder`. Attaches the stage when the settings hold a pop. Plays the chosen pop on two `RawImage`s (the back and front layers, the ribbons between them), stepping the frame through each one's UV rect on unscaled time, and flies the ribbons from the pop. A baked pushpin jabs the pop's puncture to start it, and a particle burst of shreds flies far out as it tears. Times their exit to the ascent (`LevelAscendStartedMessage` and its length) |
| `RibbonPiece`, `RibbonGraphic` (built) | `UI/CardReveal/` | A piece of ribbon with a line written on it: a UI strip notched at both ends with a wave running along it, shaded as satin, the TMP text bent glyph by glyph to ride the wave. The piece poses itself through its flight: rolled up inside, burst, drift and sway, exit |
| `BalloonParty/Sprite/LitFlipbook` (built) | `Assets/Shaders/BalloonParty/Sprite/` | Lights a layer's surface data with the scene's light (`SceneLight.cginc`): a wrapped diffuse, the occlusion, a highlight, and thin-latex transmission, so a face turned from the light glows deeper instead of going dark. The tint is the `RawImage`'s colour, re-graded by the time of day like the balloons' bodies |
| `PopSheetLoader` | `UI/CardReveal/` | Loads only the chosen pop's sheet when the level-up turns `Pending` (before the transition, so the pop starts on time) and releases it after. The 16 sheets never sit in memory together. Not needed while there is one pop: the settings reference the sheets directly, so all of them load with the game scene |
| `ICardRevealSettings` (built) | `Configuration/CardReveal/`, its own `CardRevealSettings` asset (with none assigned, a default with no pops, and the reveal stays off) | The pop sheets, how many recent pops to avoid, the sheet layout and frame rate, and how the reveal plays (the rise, the beat before the pop, the parallax, and the ribbons' flight: the frame they fly out on, their burst and exit, and when they are gone with and without an ascent) |
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
| The fall | From the tear the knot falls, and the rubber falls with it under the same gravity (switched on at the tear, so the whole balloon doesn't sag), trailing as the air holds it back. The camera follows the knot, easing lower once the rubber gathers so it stays in the frame; the game moves the frames down by the camera's path, counted in frames, so the motion in the frames and on screen are one fall at any playback speed |
| Timing | The sim runs 48 frames at a slow time scale and every one is rendered: 48 frames, 1.6 s at 30 fps; the game plays them in slow motion (`FramesPerSecond`, 24: 2 s). Roughly: 4 frames whole, 14 tearing, 16 gathering, 14 falling |
| Rendering | Cycles on the CPU (no GPU needed headless), orthographic camera, transparent background, no colour transform. Not a lit picture but the surface as data: red and green the normal's x and y as the camera sees it, blue the ambient occlusion, alpha the coverage, with the inside of the rubber turned to face the camera. Each frame is rendered twice, split at the balloon's centre by the camera's clip planes: the front layer and the back layer. A whole pop renders in about a minute |
| Lighting | In game, by `BalloonParty/Sprite/LitFlipbook` with the scene's light (§4). `pack_sheets.py` lights its review files with the same model, under three lights, to judge the bake without opening Unity |
| Packing | Each sim's 48 frames, rendered at 512 px and scaled to 256×256: the front layer's in an 8×6 grid in the top half of the sheet, the back layer's in the bottom half (2048×3072). Imported as linear data. The balloon's centre is the same pixel in every frame |

Blender's cloth made the tear read as rubber (the flaps snap back rather than sag), so the fallback
of a membrane solver in the script itself wasn't needed.

**Budget.** 16 sheets of 2048×3072 (48 frames, two layers), compressed (ASTC 6×6 on phones) at about
2.8 MB each: about 45 MB in the build, four times the single-layer 24-frame plan, and only one in memory at a time
(`PopSheetLoader`). Normals in ASTC may band; if they do on a phone, 4×4 for these sheets is the
first fix to try. The sheets stay out of the
sprite atlases, like the other baked art (`Sprites/README.md`), under `Assets/Sprites/UI/CardReveal/`.

## 6. Assets

| Asset | Size | Made by |
|---|---|---|
| 16 balloon pops | 16 sheets, 2048×3072 (48 frames of 256×256, two layers) | The bake script (§5) |
| The ribbons | None: drawn as a mesh (`RibbonGraphic`) | Code |
| The pushpin | One sheet, 512×512 (its head and needle, 256×256 each, in the top half) | `bake_pin.py`, the pops' surface data and scene |
| The shreds | One sheet, 256×256 (four torn scraps) | `make_shreds.py` |
| Card icons, if the ribbons get them | 4 × 192×192 | Drawn in the UI style, shared with the coin shop (`PLAN-Coins.md` §7) |

## 7. Phases

| Phase | What | Needs art |
|---|---|---|
| 1 (done) | `CardRevealController` and its queue, the pick that avoids the last two pops, the tooltip hand-off, EditMode tests | No |
| 2 (built, in review) | The bake script: the balloon, one pop, rendered and packed. The owner reviews that one pop from the contact sheet before the other 15 are baked | Makes it |
| 3 | The other 15 pops, reviewed together on the contact sheet | Makes it |
| 4 (built with pop 0) | `CardRevealView`, `LitFlipbook` and the ribbons: the pop, lit and in two layers, the ribbons flying with the ascent, the pushpin that pricks it, and the shreds it flings. Still to come: `PopSheetLoader`, once there is more than one pop | Yes: phases 2 and 3 |
| 5 | A playtest pass on a phone: timing, how well the ribbons read over a busy board, and whether 256 px frames are sharp enough | — |

## 8. Open questions

- Should a ribbon fly to the weapon its card changed rather than off the screen? It would tie the
  card to its effect, but it crosses the board.
- Should a card that stacks (a second Shield) say so on its ribbon, e.g. `Thick Skin ×2`?
- Are 256 px frames enough? The balloon fills about a quarter of the screen width, about 270 px on a
  1080 px phone. 384 px frames would be sharper, at about twice the build size.
