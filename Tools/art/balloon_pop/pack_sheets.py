"""Packs the frames bake_pops.py rendered into the game's pop sheets, and draws a contact sheet and
an animated preview to review them by.

The frames hold the rubber's surface, not a picture (see bake_pops.py): red and green the normal's x
and y as the camera sees it, blue the ambient occlusion, alpha the coverage. Each frame comes in two
layers, the rubber in front of the balloon's centre and the rubber behind it, so the game can put the
card's slip between them. The game lights them with the scene's light (BalloonParty/Sprite/LitFlipbook);
the review files light them here with the same model.

For each pop under OUT/frames/pop_NN/:
- OUT/sheets/pop_NN.png      the game's sheet, 1536x2048: the front layer's 24 frames of 256x256 in the
                             top half (6 columns by 4 rows, read left to right, top to bottom), the back
                             layer's in the bottom half in the same order. Import it as linear data
- OUT/review/pop_NN.png      the contact sheet: the frames large and numbered, lit from the top left,
                             tinted a balloon red, with a stand-in slip between the layers, on paper
- OUT/review/pop_NN.gif      the pop playing at 30 fps under three lights side by side: the day's sun
                             from the top left, a light from the right, and a low orange dusk

Usage: python3 Tools/art/balloon_pop/pack_sheets.py [OUT]   (OUT defaults to out/ beside this script)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

COLUMNS = 6
ROWS = 4
FRAMES = COLUMNS * ROWS
FRAME = 256
FPS = 30
TINT = (226, 72, 84)
PAPER = (244, 238, 226)
SLIP = (250, 246, 236)
HOLD_FRAMES = 15
LAYERS = ("front", "back")

# The lighting model, as BalloonParty/Sprite/LitFlipbook has it (its material's defaults).
LIGHT_HEIGHT = 0.7
AMBIENT = 0.55
WRAP = 0.4
OCCLUSION = 0.8
TRANSMISSION = 0.9
TRANSMISSION_FOCUS = 1.5
SHININESS = 48.0
SPECULAR = 0.55

# How much of the slip the intact front rubber lets through: it shows inside as a faint shadow.
FRONT_OPACITY = 0.88

# (toward-light direction on screen, light colour): the review's three lights.
DAY = ((-0.6, 0.8), (1.0, 1.0, 1.0))
RIGHT = ((1.0, 0.15), (1.0, 1.0, 1.0))
DUSK = ((0.9, -0.1), (1.0, 0.62, 0.38))


def scaled(frame, size):
    # Premultiplied while resampling, so the transparent edge doesn't pick up the background's values.
    return frame.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")


def lit(frame, light):
    """Lights a frame of surface data: its colour (not premultiplied) and its coverage."""
    (dx, dy), colour = light
    a = np.asarray(frame, dtype=np.float32) / 255.0
    nx = a[..., 0] * 2.0 - 1.0
    ny = a[..., 1] * 2.0 - 1.0
    nz = np.sqrt(np.clip(1.0 - nx * nx - ny * ny, 0.0, 1.0))
    occlusion = 1.0 - OCCLUSION * (1.0 - a[..., 2])

    to_light = np.array([dx, dy, LIGHT_HEIGHT], dtype=np.float32)
    to_light /= np.linalg.norm(to_light)
    half = to_light + np.array([0.0, 0.0, 1.0], dtype=np.float32)
    half /= np.linalg.norm(half)
    n_dot_l = nx * to_light[0] + ny * to_light[1] + nz * to_light[2]
    n_dot_h = nx * half[0] + ny * half[1] + nz * half[2]
    diffuse = np.clip((n_dot_l + WRAP) / (1.0 + WRAP), 0.0, 1.0)
    specular = np.clip(n_dot_h, 0.0, 1.0) ** SHININESS * SPECULAR * occlusion
    through = np.clip(-n_dot_l, 0.0, 1.0) ** TRANSMISSION_FOCUS * TRANSMISSION * occlusion

    light = np.array(colour, dtype=np.float32)
    tint = np.array(TINT, dtype=np.float32) / 255.0
    shade = (AMBIENT + (1.0 - AMBIENT) * diffuse) * occlusion
    body = tint * shade[..., None] + tint * tint * through[..., None]
    rgb = body * light + light * specular[..., None]
    # As the shader caps it: scaled down, hue kept, until no channel passes 1.
    rgb = rgb / np.maximum(rgb.max(axis=-1, keepdims=True), 1.0)
    return rgb, a[..., 3:4]


def slip(size, font):
    """A stand-in for the card's slip until the real one is baked: a paper strip with a card's name."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    w, h = size * 0.42, size * 0.11
    cx, cy = size * 0.5, size * 0.4
    draw.rounded_rectangle((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), radius=h * 0.2, fill=SLIP + (255,),
                           outline=(214, 204, 186, 255))
    draw.text((cx, cy), "Shield", fill=(70, 66, 60, 255), font=font, anchor="mm")
    a = np.asarray(layer, dtype=np.float32) / 255.0
    return a[..., :3], a[..., 3:4]


