# UI/HeadsUp

Short handwritten heads-ups that tell the player what just happened, written on as if by a
pencil. Built in phases from `Plans/PLAN-HeadsUp.md`; this folder holds what exists so far.

## Contents

| File | What it does |
|---|---|
| `WriteOnText` | `MonoBehaviour` that writes any `TMP_Text` (canvas or world) on left to right. `Write(text, delaySeconds)` sets the text, lays it out and hides every glyph in the same call (so a reused label never flashes fully written), waits out the delay, then a soft reveal edge sweeps across the glyphs at `_secondsPerCharacter`. The edge spans `_edgeSoftness` characters and leans forward by `_slant` (a glyph's top corners are revealed before its bottom ones), which reads as a pencil stroke. An optional `_tip` transform rides the edge with a small wobble and hides once written. `Duration` is the last write's length, delay included; `Complete()` finishes it at once. Ticks on unscaled time and only while writing: the component disables itself once the text is written, so a written line costs nothing |
| `HandwrittenHeadsUp` | One short line on the canvas: `Show(text, localPosition)` places it, writes it on through its `WriteOnText`, holds it `_holdDuration`, then fades it out; `Clear()` fades it out early. A new line replaces the one showing. Fades on unscaled time, like the write |
| `ReloadHeadsUp` | Writes a small `_text` ("+1") by the cannon on each `ThrowerReadyMessage`, `_worldOffset` from the muzzle (`ThrowerOriginProvider.Origin`), and clears it on `ProjectileFiredMessage`. It projects the world point through the game camera onto the canvas (`RectAnchorMath.GameWorldToLocalPoint`) instead of parenting to the cannon, which turns with the aim, so the mark stays upright |
| `BounceHeadsUp` | Writes how a ball's bounces change, where it happens: `_pickupFormat` ("+{0} bounce") at the shield's balloon on each `ShieldGainedMessage`, the number counting that ball's pickups this flight, and `_wallText` ("-1 bounce") at the contact on each `WallHitMessage`. Writes through its `_lines` in turn, so bounces close together don't replace each other, and clamps each line inside the canvas (`RectAnchorMath.ClampInside`), since wall contacts sit at the screen's edge. Skips a shield granted with no ball to take it |
| `FlightPickupCount` | The per-ball pickup count behind "+n": `Record` counts one more and returns the total, `Forget` drops a ball once its flight ends (`ProjectileDestroyedMessage`). EditMode-tested |
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
| `ReloadHeadsUp` (here) | Through `HandwrittenHeadsUp`, on a copy of the tooltip's label on `GameCanvas` |
| `BounceHeadsUp` (here) | Through several `HandwrittenHeadsUp` lines, copies of the same label, under a holder that fills `GameCanvas` |
| `UI/Tooltip/TooltipBanner` | Writes each tooltip line on through its optional `_writeOn`, starting once its fade-in is done (a short line would otherwise be written while still nearly invisible), and lengthens the line's hold by the write |

## Wiring requirements

- `_text` must be assigned. On the `Tooltip` prefab it's the `Text (TMP)` label, on the same object.
- The reload heads-up is a `GameCanvas` child built by **Tools > BalloonParty > HeadsUp > Build Reload
  Heads-Up**: a copy of the tooltip's label (font and `WriteOnText` included), made small, with a
  `CanvasGroup`, a `HandwrittenHeadsUp` and a `ReloadHeadsUp`. It is optional: the game scope injects a
  `ReloadHeadsUp` it finds rather than requiring one, so the game runs without it.
- The bounce heads-up is built the same way by **Tools > BalloonParty > HeadsUp > Build Bounce
  Heads-Up**: a `BounceHeadsUp` holder stretched over `GameCanvas`, with six copies of the label as its
  lines. Also optional, and injected the same way.
- The pencil look needs the Patrick Hand TMP font asset on the label. **Tools > BalloonParty >
  HeadsUp > Build Pencil Font** makes it from `Assets/Fonts/PatrickHand-Regular.ttf` and sets it on the
  tooltip's label. The grain through the letters
  and the ragged edge are the pencil text shader, still to come (`PLAN-HeadsUp.md` §4.1).
