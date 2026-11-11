# UI/CardReveal

Plays the card reveal on the game canvas. A balloon rises and pops. Two pieces of ribbon fly off it:
one carries the won card's title, the other says what the card does. On a level-up they drift while
the level's ascent plays and are gone just before the next level settles. Which reveal plays, and
when, is decided by `Game/CardReveal/`; this folder only plays it. The design is in
`Plans/PLAN-CardReveal.md`.

## Contents

| File | What it does |
|---|---|
| `CardRevealView` | `MonoBehaviour` on `GameCanvas`, the reveal's **stage**. It attaches itself (`ICardReveals.AttachStage`) only when `ICardRevealSettings.PopSheets` has a pop, so with none the tooltip keeps announcing cards. Plays each `ICardReveals.Current` (the steps are below), then calls `Finish` with it. A null `Current` (a run reset, or the stage detaching) stops it where it is. Injected by `GameScopeRegistration`'s scene-view pass, since it is optional on the canvas |
| `RibbonPiece` | One piece of ribbon with a line written on it. Sizes the ribbon to its text. Flies itself: rolled up inside the balloon before the pop, then bursting out to its spot, drifting and swaying there, and finally flying off and fading. Where it rests, where the burst takes it, its drift, sway and way off are set on it by the builder. The text bends with the ribbon's wave, glyph by glyph |
| `RibbonGraphic` | The ribbon itself, a UI graphic: a strip with a V notch cut in each end, and a wave running along it. It shades by the wave's slope, with a sheen along its middle, so the folds read as satin |

## A reveal, step by step

All on unscaled time, so slow-motion beats don't stretch it.

1. The balloon is tinted one of the level's colours, at random. It rises from below to its place (`RiseSeconds`) and holds a beat (`HoldBeforePopSeconds`). The ribbons show faintly inside it.
2. The chosen pop's 24 frames play at 30 fps. The frame follows the time played, so a slow frame skips a frame rather than slowing the pop.
3. On `RibbonsFromFrame`, once the rubber has opened, the ribbons fly out (`RibbonBurstSeconds`). The title goes above the balloon's place, to the left; the description goes under it, to the right.
4. They drift and sway, readable, until it is time to go. Then they fly off, up and outward, and fade (`RibbonExitSeconds`).

When the ribbons go depends on the ascent:

- **On a level-up**, the ascent (`LevelAscendStartedMessage`, `ICinematicsSettings.LevelAscend.AscentSeconds`) starts just after the reveal. The ribbons are gone `RibbonsGoneAtAscent` of the way through it (0.7). That is just after the next level's balloons come in (the ascent's spawn cue, 0.6) and before it settles.
- **Without an ascent** (a retry's bonus card, the cheat), they fly `RibbonSecondsWithoutAscent` from the pop.
- **Either way**, they fly at least `RibbonMinimumSeconds`, so they can always be read.

`RibbonsGoneAt` works out that moment and is tested. It is checked again every frame until the ribbons leave, since the ascent can begin after they are out. Once they have started to leave they don't come back: an ascent held up for more than about 2 s after the pop (waiting on another cinematic, or a board overflow) finds them already gone.

## How the balloon is drawn

The pop is baked (`Tools/art/balloon_pop`), but not as pictures. Each frame holds the rubber's
surface: its normal as the camera saw it, and its ambient occlusion. `BalloonParty/Sprite/LitFlipbook`
lights it with the live scene light: its direction, colour and dimming, taken the way the balloons'
bodies take them. So the reveal shades like the board around it at any time of day. The rubber's
translucency is in the shader too: a face turned away from the light glows a deeper colour instead
of going dark.

Each frame is baked in two layers, the rubber in front of the balloon's centre and the rubber behind
it. The ribbons sit between them, so they read as inside the balloon:

| Child of `Balloon` | Draws |
|---|---|
| `Back` | A `RawImage` showing the back layer's frame. Its UV rect picks the frame from the sheet's bottom half |
| `TitleRibbon`, `DescriptionRibbon` | The two ribbons (`RibbonPiece`), cream satin written in the tooltip's pencil font |
| `Front` | A `RawImage` showing the front layer's frame, from the sheet's top half. Slightly see-through (`InsideShowThrough`), so before the pop the ribbons show as a shadow through the latex |

While the balloon rises the two layers part a little, the front leading and the back lagging
(`Parallax`). They meet at its place, so the depth reads even before it pops.

## Building it

`Tools > BalloonParty > CardReveal > Build Card Reveal` does all of this:

- imports every `pop_*.png` under `Assets/Sprites/UI/CardReveal/` as linear data (no sRGB, no mipmaps, clamped), skipping sheets already imported that way;
- lists them on `Assets/Configuration/CardRevealSettings.asset` (made if missing, and assigned to the `GameLifetimeScope` prefab);
- makes `Materials/UI/CardRevealRubber.mat`;
- builds `CardReveal` on `GameCanvas`.

Running it again rebuilds the canvas part and keeps the material's and settings' tuning.

The cheat `Run > Reveal A Card` plays a reveal for a random card without holding it. Use it to look
at the reveal without playing to a card level-up. With no ascent, the ribbons fly their
`RibbonSecondsWithoutAscent`. `Score > Trigger Level Up` shows the real thing, with the ascent, on a
level that deals a card.
