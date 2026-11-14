#!/usr/bin/env python3
"""Lays captures out in a labelled grid: one row per take, one column per moment.

    python3 Tools/stage/contact_sheet.py out.png --cols dawn day dusk night \
        --row "hand-made" 'Logs/stage/old-{}.png' --row baked 'Logs/stage/new-{}.png'

Each row's pattern has a {} the column's name goes into. A missing image leaves its cell empty.
"""

import argparse
import os

from PIL import Image, ImageDraw

BACKGROUND = (24, 24, 24)
INK = (235, 235, 235)
GAP = 8
HEADER = 28
LABEL_WIDTH = 120


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("out")
    parser.add_argument("--cols", nargs="+", required=True, help="the columns' names, filled into each row's {}")
    parser.add_argument("--row", nargs=2, action="append", required=True, metavar=("LABEL", "PATTERN"))
    parser.add_argument("--cell", type=int, default=360, help="each cell's side in pixels (default 360)")
    args = parser.parse_args()

    cell = args.cell
    sheet = Image.new("RGB", (LABEL_WIDTH + len(args.cols) * (cell + GAP), HEADER + len(args.row) * (cell + GAP)),
                      BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    for j, column in enumerate(args.cols):
        draw.text((LABEL_WIDTH + j * (cell + GAP) + 6, 8), column, fill=INK)
    for i, (label, pattern) in enumerate(args.row):
        top = HEADER + i * (cell + GAP)
        draw.multiline_text((6, top + cell // 2 - 10), label.replace("\\n", "\n"), fill=INK)
        for j, column in enumerate(args.cols):
            path = pattern.replace("{}", column)
            if os.path.exists(path):
                image = Image.open(path).convert("RGB")
                image.thumbnail((cell, cell), Image.LANCZOS)
                sheet.paste(image, (LABEL_WIDTH + j * (cell + GAP), top))
    sheet.save(args.out)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
