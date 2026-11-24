"""Lays a capture of Tools/gi/gi_doc.py out as the pictures Docs/gi-light-buffer.md shows.

Every picture is a file of its own with no text in it, except the two plots: the page's tables name them. A crop
of the board is the square the capture names; a texture's crop is enlarged by whole texels, so they stay square.
"""

import json
import math
import os

import numpy as np
from PIL import Image

# The shader's tap counts (ScreenSpaceLightSmear.shader): the Editor and a desktop, and a phone (_LOW_QUALITY_SMEAR).
TAPS, PHONE_TAPS = 8, 5
# How much brighter "what the light does" is drawn than it is, as the frame page's "what it adds" is.
LIGHT_GAIN = 3
SIDE = 192
MIP_LEVELS = 6
# The settings each picture row shows: the pass it comes from, the buffer channels it moves, and the square it is
# shown in. The ring's glow lifts the sky round it through the bounce, so what moves the shadow is shown on the
# balloons above it, where no local light reaches.
KNOBS = {
    "shadowMipSpread": ("shadow-alone", ("shadow",), "shadow"),
    "smearDistance": ("shadow-alone", ("shadow", "bounce"), "shadow"),
    "tapDecay": ("shadow-alone", ("shadow", "bounce"), "shadow"),
    "tapStart": ("shadow-alone", ("shadow", "bounce"), "shadow"),
    "shadowStrength": ("shadow-alone", (), "shadow"),
    "mipSpread": ("as-is", ("bounce",), "ring"),
    "secondaryBounceWeight": ("as-is", ("bounce",), "ring"),
    "bounceStrength": ("as-is", (), "ring"),
}

SURFACE, INK, INK2, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#e4e3df"
BOUNCE, SHADOW = "#2a78d6", "#eb6834"


class Layout:
    def __init__(self, root):
        with open(os.path.join(root, "capture.json"), encoding="utf-8") as handle:
            capture = json.load(handle)
        self.root = root
        self.out = os.path.join(root, "doc")
        self.view = capture["view"]
        self.view_h = self.view["ortho"] * 2
        self.view_w = self.view_h * self.view["aspect"]
        self.squares = {"ring": capture["crop"], "shadow": capture["shadow_crop"]}
        self.settings = capture["settings"]
        self.variants = capture["variants"]
        self.tap_texel = capture["tap_texel"]
        self.written = []

    def box(self, width, height, square):
        crop = self.squares[square]
        half = crop["size"] / 2
        left = (crop["x"] - half + self.view_w / 2) / self.view_w * width
        right = (crop["x"] + half + self.view_w / 2) / self.view_w * width
        top = (self.view_h / 2 - (crop["y"] + half)) / self.view_h * height
        bottom = (self.view_h / 2 - (crop["y"] - half)) / self.view_h * height
        return round(left), round(top), round(right), round(bottom)

    def cropped(self, path, grey=False, nearest=True, gain=None, side=SIDE, square="ring"):
        picture = Image.open(path).convert("RGB")
        picture = picture.crop(self.box(*picture.size, square))
        if grey:
            # A map's alpha, and a field's density, are written as grey pictures: their red is the value.
            picture = picture.getchannel("R").convert("RGB")
        if gain:
            values = np.asarray(picture).astype(float) / 255.0
            values = np.clip(0.5 + (values - 0.5) * gain, 0, 1)
            picture = Image.fromarray((values * 255).round().astype(np.uint8))
        return picture.resize((side, side), Image.NEAREST if nearest else Image.LANCZOS)

    def save(self, picture, name):
        path = os.path.join(self.out, name)
        if name.endswith(".jpg"):
            picture.save(path, quality=88)
        else:
            picture.save(path, optimize=True)
        self.written.append(path)

    def folder(self, *parts):
        return os.path.join(self.root, *parts)


