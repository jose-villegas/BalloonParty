"""Draws the heart reward balloon's sprites (PLAN-RewardBalloons): a heart-shaped balloon wrapped in bandages.

- heart_balloon_body.png      the heart and its knot, near white, so the renderer tints it and the scene light
                              shades it like any balloon body. Its volume is baked in: the heart inflated
                              from a height map (a round profile over the distance to the edge), shaded
                              mostly by how far it turns from the viewer, so each lobe rounds off and the
                              cleft and the tip fall into shade whatever the light does, and a little from
                              the upper left.
- heart_balloon_normals.png   the same height map's surface normals (RGB, 0.5 + 0.5 * n, y up) over the heart
                              (alpha), for the shine shader (Balloon/HeartShine.shader), which lights its
                              highlight from them toward the scene light. The round balloons' shine is an arc
                              drawn for a circle; this one follows the heart's own lobes. Imported linear
                              (not sRGB) and uncompressed, or the normals bend.
- heart_balloon_bandages.png  two cream cloth strips wrapped across it, bowed the way a band round a ball
                              looks, frayed a little along their edges, and tied off in a knot with two loose
                              tails on the right lobe. The strips darken where they wrap round the edge and
                              cast a faint shadow on the body under them. Same canvas and pivot as the body,
                              so the two stack in the prefab.
- heart_balloon_shadow.png    the blurred silhouette for the baked shadow child, at half the body's pixels per
                              unit, grey like the other balloons' baked shadows (the renderer tints it).

The body, bandage and normal sprites are imported at 400 pixels per unit, pivot centre, so the heart is about
as wide as a round balloon (0.64 units); the shadow at 200. Drawn at 4x and scaled down, so the edges are
soft. Seeded, so a redraw gives the same strips. Needs numpy and scipy (the height map).

Usage: python3 Tools/art/make_heart_balloon.py Assets/Sprites/Balloons/HeartBalloon
"""
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter
from scipy import ndimage

SIZE = 288
SS = 4
HEART_WIDTH = 252
CREAM = (246, 236, 212)
SHADOW_GREY = 192


def _heart_points(scale, cx, cy, n=720):
    # The classic heart curve, its lobes rounded a little toward a circle so it reads as inflated.
    points = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        r = math.hypot(x, y + 3)
        if r > 0:
            x += 0.18 * (14.5 * x / r - x)
            y += 0.18 * (14.5 * (y + 3) / r - (y + 3))
        points.append((cx + x * scale, cy - (y + 2.5) * scale))
    return points


def _knot(draw, cx, top, s, fill):
    draw.polygon([(cx - 9 * s, top + 14 * s), (cx + 9 * s, top + 14 * s), (cx, top - 2 * s)], fill=fill)
    draw.rounded_rectangle([cx - 10 * s, top + 11 * s, cx + 10 * s, top + 17 * s], radius=3 * s, fill=fill)


def _centre_line(a, b, bow, t):
    (ax, ay), (bx, by) = a, b
    length = math.hypot(bx - ax, by - ay)
    nx, ny = -(by - ay) / length, (bx - ax) / length
    sag = bow * math.sin(math.pi * t)
    return ax + (bx - ax) * t + nx * sag, ay + (by - ay) * t + ny * sag


def _strip(size, a, b, width, bow, seed, threads=True):
    """A cloth strip from a to b, bowed by `bow`, as a mask: its edges a little ragged, a few threads loose."""
    rng = random.Random(seed)
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    steps = int(length / (3 * SS))
    upper, lower = [], []
    drift_u = drift_l = 0.0
    for i in range(steps + 1):
        t = i / steps
        px, py = _centre_line(a, b, bow, t)
        qx, qy = _centre_line(a, b, bow, min(1.0, t + 0.001))
        rx, ry = _centre_line(a, b, bow, max(0.0, t - 0.001))
        tx, ty = qx - rx, qy - ry
        tl = math.hypot(tx, ty)
        nx, ny = -ty / tl, tx / tl
        drift_u = drift_u * 0.75 + rng.uniform(-1, 1) * 0.9 * SS
        drift_l = drift_l * 0.75 + rng.uniform(-1, 1) * 0.9 * SS
        upper.append((px + nx * (width / 2 + drift_u), py + ny * (width / 2 + drift_u)))
        lower.append((px - nx * (width / 2 + drift_l), py - ny * (width / 2 + drift_l)))
    draw.polygon(upper + lower[::-1], fill=255)
    if threads:
        for _ in range(int(length / (14 * SS))):
            i = rng.randrange(len(upper))
            edge, other = (upper, lower) if rng.random() < 0.5 else (lower, upper)
            x0, y0 = edge[i]
            ox, oy = other[i]
            ol = math.hypot(x0 - ox, y0 - oy)
            reach = rng.uniform(1.5, 3.5) * SS
            lean = rng.uniform(-0.5, 0.5)
            x1 = x0 + (x0 - ox) / ol * reach + lean * reach
            y1 = y0 + (y0 - oy) / ol * reach + lean * reach
            draw.line([(x0, y0), (x1, y1)], fill=255, width=SS)
    return mask


