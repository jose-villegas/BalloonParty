# UI/Notices

Short handwritten heads-up notices that tell the player what just happened, written on as if by a
pencil. Built in phases from `Plans/PLAN-Notices.md`; this folder holds what exists so far.

## Contents

| File | What it does |
|---|---|
| `WriteOnText` | `MonoBehaviour` that writes any `TMP_Text` (canvas or world) on left to right. `Write(text, delaySeconds)` sets the text, lays it out and hides every glyph in the same call (so a reused label never flashes fully written), waits out the delay, then a soft reveal edge sweeps across the glyphs at `_secondsPerCharacter`. The edge spans `_edgeSoftness` characters and leans forward by `_slant` (a glyph's top corners are revealed before its bottom ones), which reads as a pencil stroke. An optional `_tip` transform rides the edge with a small wobble and hides once written. `Duration` is the last write's length, delay included; `Complete()` finishes it at once. Ticks on unscaled time and only while writing: the component disables itself once the text is written, so a written line costs nothing |
| `HandwrittenNotice` | One short line on the canvas: `Show(text, localPosition)` places it, writes it on through its `WriteOnText`, holds it `_holdDuration`, then fades it out; `Clear()` fades it out early. A new line replaces the one showing. Fades on unscaled time, like the write |
| `ReloadNotice` | Writes `_text` ("ready") by the cannon on each `ThrowerReadyMessage`, `_worldOffset` from the muzzle (`ThrowerOriginProvider.Origin`), and clears it on `ProjectileFiredMessage`. It projects the world point through the game camera onto the canvas (`RectAnchorMath.GameWorldToLocalPoint`) instead of parenting to the cannon, which turns with the aim, so the word stays upright |
| `WriteOnMath` | The reveal in characters, pure and EditMode-tested: where each glyph corner sits (`CornerPosition`), how revealed it is for a given head (`RevealAlpha`), and where the head starts after a delay (`StartHead`) and ends (`EndHead`) |

## How the reveal works

TMP lays the text out once. Each frame while writing, `WriteOnText` scales every visible glyph
corner's vertex alpha by how far the write head has passed it, starting from the colours TMP
generated (so rich-text colours and gradients keep their own alpha), and pushes colours only
(`UpdateVertexData(Colors32)`). It never uses `maxVisibleCharacters`, which would re-lay out the text
on every step. While writing it listens for TMP's `TEXT_CHANGED_EVENT`: a mesh TMP rebuilds mid-write
(a canvas rebuild, a re-enable) comes back fully opaque, so the colours are re-captured and the reveal
re-applied at once. A glyph the cache doesn't cover is skipped rather than read out of range. After the
write nothing listens, and a rebuild shows the text whole, which is the written state anyway.

## Who uses it

| User | How |
|---|---|
| `ReloadNotice` (here) | Through `HandwrittenNotice`, on a copy of the tooltip's label on `GameCanvas` |
| `UI/Tooltip/TooltipBanner` | Writes each tooltip line on through its optional `_writeOn`, starting once its fade-in is done (a short line would otherwise be written while still nearly invisible), and lengthens the line's hold by the write |

## Wiring requirements

- `_text` must be assigned. On the `Tooltip` prefab it's the `Text (TMP)` label, on the same object.
- The reload notice is a `GameCanvas` child built by **Tools > BalloonParty > Notices > Build Reload
  Notice**: a copy of the tooltip's label (font and `WriteOnText` included), made small, with a
  `CanvasGroup`, a `HandwrittenNotice` and a `ReloadNotice`. It is optional: the game scope injects a
  `ReloadNotice` it finds rather than requiring one, so the game runs without it.
- The pencil look needs the Patrick Hand TMP font asset on the label. **Tools > BalloonParty >
  Notices > Build Pencil Font** makes it from `Assets/Fonts/PatrickHand-Regular.ttf` and sets it on the
  tooltip's label. The grain through the letters
  and the ragged edge are the pencil text shader, still to come (`PLAN-Notices.md` §4.1).