def compose(root):
    layout = Layout(root)
    os.makedirs(layout.out, exist_ok=True)
    lead(layout)
    mips(layout)
    masks(layout)
    knobs(layout)
    plots(layout)
    return layout.written


def lead(layout):
    game = layout.folder("as-is", "game")
    for name, folder in (("frame-lit.jpg", game), ("frame-unlit.jpg", layout.folder("as-is", "unlit"))):
        picture = Image.open(os.path.join(folder, "crop.png")).convert("RGB")
        layout.save(picture.resize((SIDE * 2, SIDE * 2), Image.LANCZOS), name)
    layout.save(layout.cropped(os.path.join(game, "light.png"), nearest=False, gain=LIGHT_GAIN, side=SIDE * 2),
                "light.jpg")
    # The light at the game's settings round the ring, and above it where no local light reaches.
    for square in ("ring", "shadow"):
        layout.save(layout.cropped(os.path.join(game, "light.png"), nearest=False, gain=LIGHT_GAIN, square=square),
                    f"light-{square}.jpg")
    layout.save(layout.cropped(os.path.join(game, "gi-light-buffer-rgb.png"), side=SIDE * 2), "buffer-bounce.png")
    layout.save(layout.cropped(os.path.join(game, "gi-light-buffer-a.png"), grey=True, side=SIDE * 2),
                "buffer-shadow.png")


def mips(layout):
    """The capture's mip chain, each level half the one before and every texel the average of four, as the
    graphics card builds it; drawn at the first level's size."""
    game = layout.folder("as-is", "game")
    colour = np.asarray(Image.open(os.path.join(game, "scene-capture-rgb.png")).convert("RGB")).astype(float)
    coverage = np.asarray(Image.open(os.path.join(game, "scene-capture-a.png")).convert("RGB")).astype(float)[..., 0]
    for level in range(MIP_LEVELS):
        height, width = coverage.shape
        scale = 2 ** level
        for values, suffix in ((colour, ""), (coverage, "-coverage")):
            picture = Image.fromarray(values.round().astype(np.uint8))
            layout.save(picture.resize((width * scale, height * scale), Image.NEAREST), f"mip-{level}{suffix}.png")
        height, width = height // 2, width // 2
        colour = colour[:height * 2, :width * 2].reshape(height, 2, width, 2, 3).mean(axis=(1, 3))
        coverage = coverage[:height * 2, :width * 2].reshape(height, 2, width, 2).mean(axis=(1, 3))


def masks(layout):
    game = layout.folder("as-is", "game")
    gate = layout.variants["cloudShadowGate"]
    shadow = "gi-light-buffer-a.png"
    for name, path in (
            ("mask-coverage.png", os.path.join(game, "scene-capture-a.png")),
            ("mask-shadow-gate-low.png", layout.folder("as-is", f"cloudShadowGate-{gate[0]:g}", shadow)),
            ("mask-clouds.png", os.path.join(game, "background-field-rgb.png")),
            ("mask-shadow-gate-game.png", os.path.join(game, shadow)),
            ("mask-shadow-gate-high.png", layout.folder("as-is", f"cloudShadowGate-{gate[1]:g}", shadow))):
        layout.save(layout.cropped(path, grey=True, square="shadow"), name)


def knobs(layout):
    for knob, (run, channels, square) in KNOBS.items():
        low, high = layout.variants[knob]
        for value, folder in ((f"{low:g}", f"{knob}-{low:g}"), ("game", "game"), (f"{high:g}", f"{knob}-{high:g}")):
            pictures(layout, layout.folder(run, folder), f"{knob}-{value}", channels, square)
    for tag, folder in (("phone", "phone"), ("editor", "game")):
        pictures(layout, layout.folder("as-is", folder), tag, ("shadow", "bounce"), "shadow")


