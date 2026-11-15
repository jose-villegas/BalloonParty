"""Draws the paper scenario's tile (Assets/Textures/Scenario/PaperTileable.png), tileable, so the page keeps its
depth under the scene light with no noise run per frame.

R = the paper's tone: 1 is clean paper, lower leans toward the fibre colour (the material sets both colours, so a
retint needs no rebake). G and B = the surface's slope along x and y, 0.5-biased, the same packing as the floors'
DetailNoiseTileable: a shader lights the bump with one tap, and the mips average the slope out as it shrinks.
A = the height itself, for anything that settles into the paper's valleys.

The surface is papyrus-like: two layers of pressed strips laid across each other (the front one horizontal, the
back one fainter and vertical), long streaks along each strip, loose fibres mostly along the strips, fine grain,
a few dark specks, and a slow undulation of the sheet.
Usage: python Tools/art/make_paper.py <out.png> [preview.png]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(sys.argv[1])
PREVIEW = Path(sys.argv[2]) if len(sys.argv) > 2 else None
SIZE = 512
RNG = np.random.default_rng(20261115)


def perlin(cells_x, cells_y):
    """Gradient noise that wraps across the tile, with its own cell count per axis, in about -0.7..0.7."""
    angles = RNG.uniform(0.0, 2.0 * np.pi, (cells_y, cells_x))
    grads = np.stack([np.cos(angles), np.sin(angles)], axis=-1)
    x, y = np.meshgrid((np.arange(SIZE) + 0.5) * cells_x / SIZE, (np.arange(SIZE) + 0.5) * cells_y / SIZE)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = x - x0
    fy = y - y0

    def corner(ox, oy):
        g = grads[(y0 + oy) % cells_y, (x0 + ox) % cells_x]
        return g[..., 0] * (fx - ox) + g[..., 1] * (fy - oy)

    ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    uy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
    top = corner(0, 0) + (corner(1, 0) - corner(0, 0)) * ux
    bottom = corner(0, 1) + (corner(1, 1) - corner(0, 1)) * ux
    return top + (bottom - top) * uy


def blur(image, sigma):
    """Gaussian blur through the FFT, which wraps, so the tile stays seamless."""
    freq = np.fft.fftfreq(SIZE)
    fx, fy = np.meshgrid(freq, freq)
    kernel = np.exp(-2.0 * (np.pi * sigma) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(image) * kernel))


def normalized(image):
    return (image - image.min()) / (image.max() - image.min())


def fibres(count, length_range, sigma):
    """Loose fibres splatted into a wrapping buffer: gently bent strokes, most along the strips (the front
    layer's horizontal ones first), tapered at both ends."""
    buffer = np.zeros((SIZE, SIZE))
    xs, ys, ws = [], [], []
    for _ in range(count):
        roll = RNG.uniform()
        if roll < 0.5:
            angle = RNG.normal(0.0, 0.12)
        elif roll < 0.8:
            angle = np.pi * 0.5 + RNG.normal(0.0, 0.12)
        else:
            angle = RNG.uniform(0.0, np.pi)
        length = RNG.uniform(*length_range)
        start = RNG.uniform(0.0, SIZE, 2)
        direction = np.array([np.cos(angle), np.sin(angle)])
        normal = np.array([-direction[1], direction[0]])
        bend = RNG.normal(0.0, 0.03) * length
        t = np.linspace(0.0, 1.0, max(2, int(length * 2)))
        points = start + np.outer(t * length, direction) + np.outer(4.0 * t * (1.0 - t) * bend, normal)
        strength = RNG.uniform(0.4, 1.0) * np.sin(np.pi * t) ** 0.5
        xs.append(points[:, 0])
        ys.append(points[:, 1])
        ws.append(strength)
    x = np.concatenate(xs)
    y = np.concatenate(ys)
    w = np.concatenate(ws)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = x - x0
    fy = y - y0
    for ox, oy, weight in ((0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)), (0, 1, (1 - fx) * fy), (1, 1, fx * fy)):
        np.add.at(buffer, ((y0 + oy) % SIZE, (x0 + ox) % SIZE), w * weight)
    return blur(buffer, sigma)


