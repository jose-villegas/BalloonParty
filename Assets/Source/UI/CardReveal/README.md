# UI/CardReveal

Plays the card reveal on a canvas of its own, drawn in the scene and lit with the board. A balloon rises, a pushpin pricks it, and it pops. Two pieces of ribbon fly off it:
one carries the won card's title, the other says what the card does. On a level-up they drift while
the level's ascent plays and are gone just before the next level settles. Which reveal plays, and
when, is decided by `Game/CardReveal/`; this folder only plays it. The design is in
`Plans/PLAN-CardReveal.md`.

## Contents

| File | What it does |
|---|---|
| `CardRevealView` | `MonoBehaviour` on `CardRevealCanvas` (in the Game scene), the reveal's **stage**. It attaches itself (`ICardReveals.AttachStage`) only when `ICardRevealSettings.PopSheetPaths` has a pop, so with none the tooltip keeps announcing cards. Plays each `ICardReveals.Current` (the steps are below), then calls `Finish` with it. A null `Current` (a run reset, or the stage detaching) stops it where it is. Injected by `GameScopeRegistration`'s scene-view pass, since the scene may not have one. When the rubber tears it flashes a white light into the scene light field (`PopFlashRadius`, `PopFlashIntensity`, `PopFlashSeconds`) and stirs the disturbance field as a board pop does |
| `PopSheetLoader` | Plain C#, owned by the view. Keeps in memory only the sheets of the pop playing and of the next reveal's pop (`ICardReveals.NextPop`), loaded from `Resources` ahead, while the game plays, so a reveal starts on time; the others are unloaded (each is about 6 MB on a phone, all eight about 50). The view holds the new pair as each reveal starts and ends, and waits for the playing pop's sheet before showing the balloon, which is at once unless a reveal comes right after the scene loads. A sheet let go while still loading is unloaded when it arrives, unless it's wanted again by then: a second load of the same path would hand back the same texture. A missing sheet skips its reveal, with a warning |
| `RibbonPiece` | One piece of ribbon with a line written on it. Sizes the ribbon to its text. Flies itself: rolled up inside the balloon before the pop, then bursting out to its spot, drifting and swaying there, and finally flying off and fading. Where it rests, where the burst takes it, its drift, sway and way off are set on it by the builder. The text bends with the ribbon's wave, glyph by glyph |
| `RibbonGraphic` | The ribbon itself, a UI graphic: a strip with a V notch cut in each end, and a wave running along it. Cel shaded by its material (`BalloonParty/Sprite/CelRibbon`, `Materials/UI/CardRevealRibbon`): folds turned toward the scene light in the ribbon's colour, those turned away in a shadow tone, a satin sheen along the lit folds' middle, all in the scene light field's light where each vertex is (see below). The mesh carries a second copy of the strip, which the shader moves away from the light as the ribbon's drop shadow |

## A reveal, step by step

All on unscaled time, so slow-motion beats don't stretch it.

1. The balloon is tinted `BalloonColor` (silver), none of the board's colours, so it reads as the prize. It floats up from below the way a balloon let go does (`FloatOffset`, tested): rising like a spring, a little past its place (`RiseOvershoot`) at its highest after `RiseSeconds`, then bobbing back to rest over `HoldBeforePopSeconds`, swaying side to side (`RiseSway`, `RiseSwaySeconds`) and leaning into the sway (`RiseTilt`), the sway dying away. It pops square at its place, so the rubber falls straight down. The ribbons show faintly inside it.
2. A pushpin pricks it (`PinPose`, tested). The pin is baked as the rubber is (`Tools/art/balloon_pop/bake_pin.py`) and lit by the same shader, its head (`PinHeadColor`, red) and needle (`PinNeedleColor`, steel) tinted apart. It comes in along the angle it was baked at (`PinLean`), from `PinEnterDistance` away, fading in and easing to hover `PinHoverDistance` from the pop's puncture (`PopPunctures`: where the bake's tear starts, which the bake prints), draws back `PinWindupDistance`, and jabs in, speeding up, `PinDepth` into the rubber. The needle goes in as the balloon comes to rest, and the pop starts then, so the tear opens from it. It stays in for `PinStaySeconds` as the rubber tears, then the burst throws it back (`PinRecoilDistance`) as it fades (`PinLeaveSeconds`). With no pin built, the balloon pops on its own.
3. The chosen pop's 48 frames play at `FramesPerSecond` (24: 2 s, a slow-motion beat so the tear reads; the bake renders every frame of its simulation, so the slow motion stays smooth). The frame follows the time played, so a slow frame skips a frame rather than slowing the pop. From `RubberFallsFromFrame`, once it tears, the rubber falls, speeding up under `RubberFallGravity`. The fall is the bake's: its knot falls from the tear with the rubber falling around it under the same gravity, its camera following the knot, so the frames hold the rubber's motion as it falls and the game moves them down by the camera's path (`FallDistance`, tested). Both are counted in the pop's frames, so it falls in slow motion with the pop. Once the rubber gathers, the bake's camera also eases down a little more (`RubberRecentreFromFrame`, `RubberRecentreFrames`, `RubberRecentreHeight`) to keep it inside the frame, and the game adds that back too. On the way down it flutters side to side and leans into each swing (`FallSwing`, tested: `RubberSwing` frame widths over `RubberSwingSeconds` a swing, leaning `RubberSwingTilt` degrees), growing in from square at the tear. Each pop rolls its own reach and pace within `RubberSwingVariance` and starts at a random point of the swing, so no two fall alike. It also turns as it falls (`FallSpin`, tested): the turn picks up over one swing and runs on at `RubberSpin` degrees a second, one way or the other at random, its speed varied as the swing is. Only the rubber falls; the ribbons fly their own way. As it tears, shreds of rubber in its colour fly far out (`ShredsPrefab`, a pooled particle burst parented to the canvas and laid out in its units): the frames can't carry rubber that far from the balloon. Slowed by the air, they rock and flutter down off the screen.
4. On `RibbonsFromFrame`, once the rubber has opened, the ribbons fly out (`RibbonBurstSeconds`). The title goes above the balloon's place, to the left; the description goes under it, to the right.
5. They swell to `RibbonGrowth` (1.3) over `RibbonGrowSeconds`, so the card grows into view, but never wider than the screen allows (`GrownScale`, tested). They drift and sway, readable, until it is time to go. Then they fly off, up and outward, and fade (`RibbonExitSeconds`).

