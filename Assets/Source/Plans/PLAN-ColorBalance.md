@page plan_color_balance Colour Balance

# Colour balance — crisp gameplay colours over every scene

> Keep the colours that carry the game crisp, contrasting and recognisable in all three
> scenarios at every time of day, and let only real light sources glow. Written 2026-11-11 after
> the first scenario grade (see `ScenarioGradeView`) made the meadow look washed out. It
> measures the eight colours that matter most on the rendered frame, sets targets for them from
> colour-science research, and changes the tonemapper and the grades to meet them.
>
> Works with @ref plan_hdr, which switched on HDR rendering, the tonemapper and bloom.

---

## The eight colours

The colours a player reads to make a decision, plus the ones that have to stand apart from them.
All live in `Assets/Configuration/GamePalette.asset`; the backdrops are not in the eight, they
are what the eight are measured against.

| # | Palette id | sRGB | Job | Must stand apart from |
|---|---|---|---|---|
| 1 | Green | (0.467, 0.867, 0.467) | Gameplay colour | the other three, the meadow grass |
| 2 | Red | (0.84, 0.289, 0.21), was (1, 0.412, 0.380) | Gameplay colour | green (colour-blind players), Prediction |
| 3 | Blue | (0.012, 0.663, 0.957) | Gameplay colour | the water, Formation blue |
| 4 | Purple | (0.612, 0.153, 0.690) | Gameplay colour | blue, the night backdrops |
| 5 | Tough | (0.3, 0.3, 0.3) | Armour on tough balloons | every backdrop at night |
| 6 | UnbreakableFormation | (0.15, 0.5, 1) | The linked unbreakables' swirl | Blue |
| 7 | Sparks | (1, 0.979, 0.741) | Glow, sparks, the projectile's shine | must read as light, not as a surface |
| 8 | Prediction | (1, 0.388, 0.278) | The aim line | Red, every backdrop |

---

## What the research says

The full notes, with every source, are summarised here; the numbers in brackets are the
references at the end.

**Telling colours apart.**
- Measure differences in CIEDE2000 [1, 2] and design in OKLCh [3]. A difference of 1 is the
  laboratory threshold; on a real screen, at the size of a mark, people need about five times
  more, and colours become *nameably* different above about 20 [5, 6].
- Clutter and motion raise the threshold [5, 9]. Balloons are judged at a glance in a busy
  field, so aim for 30.
- Lightness is the channel that survives small size, colour-blindness, desaturation and
  darkness [10, 11]. Colours that differ only by hue collapse first. Today red and blue are
  only 2 L\* apart.
- Deuteranopes keep lightness and the blue–yellow axis only [8]. Today green and red are 8
  apart for them, well under 20.

**Standing out from the backdrop.**
- An object reads by its lightness edge; colour alone makes a weak edge [11]. The closest
  standard for graphics is WCAG's 3:1 contrast ratio against the adjacent colour [13].
- A darker, less saturated surround makes the same colour look more saturated (simultaneous
  contrast). Low contrast reads as distance [12], so lowering the backdrop's *chroma* pushes it
  back; lifting its *black* only makes it look hazy.

**Night.**
- Darkness costs colourfulness (Hunt effect [16]) and contrast (Stevens effect [17]). A dark
  frame is its own surround, which lowers perceived contrast further [18].
- A night picture keeps true black and sharp silhouettes and loses low-contrast texture [34].
  Night renderings darken, shift toward blue and lose colour, not black [35, 36].
- So a night grade **adds** contrast and darkens and cools the **backdrop**; it never lifts the
  shadows, and it leaves the gameplay colours as close to their day look as it can.

**Glow.**
- A surface starts to look self-luminous at about 1.7× the luminance of the scene's white [19];
  saturated colours get there sooner [21].
- A halo (bloom) around a bright core adds 20–35 % to its apparent brightness, and a Gaussian
  halo is enough [20, 22, 23].
- So: keep surfaces below white, author emitters at 2–4×, and set the bloom threshold between
  the two. Only emitters glow; saturation is not glow.