def _weave(size, a, b, bow, mask, seed):
    """Fine, faint threads across and along the strip, so it reads as cloth and not as tape."""
    rng = random.Random(seed)
    lines = Image.new("L", size, 255)
    draw = ImageDraw.Draw(lines)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    count = int(length / (2.5 * SS))
    for i in range(count + 1):
        t = i / count
        px, py = _centre_line(a, b, bow, t)
        qx, qy = _centre_line(a, b, bow, min(1.0, t + 0.002))
        tx, ty = qx - px, qy - py
        tl = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / tl, tx / tl
        span = 40 * SS
        draw.line([(px - nx * span, py - ny * span), (px + nx * span, py + ny * span)],
                  fill=255 - rng.randint(6, 13), width=max(1, SS // 2))
    for offset in range(-14, 15, 3):
        points = []
        for i in range(count + 1):
            t = i / count
            px, py = _centre_line(a, b, bow + offset * SS, t)
            points.append((px, py))
        draw.line(points, fill=255 - rng.randint(5, 10), width=max(1, SS // 2))
    return ImageChops.lighter(lines, ImageChops.invert(mask))


def _tie(size, kx, ky):
    """The knot where the strips are tied off: a bunched lump of cloth, two tails hanging from it, and its
    shading (creases, and the lump's underside)."""
    mask = Image.new("L", size, 0)
    shade = Image.new("L", size, 255)
    for (dx, dy), bow, seed in (((46, 44), 6, 21), ((14, 56), -5, 23)):
        tail = _strip(size, (kx, ky), (kx + dx * SS, ky + dy * SS), 13 * SS, bow * SS, seed)
        mask = ImageChops.lighter(mask, tail)
    lump = Image.new("L", size, 0)
    ImageDraw.Draw(lump).ellipse([kx - 14 * SS, ky - 11 * SS, kx + 14 * SS, ky + 11 * SS], fill=255)
    # The tails tuck under the lump: a shadow where they leave it.
    tuck = ImageChops.multiply(mask, lump.filter(ImageFilter.GaussianBlur(4 * SS)))
    shade = ImageChops.subtract(shade, Image.eval(tuck, lambda v: v // 4))
    mask = ImageChops.lighter(mask, lump)
    draw = ImageDraw.Draw(shade)
    draw.arc([kx - 12 * SS, ky - 9 * SS, kx + 12 * SS, ky + 9 * SS], 20, 160, fill=200, width=3 * SS)
    draw.line([(kx - 6 * SS, ky - 8 * SS), (kx - 1 * SS, ky + 7 * SS)], fill=214, width=2 * SS)
    draw.line([(kx + 4 * SS, ky - 8 * SS), (kx + 7 * SS, ky + 5 * SS)], fill=222, width=2 * SS)
    shade = shade.filter(ImageFilter.GaussianBlur(SS))
    # Rounded: each piece darkens toward its own edge.
    rounding = Image.eval(mask.filter(ImageFilter.GaussianBlur(3 * SS)), lambda v: int(170 + 85 * min(1.0, v / 210)))
    return mask, ImageChops.multiply(shade, rounding)


def _volume(heart):
    """The inflated heart's shading (0-255) and its surface normals (x right, y down, z toward the viewer)."""
    mask = np.asarray(heart, dtype=np.float32) / 255.0
    distance = ndimage.distance_transform_edt(mask > 0.5)
    deepest = distance.max()
    height = deepest * np.sqrt(1.0 - (1.0 - np.clip(distance / deepest, 0.0, 1.0)) ** 2)
    # Softened, or the ridge where the distance field folds (up the middle from the tip) shows as a crease.
    height = ndimage.gaussian_filter(height, 9 * SS)
    gy, gx = np.gradient(height)
    normal = np.dstack((-gx, -gy, np.ones_like(height)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    light = np.array([-0.45, -0.55, 0.70])
    light /= np.linalg.norm(light)
    lambert = np.clip(normal @ light, 0.0, 1.0)
    shade = np.clip(0.48 + 0.34 * normal[..., 2] + 0.22 * lambert, 0.0, 1.0)
    shade_image = Image.fromarray((shade * 255).astype(np.uint8), "L")
    return shade_image, normal


def _normals(normal, heart):
    """The normals as a sprite for the shine shader: RGB the normal (y flipped to point up, as the sprite's
    space does), 0.5 + 0.5 * n; alpha the heart, so nothing shines off its edge."""
    up = normal * np.array([1.0, -1.0, 1.0])
    rgb = Image.fromarray(np.round((up * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")
    image = rgb.convert("RGBA")
    image.putalpha(heart)
    return image


def main(out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    big = SIZE * SS
    size = (big, big)
    scale = HEART_WIDTH * SS / 32 / 1.04
    cx, cy = big / 2, big / 2 + 2 * SS
    outline = _heart_points(scale, cx, cy)

    heart = Image.new("L", size, 0)
    ImageDraw.Draw(heart).polygon(outline, fill=255)
    tip_y = max(y for _, y in outline)

    # Body: the inflated heart's shading; the knot under the tip.
    inner = heart.filter(ImageFilter.GaussianBlur(14 * SS))
    volume, normal = _volume(heart)
    body_rgb = Image.merge("RGB", (volume, volume, volume))
    knot = Image.new("L", size, 0)
    _knot(ImageDraw.Draw(knot), cx, tip_y - 6 * SS, SS, 255)
    body_alpha = ImageChops.lighter(heart, knot)
    body_rgb.paste((214, 214, 214), mask=ImageChops.subtract(knot, heart))
    body = body_rgb.convert("RGBA")
    body.putalpha(body_alpha)

    # Bandages: two strips crossing the heart, clipped to it (they wrap round behind), tied on the right lobe.
    strips = [
        ((cx - 150 * SS, cy - 40 * SS), (cx + 150 * SS, cy - 8 * SS), 30 * SS, 16 * SS, 7),
        ((cx - 150 * SS, cy + 84 * SS), (cx + 150 * SS, cy - 22 * SS), 26 * SS, 14 * SS, 11),
    ]
    cloth_mask = Image.new("L", size, 0)
    cloth_shade = Image.new("L", size, 255)
    for a, b, width, bow, seed in strips:
        mask = ImageChops.multiply(_strip(size, a, b, width, bow, seed), heart)
        # Where two strips cross, the upper one's edge shades the one under it.
        under_edge = ImageChops.multiply(cloth_mask, mask.filter(ImageFilter.GaussianBlur(2 * SS)))
        cloth_shade = ImageChops.subtract(cloth_shade, Image.eval(under_edge, lambda v: v // 6))
        weave = _weave(size, a, b, bow, mask, seed + 1)
        cloth_shade = ImageChops.composite(ImageChops.multiply(cloth_shade, weave), cloth_shade, mask)
        cloth_mask = ImageChops.lighter(cloth_mask, mask)

    # The cloth follows the heart's volume, a little lifted (it is paler and sits proud of the rubber).
    cloth_shade = ImageChops.multiply(cloth_shade, Image.eval(volume, lambda v: int(70 + v * 0.75)))

    # The knot sits on the right lobe, over the first strip; its tails may hang past the edge.
    tie_mask, tie_shade = _tie(size, cx + 80 * SS, cy - 2 * SS)
    tie_drop = ImageChops.offset(tie_mask.filter(ImageFilter.GaussianBlur(2 * SS)), SS, 3 * SS)
    cloth_shade = ImageChops.subtract(cloth_shade, Image.eval(tie_drop, lambda v: v // 4))
    cloth_shade = ImageChops.composite(tie_shade, cloth_shade, tie_mask)
    cloth_mask = ImageChops.lighter(cloth_mask, tie_mask)

    cloth_rgb = Image.merge("RGB", [ImageChops.multiply(Image.new("L", size, c), cloth_shade) for c in CREAM])

    # The cloth's faint shadow on the body, down and to the right of it, under the cloth itself.
    drop = ImageChops.offset(cloth_mask.filter(ImageFilter.GaussianBlur(3 * SS)), 2 * SS, 4 * SS)
    drop_alpha = Image.eval(ImageChops.multiply(drop, heart), lambda v: int(v * 0.32))

    bandages = Image.new("RGBA", size, (40, 20, 20, 0))
    bandages.putalpha(drop_alpha)
    cloth = cloth_rgb.convert("RGBA")
    cloth.putalpha(cloth_mask)
    bandages = Image.alpha_composite(bandages, cloth)

    body.resize((SIZE, SIZE), Image.LANCZOS).save(out / "heart_balloon_body.png")
    bandages.resize((SIZE, SIZE), Image.LANCZOS).save(out / "heart_balloon_bandages.png")

    _normals(normal, heart).resize((SIZE, SIZE), Image.LANCZOS).save(out / "heart_balloon_normals.png")

    # Shadow: the silhouette (heart, knot, tails) blurred, at half the pixels per unit, padded for the blur.
    silhouette = ImageChops.lighter(body_alpha, tie_mask)
    shadow_size = SIZE // 2 + 24
    padded = Image.new("L", (shadow_size, shadow_size), 0)
    padded.paste(silhouette.resize((SIZE // 2, SIZE // 2), Image.LANCZOS), (12, 12))
    blurred = padded.filter(ImageFilter.GaussianBlur(5))
    shadow = Image.new("RGBA", (shadow_size, shadow_size), (SHADOW_GREY, SHADOW_GREY, SHADOW_GREY, 0))
    shadow.putalpha(Image.eval(blurred, lambda v: int(v * 160 / 255)))
    shadow.save(out / "heart_balloon_shadow.png")
    print("wrote", out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Assets/Sprites/Balloons/HeartBalloon")