When the ribbons go depends on the ascent:

- **On a level-up**, the ascent (`LevelAscendStartedMessage`, `ICinematicsSettings.LevelAscend.AscentSeconds`) starts just after the reveal. The ribbons stay until `RibbonsGoneAtAscent` of the way through it (0.82), after the next level's balloons come in (the ascent's spawn cue, 0.6), and then `RibbonsAfterAscentSeconds` (0.5 s) more, so they leave just before the new level settles.
- **Without an ascent** (a retry's bonus card, the cheat), they fly `RibbonSecondsWithoutAscent` from the pop.
- **Either way**, they fly at least `RibbonMinimumSeconds`, so they can always be read.

`RibbonsGoneAt` works out that moment and is tested. It is checked again every frame until the ribbons leave, since the ascent can begin after they are out. Once they have started to leave they don't come back: an ascent held up for more than about 2 s after the pop (waiting on another cinematic, or a board overflow) finds them already gone.

## How the balloon is drawn

The pop is baked (`Tools/art/balloon_pop`), but not as pictures. Each frame holds the rubber's
surface: its normal as the camera saw it, and its ambient occlusion. `BalloonParty/Sprite/LitFlipbook`
cel shades it with the live scene light: its direction, colour and dimming, taken the way the
balloons' bodies take them. So the reveal sits in the board's light, and in its flat, toon style:

- **Three tones**, lit, middle and shadow, with hard steps between them, and the baked deep folds
  drawn as creases in a darker tone. A crisp highlight in the light's own colour (white at noon), as
  on the board's balloons. The steps are anti-aliased to a screen pixel, however large the balloon is
  drawn.
- **Subsurface scattering**, done cheaply on screen. A few samples toward the light add up how much
  rubber the light crosses to reach each point. Where it crosses little (the rim facing the light, a
  flap or shred with no rubber behind it) and on faces turned away from the light, the latex glows a
  richer, lighter tint, the way light shows through a balloon.
- **A drop shadow**, a soft dark shade of the balloon's colour, away from the scene light. It is drawn
  on an image of its own (`Shadow`, on `CardRevealRubberShadow`) that the shader moves away from the
  light, so the frame's edge never cuts it off. It takes both layers' outline.

The shader keeps its samples inside the frame shown, so it needs the sheet's grid: the view sets it,
from the settings, on its own copies of the two rubber materials. Once the pop's last frame has shown,
the layers stop drawing.

Each frame is baked in two layers, the rubber in front of the balloon's centre and the rubber behind
it. The ribbons sit between them, so they read as inside the balloon:

| Child of `Balloon` | Draws |
|---|---|
| `Shadow` | A `RawImage` of the back layer's frame on the shadow's material: the balloon's drop shadow |
| `Back` | A `RawImage` showing the back layer's frame. Its UV rect picks the frame from the sheet's bottom half |
| `TitleRibbon`, `DescriptionRibbon` | The two ribbons (`RibbonPiece`), cream satin written in the tooltip's pencil font |
| `Front` | A `RawImage` showing the front layer's frame, from the sheet's top half. Slightly see-through (`InsideShowThrough`), so before the pop the ribbons show as a shadow through the latex |

While the balloon rises the two layers part a little, the front leading and the back lagging
(`Parallax`). They meet at its place, so the depth reads even before it pops.

The pushpin is `Pin`, beside `Balloon` and after it, so it draws over the rubber and the ribbons: a
`Needle` and a `Head` image, cut from the two cells of its sheet (`pin.png`, the head on the left, the
needle on the right, in the top half as a pop's front layer) on `CardRevealPin`, the rubber's shader
without its glow (plastic and steel let no light through). The view lays it out from the settings:
its size (`PinSize`, in frame heights, so it is drawn to the balloon's scale), hung from its needle's
tip (`PinTip`), and placed by the puncture from the balloon's place, not its layers, since it goes in
as the balloon rests.

## In the game's light

The reveal is drawn in the scene, not over it, so it takes part in the game's lighting as the
board's balloons do:

- **Its own canvas.** `CardRevealCanvas` is a World Space canvas, kept over the main camera's view by
  `CameraFittedCanvas` (sized as the game canvas's `CanvasScaler` sizes it, so the layout is the
  same, and following the level-up's zoom). It is on the `Balloons` layer, which the main camera
  draws and the screen-space light's scene capture records, so the reveal casts and bounces light.
  It sorts at `Sky` 31999, above every gameplay sprite and just under the screen-space light's
  overlay (`Sky` 32000), which then tints it like the board. A Screen Space canvas couldn't do
  this: the HUD camera draws after the overlay and the capture.
- **The scene light field.** The rubber and ribbon shaders read the field per vertex
  (`SceneLight*AtLOD`): the time-of-day light, plus local lights, which add their colour and turn
  the light toward them. A flash or a nearby lit item reaches the reveal.
- **Its own pop.** When the rubber tears, `CardRevealView` flashes a white light
  into the field and stirs the disturbance field, as a board pop does: the board around it lights
  up, and the ribbons bursting out catch it. The shreds are on `CardRevealShred`
  (`BalloonParty/Sprite/Diffuse`), lit by the field as the board's sprites are. Their scraps are near
  white, so the tint and the light shade them as the cel tones shade the rubber.
- **The pin** is lit by the field too, as the rubber is.

The drop shadows still fall along the time-of-day light only, so they don't swing toward a local
light.

## Building it

`Tools > BalloonParty > CardReveal > Build Card Reveal` does all of this:

- imports every `pop_*.png` under `Assets/Sprites/UI/CardReveal/Resources/CardRevealPops/` as linear data (no sRGB, no mipmaps, clamped), and on phones as ASTC 4×4 at its best quality (the default, ETC2, smears the normals into 4×4 blocks, which the cel steps draw as stairs; ASTC 4×4 is the same 8 bits a texel), skipping sheets already imported that way;
- lists their Resources paths (`CardRevealPops/pop_NN`) on `Assets/Configuration/CardRevealSettings.asset` (made if missing, and assigned to the `GameLifetimeScope` prefab): paths, not references, so none loads with the scene;
- makes `Materials/UI/CardRevealRubber.mat`, `CardRevealRubberShadow.mat` (the same shader with Drop Shadow on), `CardRevealRibbon.mat` and `CardRevealPin.mat` (the rubber's shader on the pin's two cells, without the glow);
- imports `pin.png` (`bake_pin.py`) as data, as the pop sheets are; without it the reveal is built with no pin, with a warning;
- sets up the shreds (`CardRevealShredsBuilder`): imports `shreds.png` (`Tools/art/balloon_pop/make_shreds.py`), makes `Materials/UI/CardRevealShred.mat` and the `Prefabs/Game/CardRevealShreds` particle prefab if they are missing, and assigns the prefab on the settings if it has none. Delete the prefab to rebuild it with the builder's values;
- builds the `Assets/Prefabs/Game/CardRevealCanvas` prefab, every object on the `Balloons` layer (the ribbons' text is cloned from the tooltip's, on `UI`, which the main camera skips);
- removes a reveal built on `GameCanvas` by an older version of the tool;
- places an instance in `Assets/Scenes/Game.unity` if it has none (a Game scene open with unsaved changes is left alone, with a warning).

Running it again rebuilds the prefab and keeps the materials' and settings' tuning.

The cheat `Run > Reveal A Card` plays a reveal for a random card without holding it. Use it to look
at the reveal without playing to a card level-up. With no ascent, the ribbons fly their
`RibbonSecondsWithoutAscent`. `Score > Trigger Level Up` shows the real thing, with the ascent, on a
level that deals a card.
