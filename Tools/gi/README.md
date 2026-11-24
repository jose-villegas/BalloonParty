# GI

Stages a board and captures the GI light buffer under each of its settings, for `Docs/gi-light-buffer.md`:
the frame, every render map and the light quad drawn alone, with each setting at the game's value, a lower one
and a higher one. Then it lays them out as the page's pictures, and plots the march's cone from the game's
settings.

```bash
python3 Tools/gi/gi_doc.py              # stage, capture, compose into Logs/gi/doc/
python3 Tools/gi/gi_doc.py --compose    # compose the last capture again; no Editor needed
python3 Tools/gi/gi_doc.py --publish    # stage, capture, compose, copy the pictures to Docs/gi/
```

The Unity Editor must be open and out of Play mode; `--project` picks the project it has open (default: the
main checkout). The tool drives `Tools/stage`. A capture takes a few minutes. It needs the Unity CLI
(`com.unity.pipeline`), Pillow, NumPy and Matplotlib.

| File | What it is |
|---|---|
| `gi_doc.py` | Builds the board, changes each setting, captures, and puts the settings back |
| `compose.py` | Lays the capture out as the page's pictures, and draws the two plots |

## The board

The meadow at night (level 7, seed 7, its obstacles taken off): coloured balloons fill the top four rows, so
what comes after rests in the middle of the board, away from the danger gradient at the top. Below them hang three
columns of different lengths, with open grass beside each. The night's light comes from below, so a balloon's shadow
falls up beside its column, on the grass, and on the meadow the cloud gate takes none of it away. The board is
simple balloons only: toughs walk along their row, and a golden balloon gives off light of its own. A balloon
placed in a slot with an empty one above it floats up into it, so the tool says when a placed balloon is not where
it was put; look at the crop before publishing then.

What the board is, the square the pictures crop (`CROP`), the texel the tap diagram follows (`TAP_TEXEL`) and the
values each setting is taken at (`VARIANTS`) are constants at the top of `gi_doc.py`. Only the frame's own crop
is cut at capture time; the maps are cut, and the tap diagram drawn, when composing, so a change to `TAP_TEXEL`
needs only `--compose`.

## What a capture holds

`Logs/gi/` (git-ignored):

| Where | What |
|---|---|
| `capture.json` | The view (the camera's size, the light's direction), the crop, the game's GI settings and the values each was taken at |
| `as-is/` | The game's settings (`game/`), the frame without the light quad (`unlit/`), and each setting at its lower and higher value (`<setting>-<value>/`) |
| `shadow-alone/` | For the settings that move the shadow: the same with the bounce strength at 0, so the light quad lays on the shadow alone |
| `doc/` | The composed pictures, which `--publish` copies to `Docs/gi/` |

Each folder has `crop.png` (the frame's square), every render map as `Tools/stage`'s `maps` writes them, and
`light.png`: the light quad drawn alone over mid grey, so grey is a pixel the light leaves as it is.

## How it changes the settings

The settings are the `SceneLightFieldSettings` asset the running game reads. The tool writes each value into
it in Play mode, has the buffer worked out again, steps three frames and captures. An asset changed in Play
mode keeps the change after it, so before it leaves Play mode the tool writes every value back as it found it,
reads them all again, and stops with an error if any reads back different.
