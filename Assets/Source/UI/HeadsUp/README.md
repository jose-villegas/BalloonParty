# UI/HeadsUp

Short handwritten heads-ups that tell the player what just happened, written on as if by a
pencil. Built in phases from `Plans/PLAN-HeadsUp.md`; this folder holds what exists so far.

## Contents

| File | What it does |
|---|---|
| `WriteOnText` | `MonoBehaviour` that writes any `TMP_Text` (canvas or world) on left to right. `Write(text, delaySeconds)` sets the text, lays it out and hides every glyph in the same call (so a reused label never flashes fully written), waits out the delay, then a soft reveal edge sweeps across the glyphs at `_secondsPerCharacter`. The edge spans `_edgeSoftness` characters and leans forward by `_slant` (a glyph's top corners are revealed before its bottom ones), which reads as a pencil stroke. An optional `_tip` transform rides the edge with a small wobble and hides once written. `Duration` is the last write's length, delay included; `Complete()` finishes it at once. Ticks on unscaled time and only while writing: the component disables itself once the text is written, so a written line costs nothing |
| `HandwrittenHeadsUp` | One short line on the canvas: `Show(text, localPosition)` places it, writes it on through its `WriteOnText`, holds it `_holdDuration`, then fades it out; `Clear()` fades it out early. A new line replaces the one showing. Fades on unscaled time, like the write. `Show(text, localPosition, style, count)` writes it in a `HeadsUpStyle` (colour, size against the label's own, weight), sized for an escalating count; the plain `Show` puts the label's own look back. Gives its label a cream halo (`_haloColor`, `_haloWidth`, the TMP outline), so dark ink reads over the day and night sky alike |
| `HeadsUpStyle` | Serializable look of one kind of heads-up: colour, size, growth and max size, `FontStyles`. `SizeAt(count)` grows logarithmically and caps (`HeadsUpSizing`, EditMode-tested) |
| `ReloadHeadsUp` | Writes a small `_text` ("+1") by the cannon on each `ThrowerReadyMessage`, `_worldOffset` from the muzzle (`ThrowerOriginProvider.Origin`), and clears it on `ProjectileFiredMessage`. It projects the world point through the game camera onto the canvas (`RectAnchorMath.GameWorldToLocalPoint`) instead of parenting to the cannon, which turns with the aim, so the mark stays upright |
| `BounceHeadsUp` | Writes how a ball's bounces go, where it happens: `_pickupFormat` ("+{0} *bounce*") at the shield's balloon on each `ShieldGainedMessage`, the number counting that ball's pickups this flight (`Projectile/ProjectileTally`); `_wallText` ("-1 *bounce*") at the contact on each `WallHitMessage`, in `_lastShieldStyle` when the ball is down to its last shield; and `_ricochetFormat` ("x{0} *ricochet*") on each `DeflectChainMessage`. Each kind has its `HeadsUpStyle`. Writes through its `_lines` in turn, so bounces close together don't replace each other, and clamps each line inside the canvas (`RectAnchorMath.ClampInside`), since wall contacts sit at the screen's edge. Skips a shield granted with no ball to take it |
| `WriteOnMath` | The reveal in characters, pure and EditMode-tested: where each glyph corner sits (`CornerPosition`), how revealed it is for a given head (`RevealAlpha`), and where the head starts after a delay (`StartHead`) and ends (`EndHead`) |

## Visual language

Each kind of heads-up has a look of its own, so the player reads what happened from the colour and size before the words. The defaults come from a research pass (perception, game-accessibility guidelines, game feedback studies); where it's convention rather than evidence, it says so.

| Kind | Colour | Size | Weight | Grows |
|---|---|---|---|---|
| `+1` ready, by the cannon | Graphite `#4A4A52` | 0.85× | Regular | No (the quietest: nearly every shot) |
| `-1 bounce` at a wall | Graphite at 80% | 0.9× | Regular | No |
| `-1 bounce` leaving the last shield | Dark vermilion `#B34700` | 0.9× | Bold | No |
| `+n bounce` | Blue `#0072B2` | 1× | Bold | ×(1 + 0.15·log2 n), up to 1.35× |
| `xn ricochet` | Plum `#8E3B8E` | 1.1× | Bold | ×(1 + 0.25·log2 n), up to 1.6× |

- **Words in italics, numbers upright**, through rich text in the format strings (`+{0} <i>bounce</i>`).
- **Luminance reads, hue means.** Legibility comes from dark ink on the cream halo, not from colour, which only says what kind of event it was. The sign, the word and the place say it too, so nothing depends on colour alone.
- **Colour-blind safe pairs.** Gains are blue and losses orange-red, the pair that survives red-green colour blindness (Okabe-Ito / Wong 2011); the inks are duller than the flat balloon colours, so a heads-up is never mistaken for a balloon.
- **Emphasis follows consequence, not frequency.** A wall spends a shield on nearly every flight, so it stays quiet (frequent, non-actionable alerts teach players to ignore them); only the wall that leaves the ball on its last shield, after which the next wall ends the flight, is loud.
- **Escalation is logarithmic and capped**: doubling a count adds one visible step, and a long chain tops out rather than filling the screen. The coefficients are a starting point to tune by eye.
- **Convention, not research**: gold or warm colours for achievement, red for danger and size for importance are genre convention, and red-means-loss is cultural (red reads as "up" in mainland China).

Planned kinds keep to the same scheme: a streak `x3!` in ochre `#9A6700`, a lost heart `-1` in vermilion at 1.5× and never suppressed, `tap!` / `pierce!` in italic graphite.

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
  lines. Also optional, and injected the same way. Its texts and styles aren't stored by the builder, so the
  code's defaults apply until someone edits them on the prefab.
- The pencil look needs the Patrick Hand TMP font asset on the label. **Tools > BalloonParty >
  HeadsUp > Build Pencil Font** makes it from `Assets/Fonts/PatrickHand-Regular.ttf` and sets it on the
  tooltip's label. The grain through the letters
  and the ragged edge are the pencil text shader, still to come (`PLAN-HeadsUp.md` §4.1).