def pictures(layout, folder, tag, channels, square):
    layout.save(layout.cropped(os.path.join(folder, "light.png"), nearest=False, gain=LIGHT_GAIN, square=square),
                f"{tag}-light.jpg")
    if "shadow" in channels:
        layout.save(layout.cropped(os.path.join(folder, "gi-light-buffer-a.png"), grey=True, square=square),
                    f"{tag}-shadow.png")
    if "bounce" in channels:
        layout.save(layout.cropped(os.path.join(folder, "gi-light-buffer-rgb.png"), square=square),
                    f"{tag}-bounce.png")


def plots(layout):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Rectangle

    settings = layout.settings
    capture = Image.open(layout.folder("as-is", "game", "scene-capture-rgb.png")).convert("RGB")
    texel = layout.view_h / capture.size[1]
    max_mip = math.log2(max(capture.size))
    taps = np.arange(TAPS)
    distance = (settings["_tapStart"] + taps) * settings["_smearDistance"] / TAPS
    weight = settings["_tapDecay"] ** taps
    bounce_mip = np.minimum(settings["_mipSpread"] * np.log2(1 + taps), max_mip)
    shadow_mip = np.minimum(settings["_shadowMipSpread"] * np.log2(1 + taps), max_mip)

    plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 10, "text.color": INK, "axes.labelcolor": INK2,
                         "xtick.color": INK2, "ytick.color": INK2, "axes.edgecolor": GRID})

    def style(ax):
        ax.set_facecolor(SURFACE)
        ax.grid(True, color=GRID, linewidth=0.8)
        ax.set_axisbelow(True)
        for side in ("top", "right"):
            ax.spines[side].set_visible(False)

    # The cone seen from the side, each tap the square of the capture it averages, and the mip level each reads.
    fig, (side, levels) = plt.subplots(1, 2, figsize=(11, 4.2), dpi=150, gridspec_kw={"width_ratios": [1.35, 1]})
    fig.patch.set_facecolor(SURFACE)
    style(side)
    for mip, colour, filled in ((bounce_mip, BOUNCE, True), (shadow_mip, SHADOW, False)):
        width = 2 ** mip * texel
        for tap in taps:
            alpha = 0.15 + 0.55 * weight[tap]
            side.add_patch(Rectangle((distance[tap] - width[tap] / 2, -width[tap] / 2), width[tap], width[tap],
                                     facecolor=colour if filled else "none", edgecolor=colour,
                                     alpha=alpha if filled else min(1.0, alpha + 0.3), linewidth=0 if filled else 2))
        side.plot(distance, width / 2, color=colour, linewidth=2)
        side.plot(distance, -width / 2, color=colour, linewidth=2)
    reach = max(distance[-1] + 2 ** bounce_mip[-1] * texel / 2, distance[-1] + 0.1)
    half = max(2 ** bounce_mip[-1] * texel / 2 + 0.06, 0.15)
    side.plot([0], [0], marker="o", markersize=8, color=INK)
    side.annotate("the texel", (0, 0), xytext=(0.02, half * 0.55), color=INK2, fontsize=9)
    phone_end = (distance[PHONE_TAPS - 1] + distance[PHONE_TAPS]) / 2
    side.axvline(phone_end, color=INK2, linewidth=1, linestyle=(0, (4, 3)))
    side.text(phone_end + 0.01, -half * 0.9, f"a phone stops here\n({PHONE_TAPS} taps)", color=INK2, fontsize=8.5,
              va="bottom")
    side.text(distance[-1], 2 ** bounce_mip[-1] * texel / 2 + 0.02, "bounce", color=BOUNCE, ha="center")
    side.text(distance[-1], 2 ** shadow_mip[-1] * texel / 2 + 0.02, "shadow", color=SHADOW, ha="center")
    side.set_xlim(-0.05, reach + 0.05)
    side.set_ylim(-half, half)
    side.set_aspect("equal")
    side.set_xlabel("along the march, world units")
    side.set_ylabel("across it, world units")
    side.set_title("Each tap's footprint: the square of the capture it averages", loc="left", fontsize=11, color=INK)

    style(levels)
    levels.plot(taps + 1, bounce_mip, color=BOUNCE, linewidth=2, marker="o", markersize=8, label="bounce")
    levels.plot(taps + 1, shadow_mip, color=SHADOW, linewidth=2, marker="o", markersize=8, label="shadow")
    levels.text(TAPS + 0.2, bounce_mip[-1], f"bounce\nmip {bounce_mip[-1]:.2f}", color=INK, va="center", fontsize=9)
    levels.text(TAPS + 0.2, shadow_mip[-1], f"shadow\nmip {shadow_mip[-1]:.2f}", color=INK, va="center", fontsize=9)
    levels.set_xticks(taps + 1)
    levels.set_xlim(0.5, TAPS + 1.6)
    levels.set_ylim(-0.2, max(bounce_mip[-1], shadow_mip[-1]) + 0.45)
    levels.set_xlabel("tap")
    levels.set_ylabel("mip level read")
    levels.legend(loc="upper left", frameon=False)
    levels.set_title("Mip level = spread x log2(tap)", loc="left", fontsize=11, color=INK)
    fig.tight_layout()
    path = os.path.join(layout.out, "cone.png")
    fig.savefig(path, facecolor=SURFACE)
    plt.close(fig)
    layout.written.append(path)

    # Where the taps land on the capture for one texel, along the light's direction where the evening light is
    # untouched by any local one.
    x, y = layout.tap_texel
    toward = np.array(layout.view["toward_light"], dtype=float)
    toward /= np.linalg.norm(toward)
    away = -toward
    fig, axes = plt.subplots(1, 2, figsize=(10, 5.2), dpi=150)
    fig.patch.set_facecolor(SURFACE)
    extent = (-layout.view_w / 2, layout.view_w / 2, -layout.view_h / 2, layout.view_h / 2)
    titles = ("Shadow: one march, toward the light", "Bounce: four marches, the first away from it")
    for ax, title in zip(axes, titles):
        ax.imshow(capture, extent=extent, interpolation="nearest")
        ax.set_xlim(x - 1.0, x + 1.0)
        ax.set_ylim(y - 1.0, y + 1.0)
        ax.set_xticks([])
        ax.set_yticks([])
        ax.set_title(title, loc="left", fontsize=11, color=INK)
        ax.plot([x], [y], marker="o", markersize=7, color="white", markeredgecolor=INK)

    def march(ax, direction, mip, colour, strength):
        for tap in taps:
            width = 2 ** mip[tap] * texel
            cx, cy = x + direction[0] * distance[tap], y + direction[1] * distance[tap]
            ax.add_patch(Rectangle((cx - width / 2, cy - width / 2), width, width, facecolor="none", edgecolor=colour,
                                   linewidth=2, alpha=(0.35 + 0.65 * weight[tap]) * strength))

    march(axes[0], toward, shadow_mip, SHADOW, 1.0)
    head = np.array([x, y]) + toward * 0.95
    axes[0].annotate("", xy=head, xytext=head - toward * 0.18,
                     arrowprops=dict(arrowstyle="-|>", color="white", linewidth=2))
    # Beside the arrow, a little to its side, clear of the taps.
    label = head + np.array([-toward[1], toward[0]]) * 0.2
    axes[0].text(label[0], label[1], "to the light", color="white", fontsize=9, ha="center", va="center")
    directions = [away, np.array([-away[1], away[0]]), -away, np.array([away[1], -away[0]])]
    for index, direction in enumerate(directions):
        march(axes[1], direction, bounce_mip, BOUNCE if index == 0 else "white", 1.0 if index == 0 else 0.8)
    fig.tight_layout()
    path = os.path.join(layout.out, "taps.png")
    fig.savefig(path, facecolor=SURFACE)
    plt.close(fig)
    layout.written.append(path)