**Tonemapping.**
- Curves applied per RGB channel change the ratio between channels: hues drift toward the
  primaries and highlights wash out [24–27]. Compress lightness, then reduce chroma at
  constant hue [27–29].
- URP's *Neutral* tonemapper is a per-channel Hable curve [25, 33]. On this palette it takes
  25–30 % of the chroma and turns blue 14° toward cyan at 1× (31° at 4×).
- Khronos PBR Neutral [30] leaves everything below 0.76 alone, compresses only the brightest
  channel and blends toward white at constant hue. On this palette it keeps hue within 1° and
  chroma within 7 % at 1×.

---

## Targets

Measured on the final frame (after the tonemapper and the grade), for each scenario at noon and
at midnight, by `balloons` (where each balloon is) and `palette_measure2.py` (the numbers).

| Metric | Target | Why |
|---|---|---|
| CIEDE2000 between every pair of gameplay colours | ≥ 20, aim ≥ 30 | nameably different at a glance [5] |
| Same, for a simulated deuteranope and protanope | ≥ 15 | about 1 in 12 men [8] |
| Lightness between gameplay colours | ≥ 10 L\* where the palette allows | lightness survives everything else [10, 11] |
| Each balloon against the backdrop around it | ≥ 3:1 contrast, or a rim that reaches it | figure–ground [13] |
| A balloon's colour from scene to scene | hue within 5°, chroma ≥ 85 % of noon | the player learns one colour, not six |
| The backdrop's darkest 1 % | not raised by the grade | a raised black reads as haze [12, 34] |
| The backdrop's median chroma | below about 60 % of the balloons' | simultaneous contrast works for the balloons |
| Pixels above surface white | only on emitters | glow is for light sources [19] |

---

## Phases

### Phase 1 — a hue-preserving tonemapper, and no lift · **done**

- **PBR Neutral in the grading LUT.** URP picks the shader that builds the HDR grading LUT from
  the renderer's PostProcessData asset. `Assets/Settings/URP/GamePostProcessData.asset` is a copy
  of URP's with one change: it builds the LUT with
  `Assets/Shaders/BalloonParty/Display/LutBuilderHdr.shader`, a copy of URP's that tonemaps with
  Khronos PBR Neutral when the volume asks for Neutral. The tonemapper is baked into the LUT, so
  the frame costs the same. The `Scenario Grade` cheat swaps back to URP's curve for comparison.
- **Re-tune the base look** in `Assets/DefaultVolumeProfile.asset`. The +0.35 stops and +15
  contrast compensated for URP Neutral darkening the midtones (0.5 became 0.42); PBR Neutral
  does not. The base is now +15 contrast and −10 saturation. PBR Neutral keeps the authored
  colours below 0.76 as authored, and the contrast adds some chroma on top, so the frame came out
  about 35 % more colourful than under URP Neutral; the user found that a bit much, and −10 takes
  the edge off without moving any pair under 30. +0.2 stops and +20 contrast measured close.
- **Remove the meadow's shadow lift and the global desaturation** from
  `ScenarioGradeSettings.asset`. A lift adds the same amount to every channel, which is what
  haze does: it cuts contrast and chroma together.

### Phase 2 — night darkens the backdrop, not the balloons · **balloon body done**

- **Done:** the balloon body (`Sprite/SpriteDiffuse.shader` on `DiffuseSprite.mat`) takes a
  quarter of the time-of-day light's colour (`_AmbientHue` 0.25) and 40 % of its dimming
  (`_AmbientDimming` 0.4). Local lights (a pop, an item) still tint it in full. Before, the
  balloons took all of the blue midnight light: red turned maroon, green turned teal, and blue
  and purple sank into the night sky. Four settings were compared at night in all three scenes;
  this one kept every pair furthest apart and every hue within 11° of its noon hue (OKLCh).
- **Done:** kept that bright at full chroma, the balloons looked more vivid than the night around
  them; colourfulness falls with light (the Hunt effect [16]). `_AmbientDesaturation` (0.5) fades
  the balloon's colour toward a grey of the same OKLab lightness as the time-of-day light darkens:
  nothing at noon, about a quarter of the chroma at midnight. A luma grey was tried first and
  turned red and purple muddy and green pale, because saturated colours look lighter than their
  luma (Helmholtz–Kohlrausch [15]).
