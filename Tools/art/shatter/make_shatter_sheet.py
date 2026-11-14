"""Bakes a shatter sprite sheet from a picture of a balloon: the steps that used to be done by hand in GIMP.

1. Split. The balloon's silhouette is cut into Voronoi cells (seeds spread out by a few rounds of Lloyd
   relaxation, so no shard is a sliver), and the cracks wander a little (the pixel grid is pushed by a smooth
   noise field before each pixel finds its nearest seed), so the edges don't read as ruled lines.
   With --swirl each piece is then whirled about its own middle (as GIMP's Whirl), so its edges bend into
   S-curves: the tough balloons' rubbery tear rather than a plate's crack.
2. Border. Each shard gets a side wall: its own pixels, darkened, extruded a few pixels toward the shadow,
   so it reads as a piece of plate with thickness. The freshly cut edges catch a thin highlight.
3. Shadow. A soft drop shadow is baked under the shard and its wall, offset the same way.
4. Sheet. The shards go one to a cell, row by row from the top left (the particle system's frame order),
   each centred on its centroid so it spins about its middle, at the balloon's own scale: the balloon spans
   --diameter of a cell, so a particle drawn at its largest start size shows a shard at true size.

The particle system reads the sheet as a grid (Texture Sheet Animation, Grid mode, tiles = --grid, columns x
rows) and picks a random start frame per particle. Seeded, so a re-run gives the same shards.

Usage:
  python3 Tools/art/shatter/make_shatter_sheet.py SOURCE.png OUT.png [--pieces 9] [--grid 3 | 4x3] [--size 2048]
                                                   [--diameter 0.95] [--swirl 0] [--seed 7] [--review REVIEW.png]

SOURCE is the balloon alone on a transparent background (Tools/art/shatter/capture_balloon.cs renders one from
the open Editor). Needs numpy, scipy and Pillow.
"""
import argparse
import math
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

# Share of a cell the whole balloon spans by default: the unbreakable's sheet draws its shards at about this scale.
DIAMETER_SHARE = 0.95

# The balloon's drawn size (pixels) the measures below are for: a 2048 sheet of 3x3 at DIAMETER_SHARE. They
# scale with the balloon, so a smaller cell or --diameter keeps the same look.
REFERENCE_DIAMETER = DIAMETER_SHARE * 2048 / 3

# The side wall: how many pixels the shard is extruded (at a 2048 sheet), toward which way, and how dark.
WALL_DEPTH = 14
LIGHT_FROM = (-0.6, 0.8)  # x right, y up: the shard's lit side; the wall and shadow fall the other way
WALL_SHADE = 0.42
WALL_DESATURATE = 0.25

# The cut edge's highlight: a band this wide along the cracks (not the balloon's own outline), this bright.
EDGE_WIDTH = 2.5
EDGE_LIGHT = 0.22

# The baked shadow: offset (pixels at a 2048 sheet, away from the light), blur and strength.
SHADOW_OFFSET = 26
SHADOW_BLUR = 16
SHADOW_ALPHA = 0.65

# How far the cracks wander (pixels at a 2048 sheet) and how broad their wobble is.
CRACK_WANDER = 7
CRACK_SCALE = 40

# A shard smaller than this share of the balloon is merged away rather than kept as a speck.
MIN_PIECE_SHARE = 0.03


def load_source(path, diameter):
    """The source, cropped to its silhouette and scaled so its wider side spans `diameter` pixels."""
    image = Image.open(path).convert('RGBA')
    bbox = image.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    if bbox is None:
        sys.exit(f'{path}: nothing opaque to shatter')

    image = image.crop(bbox)
    scale = diameter / max(image.size)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    rgba = np.asarray(image.resize(size, Image.LANCZOS)).astype(np.float64) / 255.0
    return rgba


