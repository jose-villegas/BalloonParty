@page plan_card_reveal The Card Reveal

# The Card Reveal

> Working plan for showing a won upgrade card: during the level-to-level transition a realistic
> balloon pops and leaves behind a slip of paper, like the one inside a fortune cookie, with the card
> handwritten on it.

**Status:** Planned, not started. The pops are 16 separate physics simulations, baked in Blender by
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
| Replaces the `Level N: Card` tooltip on card level-ups | One announcement, not two. Level-ups without a card keep the tooltip |
| Also plays for a bonus card at the start of a retry (the ad's or one bought with coins, `PLAN-Coins.md`) | The same prize, the same way. Replaces the `BonusCardDealtMessage` tooltip |
| A pop is picked at random, never one of the last two shown | 16 sims are there for variety; repeating the last one would waste it |
| The balloon is tinted from the palette, not baked in one colour | The sims are rendered in greyscale with the shading kept, and tinted at runtime, so the reveal can follow the level's colours |
| Text, never baked into the art | The card's title and description come from `IUpgradeCardSettings`, so a new card needs no new art beyond its icon |
| A second card won while a slip is showing waits its turn | Rare (cards come every 2 levels), but a retry's bonus card can meet a level-up's |

## 4. Architecture

| Piece | Where | Responsibility |
|---|---|---|
| `CardRevealController` | `UI/CardReveal/` | Plain C#. Subscribes the card being held at a level-up (the `Transitioning` phase, when `RunUpgrades` holds the pick) and `BonusCardDealtMessage`; queues reveals, picks the pop, and tells the view which card to show |
| `CardRevealView` | `UI/CardReveal/` | `MonoBehaviour` on the game canvas. Plays the chosen pop on a UI `Image`, then the slip: a `HandwrittenHeadsUp`-style label written through `WriteOnText`, the icon, and the flutter-out tween. Kills its tweens on disable |
| `FlipbookPlayer` | `UI/CardReveal/` | Steps a `Sprite[]` (one pop's frames, sliced from its sheet) at a set frame rate on unscaled time. Swapping the sprite on one `Image` keeps it one draw call |
| `PopSheetLoader` | `UI/CardReveal/` | Loads only the chosen pop's sheet when the level-up turns `Pending` (before the transition, so the pop starts on time) and releases it after. The 16 sheets never sit in memory together |
| `ICardRevealSettings` | `Shared/`, authored on the game display config | The pop sheets, frame rate, hold time, slip sway, and per card kind the icon and where the slip flies to |
| `LevelUpPopUp` (changed) | `UI/LevelUp/` | Skips its card tooltip when a reveal is wired, so the reveal is the one announcement |

The flipbook is a sprite swap rather than a shader so each frame can be checked in the Editor. If
the profiler flags it, one material with a UV offset is the fallback.

## 5. How the pops are made

A script under `Tools/art/balloon_pop/` drives Blender headless (`blender -b -P bake_pops.py`) and
writes the 16 sheets and a contact sheet to review them. Everything is seeded, so a run redraws the
same set, and a seed can be swapped to replace one pop.

| Step | How |
|---|---|
| The balloon | A balloon mesh (body, neck, knot), modelled once in the script, inflated by cloth pressure to its rest shape. The same rest frame starts every sim, so the drift-up looks the same whatever pop follows |
| The puncture | A random point on the body. Cracks grow from it across the mesh, branching, and the mesh is cut along them. Seeded per sim: where it starts (side, top, near the knot), how many branches, how far they run before the pop |
| The tear | At the cut the pressure goes, and the rubber's stored stretch is released (cloth shrinking): each flap snaps back toward the knot and curls on itself, with self-collision so flaps don't pass through each other. Rubber thickness and stiffness vary a little per sim |
| Timing | A real pop is over in about 40 ms. The sim runs with fine substeps over that time and is sampled into 24 frames, played at 30 fps: about 0.8 s, roughly 20× slow motion |
| Rendering | Eevee, orthographic camera, transparent background. A rubber material (soft subsurface, one sharp highlight), lit from the top left like the UI's sun, rendered in greyscale for tinting |
| Packing | Each sim's 24 frames, 256×256 each, into a 6×4 sheet (1536×1024). The balloon's centre is the same pixel in every frame |
| The slip | Rendered by the same script with a paper material, so it matches the balloon's lighting: a narrow, curled strip, off-white, with a faint fold |

If Blender's cloth can't make the tear read as rubber (flaps that sag instead of snapping), the
fallback is a small membrane solver in the script itself (masses, springs that break past a strain,
the stored stretch released at the cut), with Blender only rendering its baked frames.

**Budget.** 16 sheets of 1536×1024, compressed (ASTC 6×6 on phones) at about 0.7 MB each: about
11 MB in the build, and only one in memory at a time (`PopSheetLoader`). The sheets stay out of the
sprite atlases, like the other baked art (`Sprites/README.md`), under `Assets/Sprites/UI/CardReveal/`.

## 6. Assets

| Asset | Size | Made by |
|---|---|---|
| 16 balloon pops | 16 sheets, 1536×1024 (24 frames of 256×256) | The bake script (§5) |
| Fortune-cookie slip, and its shadow | About 640×180 | The bake script, so it's lit like the balloon |
| Card icons | 4 × 192×192 | Drawn in the UI style, shared with the coin shop (`PLAN-Coins.md` §7) |

## 7. Phases

| Phase | What | Needs art |
|---|---|---|
| 1 | `CardRevealController` and its queue, the pick that avoids the last two pops, the tooltip hand-off, EditMode tests | No |
| 2 | The bake script: the balloon, one pop, rendered and packed. The owner reviews that one pop from the contact sheet before the other 15 are baked | Makes it |
| 3 | The other 15 pops and the slip, reviewed together on the contact sheet | Makes it |
| 4 | `FlipbookPlayer`, `PopSheetLoader` and `CardRevealView`: the pop, the write-on and the flutter-out | Yes: phases 2 and 3 |
| 5 | A playtest pass on a phone: timing, how well the slip reads over a busy board, and whether 256 px frames are sharp enough | — |

## 8. Open questions

- Does the slip fly to the weapon it changed, or just fade? Flying ties the card to its effect, but
  it crosses the board.
- Should a card that stacks (a second Shield) say so on the slip, e.g. `Shield ×2`?
- Are 256 px frames enough? The balloon fills about a quarter of the screen width, about 270 px on a
  1080 px phone. 384 px frames would be sharper, at about twice the build size.
