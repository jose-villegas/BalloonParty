# UI/CardReveal

Plays the card reveal on the game canvas: a balloon rises, pops, and leaves a paper slip with the won
card's title written on it in pencil. Which reveal plays, and when, is decided by `Game/CardReveal/`;
this folder only plays it. The design is in `Plans/PLAN-CardReveal.md`.

## Contents

| File | What it does |
|---|---|
| `CardRevealView` | `MonoBehaviour` on `GameCanvas`, the reveal's **stage**. Attaches itself (`ICardReveals.AttachStage`) only when `ICardRevealSettings.PopSheets` has a pop, so with none the tooltip keeps announcing cards. For each `ICardReveals.Current` it plays: the balloon (tinted one of the level's colours, at random) rises from below to its place, holds a beat, then plays the chosen pop's 24 frames at 30 fps; once the rubber has opened (`WriteFromFrame`) the card's title is pencilled on the slip (`UI/HeadsUp/WriteOnText`); the slip holds, the whole reveal fades, and the view calls `Finish` with it. All on unscaled time, so slow-motion beats don't stretch it. A null `Current` (a run reset, or the stage detaching) stops it where it is. Injected by `GameScopeRegistration`'s scene-view pass, since it is optional on the canvas |

## How the balloon is drawn

The pop is baked (`Tools/art/balloon_pop`), but not as pictures: each frame holds the rubber's surface,
its normal as the camera saw it and its ambient occlusion. `BalloonParty/Sprite/LitFlipbook` lights it
with the live scene light (its direction, colour and dimming, taken the way the balloons' bodies take
them), so the reveal shades like the board around it at any time of day. The rubber's translucency is
in the shader too: a face turned away from the light glows a deeper colour instead of going dark.

Each frame is baked in two layers, the rubber in front of the balloon's centre and the rubber behind it,
and the slip sits between them, so it reads as inside the balloon:

| Child of `Balloon` | Draws |
|---|---|
| `Back` | A `RawImage` showing the back layer's frame (its UV rect picks the frame from the sheet's bottom half) |
| `Slip` | The paper slip and its `Title`. A stand-in until the baked slip (phase 3): `rounded_panel` in paper white, with the tooltip's pencil font |
| `Front` | A `RawImage` showing the front layer's frame, from the sheet's top half. Slightly see-through (`SlipShowThrough`), so before the pop the slip shows as a shadow through the latex |

While the balloon rises the two layers part a little, the front leading and the back lagging
(`Parallax`), and meet at its place: the depth reads even before it pops.

## Building it

`Tools > BalloonParty > CardReveal > Build Card Reveal` imports every `pop_*.png` under
`Assets/Sprites/UI/CardReveal/` as linear data (no sRGB, no mipmaps, clamped), lists them on
`Assets/Configuration/CardRevealSettings.asset` (made if missing, and assigned to the
`GameLifetimeScope` prefab), makes `Materials/UI/CardRevealRubber.mat`, and builds `CardReveal` on
`GameCanvas`. Running it again rebuilds the canvas part and keeps the material's and settings' tuning.

The cheat `Run > Reveal A Card` plays a reveal for a random card without holding it, to look at the
reveal without playing to a card level-up.