def composite(front, back, light, card):
    """Back rubber, the slip inside, then the front rubber over it, on paper."""
    out = np.broadcast_to(np.array(PAPER, dtype=np.float32) / 255.0, (back.height, back.width, 3)).copy()
    for rgb, alpha in (lit(back, light), card):
        out = rgb * alpha + out * (1.0 - alpha)
    rgb, alpha = lit(front, light)
    alpha = alpha * FRONT_OPACITY
    out = rgb * alpha + out * (1.0 - alpha)
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def load(pop_dir, layer):
    return [Image.open(pop_dir / f"{i:02d}_{layer}.png").convert("RGBA") for i in range(FRAMES)]


def pack(pop_dir, out):
    if not all((pop_dir / f"{i:02d}_{layer}.png").exists() for i in range(FRAMES) for layer in LAYERS):
        print(f"{pop_dir.name}: missing frames (expected {FRAMES} per layer); skipped")
        return
    front, back = load(pop_dir, "front"), load(pop_dir, "back")

    sheet = Image.new("RGBA", (COLUMNS * FRAME, 2 * ROWS * FRAME), (0, 0, 0, 0))
    for half, frames in enumerate((front, back)):
        for i, frame in enumerate(frames):
            x, y = (i % COLUMNS) * FRAME, (half * ROWS + i // COLUMNS) * FRAME
            sheet.paste(scaled(frame, FRAME), (x, y))
    (out / "sheets").mkdir(parents=True, exist_ok=True)
    sheet.save(out / "sheets" / f"{pop_dir.name}.png")

    size = front[0].width
    font = ImageFont.load_default(size=max(14, size // 20))
    card = slip(size, ImageFont.load_default(size=max(12, size // 22)))
    contact = Image.new("RGB", (COLUMNS * size, ROWS * size), PAPER)
    draw = ImageDraw.Draw(contact)
    for i in range(FRAMES):
        x, y = (i % COLUMNS) * size, (i // COLUMNS) * size
        contact.paste(composite(front[i], back[i], DAY, card), (x, y))
        draw.rectangle((x, y, x + size - 1, y + size - 1), outline=(205, 196, 180))
        draw.text((x + 8, y + 6), f"{i:02d}", fill=(120, 110, 96), font=font)
    (out / "review").mkdir(parents=True, exist_ok=True)
    contact.save(out / "review" / f"{pop_dir.name}.png")

    lights = (DAY, RIGHT, DUSK)
    panels = []
    for i in range(FRAMES):
        panel = Image.new("RGB", (size * len(lights), size), PAPER)
        for j, light in enumerate(lights):
            panel.paste(composite(front[i], back[i], light, card), (j * size, 0))
        panels.append(panel)
    gif = panels + [panels[-1]] * HOLD_FRAMES
    gif[0].save(out / "review" / f"{pop_dir.name}.gif", save_all=True, append_images=gif[1:],
                duration=round(1000 / FPS), loop=0)
    print(f"{pop_dir.name}: sheet, contact sheet and preview written")


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent / "out"
    for pop_dir in sorted((out / "frames").glob("pop_*")):
        pack(pop_dir, out)


main()
