"""Draws the UI theme's shapes, white (or white-to-grey) so an Image tints them. The theme is the
"glossy balloon" look: layered buttons and panels (shadow, glow outline, lip, gradient face, sheen).

Every edge is soft, and evenly so: the shapes are drawn from a signed distance field, so the falloff is
the same width along a straight edge as round a corner (blurring a filled shape instead fades the corners
faster than the edges).

All rounded shapes share one corner: radius 64 px on 256 px, 9-slice border 80. Set an Image's Pixels
Per Unit Multiplier to size the corners (4 gives a 16-unit radius on the 225x400 canvas).

- rounded_panel.png     flat rounded rectangle, soft edge (lip, rim, wells, ribbons)
- rounded_gradient.png  the same, white at the top to light grey at the bottom (faces)
- rounded_glow.png      the shape with a soft glow round it, 16 px of room (border 96; outlines)
- rounded_shadow.png    the shape with a wide soft falloff, 32 px of room (border 112; drop shadows)
- rounded_smear.png     a blurred edge, hollow, 32 px of room (border 112; a see-through card's outline)
- rounded_drop_shadow.png  rounded_shadow dropped 20 px, hollow under the shape (borders 112, 132 top/bottom)
- gloss.png             the face's upper sheen: white, fading out downward, soft sides (border 80 l/r/top)
- card_plus.png         an upgrade card with a plus: "a card for your retry"
- play.png              a rounded play triangle: "watch an ad"

The hollow ones are for a see-through card, whose body must not show its back layers through it. They
are drawn for a corner multiplier of 4: a shadow dropped 5 units.

Usage: python3 Tools/art/make_ui_shapes.py Assets/Sprites/UI
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SS = 4
SIZE = 256
RADIUS = 64
FEATHER = 3.0


def _grid(w, h):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    return xs + 0.5, ys + 0.5


def _rect_sdf(xs, ys, cx, cy, w, h, r):
    """Signed distance (px, negative inside) to a w x h rounded rectangle of corner r centred on (cx, cy)."""
    qx = np.abs(xs - cx) - w / 2 + r
    qy = np.abs(ys - cy) - h / 2 + r
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - r


def _rounded_sdf(w, h, pad, radius=RADIUS, shrink=0.0):
    """Signed distance to a rounded rectangle filling the canvas inside its pad, `shrink` px smaller."""
    xs, ys = _grid(w + 2 * pad, h + 2 * pad)
    return _rect_sdf(xs, ys, w / 2 + pad, h / 2 + pad, w - 2 * shrink, h - 2 * shrink, radius - shrink)


def _smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)


def _soft(d, feather=FEATHER):
    """Alpha falling from 1 to 0 across `feather` px centred on the edge."""
    return _smoothstep(feather / 2, -feather / 2, d)


def _image(alpha, grey=None):
    a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    g = np.full_like(a, 255) if grey is None else (np.clip(grey, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(np.dstack([g, g, g, a]), "RGBA")


def rounded_panel():
    return _image(_soft(_rounded_sdf(SIZE, SIZE, 0, shrink=FEATHER / 2)))


def rounded_gradient(bottom=200 / 255):
    alpha = _soft(_rounded_sdf(SIZE, SIZE, 0, shrink=FEATHER / 2))
    _, ys = _grid(SIZE, SIZE)
    return _image(alpha, 1 + (bottom - 1) * (ys / SIZE))


def rounded_glow(pad=16, reach=13):
    return _image(_smoothstep(reach, 0, _rounded_sdf(SIZE, SIZE, pad)))


def rounded_shadow(pad=32, reach=30, inner=10):
    return _image(_smoothstep(reach, -inner, _rounded_sdf(SIZE, SIZE, pad)))


def rounded_smear(pad=32, reach=28, inner=8):
    """A blurred edge with no solid band: strongest on the shape's edge, fading out across `reach` px
    outside it and `inner` px inside it, and nothing further in, so it smears into whatever is around."""
    d = _rounded_sdf(SIZE, SIZE, pad)
    return _image(np.where(d > 0, _smoothstep(reach, 0, d) ** 1.5, _smoothstep(-inner, 0, d)))


def rounded_drop_shadow(pad=32, reach=30, inner=10, drop=20, feather=8):
    """rounded_shadow dropped `drop` px, fading out across the unmoved shape's edge over `feather` px so
    nothing of it shows through what casts it. The drop is extra room at the bottom (border 132 top and
    bottom, 112 the sides)."""
    xs, ys = _grid(SIZE + 2 * pad, SIZE + 2 * pad + drop)
    centre = SIZE / 2 + pad
    shadow = _rect_sdf(xs, ys, centre, centre + drop, SIZE, SIZE, RADIUS)
    shape = _rect_sdf(xs, ys, centre, centre, SIZE, SIZE, RADIUS)
    return _image(_smoothstep(reach, -inner, shadow) * _soft(-shape, feather))


def gloss(feather=10):
    alpha = _soft(_rounded_sdf(SIZE, SIZE, 0, shrink=feather / 2), feather)
    _, ys = _grid(SIZE, SIZE)
    fade = np.clip(1 - (ys / SIZE) * 1.15, 0, 1) ** 1.6
    return _image(alpha * fade)


def _down(img, size):
    return img.resize(size, Image.LANCZOS)


def _white(mask):
    img = Image.new("RGBA", mask.size, (255, 255, 255, 0))
    img.putalpha(mask)
    return img


def card_plus(size=128):
    s = size * SS
    m = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(m)
    # A card, tilted a little, with a plus cut out of it.
    card = Image.new("L", (s, s), 0)
    ImageDraw.Draw(card).rounded_rectangle((s * 0.22, s * 0.1, s * 0.78, s * 0.9), s * 0.09, fill=255)
    card = card.rotate(-8, resample=Image.BICUBIC)
    m.paste(card, (0, 0))
    bar, arm = s * 0.07, s * 0.17
    cx, cy = s * 0.5, s * 0.5
    d.rectangle((cx - arm, cy - bar, cx + arm, cy + bar), fill=0)
    d.rectangle((cx - bar, cy - arm, cx + bar, cy + arm), fill=0)
    return _down(_white(m), (size, size))


def play(size=128):
    s = size * SS
    mask = Image.new("L", (s, s), 0)
    inset = 0.2 * s
    points = [(inset + 0.06 * s, inset), (inset + 0.06 * s, s - inset), (s - inset + 0.04 * s, s / 2)]
    ImageDraw.Draw(mask).polygon(points, fill=255)
    # Round the corners: blur the hard triangle, then cut it back to a crisp edge.
    mask = mask.filter(ImageFilter.GaussianBlur(0.05 * s)).point(lambda v: 255 if v > 140 else 0)
    return _down(_white(mask), (size, size))


out = Path(sys.argv[1])
rounded_panel().save(out / "rounded_panel.png")
rounded_gradient().save(out / "rounded_gradient.png")
rounded_glow().save(out / "rounded_glow.png")
rounded_shadow().save(out / "rounded_shadow.png")
rounded_smear().save(out / "rounded_smear.png")
rounded_drop_shadow().save(out / "rounded_drop_shadow.png")
gloss().save(out / "gloss.png")
card_plus().save(out / "card_plus.png")
play().save(out / "play.png")
