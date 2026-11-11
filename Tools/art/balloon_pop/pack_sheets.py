"""Packs the frames bake_pops.py rendered into the game's pop sheets, and draws a contact sheet and
an animated preview to review them by.

For each pop under OUT/frames/pop_NN/:
- OUT/sheets/pop_NN.png      the game's sheet: 24 frames of 256x256 in 6 columns by 4 rows, read left
                             to right, top to bottom. Greyscale with alpha, so the game tints it.
- OUT/review/pop_NN.png      the contact sheet: the frames large, numbered, tinted a balloon red, on paper
- OUT/review/pop_NN.gif      the pop playing at 30 fps, then holding on its last frame

Usage: python3 Tools/art/balloon_pop/pack_sheets.py [OUT]   (OUT defaults to out/ beside this script)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

COLUMNS = 6
ROWS = 4
FRAME = 256
FPS = 30
TINT = (226, 72, 84)
PAPER = (244, 238, 226)
HOLD_FRAMES = 15


def greyscale(frame):
    """Luminance into all three channels, alpha kept: the shading, ready to be tinted."""
    a = np.asarray(frame.convert("RGBA"), dtype=np.float32)
    lum = a[..., 0] * 0.2126 + a[..., 1] * 0.7152 + a[..., 2] * 0.0722
    out = np.dstack([lum, lum, lum, a[..., 3]])
    return Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), "RGBA")


def scaled(frame, size):
    # Premultiplied while resampling, so the transparent edge doesn't pick up a dark fringe.
    return frame.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")


def tinted_on_paper(frame):
    a = np.asarray(frame, dtype=np.float32) / 255.0
    rgb = a[..., :3] * (np.array(TINT, dtype=np.float32) / 255.0)
    alpha = a[..., 3:4]
    paper = np.array(PAPER, dtype=np.float32) / 255.0
    out = rgb * alpha + paper * (1.0 - alpha)
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def pack(pop_dir, out):
    frames = [greyscale(Image.open(p)) for p in sorted(pop_dir.glob("[0-9][0-9].png"))]
    if len(frames) != COLUMNS * ROWS:
        print(f"{pop_dir.name}: {len(frames)} frames, expected {COLUMNS * ROWS}; skipped")
        return

    sheet = Image.new("RGBA", (COLUMNS * FRAME, ROWS * FRAME), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        sheet.paste(scaled(frame, FRAME), ((i % COLUMNS) * FRAME, (i // COLUMNS) * FRAME))
    (out / "sheets").mkdir(parents=True, exist_ok=True)
    sheet.save(out / "sheets" / f"{pop_dir.name}.png")

    size = frames[0].width
    font = ImageFont.load_default(size=max(14, size // 20))
    contact = Image.new("RGB", (COLUMNS * size, ROWS * size), PAPER)
    draw = ImageDraw.Draw(contact)
    previews = []
    for i, frame in enumerate(frames):
        shown = tinted_on_paper(frame)
        previews.append(shown)
        x, y = (i % COLUMNS) * size, (i // COLUMNS) * size
        contact.paste(shown, (x, y))
        draw.rectangle((x, y, x + size - 1, y + size - 1), outline=(205, 196, 180))
        draw.text((x + 8, y + 6), f"{i:02d}", fill=(120, 110, 96), font=font)
    (out / "review").mkdir(parents=True, exist_ok=True)
    contact.save(out / "review" / f"{pop_dir.name}.png")

    gif = previews + [previews[-1]] * HOLD_FRAMES
    gif[0].save(out / "review" / f"{pop_dir.name}.gif", save_all=True, append_images=gif[1:],
                duration=round(1000 / FPS), loop=0)
    print(f"{pop_dir.name}: sheet, contact sheet and preview written")


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent / "out"
    for pop_dir in sorted((out / "frames").glob("pop_*")):
        pack(pop_dir, out)


main()
