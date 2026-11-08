"""Draws the coin sprites in the existing UI style: a white rim with a thin dark inner stroke (as the
shield), a softly graded gold face (the sun's palette), an embossed inner ring, and a balloon stamped
in the middle. Supersampled 4x, then downscaled."""
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = Path(sys.argv[1])
SIZE = 512
SS = 4
S = SIZE * SS
C = S / 2


def disc(radius, fill, size=S):
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse((C - radius, C - radius, C + radius, C + radius), fill=fill)
    return layer


def mask_disc(radius):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse((C - radius, C - radius, C + radius, C + radius), fill=255)
    return m


def gradient(top_left, bottom_right):
    """A diagonal gradient, light at the top left like the sun."""
    small = Image.new("RGBA", (256, 256))
    px = small.load()
    for y in range(256):
        for x in range(256):
            t = min(1.0, max(0.0, (x + y) / 510.0))
            t = t * t * (3 - 2 * t)
            px[x, y] = tuple(int(a + (b - a) * t) for a, b in zip(top_left, bottom_right))
    return small.resize((S, S), Image.BICUBIC)


def balloon(layer_fill, scale=1.0, dx=0, dy=0):
    """A balloon glyph: an egg-shaped body, a knot, and a curling string."""
    g = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(g)
    w, h = 150 * SS * scale, 185 * SS * scale
    cx, cy = C + dx, C - 25 * SS * scale + dy
    d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=layer_fill)
    knot = 18 * SS * scale
    base = cy + h / 2 - 4 * SS
    d.polygon([(cx, base - knot * 0.4), (cx - knot, base + knot), (cx + knot, base + knot)], fill=layer_fill)
    pts = []
    for i in range(41):
        t = i / 40
        pts.append((cx + 16 * SS * scale * __import__("math").sin(t * 6.0), base + knot + t * 70 * SS * scale))
    d.line(pts, fill=layer_fill, width=int(9 * SS * scale), joint="curve")
    return g


def coin():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    rim, stroke, face, ring = 236 * SS, 214 * SS, 206 * SS, 168 * SS

    # A faint blue-grey halo, as the shield's soft edge.
    halo = disc(rim + 4 * SS, (142, 205, 239, 60)).filter(ImageFilter.GaussianBlur(6 * SS))
    img.alpha_composite(halo)
    img.alpha_composite(disc(rim, (255, 255, 255, 255)))
    img.alpha_composite(disc(stroke, (161, 161, 161, 255)))

    face_layer = gradient((255, 246, 168), (247, 176, 40))
    face_layer.putalpha(mask_disc(face))
    img.alpha_composite(face_layer)

    # Embossed inner ring: a darker groove with a light edge under it.
    groove = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(groove).ellipse((C - ring, C - ring, C + ring, C + ring), outline=(214, 138, 22, 255),
                                   width=10 * SS)
    light = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    r2 = ring - 10 * SS
    ImageDraw.Draw(light).ellipse((C - r2, C - r2, C + r2, C + r2), outline=(255, 248, 200, 200), width=5 * SS)
    img.alpha_composite(groove)
    img.alpha_composite(light)

    # The stamped balloon: a light offset under a darker body reads as pressed into the metal.
    img.alpha_composite(balloon((255, 249, 205, 230), dx=4 * SS, dy=5 * SS))
    img.alpha_composite(balloon((222, 146, 24, 255)))

    # A soft shine on the top left of the face.
    shine = Image.new("L", (S, S), 0)
    ImageDraw.Draw(shine).ellipse((C - 150 * SS, C - 175 * SS, C + 10 * SS, C - 60 * SS), fill=90)
    shine = shine.filter(ImageFilter.GaussianBlur(18 * SS))
    shine = ImageChops.multiply(shine, mask_disc(face))
    white = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    white.putalpha(shine)
    img.alpha_composite(white)
    return img.resize((SIZE, SIZE), Image.LANCZOS)


def fill():
    """The coin's silhouette in white, for tinting and the counter's punch flash (as shield-fill)."""
    return disc(236 * SS, (255, 255, 255, 255)).resize((SIZE, SIZE), Image.LANCZOS)


OUT.mkdir(parents=True, exist_ok=True)
coin().save(OUT / "coin.png")
fill().save(OUT / "coin-fill.png")
