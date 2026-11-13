"""Draws the upgrade cards' icons as hand-drawn doodles, white so the text they sit in tints them, side
by side in one atlas that `Editor/CardIconSpriteAssetBuilder` turns into the `CardIcons` TextMeshPro sprite
asset. Card titles prefix their icon with `<sprite="CardIcons" name="<UpgradeKind>" tint=1>`.

Each icon is a few pen strokes, drawn the way a hand draws them, to sit with the handwritten font: the line
wobbles a little, its weight swells and thins along the stroke, ends overshoot, and a closed shape's ends
don't quite meet. The wobble is seeded per icon, so a redraw gives the same doodle. Drawn at 4x and scaled
down, so the edges are soft.

- BallCap       Extra Pen: a pen and a plus
- FasterReload  Quick Hands: a fast-forward
- Shield        Thick Skin: a shield
- ItemOdds      Lucky Find: a four-leaf clover
- RaiseLimits   Raise the Bar: an arrow up through a bar
- SnipeChance   Sharpshooter: a crosshair

Usage: python3 Tools/art/make_card_icons.py Assets/Sprites/UI/CardIcons/card_icons.png
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw

CELL = 128
SS = 4
STROKE = 0.085
WOBBLE = 0.012
ICONS = ("BallCap", "FasterReload", "Shield", "ItemOdds", "RaiseLimits", "SnipeChance")


def _densify(points, closed, step=0.006):
    if closed:
        points = points + [points[0]]
    out = []
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        out += [(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n) for i in range(n)]
    out.append(points[-1])
    return out


def _arc(cx, cy, r, start, end, n=40):
    return [(cx + r * math.cos(math.radians(start + (end - start) * i / n)),
             cy + r * math.sin(math.radians(start + (end - start) * i / n))) for i in range(n + 1)]


class Pen:
    """Draws strokes into a 4x mask, in 0-1 cell units."""

    def __init__(self, seed):
        self.size = CELL * SS
        self.mask = Image.new("L", (self.size, self.size), 0)
        self.draw = ImageDraw.Draw(self.mask)
        self.rng = random.Random(seed)

    def stroke(self, points, closed=False, weight=1.0, overshoot=0.03):
        pts = _densify(list(points), closed)
        if closed:
            # A hand closing a loop runs a little past where it started.
            pts = pts + _densify(pts[:max(2, len(pts) // 14)], False)[1:]
        else:
            pts = self._overshoot(pts, overshoot)
        phases = [self.rng.uniform(0, math.tau) for _ in range(4)]
        rate = [self.rng.uniform(5, 9), self.rng.uniform(11, 17)]
        length = 0.0
        prev = pts[0]
        drift = (self.rng.uniform(-1, 1) * WOBBLE, self.rng.uniform(-1, 1) * WOBBLE)
        for i, (x, y) in enumerate(pts):
            length += math.hypot(x - prev[0], y - prev[1])
            prev = (x, y)
            t = i / max(1, len(pts) - 1)
            wx = WOBBLE * (math.sin(rate[0] * length + phases[0]) + 0.5 * math.sin(rate[1] * length + phases[1]))
            wy = WOBBLE * (math.sin(rate[0] * length + phases[2]) + 0.5 * math.sin(rate[1] * length + phases[3]))
            # Heavier in the middle of a stroke, lighter where the pen lands and lifts.
            press = 0.72 + 0.28 * math.sin(math.pi * min(1.0, max(0.0, t * 1.15 - 0.05))) ** 0.6
            r = STROKE * weight * press / 2 * self.size
            cx, cy = (x + wx + drift[0] * t) * self.size, (y + wy + drift[1] * t) * self.size
            self.draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)

    def dot(self, x, y, r):
        r *= self.size
        self.draw.ellipse((x * self.size - r, y * self.size - r, x * self.size + r, y * self.size + r), fill=255)

    @staticmethod
    def _overshoot(pts, amount):
        def extend(a, b):
            dx, dy = a[0] - b[0], a[1] - b[1]
            n = math.hypot(dx, dy) or 1
            return a[0] + dx / n * amount, a[1] + dy / n * amount

        return _densify([extend(pts[0], pts[min(3, len(pts) - 1)])], False) + pts + \
            [extend(pts[-1], pts[max(-4, -len(pts))])]


def ball_cap(pen):
    tip, end = (0.1, 0.9), (0.5, 0.5)
    ux, uy = (end[0] - tip[0]), (end[1] - tip[1])
    length = math.hypot(ux, uy)
    ux, uy = ux / length, uy / length
    px, py = -uy, ux
    half, cone = 0.1, 0.2

    def at(along, across):
        return tip[0] + ux * along + px * across, tip[1] + uy * along + py * across

    body = [at(cone, half), at(length, half)] + \
        [at(length + half * math.sin(a), half * math.cos(a)) for a in [i * math.pi / 8 for i in range(9)]] + \
        [at(length, -half), at(cone, -half), tip, at(cone, half)]
    pen.stroke(body, closed=True)
    pen.stroke([at(cone, half), at(cone, -half)], weight=0.8, overshoot=0.0)
    pen.stroke([(0.62, 0.24), (0.94, 0.24)], weight=0.9)
    pen.stroke([(0.78, 0.08), (0.78, 0.4)], weight=0.9)


def faster_reload(pen):
    pen.stroke([(0.12, 0.2), (0.44, 0.5), (0.12, 0.8)], overshoot=0.02)
    pen.stroke([(0.5, 0.2), (0.84, 0.5), (0.5, 0.8)], overshoot=0.02)


def shield(pen):
    left = [(0.5, 0.08), (0.2, 0.16), (0.18, 0.46)] + \
        [(0.5 - 0.32 * math.cos(t * math.pi / 2) ** 0.9, 0.46 + 0.44 * math.sin(t * math.pi / 2))
         for t in [i / 10 for i in range(1, 11)]]
    right = [(1 - x, y) for x, y in reversed(left[:-1])]
    pen.stroke(left + right, closed=True)
    pen.stroke([(0.5, 0.24), (0.5, 0.72)], weight=0.8)


def item_odds(pen):
    c = (0.42, 0.4)
    for angle in (180, 270, 0, 90):
        a = math.radians(angle)
        lx, ly = c[0] + math.cos(a) * 0.19, c[1] + math.sin(a) * 0.19
        pen.stroke(_arc(lx, ly, 0.13, angle - 90, angle + 270, 36), closed=True)
    pen.stroke([(0.5, 0.48), (0.68, 0.64), (0.8, 0.9)], overshoot=0.0)


def raise_limits(pen):
    pen.stroke([(0.1, 0.58), (0.9, 0.56)])
    pen.stroke([(0.5, 0.92), (0.5, 0.14)], overshoot=0.0)
    pen.stroke([(0.24, 0.38), (0.5, 0.1), (0.76, 0.38)], overshoot=0.02)


def snipe_chance(pen):
    pen.stroke(_arc(0.5, 0.5, 0.3, -80, 280, 48), closed=True)
    pen.stroke([(0.5, 0.06), (0.5, 0.3)], weight=0.85, overshoot=0.0)
    pen.stroke([(0.5, 0.7), (0.5, 0.94)], weight=0.85, overshoot=0.0)
    pen.stroke([(0.06, 0.5), (0.3, 0.5)], weight=0.85, overshoot=0.0)
    pen.stroke([(0.7, 0.5), (0.94, 0.5)], weight=0.85, overshoot=0.0)
    pen.dot(0.5, 0.5, 0.055)


DRAWERS = {
    "BallCap": ball_cap,
    "FasterReload": faster_reload,
    "Shield": shield,
    "ItemOdds": item_odds,
    "RaiseLimits": raise_limits,
    "SnipeChance": snipe_chance,
}


def atlas():
    sheet = Image.new("RGBA", (CELL * len(ICONS), CELL), (255, 255, 255, 0))
    for i, name in enumerate(ICONS):
        pen = Pen(seed=1000 + i)
        DRAWERS[name](pen)
        icon = Image.new("RGBA", (CELL, CELL), (255, 255, 255, 0))
        icon.putalpha(pen.mask.resize((CELL, CELL), Image.LANCZOS))
        sheet.paste(icon, (i * CELL, 0))
    return sheet


out = Path(sys.argv[1])
out.parent.mkdir(parents=True, exist_ok=True)
atlas().save(out)
print(out, len(ICONS))
