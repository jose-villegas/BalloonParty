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

The sky at dusk (level 7, seed 7, the enemies taken off): coloured balloons fill the top rows, so what comes
after rests in the middle of the board, away from the danger gradient at the top. Below them five unbreakables
are placed round a golden balloon, among toughs, toughers, coloured balloons and a disco ball; the unbreakables
form a ring on the run's first reshape, and the toughs a snake of their own. The level's own puff clouds stay,
and the board is placed round them. For the staging the formation rolls rings only: the tool sets every other shape's weight to 0
and puts the weights back afterwards. A ring stands where its roll puts it, so the tool says when the golden
balloon is not its centre; look at the crop before publishing then.

What the board is, the square the pictures crop (`CROP`), the texel the tap diagram follows (`TAP_TEXEL`) and
the values each setting is taken at (`VARIANTS`) are constants at the top of `gi_doc.py`.

## What a capture holds

`Logs/gi/` (git-ignored):

| Where | What |
|---|---|
| `capture.json` | The view (the camera's size, the light's direction), the crop, the game's GI settings and the values each was taken at |
| `as-is/` | The game's settings (`game/`), the frame without the light quad (`unlit/`), the phone's lighter buffer (`phone/`), and each setting at its lower and higher value (`<setting>-<value>/`) |
| `gate-off/` | The same with the cloud gate at 0, so the whole shadow shows, for the settings that move the shadow |
| `doc/` | The composed pictures, which `--publish` copies to `Docs/gi/` |

Each folder has `crop.png` (the frame's square), every render map as `Tools/stage`'s `maps` writes them, and
`light.png`: the light quad drawn alone over mid grey, so grey is a pixel the light leaves as it is.

## How it changes the settings

The settings are the `SceneLightFieldSettings` asset the running game reads. The tool writes each value into
it in Play mode, has the buffer worked out again, steps three frames and captures. An asset changed in Play
mode keeps the change after it, so before it leaves Play mode the tool writes every value back as it found it,
reads them all again, and stops with an error if any reads back different. The phone's buffer is the
`_LOW_QUALITY_SMEAR` keyword switched on the buffer's material for one capture.