def spread_seeds(mask, count, rng, rounds=3):
    """`count` seeds inside the mask, evened out by a few rounds of Lloyd relaxation."""
    ys, xs = np.nonzero(mask)
    pick = rng.choice(len(xs), size=count, replace=False)
    seeds = np.stack([xs[pick], ys[pick]], axis=1).astype(np.float64)
    points = np.stack([xs, ys], axis=1).astype(np.float64)
    for _ in range(rounds):
        owner = nearest(points, seeds)
        for i in range(count):
            mine = points[owner == i]
            if len(mine):
                # Part way only: fully relaxed cells come out as a honeycomb.
                seeds[i] = 0.5 * seeds[i] + 0.5 * mine.mean(axis=0)

    return seeds


def nearest(points, seeds):
    best = np.full(len(points), np.inf)
    owner = np.zeros(len(points), dtype=np.int32)
    for i, (sx, sy) in enumerate(seeds):
        d = (points[:, 0] - sx) ** 2 + (points[:, 1] - sy) ** 2
        closer = d < best
        best[closer] = d[closer]
        owner[closer] = i

    return owner


def smooth_noise(shape, scale, rng):
    """A smooth field in [-1, 1]: coarse random values, blurred up to the image's size."""
    field = rng.standard_normal(shape)
    field = ndimage.gaussian_filter(field, sigma=scale, mode='wrap')
    return field / (np.abs(field).max() + 1e-9)


def whirl(rgba, piece, outline, turns):
    """The piece twisted about its own middle, as GIMP's Whirl: `turns` radians at the centre, easing to none at
    its farthest point, so its corners stay put and its straight edges bend into S-curves. The picture on it
    turns with it. Returns the three images with only the piece's surroundings changed."""
    ys, xs = np.nonzero(piece)
    cy, cx = ys.mean(), xs.mean()
    radius = math.sqrt(((ys - cy) ** 2 + (xs - cx) ** 2).max()) + 2.0
    h, w = piece.shape
    y0, y1 = max(0, int(cy - radius)), min(h, int(cy + radius) + 1)
    x0, x1 = max(0, int(cx - radius)), min(w, int(cx + radius) + 1)
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float64)
    dy, dx = yy - cy, xx - cx
    spin = -turns * np.clip(1.0 - np.hypot(dx, dy) / radius, 0.0, 1.0) ** 2
    cos, sin = np.cos(spin), np.sin(spin)
    at = np.stack([cy + dx * sin + dy * cos, cx + dx * cos - dy * sin])

    def warp(image):
        out = image.copy()
        out[y0:y1, x0:x1] = ndimage.map_coordinates(image.astype(np.float64), at, order=1, mode='constant')
        return out

    # Colours are warped premultiplied, so the piece's rim doesn't pick up the dark of empty pixels.
    alpha = rgba[..., 3]
    turned = np.stack([warp(rgba[..., c] * alpha) for c in range(3)] + [warp(alpha)], axis=-1)
    turned[..., :3] /= np.maximum(turned[..., 3:4], 1e-6)
    return turned, warp(piece) > 0.5, warp(outline) > 0.5


def label_pieces(alpha, count, rng, px):
    h, w = alpha.shape
    mask = alpha > 0.5
    seeds = spread_seeds(mask, count, rng)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float64)
    wander = CRACK_WANDER * px
    xx += wander * smooth_noise((h, w), CRACK_SCALE * px, rng)
    yy += wander * smooth_noise((h, w), CRACK_SCALE * px, rng)
    owner = nearest(np.stack([xx.ravel(), yy.ravel()], axis=1), seeds).reshape(h, w)

    # Merge specks into their neighbour, and keep each piece in one connected part.
    total = mask.sum()
    labels = np.full((h, w), -1, dtype=np.int32)
    for i in range(count):
        parts, n = ndimage.label((owner == i) & (alpha > 0.02))
        if n == 0:
            continue

        sizes = ndimage.sum(np.ones_like(parts), parts, index=range(1, n + 1))
        biggest = 1 + int(np.argmax(sizes))
        labels[parts == biggest] = i

    # Orphans (a cut-off bit of a piece) join whichever piece is closest.
    orphans = (labels < 0) & (alpha > 0.02)
    if orphans.any():
        _, (iy, ix) = ndimage.distance_transform_edt(labels < 0, return_indices=True)
        labels[orphans] = labels[iy[orphans], ix[orphans]]

    pieces = []
    for i in range(count):
        piece = labels == i
        if piece.sum() >= MIN_PIECE_SHARE * total:
            pieces.append(piece)
        elif piece.any():
            # Too small to keep on its own: give it to its nearest neighbour.
            others = (labels >= 0) & ~piece
            _, (iy, ix) = ndimage.distance_transform_edt(~others, return_indices=True)
            labels[piece] = labels[iy[piece], ix[piece]]

    return [labels == labels[p][0] for p in pieces if p.any()], mask


