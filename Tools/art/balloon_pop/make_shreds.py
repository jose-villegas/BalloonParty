"""Draws the card reveal's flying shreds: torn scraps of balloon rubber, four to a sheet, for the burst
CardRevealView fires as the balloon tears (Assets/Prefabs/Game/CardRevealShreds, lit by
BalloonParty/Sprite/Diffuse and tinted the balloon's colour).

Each scrap is a curled, torn strip: a ragged outline, a lit and a shadowed tone split along its curl as
the cel-shaded balloon is, and a thin highlight along the lit edge. Greys, so the particles' colour tints
them. Seeded, so the sheet is the same every run.

Usage: python3 Tools/art/balloon_pop/make_shreds.py [OUT]
       (OUT defaults to Assets/Sprites/UI/CardReveal/shreds.png)
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

CELL = 128
GRID = 2
SUPERSAMPLE = 4
LIT = 236
SHADOW = 168
HIGHLIGHT = 255
SEED = 7


def ragged_strip(rng, size):
    """A torn strip's outline: a curved band whose two long edges are ragged, its ends torn across."""
    length = rng.uniform(0.62, 0.86) * size
    width = rng.uniform(0.2, 0.34) * size
    bend = rng.uniform(-0.35, 0.35)
    steps = 14
    top, bottom = [], []
    for i in range(steps + 1):
        t = i / steps
        # The strip's spine bows along a circle arc; its width tapers toward torn, uneven ends.
        x = (t - 0.5) * length
        y = bend * length * (t - 0.5) ** 2 * 2.0
        taper = 0.55 + 0.45 * math.sin(math.pi * t) ** 0.6
        half = 0.5 * width * taper
        top.append((x, y - half * rng.uniform(0.75, 1.15)))
        bottom.append((x, y + half * rng.uniform(0.75, 1.15)))
    angle = rng.uniform(0.0, math.tau)
    c, s = math.cos(angle), math.sin(angle)
    outline = top + bottom[::-1]
    return [(size / 2 + x * c - y * s, size / 2 + x * s + y * c) for x, y in outline], (c, s), bend


def scrap(rng):
    size = CELL * SUPERSAMPLE
    points, (c, s), bend = ragged_strip(rng, size)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)

    # The curl: one side of the strip's spine faces the light, the other is in shadow, split by a soft edge.
    side = Image.new("L", (size, size), 0)
    px = side.load()
    flip = 1.0 if bend >= 0 else -1.0
    for yy in range(0, size, 2):
        for xx in range(0, size, 2):
            u = (xx - size / 2) * -s + (yy - size / 2) * c
            px[xx, yy] = 255 if u * flip < size * 0.02 else 0
    side = side.resize((size // 2, size // 2)).resize((size, size)).filter(ImageFilter.GaussianBlur(SUPERSAMPLE))
    tone = Image.composite(Image.new("L", (size, size), LIT), Image.new("L", (size, size), SHADOW), side)

    # A thin highlight just inside the lit edge.
    edge = mask.filter(ImageFilter.MaxFilter(1 + 2 * SUPERSAMPLE))
    inner = mask.filter(ImageFilter.MinFilter(1 + 4 * SUPERSAMPLE))
    rim = ImageChops.subtract(mask, inner)
    rim = ImageChops.multiply(rim, side)
    tone = Image.composite(Image.new("L", (size, size), HIGHLIGHT), tone, rim.point(lambda v: v // 2))
    del edge

    rgba = Image.merge("RGBA", (tone, tone, tone, mask))
    return rgba.resize((CELL, CELL), Image.LANCZOS)


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else (
        Path(__file__).resolve().parents[3] / "Assets" / "Sprites" / "UI" / "CardReveal" / "shreds.png")
    rng = random.Random(SEED)
    sheet = Image.new("RGBA", (CELL * GRID, CELL * GRID), (0, 0, 0, 0))
    for i in range(GRID * GRID):
        sheet.paste(scrap(rng), ((i % GRID) * CELL, (i // GRID) * CELL))
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print(f"wrote {out}")


main()
