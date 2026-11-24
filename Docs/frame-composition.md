# How a frame is made

One frame of BalloonParty, taken apart: what the game works out before it draws, what it draws and in
which order, and what each part adds. The frame is a staged one. In the meadow at dusk a ball loaded as a
lance ran through a field of silver balloons and hit the nuke. Four seconds later the nuke went off, and this
is a fifth of a second into its blast.

| The frame this page takes apart | The whole shot |
|---|---|
| <img src="frame/frame.jpg" width="300"> | <img src="frame/frame.webp" width="300"> |

Every picture here is a file of its own, retaken by a script from a scene described in a text file
([Retaking the pictures](#retaking-the-pictures)): open one to see it at full size. The moving pictures all
show the same shot, from the cannon firing to the cloud the blast leaves, a little slower than it happened
and with the fuse's four seconds cut to a few beats.

The script also writes all of it as one video, the frame beside its ten steps with every map under them:
once at the shot's own speed, then two and a half times slower. At some eighty megabytes it is not kept in
the repository; it is `Logs/frame/glory/doc/composition.mp4` after a capture.

## What the game does not use

The game draws with Unity's 2D renderer (URP) and uses none of its lighting: no 2D lights, no normal maps, no
shadow pass, no custom render passes. To the renderer every sprite is unlit.

The light is the game's own: three numbers for the time of day and a handful of small textures that cover the
board, which the game fills in each frame and its shaders read. How a frame is built is mostly the story of
those textures: who writes each one, who reads it, and what it adds.

## 1. Before anything is drawn: the fields

A field is a small texture laid over the board in world space, a few texels to each world unit (the board is
5.2 by 9.6 units). Play writes into the fields: a ball flies, a balloon pops, a blast spreads. Shaders look
up their own world position in a field to find out what is happening where they stand.

The meadow adds a wake buffer that lays the grass down behind the ball, and the water level has two of its
own, the ripples and the caustics on the sand; the tool shows none of the three.

|  | Size | In this frame | Its alpha | Through the shot | Its alpha, through the shot |
|---|---|---|---|---|---|
| **Disturbance field** | 43 x 77 | <img src="frame/map-disturbance-field.jpg" width="150"> | <img src="frame/map-disturbance-field-alpha.jpg" width="150"> | <img src="frame/map-disturbance-field.webp" width="150"> | <img src="frame/map-disturbance-field-alpha.webp" width="150"> |
| **Light field** | 86 x 154 | <img src="frame/map-scene-light-field.jpg" width="150"> | <img src="frame/map-scene-light-field-alpha.jpg" width="150"> | <img src="frame/map-scene-light-field.webp" width="150"> | <img src="frame/map-scene-light-field-alpha.webp" width="150"> |
| **Light colour** | 43 x 77 | <img src="frame/map-scene-light-colour.jpg" width="150"> | <img src="frame/map-scene-light-colour-alpha.jpg" width="150"> | <img src="frame/map-scene-light-colour.webp" width="150"> | <img src="frame/map-scene-light-colour-alpha.webp" width="150"> |
| **Background field** | 86 x 154 | <img src="frame/map-background-field.jpg" width="150"> |  | <img src="frame/map-background-field.webp" width="150"> |  |
| **Smoke field** | 86 x 154 | <img src="frame/map-smoke-field.jpg" width="150"> | <img src="frame/map-smoke-field-alpha.jpg" width="150"> | <img src="frame/map-smoke-field.webp" width="150"> | <img src="frame/map-smoke-field-alpha.webp" width="150"> |

## 2. The frame looks at itself

Two more textures are made from the picture itself, not from play.

|  | Size | In this frame | Its alpha | Through the shot | Its alpha, through the shot |
|---|---|---|---|---|---|
| **Scene capture** | 154 x 274 | <img src="frame/map-scene-capture.jpg" width="150"> | <img src="frame/map-scene-capture-alpha.jpg" width="150"> | <img src="frame/map-scene-capture.webp" width="150"> | <img src="frame/map-scene-capture-alpha.webp" width="150"> |
| **GI light buffer** | 77 x 137 | <img src="frame/map-gi-light-buffer.jpg" width="150"> | <img src="frame/map-gi-light-buffer-alpha.jpg" width="150"> | <img src="frame/map-gi-light-buffer.webp" width="150"> | <img src="frame/map-gi-light-buffer-alpha.webp" width="150"> |

The GI light buffer is worked out from the scene capture. Its colour is the *bounce*: the scene's colours
gathered round the point, most from the side away from the light, where what stands there turns its lit face
to it; so a red balloon lends a little red to what stands in front of its lit side. Its alpha is the
*shadow*: how much stands between the point and the light. [The GI light buffer](gi-light-buffer.md) takes it
apart: the march along the light, the mip levels that widen it into a cone, what is masked out, and what each
setting does.

Both run one frame late: the buffer is worked out from the capture of the frame before. The capture is also
what the unbreakable balloon's chrome and the disco ball's mirrors reflect.

## 3. Drawing, back to front

Now the main camera draws. Each row holds one more group than the last, in the order the camera draws them.
*What it adds* is the difference from the row above, made three times brighter, since a soft shadow or a tint
is too faint to see otherwise.

- **1. The backdrop.** The camera clears to a sky colour tinted by the time of day, then draws the ground and its grass, the smoke and the clouds. The ground reads the light field at every pixel, so the blast's light is already on the grass.
- **2. The specks.** Dust and pollen, a few hundred points moved on the graphics card and pushed by the disturbance field. They lie over the ground and under every balloon.
- **3. The balloons' shadows.** Each balloon carries its shadow as a sprite of its own. The shader slides it away from the light.
- **4. The balloons' bodies**, with their knots and items. The mushrooms' caps, the unbreakables' ring and the nuke's blast disc sort with them.
- **5. The shines.** Each highlight is another sprite, turned to face the light.
- **6. The rest of the board.** The cannon, the ball, clouds and deflectors on some levels, the frame's soft edges.
- **7. The effects.** Pops, shards, sparks and the nuke's cloud, all particles.
- **8. The light, laid over.** One quad the size of the screen multiplies the frame by the GI light buffer: it darkens where the shadow is strong and tints where the bounce is.
- **9. Post-processing.** Bloom, colour grading and the tone map.
- **10. The HUD**, drawn by a second camera after all of that, so the tone map never touches it. The score shapes and their trails belong here, not to the effects.

| Step | The frame so far | What it adds | Through the shot | What it adds, through the shot |
|---|---|---|---|---|
| 1. The backdrop | <img src="frame/step-backdrop.jpg" width="165"> | all of it | <img src="frame/step-backdrop.webp" width="165"> | all of it |
| 2. The specks | <img src="frame/step-specks.jpg" width="165"> | <img src="frame/added-specks.jpg" width="165"> | <img src="frame/step-specks.webp" width="165"> | <img src="frame/added-specks.webp" width="165"> |
| 3. The balloons' shadows | <img src="frame/step-shadows.jpg" width="165"> | <img src="frame/added-shadows.jpg" width="165"> | <img src="frame/step-shadows.webp" width="165"> | <img src="frame/added-shadows.webp" width="165"> |
| 4. The balloons' bodies | <img src="frame/step-bodies.jpg" width="165"> | <img src="frame/added-bodies.jpg" width="165"> | <img src="frame/step-bodies.webp" width="165"> | <img src="frame/added-bodies.webp" width="165"> |
| 5. The shines | <img src="frame/step-shines.jpg" width="165"> | <img src="frame/added-shines.jpg" width="165"> | <img src="frame/step-shines.webp" width="165"> | <img src="frame/added-shines.webp" width="165"> |
| 6. The rest of the board | <img src="frame/step-board.jpg" width="165"> | <img src="frame/added-board.jpg" width="165"> | <img src="frame/step-board.webp" width="165"> | <img src="frame/added-board.webp" width="165"> |
| 7. The effects | <img src="frame/step-effects.jpg" width="165"> | <img src="frame/added-effects.jpg" width="165"> | <img src="frame/step-effects.webp" width="165"> | <img src="frame/added-effects.webp" width="165"> |
| 8. The light, laid over | <img src="frame/step-light.jpg" width="165"> | <img src="frame/added-light.jpg" width="165"> | <img src="frame/step-light.webp" width="165"> | <img src="frame/added-light.webp" width="165"> |
| 9. Post-processing | <img src="frame/step-post.jpg" width="165"> | <img src="frame/added-post.jpg" width="165"> | <img src="frame/step-post.webp" width="165"> | <img src="frame/added-post.webp" width="165"> |
| 10. The HUD | <img src="frame/frame.jpg" width="165"> | <img src="frame/added-hud.jpg" width="165"> | <img src="frame/frame.webp" width="165"> | <img src="frame/added-hud.webp" width="165"> |

Steps 1 to 8 are written out before the tone map, which folds colours brighter than white back into what a
screen can show. So anything brighter than white is flat white in them: the blast's disc looks blank until
step 9, where its insides return.

### The light, laid over

| Before the light | The scene capture | The bounce (the buffer's colour) | The shadow (the buffer's alpha) | After it |
|---|---|---|---|---|
| <img src="frame/step-effects.jpg" width="150"> | <img src="frame/map-scene-capture.jpg" width="150"> | <img src="frame/map-gi-light-buffer.jpg" width="150"> | <img src="frame/map-gi-light-buffer-alpha.jpg" width="150"> | <img src="frame/step-light.jpg" width="150"> |

The quad multiplies: the result is the frame times twice the quad's colour, so a mid grey leaves the frame as
it was, darker shades it and brighter lifts it. The lift is capped just above the brightest lit balloon, so
the bounce can never push a balloon into the bloom. Balloons take no shadow from it: the shadow is masked off
the things that cast it, so only the ground beside them darkens.

### Post-processing

| Before post-processing | After it |
|---|---|
| <img src="frame/step-light.jpg" width="320"> | <img src="frame/step-post.jpg" width="320"> |

- **Bloom.** Whatever is brighter than 1.4 times white spills light around itself. Lit balloons are kept
  under that, so only real lights bloom: a blast, a lightning strike, the glow of a rim.
- **Grading.** A little contrast is added (+15) and a little saturation taken away (−10), with room for each
  scenario to grade on top (all three are neutral today).
- **The tone map.** The Khronos PBR Neutral curve in place of Unity's own. It leaves colours below about
  three quarters of white very nearly as drawn, so a balloon keeps its authored colour, and rolls off what is
  brighter without bending its hue.

## 4. The lens around a flying ball

While a ball is in the air there is one more step. This frame is from the same shot, as the ball goes
through the balloons it has just popped:

| The frame in flight | Without the effects | The lens mask | The effects, through the lens |
|---|---|---|---|
| <img src="frame/flight.jpg" width="200"> | <img src="frame/lens-board.jpg" width="200"> | <img src="frame/lens-mask.jpg" width="200"> | <img src="frame/lens-effects.jpg" width="200"> |

Every ball carries a dome of glass. To draw it, the game takes the effects off the main camera, and with
them the specks and the unbreakables' rings, and draws them alone into their own texture, the effects map, at
full size. A second texture, the lens mask, a quarter of the screen's size, holds a soft disc wherever a ball
is or just was. Then one quad puts the effects back into the frame *through* the mask: inside a dome they are
bent, faded and given a glint, and outside it they are drawn as they were.

| The effects map | Its alpha | The HUD effects map | Its alpha |
|---|---|---|---|
| <img src="frame/lens-effects-map.jpg" width="200"> | <img src="frame/lens-effects-map-alpha.jpg" width="200"> | <img src="frame/lens-hud-map.jpg" width="200"> | <img src="frame/lens-hud-map-alpha.jpg" width="200"> |

The score shapes are not in the effects map, though they sit right beside the pops. They belong to the HUD's
side of the frame, which has an effects map of its own; its quad puts the shapes back through the same mask.

When no ball is flying, as in the frame of the blast, none of this runs and the effects are drawn straight
in. The maps through the shot show it: all go dark once the ball has landed.

| Lens mask | Effects map | Its alpha | HUD effects map | Its alpha |
|---|---|---|---|---|
| <img src="frame/map-effects-clearing-mask.webp" width="150"> | <img src="frame/map-main-effects-map.webp" width="150"> | <img src="frame/map-main-effects-map-alpha.webp" width="150"> | <img src="frame/map-hud-effects-map.webp" width="150"> | <img src="frame/map-hud-effects-map-alpha.webp" width="150"> |

## 5. How a balloon builds its look

A plain balloon is five sprites and no lighting model. Four of them ask the light one question each, once,
at the balloon's centre, and the answers are what make it look lit.

| Sprite | What it asks | What it does with the answer |
|---|---|---|
| Shadow | Which way is the light, and how strong? | Slides away from the light and fades as the light dims. It also fades where nothing is under it to catch a shadow: no cloud, no smoke, no ground |
| Knot | Nothing | Drawn as it is: the one part the light does not touch |
| Body | What colour is the time of day? | Takes that tint, pulled back at night so a red balloon stays red in the dark |
| Shine | Which way is the light, and how strong? | Turns so the glint lies on the lit side, takes the light's colour, and fades at night |
| Second shine | Is there a local light near? | Invisible until one is. Then it fades in on the side of that light, in its colour |

The light going once round one balloon: the shine turns and the shadow travels.

<img src="frame/balloon-turning.webp" width="300">

The same balloon built up at the four named times of day:

|  | The art, under white light | + shadow | + body | + shine | The finished frame |
|---|---|---|---|---|---|
| **Dawn** | <img src="frame/balloon-dawn-flat.jpg" width="140"> | <img src="frame/balloon-dawn-shadows.jpg" width="140"> | <img src="frame/balloon-dawn-bodies.jpg" width="140"> | <img src="frame/balloon-dawn-shines.jpg" width="140"> | <img src="frame/balloon-dawn-post.jpg" width="140"> |
| **Day** | <img src="frame/balloon-day-flat.jpg" width="140"> | <img src="frame/balloon-day-shadows.jpg" width="140"> | <img src="frame/balloon-day-bodies.jpg" width="140"> | <img src="frame/balloon-day-shines.jpg" width="140"> | <img src="frame/balloon-day-post.jpg" width="140"> |
| **Dusk** | <img src="frame/balloon-dusk-flat.jpg" width="140"> | <img src="frame/balloon-dusk-shadows.jpg" width="140"> | <img src="frame/balloon-dusk-bodies.jpg" width="140"> | <img src="frame/balloon-dusk-shines.jpg" width="140"> | <img src="frame/balloon-dusk-post.jpg" width="140"> |
| **Night** | <img src="frame/balloon-night-flat.jpg" width="140"> | <img src="frame/balloon-night-shadows.jpg" width="140"> | <img src="frame/balloon-night-bodies.jpg" width="140"> | <img src="frame/balloon-night-shines.jpg" width="140"> | <img src="frame/balloon-night-post.jpg" width="140"> |

And the corner of the frame at the edge of the blast, built up the same way. The shadows have slid away from
the blast, not from the sun, and the shines have turned to face it and taken its colour: that is the light
field at work. In step 8 the light quad adds what no sprite carries, the soft shadow each balloon casts on
the grass.

| The art, under white light | 1 Backdrop | 2 + specks | 3 + shadows | 4 + bodies |
|---|---|---|---|---|
| <img src="frame/balloon-flat.jpg" width="150"> | <img src="frame/balloon-backdrop.jpg" width="150"> | <img src="frame/balloon-specks.jpg" width="150"> | <img src="frame/balloon-shadows.jpg" width="150"> | <img src="frame/balloon-bodies.jpg" width="150"> |

| 5 + shines | 6 + the rest of the board | 7 + effects | 8 + the light | 9 + post-processing |
|---|---|---|---|---|
| <img src="frame/balloon-shines.jpg" width="150"> | <img src="frame/balloon-board.jpg" width="150"> | <img src="frame/balloon-effects.jpg" width="150"> | <img src="frame/balloon-light.jpg" width="150"> | <img src="frame/balloon-post.jpg" width="150"> |

The special balloons keep the idea and change the parts:

- **Golden, disco and nuke** have bodies drawn by a shader, with no sprite: a sphere of plates, mirrors or
  dents that works out its own highlight from the light's direction. The disco ball's mirrors also reflect
  the scene capture.
- **The heart** has a shine made for its shape, lit from normals baked into the sprite.
- **The reward balloons' rays** are not lit at all, but each registers a light of its own in the light field,
  so a golden balloon really does light the balloons beside it.
- **The rainbow balloon** reads the light field at every pixel, not once at its centre.

## 6. Every step, in order

The whole frame as a list. Steps 1 to 12 happen before any camera draws.

**While the game updates**

1. The time of day sets the light's direction, colour and strength (`TimeOfDayService`).
2. The disturbance field takes this frame's stamps and, every 0.05 s, spreads and settles
   (`DisturbanceFieldService`).
3. The light field is rebuilt if a light moved, appeared or faded: filled, every light added, then the way
   to the light worked out; the colour map with it (`SceneLightFieldService`).
4. The background field is rebaked every third frame from noise and the disturbance field
   (`BackgroundFieldService`).
5. The smoke field takes its stamps and fades (`SmokeFieldService`).
6. The camera's clear colour is tinted by the light (`CameraBackgroundTint`).

**After the update, before the cameras**

7. The ground's own buffers step: the grass wake in the meadow, the ripples and caustics on the water
   (`GroundView`).
8. The scene capture camera is switched on for this frame (`SceneCaptureService`).
9. The GI light buffer is made from the capture of the frame before: gather, then soften
   (`ScreenSpaceLightService`).
10. The specks of dust and pollen move, pushed by the disturbance field and tinted by the light
    (`SpeckField`).
11. The scenario's grade is written for the post-processing (`ScenarioGradeView`).
12. While a ball flies: the lens mask is stamped (`EffectsClearingMaskView`) and the effects are handed to
    their own camera (`EffectsMapCompositor`).

**The cameras**

13. The scene capture camera draws the board at a seventh of the size, for the next frame.
14. While a ball flies: the effects camera draws the effects into the effects map.
15. The main camera clears to the tinted sky colour and draws the backdrop: ground or sky, smoke, clouds,
    and over them the specks.
16. The balloons, each back to front: shadow, knot, body, shines.
17. The ball, the items, the obstacles, the score trails, the frame's edges.
18. The effects, straight in or through the lens.
19. The light quad multiplies the frame by the GI light buffer.
20. Bloom, grading and the tone map.
21. The HUD camera draws the interface on top, with the score shapes and trails, through the lens while a
    ball flies.

## Retaking the pictures

The scene is `Tools/frame/glory.json`: the scenario, level and time of day, the board drawn as rows of text
(one character a balloon), the shot, and which frame is taken apart. With the Unity Editor open:

```bash
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --preview 400   # a sheet of the shot, to choose the frame by
python3 Tools/frame/frame_doc.py Tools/frame/glory.json --publish       # capture, compose, copy to Docs/frame/
```

`Tools/frame/README.md` says how the file is written and what the tool captures.