- **Open:** the tough, unbreakable and rainbow balloons have their own shaders and still take the
  full night light; give them the same two controls if they read too dark at night.
- **Open:** the backdrops keep their own night look. Cut their chroma at night only if the
  balloons still don't separate.

### Phase 3 — palette · **red done**

- **Done, signed off by the user:** red went from (1, 0.412, 0.380) to (0.84, 0.289, 0.21): the
  same hue and chroma, OKLab lightness 0.71 → 0.60 (L\* 63.5 → 51). Lightness is what a
  deuteranope keeps, so green–red went from 8 to about 19 for them (authored values), and red
  moved from 6 to 10 away from the aim line (Prediction). Three candidates were rendered; at full
  chroma the darker red looked more saturated than the old one, so the chroma stayed where it was.
- **Open:** check green against the pale sky and purple against the night sky and water; give either a rim if
  it misses 3:1.

### Phase 4 — glow only on emitters

- Sparks, the shield halo, lightning and the laser are the emitters (see @ref plan_hdr). Bloom's
  threshold is 1.4, above every surface; check no balloon surface crosses it after Phase 1–3.

---

## Measurements

Before is URP Neutral with the first grade (+0.35 stops, the meadow's lift and desaturation);
after is Phases 1–3 as shipped (PBR Neutral, +15 contrast and −10 saturation, the balloons'
night response, the darker red). Each cell is before → **after**, measured in the Editor on full
frames (1080 × 1920) at noon (time-of-day angle 135) and midnight (315), on boards with every
colour of the level. Smallest ΔE is the closest pair of gameplay colours; the contrast column is
the range over the gameplay colours, each against the backdrop around it.

| Scene | Smallest ΔE2000 | Smallest ΔE2000, deuteranope | Contrast with backdrop | Mean balloon chroma (OKLCh) | Backdrop black (1st percentile Y) |
|---|---|---|---|---|---|
| sky noon | 34 → **37** | 9 → **16** | 1.1–2.5 → **1.1–2.4** | 0.157 → **0.183** | 0.2512 → **0.1579** |
| sky night | 19 → **31** | 9 → **17** | 1.1–1.6 → **1.1–3.9** | 0.105 → **0.122** | 0.0300 → **0.0089** |
| meadow noon | 37 → **48** | 8 → **18** | 2.1–3.3 → **3.9–8.8** | 0.114 → **0.166** | 0.0482 → **0.0076** |
| meadow night | 27 → **42** | 11 → **27** | 2.0–3.4 → **2.3–6.9** | 0.111 → **0.120** | 0.0048 → **0.0003** |
| water noon | 33 → **37** | 8 → **14** | 1.1–2.2 → **1.2–3.6** | 0.140 → **0.187** | 0.0905 → **0.0341** |
| water night | 17 → **31** | 10 → **18** | 1.1–1.9 → **1.5–6.1** | 0.114 → **0.133** | 0.0075 → **0.0018** |

What it shows:
- Every pair clears 30 in every scene, night included (it was 17–19 at night on the sky and the
  water). The blacks went down everywhere, so nothing reads as haze.
- For a deuteranope the closest pair went from 8–11 to 14–27; green–red is no longer the
  weakest, blue–purple is (13–20, unchanged).
- At night the balloons are about as colourful as before, but in their own hues; at noon they are
  more colourful than before, which the −10 saturation tempers.
- **Still short:** on the pale noon sky and the water the lightest balloons sit about 1.2:1 from
  the backdrop, and purple and red are 1.1–1.5:1 against the night sky and water. They are told
  apart by hue there, not lightness. A rim, or a lighter purple, would fix it (Phase 3, open).

---

## References

1. Luo, Cui & Rigg (2001), *The development of the CIE 2000 colour-difference formula: CIEDE2000*. Color Res. Appl. 26(5).
2. Sharma, Wu & Dalal (2005), *The CIEDE2000 colour-difference formula: implementation notes*. Color Res. Appl. 30(1).
3. Ottosson (2020), *A perceptual color space for image processing* (OKLab).
4. Li et al. (2017), *CAM16, CAT16 and CAM16-UCS*. Color Res. Appl. 42(6).
5. Szafir (2018), *Modeling color difference for visualization design*. IEEE TVCG 24(1).
6. Stone, Szafir & Setlur (2014), *An engineering model for color difference as a function of size*. IS&T CIC 22.
7. Gramazio, Laidlaw & Schloss (2017), *Colorgorical*. IEEE TVCG 23(1).
8. Machado, Oliveira & Fernandes (2009), *A physiologically-based model for simulation of color vision deficiency*. IEEE TVCG 15(6).
9. Nagy & Sanchez (1990), *Critical color differences determined with a visual search task*. JOSA A 7(7).
10. Mullen (1985), *Contrast sensitivity of human colour vision to red-green and blue-yellow chromatic gratings*. J. Physiol. 359.
11. Livingstone & Hubel (1987), *Psychophysical evidence for separate channels for the perception of form, color, movement, and depth*. J. Neurosci. 7(11).
12. O'Shea, Blackburn & Ono (1994), *Contrast as a depth cue*. Vision Res. 34(12).
13. W3C (2023), WCAG 2.2, Understanding SC 1.4.11 Non-text Contrast.
14. Somers, APCA (a WCAG 3 candidate; not peer-reviewed).
15. Fairchild & Pirrotta (1991), *Predicting the lightness of chromatic object colors using CIELAB*. Color Res. Appl. 16(6).
16. Hunt (1952), *Light and dark adaptation and the perception of color*. JOSA 42(3).
17. Stevens & Stevens (1963), *Brightness function: effects of adaptation*. JOSA 53(3).
18. Bartleson & Breneman (1967), *Brightness perception in complex fields*. JOSA 57.
19. Bonato & Gilchrist (1994), *The perception of luminosity on different backgrounds and in different illuminations*. Perception 23(9).
20. Zavagno & Caputo (2001), *The glare effect and the perception of luminosity*. Perception 30(2).
21. Morimoto, Numata, Fukuda & Uchikawa (2021), *Luminosity thresholds of colored surfaces are determined by their upper-limit luminances*. J. Vision 21(13).
22. Spencer, Shirley, Zimmerman & Greenberg (1995), *Physically-based glare effects for digital images*. SIGGRAPH.
23. Yoshida, Ihrke, Mantiuk & Seidel (2008), *Brightness of the glare illusion*. APGV.
24. Reinhard, Stark, Shirley & Ferwerda (2002), *Photographic tone reproduction for digital images*. ACM TOG 21(3).
25. Hable (2010), *Uncharted 2: HDR Lighting*. GDC.
26. Lottes (2016), *Advanced techniques and optimization of HDR/VDR color pipelines*. GDC.
27. Fry (2017), *HDR color grading and display in Frostbite*. GDC.
28. Mantiuk, Mantiuk, Tomaszewska & Heidrich (2009), *Color correction for tone mapping*. CGF 28(2).
29. Šikudová et al. (2016), *A gamut-mapping framework for color-accurate reproduction of HDR images*. IEEE CG&A 36(4).
30. Khronos (2024), *PBR Neutral tone mapper*. github.com/KhronosGroup/ToneMapping.
31. Academy (2021), *ACES Reference Gamut Compression*.
32. Blender Foundation (2023), *4.0 colour management* (AgX).
33. Unity, URP 17 `Color.hlsl` `NeutralTonemap` and `LutBuilderHdr.shader`.
34. Wanat & Mantiuk (2014), *Simulating and compensating changes in appearance between day and night vision*. ACM TOG 33(4).
35. Thompson, Shirley & Ferwerda (2002), *A spatial post-processing algorithm for images of night scenes*. J. Graphics Tools 7(1).
36. Kirk & O'Brien (2011), *Perceptually based tone mapping for low-light conditions*. ACM TOG 30(4).