def soft(piece, sigma):
    """The piece's hard mask, antialiased."""
    return np.clip(ndimage.gaussian_filter(piece.astype(np.float64), sigma), 0.0, 1.0)


def shift(image, dx, dy):
    """Moves an image by whole pixels (x right, y down), filling with zeros."""
    out = np.zeros_like(image)
    h, w = image.shape[:2]
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    out[yd, xd] = image[ys, xs]
    return out


def over(top, bottom):
    """Straight-alpha 'over'."""
    ta, ba = top[..., 3:4], bottom[..., 3:4]
    a = ta + ba * (1.0 - ta)
    rgb = np.where(a > 1e-6, (top[..., :3] * ta + bottom[..., :3] * ba * (1.0 - ta)) / np.maximum(a, 1e-6), 0.0)
    return np.concatenate([rgb, a], axis=-1)


def render_piece(rgba, piece, outline, px):
    """One shard on a canvas padded for its wall and shadow: the shard over its wall over its shadow."""
    h, w = piece.shape
    pad = int(math.ceil((WALL_DEPTH + SHADOW_OFFSET + 3 * SHADOW_BLUR) * px)) + 4
    ys, xs = np.nonzero(piece)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    box = (slice(y0, y1), slice(x0, x1))
    ch, cw = y1 - y0 + 2 * pad, x1 - x0 + 2 * pad

    def canvas(src):
        out = np.zeros((ch, cw) + src.shape[2:], dtype=np.float64)
        out[pad:pad + y1 - y0, pad:pad + x1 - x0] = src[box]
        return out

    cover = soft(piece, 0.6 * px) * rgba[..., 3]
    shard = canvas(np.concatenate([rgba[..., :3], cover[..., None]], axis=-1))

    # The freshly cut edge: a thin band inside the cracks (where the piece ends but the balloon doesn't).
    inner = ndimage.distance_transform_edt(piece)
    crack = (inner <= EDGE_WIDTH * px) & piece & ~outline
    edge = canvas(ndimage.gaussian_filter(crack.astype(np.float64), 0.7 * px))
    shard[..., :3] = shard[..., :3] + (1.0 - shard[..., :3]) * (EDGE_LIGHT * edge[..., None])

    # The wall: the shard's colours darkened and a little greyed, stacked step by step away from the light.
    lx, ly = LIGHT_FROM
    norm = math.hypot(lx, ly)
    away = (-lx / norm, ly / norm)  # in image space (y down), away from the light
    grey = shard[..., :3].mean(axis=-1, keepdims=True)
    wall_rgb = (shard[..., :3] * (1.0 - WALL_DESATURATE) + grey * WALL_DESATURATE) * WALL_SHADE
    wall_layer = np.concatenate([wall_rgb, shard[..., 3:4]], axis=-1)
    wall = np.zeros_like(shard)
    depth = max(1, round(WALL_DEPTH * px))
    for step in range(depth, 0, -1):
        wall = over(shift(wall_layer, round(away[0] * step), round(away[1] * step)), wall)

    body = over(shard, wall)

    shadow_alpha = ndimage.gaussian_filter(body[..., 3], SHADOW_BLUR * px)
    offset = SHADOW_OFFSET * px
    shadow_alpha = shift(shadow_alpha, round(away[0] * offset), round(away[1] * offset)) * SHADOW_ALPHA
    shadow = np.zeros_like(body)
    shadow[..., 3] = shadow_alpha

    # The centroid of the shard itself, in canvas pixels, so the cell centres the plate rather than its shadow.
    cy, cx = ndimage.center_of_mass(canvas(piece.astype(np.float64)))
    return over(body, shadow), (cx, cy)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('source')
    parser.add_argument('out')
    parser.add_argument('--pieces', type=int, default=9)
    parser.add_argument('--grid', default='3', help='cells: N for N x N, or COLSxROWS')
    parser.add_argument('--size', type=int, default=2048, help="the sheet's width; its height follows the grid")
    parser.add_argument('--diameter', type=float, default=DIAMETER_SHARE,
                        help="share of a cell the whole balloon spans: match the particle system's start size")
    parser.add_argument('--swirl', type=float, default=0.0,
                        help="radians each piece is whirled at its middle (0: none, the plates' straight cracks)")
    parser.add_argument('--seed', type=int, default=7)
    parser.add_argument('--review', help='also write the sheet over a flat background, for a look')
    args = parser.parse_args()

    cols, _, rows = args.grid.lower().partition('x')
    cols, rows = int(cols), int(rows or cols)
    if args.pieces > cols * rows:
        sys.exit(f'--pieces {args.pieces} needs a bigger --grid than {args.grid}')

    cell = args.size / cols
    height = round(cell * rows)
    diameter = args.diameter * cell
    px = diameter / REFERENCE_DIAMETER
    rgba = load_source(args.source, diameter)
    if args.swirl:
        # Room around the balloon: a whirled piece reaches past where it was cut.
        margin = round(0.5 * diameter)
        rgba = np.pad(rgba, ((margin, margin), (margin, margin), (0, 0)))

    rng = np.random.default_rng(args.seed)
    pieces, mask = label_pieces(rgba[..., 3], args.pieces, rng, px)

    # The balloon's own outline, so only the cracks catch the cut-edge highlight.
    outline = ndimage.distance_transform_edt(mask) <= EDGE_WIDTH * px + 1

    # Largest first, so a reader of the sheet sees the big plates at the top.
    pieces.sort(key=lambda p: -p.sum())
    sheet = np.zeros((height, args.size, 4), dtype=np.float64)
    for index, piece in enumerate(pieces):
        if args.swirl:
            image, (cx, cy) = render_piece(*whirl(rgba, piece, outline, args.swirl), px)
        else:
            image, (cx, cy) = render_piece(rgba, piece, outline, px)
        row, col = divmod(index, cols)
        left = round((col + 0.5) * cell - cx)
        top = round((row + 0.5) * cell - cy)
        h, w = image.shape[:2]

        # Clip to the cell: a shard's shadow must not bleed into the next frame.
        cl, ct = math.ceil(col * cell), math.ceil(row * cell)
        cr, cb = math.floor((col + 1) * cell), math.floor((row + 1) * cell)
        x0, y0 = max(left, cl), max(top, ct)
        x1, y1 = min(left + w, cr), min(top + h, cb)
        clipped = image[y0 - top:y1 - top, x0 - left:x1 - left]
        lost = image[..., 3].sum() - clipped[..., 3].sum()
        if lost > 0.01 * image[..., 3].sum():
            print(f'warning: shard {index} loses {lost / image[..., 3].sum():.0%} of itself to its cell edge')

        sheet[y0:y1, x0:x1] = over(clipped, sheet[y0:y1, x0:x1])

    out = Image.fromarray((np.clip(sheet, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), 'RGBA')
    out.save(args.out)
    print(f'{len(pieces)} shards on a {cols}x{rows} sheet of {args.size}x{height} px -> {args.out}')

    if args.review:
        backdrop = Image.new('RGBA', out.size, (88, 140, 110, 255))
        backdrop.alpha_composite(out)
        backdrop.convert('RGB').resize((args.size // 2, height // 2), Image.LANCZOS).save(args.review)


if __name__ == '__main__':
    main()