# The slow wave of the sheet: a few broad swells across the tile.
undulation = perlin(2, 2) * 0.6 + perlin(4, 4) * 0.4

# Pressed strips: each layer varies quickly across its strips and slowly along them, with long streaks inside.
front_strips = perlin(3, 14) * 0.7 + perlin(5, 28) * 0.3
front_streaks = perlin(6, 140) * 0.6 + perlin(10, 260) * 0.4
back_strips = perlin(14, 3) * 0.7 + perlin(28, 5) * 0.3
back_streaks = perlin(140, 6) * 0.6 + perlin(260, 10) * 0.4

fine_fibres = normalized(fibres(2600, (8.0, 40.0), 0.7))
coarse_fibres = normalized(fibres(260, (30.0, 110.0), 1.6))

raw = RNG.uniform(0.0, 1.0, (SIZE, SIZE))
grain = normalized(blur(raw, 0.6))

# Each layer's share of the bump; the undulation is broad, so it needs a large height for a visible slope.
height = (undulation * 9.0
          + front_strips * 1.6 + front_streaks * 0.9
          + back_strips * 0.7 + back_streaks * 0.35
          + fine_fibres * 0.35 + coarse_fibres * 0.45
          + grain * 0.3)
height = normalized(height)

# Tone: the strips' seams and the denser fibres read a little darker, the grain mottles, and a few specks sit in the
# pulp. 1 = clean paper.
specks = np.zeros((SIZE, SIZE))
for _ in range(70):
    cx, cy = RNG.uniform(0.0, SIZE, 2).astype(int)
    specks[cy % SIZE, cx % SIZE] += RNG.uniform(0.5, 1.0)
specks = normalized(blur(specks, 0.9))
tone = (1.0
        - 0.10 * normalized(np.abs(front_strips)) ** 2
        - 0.05 * normalized(np.abs(back_strips)) ** 2
        - 0.08 * normalized(front_streaks + back_streaks * 0.5)
        - 0.22 * coarse_fibres
        - 0.12 * fine_fibres
        - 0.05 * grain
        - 0.7 * specks)
tone = np.clip(tone, 0.0, 1.0)

# Central differences across the wrap, scaled so all but the steepest 1% fit the 0..1 range.
slope_x = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
slope_y = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
scale = np.percentile(np.abs(np.concatenate([slope_x, slope_y])), 99)
# Image rows run down; texture v runs up, so y's slope flips.
gx = np.clip(0.5 + 0.5 * slope_x / scale, 0.0, 1.0)
gy = np.clip(0.5 - 0.5 * slope_y / scale, 0.0, 1.0)

rgba = np.stack([tone, gx, gy, height], axis=-1)
OUT.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.round(rgba * 255).astype(np.uint8), "RGBA").save(OUT)
print(f"{OUT}: slope scale {scale:.5f} per texel, tone mean {tone.mean():.3f}")

if PREVIEW:
    # Lit the way Paper.shader lights it, from the upper left, on a warm white, two tiles by two.
    light = np.array([-0.707, 0.707])
    sx = gx * 2.0 - 1.0
    sy = gy * 2.0 - 1.0
    facing = -(sx * light[0] + sy * light[1])
    shade = 1.0 + facing * 0.18
    paper = np.array([0.95, 0.92, 0.84])
    fibre = np.array([0.72, 0.62, 0.46])
    albedo = fibre + (paper - fibre) * tone[..., None]
    lit = np.clip(albedo * shade[..., None], 0.0, 1.0)
    tiled = np.tile(lit, (2, 2, 1))
    Image.fromarray(np.round(tiled * 255).astype(np.uint8), "RGB").save(PREVIEW)
