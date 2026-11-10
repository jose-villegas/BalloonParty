"""Draws the floors' microdetail map (Assets/Textures/Noise/DetailNoiseTileable.png), tileable. R = height:
small rounded grains over fine fractal noise and the speckle. G and B = its slope along x and y, 0.5-biased,
so a shader lights the bump with one tap and the mipmaps average the slope out as it shrinks (no shimmer).
A = the speckle alone (one random value per texel, softened a little by its neighbours), with its histogram
flattened: ranked, so every value 0..1 is equally common. A shader then flags an exact share of the grains with
one threshold, step(1 - share, A), as the 1-bit mask of grains that catch the light.
Usage: python Tools/art/make_detail_noise.py <out.png>"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(sys.argv[1])
SIZE = 256
RNG = np.random.default_rng(20261110)


def perlin(cells):
    """Gradient noise that wraps every `cells` cells across the tile, in about -0.7..0.7."""
    angles = RNG.uniform(0.0, 2.0 * np.pi, (cells, cells))
    grads = np.stack([np.cos(angles), np.sin(angles)], axis=-1)
    coords = (np.arange(SIZE) + 0.5) * cells / SIZE
    x, y = np.meshgrid(coords, coords)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = x - x0
    fy = y - y0

    def corner(ox, oy):
        g = grads[(y0 + oy) % cells, (x0 + ox) % cells]
        return g[..., 0] * (fx - ox) + g[..., 1] * (fy - oy)

    ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    uy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
    top = corner(0, 0) + (corner(1, 0) - corner(0, 0)) * ux
    bottom = corner(0, 1) + (corner(1, 1) - corner(0, 1)) * ux
    return top + (bottom - top) * uy


def grains(cells):
    """Rounded bumps, one per cell at a random point: 1 at a grain's top, 0 between grains."""
    # points[row, column] = the grain's (x, y) in cell units, inside its own cell.
    points = RNG.uniform(0.0, 1.0, (cells, cells, 2))
    points[..., 0] += np.arange(cells)[None, :]
    points[..., 1] += np.arange(cells)[:, None]
    coords = (np.arange(SIZE) + 0.5) * cells / SIZE
    x, y = np.meshgrid(coords, coords)
    nearest = np.full((SIZE, SIZE), np.inf)
    for oy in range(-1, 2):
        for ox in range(-1, 2):
            cx = (np.floor(x).astype(int) + ox)
            cy = (np.floor(y).astype(int) + oy)
            p = points[cy % cells, cx % cells]
            dx = x - (p[..., 0] + (cx - cx % cells))
            dy = y - (p[..., 1] + (cy - cy % cells))
            nearest = np.minimum(nearest, np.sqrt(dx * dx + dy * dy))
    return np.clip(1.0 - nearest / 0.75, 0.0, 1.0) ** 1.5


raw = RNG.uniform(0.0, 1.0, (SIZE, SIZE))
neighbours = sum(np.roll(raw, shift, axis) for shift in (-1, 1) for axis in (0, 1)) / 4.0
speckle = raw * 0.7 + neighbours * 0.3
speckle = (speckle - speckle.min()) / (speckle.max() - speckle.min())

height = grains(48) * 0.5 + (perlin(32) * 0.6 + perlin(64) * 0.4) * 0.5 + speckle * 0.3
height = (height - height.min()) / (height.max() - height.min())

# Central differences across the wrap, scaled so all but the steepest 1% fit the 0..1 range.
slope_x = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
slope_y = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
scale = np.percentile(np.abs(np.concatenate([slope_x, slope_y])), 99)
# Image rows run down; texture v runs up, so y's slope flips.
gx = np.clip(0.5 + 0.5 * slope_x / scale, 0.0, 1.0)
gy = np.clip(0.5 - 0.5 * slope_y / scale, 0.0, 1.0)

order = np.argsort(np.argsort(speckle, axis=None))
flat = (order / (order.size - 1)).reshape(speckle.shape)

rgba = np.stack([height, gx, gy, flat], axis=-1)
Image.fromarray(np.round(rgba * 255).astype(np.uint8), "RGBA").save(OUT)
print(f"{OUT}: slope scale {scale:.4f} per texel")
