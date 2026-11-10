# UI/HeadsUp

Short handwritten heads-ups that tell the player what just happened, written on as if by a
pencil. Built in phases from `Plans/PLAN-HeadsUp.md`. This folder draws them; which ones show, and when, is
`Game/HeadsUp/`.

## Contents

| File | What it does |
|---|---|
| `WriteOnText` | `MonoBehaviour` that writes any `TMP_Text` (canvas or world) on left to right. `Write(text, delaySeconds)` sets the text, lays it out and hides every glyph in the same call (so a reused label never flashes fully written), waits out the delay, then a soft reveal edge sweeps across the glyphs at `_secondsPerCharacter`. The edge spans `_edgeSoftness` characters and leans forward by `_slant` (a glyph's top corners are revealed before its bottom ones), which reads as a pencil stroke. An optional `_tip` transform rides the edge with a small wobble and hides once written. `Duration` is the last write's length, delay included; `Complete()` finishes it at once; `Stop()` lifts the pencil where it is, for a line going away mid-write. Ticks itself on unscaled time and only while writing: the component disables itself once the text is written, so a written line costs nothing. An owner with its own clock sets `ExternallyTicked` and calls `Tick(delta)` (the heads-up lines, which freeze under a pause) |
| `HeadsUpLayer` | Draws what `Game/HeadsUp/HeadsUpService` lets through (`IHeadsUpPresenter`). Every line is a pooled clone of `_linePrefab`, prewarmed to twice the cap plus two, since released and evicted lines fade beside their replacements. A line written where something happened is packed clear of the lines still showing and inside the view (`HeadsUpPacking`, with `_packingGap`); one at a HUD endpoint (`TrailEndpointRegistry`: the heart, a colour's bar, the cannon) follows it every frame. Formats each style's text once per number and keeps it. A line it can't place (an endpoint not registered yet, no camera) reports itself finished at once, so the queue doesn't lose the slot. Reports each line that fades out by itself through `Finished`; a released, evicted or cleared one fades without reporting |
| `HandwrittenHeadsUp` | One pooled line in world space, a `TextMeshPro` rather than a canvas label, so it stays where it happened through the camera rig's zoom and shake. The layer moves it on with two clocks: the write, the style's hold and its own fade (`_fadeOutDuration`) on live time, which stops under a pause; a release (`_clearDuration`) on unscaled time, so a line pushed out during a pause doesn't stay frozen. A held style (`ready`) stays written until released. `Rewrite` keeps the line where it is and takes the latest text at the next tick, so a pierce that grows a streak several times in a frame writes it once; the same text again does nothing. Fades through its material's face and outline alpha (its own instance, destroyed with it): setting the text's colour would rebuild the mesh and undo the write-on mid-stroke. Gives its label a cream halo (`_haloColor`, `_haloWidth`, the TMP outline), so dark ink reads over the day and night sky alike. A style that underlines (`UnderlinesAt`: the streak from `x6!`) gets a pencil underline once the line is written: its `Underline` child, a `LineRenderer` in the line's own space on the `PencilUnderline` material, drawn left to right over `_underlineSeconds`, a little under the ink (`_underlineGap`), past its ends (`_underlineOverhang`) and climbing slightly (`_underlineTilt`), in the line's ink colour and fading with it. A rewrite takes it off and draws it again under the new ink |
| `HeadsUpStyle` | In `Configuration/HeadsUp/`, beside the settings that hold one per kind. Serializable look of one kind of heads-up: colour, size, growth and max size, `FontStyles`. `SizeAt(count)` grows logarithmically and caps (`HeadsUpSizing`, EditMode-tested) |
| `HeadsUpPacking` | Where a new line goes so it doesn't sit on the lines still showing: from where it happened, it tries spots in rings of one line height up and down and half a line width sideways (four rings out), and takes the nearest spot that overlaps no showing line by more than `AllowedOverlap` (15% of its area). With no such spot in reach it takes the one that overlaps least. Every spot is clamped inside the view (`ViewRect`, what the orthographic game camera shows, in world units). Lines are boxed by their written ink (`HandwrittenHeadsUp.InkRect`) plus `_packingGap` |
| `WriteOnMath` | The reveal in characters, pure and EditMode-tested: where each glyph corner sits (`CornerPosition`), how revealed it is for a given head (`RevealAlpha`), and where the head starts after a delay (`StartHead`) and ends (`EndHead`) |

## Visual language

Each kind of heads-up has a look of its own, so the player reads what happened from the colour and size before the words. The defaults come from a research pass (perception, game-accessibility guidelines, game feedback studies); where it's convention rather than evidence, it says so.

| Kind | Colour | Size | Weight | Grows |
|---|---|---|---|---|
| `ready`, beside the cannon | Graphite `#4A4A52` | 0.85× | Regular | No (the quietest: nearly every shot) |
| `-1 bounce` at a wall | Graphite at 80% | 0.9× | Regular | No |
| `-1 bounce` leaving the last shield | Dark vermilion `#B34700` | 0.9× | Bold | No |
| `+n bounce` | Blue `#0072B2` | 1× | Bold | ×(1 + 0.15·log2 n), up to 1.35× |
| `xn ricochet` | Plum `#8E3B8E` | 1.1× | Bold | ×(1 + 0.25·log2 n), up to 1.6× |
| `+1 bounce` under a ricochet that paid | Blue, as a pickup | 0.65× of the ricochet line | Bold | With the ricochet line |

- **Words in italics, numbers upright**, through rich text in the format strings (`+{0} <i>bounce</i>`).
- **Luminance reads, hue means.** Legibility comes from dark ink on the cream halo, not from colour, which only says what kind of event it was. The sign, the word and the place say it too, so nothing depends on colour alone.
- **Colour-blind safe pairs.** Gains are blue and losses orange-red, the pair that survives red-green colour blindness (Okabe-Ito / Wong 2011); the inks are duller than the flat balloon colours, so a heads-up is never mistaken for a balloon.
- **Emphasis follows consequence, not frequency.** A wall spends a shield on nearly every flight, so it stays quiet (frequent, non-actionable alerts teach players to ignore them); only the wall that leaves the ball on its last shield, after which the next wall ends the flight, is loud.
- **Escalation is logarithmic and capped**: doubling a count adds one visible step, and a long chain tops out rather than filling the screen. The coefficients are a starting point to tune by eye.
- **Convention, not research**: gold or warm colours for achievement, red for danger and size for importance are genre convention, and red-means-loss is cultural (red reads as "up" in mainland China).

Planned kinds keep to the same scheme: a streak `x3!` in ochre `#9A6700`, a lost heart `-1` in vermilion at 1.5× and never suppressed, `tap!` / `pierce!` in italic graphite.

## How the reveal works

TMP lays the text out once. Each frame while writing, `WriteOnText` works out how far the write head has
passed every visible glyph corner and hands that to the mesh in one of two ways, chosen per mesh by its
material:

- On the pencil shader (its shader declares `_PencilRevealInUv`), it writes how hidden the corner is into the
  vertex's `uv0.z` and pushes only those (`UpdateVertexData(Uv0)`); the shader turns it into the ragged
  edge. TMP never sets `uv0.z`, so text no write touches reads 0, whole, and vertex alpha stays the text's
  own opacity: a banner fading through its `CanvasGroup`, or a translucent colour, stays smooth and whole.
- On any other material (a fallback font on TMP's stock shader), it scales the corner's vertex alpha,
  starting from the colours TMP generated so rich-text colours and gradients keep their own alpha, and
  pushes only colours (`UpdateVertexData(Colors32)`).

It never uses `maxVisibleCharacters`, which would re-lay out the text on every step. While writing it
listens for TMP's `TEXT_CHANGED_EVENT`: a mesh TMP rebuilds mid-write (a canvas rebuild, a re-enable) comes
back fully shown, so the colours and materials are re-captured and the reveal re-applied at once. A glyph the cache doesn't cover is skipped rather than read out of range. After the
write nothing listens, and a rebuild shows the text whole, which is the written state anyway.

## Who uses it

| User | How |
|---|---|
| `HeadsUpLayer` (here) | Every heads-up line is a pooled clone of its `Line`, a `HandwrittenHeadsUp` that ticks its `WriteOnText` itself (`ExternallyTicked`), on live time |
| `UI/Tooltip/TooltipBanner` | Writes each tooltip line on through its optional `_writeOn`, starting once its fade-in is done (a short line would otherwise be written while still nearly invisible), and lengthens the line's hold by the write |

## Wiring requirements

- `_text` must be assigned. On the `Tooltip` prefab it's the `Text (TMP)` label, on the same object.
- The heads-ups live in world space on the `HeadsUps` prefab (`Assets/Prefabs/Game/`), whose instance sits
  in the Game scene, not on `GameCanvas`: the canvas is bound to the camera. Each line is a `TextMeshPro` on
  the `UI` sorting layer and GameObject layer (above the balloons, out of the GI capture), in the tooltip's
  font and material with its `WriteOnText` settings. Sizes carry over from the canvas build: a canvas unit
  is 0.024 world units at the reference 9.6-unit view, and a `TextMeshPro` draws a tenth of its font size
  per world unit, so canvas size 8 is 1.92. Each line has a `PencilTip` child, its `WriteOnText`'s `_tip`: the
  pencil sprite (`Assets/Sprites/UI/pencil-tip.png`, pivot on the graphite point, 0.33 world units across),
  sorted just above the text and hidden until a write. The banner on the canvas has no tip.
- The layer is built by **Tools > BalloonParty > HeadsUp > Build Heads-Up Layer**: a `HeadsUpLayer` with
  one inactive `Line` it clones. The game scope requires it (`RegisterComponentInHierarchy`). The
  builder makes the `HeadsUps` prefab if it's missing, places it in the Game scene when that scene is open
  (save the scene afterwards), and removes the single-purpose `ReloadHeadsUp` and `BounceHeadsUp` an
  earlier build left, on the prefab or on `GameCanvas`. What each heads-up says and how it looks is in
  `HeadsUpSettings` (`Configuration/HeadsUp/`), not on the prefab.
- The pencil look needs the Patrick Hand TMP font asset on the label. **Tools > BalloonParty >
  HeadsUp > Build Pencil Font** makes it from `Assets/Fonts/PatrickHand-Regular.ttf` and sets it on the
  tooltip's label.
- The grain through the letters and the ragged edge are the font's own material, on the
  `BalloonParty/Text/Pencil` shader (a copy of TMP's SDF shader), so every Patrick Hand text gets them, the
  banner and the heads-ups alike. The shader takes `WriteOnText`'s reveal from `uv0.z` (see How the reveal
  works) and jitters where it breaks through with an edge noise, so the edge goes down ragged; a written
  glyph is whole. Paper specks thin the graphite of the face, not the halo. Both noises are packed in
  `Assets/Textures/Text/pencil-noise.png` (edge in RGB, specks in alpha; mip-mapped, so a noise finer than a
  pixel fades to its average rather than shimmering as the camera zooms) and sampled in atlas texels: Grain
  Scale and Edge Noise Scale are how many atlas texels one noise texel covers, so a speck is the same size
  against the letters at any text scale. Tune them in the font material's Pencil panel outside Play Mode:
  each heads-up line copies the material when it starts, so a change made while playing doesn't reach them.
