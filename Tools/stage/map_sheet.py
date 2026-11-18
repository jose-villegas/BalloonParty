#!/usr/bin/env python3
"""Lays the render maps `stage.py maps` wrote out beside the frame they belong to.

    python3 Tools/stage/map_sheet.py Logs/stage/maps-night --title "Night, ring light on"

Reads DIR/frame.png and every DIR/<map>-rgb.png with its <map>-a.png, and writes DIR/sheet.png: the frame on
the left, then a column per map with its colour channels above and its alpha below, each scaled up without
smoothing so a map's real texels show. A map with nothing in it is left out and named under the heading.
"""

import argparse
import glob
import os

from PIL import Image, ImageDraw, ImageFont

BACKGROUND = (22, 22, 26)
INK = (240, 240, 240)
NOTE = (170, 180, 200)
GAP = 8
HEADER = 64
LABEL = 44
FRAME_WIDTH = 540
PANEL_WIDTH = 250


def font(size, bold=False):
    for name in (("segoeuib.ttf", "DejaVuSans-Bold.ttf") if bold else ("segoeui.ttf", "DejaVuSans.ttf")):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def is_blank(image):
    return image.convert("L").getextrema() == (0, 0)


def compose(directory, found=None, title=""):
    """Writes DIR/sheet.png. `found` is what `Stage.maps` returns ({map: (width, height, max colour, max alpha)} or
    None for a map that doesn't exist); without it the maps are the PNGs in the folder."""
    names = list(found) if found else sorted(
        os.path.basename(path)[:-len("-rgb.png")] for path in glob.glob(os.path.join(directory, "*-rgb.png")))
    shown, left_out = [], []
    for name in names:
        path = os.path.join(directory, name + "-rgb.png")
        if (found and found.get(name) is None) or not os.path.exists(path):
            left_out.append(name + " (not made yet)")
            continue
        colour = Image.open(path).convert("RGB")
        alpha = Image.open(os.path.join(directory, name + "-a.png")).convert("RGB")
        if is_blank(colour) and is_blank(alpha):
            left_out.append(name + " (empty)")
            continue
        shown.append((name, colour, alpha))

    frame = Image.open(os.path.join(directory, "frame.png")).convert("RGB")
    frame_height = round(FRAME_WIDTH * frame.height / frame.width)
    panel_height = (frame_height - 2 * LABEL - GAP) // 2
    width = FRAME_WIDTH + GAP + len(shown) * (PANEL_WIDTH + GAP)
    sheet = Image.new("RGB", (width, HEADER + frame_height + GAP), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 6), title or os.path.basename(os.path.normpath(directory)), fill=INK, font=font(24, True))
    if left_out:
        draw.text((10, 38), "Not shown: " + ", ".join(left_out), fill=NOTE, font=font(16))
    sheet.paste(frame.resize((FRAME_WIDTH, frame_height), Image.LANCZOS), (0, HEADER))

    small = font(16)
    for column, (name, colour, alpha) in enumerate(shown):
        x = FRAME_WIDTH + GAP + column * (PANEL_WIDTH + GAP)
        scale = found.get(name) if found else None
        for row, (image, channels, top) in enumerate(
                ((colour, "RGB", scale[2] if scale else 1.0), (alpha, "A", scale[3] if scale else 1.0))):
            y = HEADER + row * (panel_height + LABEL + GAP)
            draw.text((x, y), name, fill=INK, font=small)
            note = f"{channels}  {image.width}x{image.height}"
            if top > 1.0:
                note += f"  shown at 1/{top:.2f}"
            draw.text((x, y + 20), note, fill=NOTE, font=small)
            # Fitted inside the panel, keeping the map's own shape: the maps aren't all the screen's.
            fit = min(PANEL_WIDTH / image.width, panel_height / image.height)
            size = (max(1, round(image.width * fit)), max(1, round(image.height * fit)))
            sheet.paste(image.resize(size, Image.NEAREST), (x, y + LABEL))

    out = os.path.join(directory, "sheet.png")
    sheet.save(out)
    print(f"wrote {out} ({sheet.width}x{sheet.height}, {len(shown)} maps)")
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("directory")
    parser.add_argument("--title", default="")
    args = parser.parse_args()
    compose(args.directory, None, args.title)


if __name__ == "__main__":
    main()
