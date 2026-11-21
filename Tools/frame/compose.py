"""Composes a capture of frame_doc.py into the pictures Docs/frame-composition.md shows.

Reads OUT/capture.json, OUT/frames/NNN/ (every map, steps/, screen.png, and closeup/ at the glory frame) and
OUT/light/, and writes OUT/doc/: stills of the glory frame, and an animation over the whole shot of the frame,
of each step of its build-up, of what each step adds, and of each map. Every picture is a file of its own with
no text in it: the document lays them out in Markdown tables and names them there, so each one opens at its
full size. The animations run over the capture's key frames; the video runs over every frame it holds. Needs
Pillow. An animation is written as animated WebP (full colour, about a third of a GIF's size), as a GIF (which
every page host plays; smaller with ffmpeg on the path, which only stores what changed from one frame to the
next), or as both. With ffmpeg there is also one video of everything side by side, composition.mp4: the frame,
its ten steps and every map on one labelled sheet, moving together through the shot.
"""

import glob
import json
import os
import shutil
import subprocess
import tempfile

from PIL import Image, ImageChops, ImageDraw, ImageFont

BACKGROUND = (22, 22, 26)
INK = (240, 240, 240)
NOTE = (170, 180, 200)
GAP = 8
LABEL = 30
UNDER = 16
# How long each captured frame shows, by the part of the shot it belongs to: the flight and the blast a little
# slower than they were, the fuse's four seconds in a few beats.
PHASE_MS = {"flight": 90, "fuse": 320, "blast": 130, "after": 170}
TURN_MS = 140
# The video plays the shot once as fast as it happened, then again this many times slower, to be read. Where a
# capture skipped through the fuse instead of taking its every frame, that skip keeps its own pace both times.
VIDEO_SLOW = 2.5
# The timeline's setting that says how many game frames lie between two captures, for each part of the shot:
# for a capture that did not write the gaps down itself.
PHASE_GAP = {"flight": "flight_every", "fuse": "fuse_every", "blast": "blast_every", "after": "after_every"}
# A gap longer than this many game frames is a skip (the fuse), not motion: the slow pass leaves it as it is.
LONGEST_MOTION_GAP = 8

# A step's own contribution is the difference from the step before, shown this many times brighter: a soft
# shadow or a tint is a few levels out of 255, and would not show otherwise.
ADDED_GAIN = 3
# The maps that only hold something while a ball flies.
LENS_MAPS = ("effects-clearing-mask", "main-effects-map", "hud-effects-map")
# The file types an animation can be written as.
ANIMATIONS = ("webp", "gif")
# How wide the document's pictures are written. A still of the frame keeps the Game view's size, and a step's
# the size it was captured at (half of that). A map is a small texture: it is enlarged by a whole number, so its
# texels stay square blocks, to about this width (a large one is shrunk to its limit).
ANIMATION_WIDTH = 216
FRAME_ANIMATION_WIDTH = 360
MAP_STILL_WIDTH, MAP_STILL_LIMIT = 480, 540
MAP_ANIMATION_WIDTH, MAP_ANIMATION_LIMIT = 200, 270

# The build-up's pictures, in order, and what each adds.
STEPS = [
    ("backdrop", "1 Backdrop"),
    ("specks", "2 + specks"),
    ("shadows", "3 + balloon shadows"),
    ("bodies", "4 + balloon bodies"),
    ("shines", "5 + shines"),
    ("board", "6 + the rest of the board"),
    ("effects", "7 + effects"),
    ("light", "8 + shadows and bounce"),
    ("post", "9 + post-processing"),
]

# The maps the stage writes, in the order the frame makes them, and how each is titled.
MAPS = [
    ("disturbance-field", "Disturbance field"),
    ("scene-light-field", "Light field"),
    ("scene-light-colour", "Light colour"),
    ("background-field", "Background field"),
    ("smoke-field", "Smoke field"),
    ("scene-capture", "Scene capture"),
    ("gi-light-buffer", "GI light buffer"),
    ("effects-clearing-mask", "Lens mask"),
    ("main-effects-map", "Effects map"),
    ("hud-effects-map", "HUD effects map"),
]


def font(size, bold=False):
    for name in (("segoeuib.ttf", "DejaVuSans-Bold.ttf") if bold else ("segoeui.ttf", "DejaVuSans.ttf")):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def fit(image, width, smooth=True):
    """The image at `width` pixels wide; a map's texels stay square blocks with smooth off."""
    height = max(1, round(width * image.height / image.width))
    return image.convert("RGB").resize((width, height), Image.LANCZOS if smooth else Image.NEAREST)


def labelled(image, text, note="", under=""):
    """The image under a line of text: a title, a note beside it, and with `under` a second, smaller line."""
    head = LABEL + (UNDER if under else 0)
    panel = Image.new("RGB", (image.width, image.height + head), BACKGROUND)
    panel.paste(image, (0, head))
    draw = ImageDraw.Draw(panel)
    draw.text((4, 5), text, font=font(15, True), fill=INK)
    if note:
        width = draw.textlength(text, font=font(15, True))
        draw.text((12 + width, 7), note, font=font(13), fill=NOTE)
    if under:
        draw.text((4, LABEL - 4), under, font=font(13), fill=NOTE)
    return panel


def row(panels):
    """Panels side by side, top aligned."""
    width = sum(p.width for p in panels) + GAP * (len(panels) + 1)
    height = max(p.height for p in panels) + 2 * GAP
    sheet = Image.new("RGB", (width, height), BACKGROUND)
    x = GAP
    for panel in panels:
        sheet.paste(panel, (x, GAP))
        x += panel.width + GAP
    return sheet


def stack(rows):
    """Rows one under another."""
    sheet = Image.new("RGB", (max(r.width for r in rows), sum(r.height for r in rows)), BACKGROUND)
    y = 0
    for strip in rows:
        sheet.paste(strip, (0, y))
        y += strip.height
    return sheet


def grid(panels, columns):
    return stack([row(panels[i:i + columns]) for i in range(0, len(panels), columns)])


def save_still(image, path):
    """A still as a JPEG: these are pictures of the game, and a PNG of one is five times the size."""
    image.convert("RGB").save(path, quality=90, optimize=True)
    return path


def enlarged(image, width, limit):
    """A map at about `width` pixels wide, by a whole number of pixels to the texel; one wider than `limit` is
    shrunk to it."""
    if image.width > limit:
        return fit(image, limit)
    scale = max(1, round(width / image.width))
    return image.convert("RGB").resize((image.width * scale, image.height * scale), Image.NEAREST)


def frame_list(folder, names, durations):
    """Writes the list ffmpeg reads the pictures from, each with the time it shows (in ms), and answers its name.

    Each picture is read at 600 a second: ffmpeg reads a picture as one frame of a 25-a-second stream otherwise,
    and then every time in the list is snapped to that 1/25 s grid, which shows a 60-a-second shot as fewer than
    half its frames, each one held. 1/600 s holds a captured frame (1/50 s is 12 of them, 1/60 s is 10) and one
    slowed 2.5 times exactly."""
    lines = ["ffconcat version 1.0"]
    for name, shown in zip(names, durations):
        lines += [f"file '{name}'", "option framerate 600", f"duration {shown / 1000:.6f}"]
    # The concat format drops the last entry's time unless the file is named once more.
    lines += [f"file '{names[-1]}'", "option framerate 600"]
    with open(os.path.join(folder, "frames.txt"), "w", encoding="utf-8", newline=chr(10)) as out:
        out.write("".join(line + chr(10) for line in lines))
    return "frames.txt"


def save_animation(path, frames, durations):
    """A looping animation of the frames, each shown for its own time, as the path's type says."""
    if path.endswith(".webp"):
        frames[0].save(path, save_all=True, append_images=frames[1:], duration=durations, loop=0, quality=75, method=4)
        return path
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        return save_gif_plain(path, frames, durations)
    with tempfile.TemporaryDirectory() as folder:
        names = [f"{index:04d}.png" for index in range(len(frames))]
        for name, frame in zip(names, frames):
            frame.save(os.path.join(folder, name))
        listed = frame_list(folder, names, durations)
        # One palette for all frames, no dithering (flat colours stay flat), and only the rectangle that changed
        # stored for each frame: most of a map stands still from one frame to the next.
        look = ("split[a][b];[a]palettegen=max_colors=200:stats_mode=diff[p];"
                "[b][p]paletteuse=dither=none:diff_mode=rectangle")
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listed,
                        "-vf", look, "-fps_mode", "vfr", "-loop", "0", os.path.abspath(path)], cwd=folder, check=True)
    return path


def save_video(path, sheets, passes):
    """A high-quality H.264 video of the sheets, played once for each list of times in `passes` (the shot at
    its own speed, then slowed). `sheets` may be a generator: a sheet is some megabytes, and they are written out
    one at a time. Returns None without ffmpeg."""
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("no ffmpeg on the path: no video of the whole composition", flush=True)
        return None
    with tempfile.TemporaryDirectory() as folder:
        names = []
        for index, sheet in enumerate(sheets):
            names.append(f"{index:04d}.png")
            # The codec wants even sides.
            even = Image.new("RGB", (sheet.width + sheet.width % 2, sheet.height + sheet.height % 2), BACKGROUND)
            even.paste(sheet, (0, 0))
            even.save(os.path.join(folder, names[-1]))
        listed = frame_list(folder, [name for _ in passes for name in names],
                            [shown for durations in passes for shown in durations])
        # Each sheet is one frame of the video, at its own time: as many a second as the capture stepped where
        # the shot plays at its own speed, and fewer, evenly spaced, where it is slowed. A fixed sixty a second
        # would show some sheets for one frame more than others.
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", listed,
                        "-vf", "format=yuv420p", "-fps_mode", "vfr", "-c:v", "libx264", "-crf", "20",
                        "-preset", "slow", "-video_track_timescale", "600", "-movflags", "+faststart",
                        os.path.abspath(path)], cwd=folder, check=True)
    return path


def save_gif_plain(path, frames, durations):
    """Without ffmpeg: one palette taken from a few of the frames, every frame stored whole."""
    count = min(4, len(frames))
    sample = Image.new("RGB", (frames[0].width, frames[0].height * count))
    for index in range(count):
        sample.paste(frames[round(index * (len(frames) - 1) / max(1, count - 1))], (0, index * frames[0].height))
    palette = sample.quantize(255, method=Image.Quantize.MEDIANCUT)
    quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    quantized[0].save(path, save_all=True, append_images=quantized[1:], duration=durations, loop=0, optimize=False)
    return path


def is_flat(image):
    """Whether every channel is one value all over (to within a step or two): such a channel says nothing."""
    extrema = image.getextrema()
    extrema = [extrema] if isinstance(extrema[0], int) else extrema
    return all(high - low < 4 for low, high in extrema)


def read_map(directory, name):
    """A map's colour and alpha pictures in one frame, or None where the frame has no such map."""
    colour_path = os.path.join(directory, name + "-rgb.png")
    if not os.path.exists(colour_path):
        return None
    return Image.open(colour_path).convert("RGB"), Image.open(os.path.join(directory, name + "-a.png")).convert("L")


class MapLook:
    """How one map is drawn over a run of frames: its size, and whether its alpha is worth a panel. Decided over
    all the frames together, so every frame's panel is the same size and the animation holds still."""

    def __init__(self, name, title, frames, width):
        self.name, self.title, self.width = name, title, width
        seen = [pictures for pictures in (read_map(d, name) for d in frames) if pictures is not None]
        self.shown = any(not is_flat(colour) or not is_flat(alpha) for colour, alpha in seen)
        self.with_alpha = any(not is_flat(alpha) for _, alpha in seen)
        self.size = seen[0][0].size if seen else (1, 1)

    def panel(self, directory, note=""):
        """The map in one frame: its colour over its alpha, under its name, and under that its size in texels and
        the note (where in the shot the frame is). A frame without the map gets it black, with a word."""
        pictures = read_map(directory, self.name)
        colour, alpha = pictures if pictures else (Image.new("RGB", self.size), Image.new("L", self.size))
        said = [f"{self.size[0]} x {self.size[1]}", note, "" if pictures else "not made this frame"]
        top = labelled(fit(colour, self.width, smooth=False), self.title, under="  ·  ".join(t for t in said if t))
        if not self.with_alpha:
            return top
        bottom = labelled(fit(alpha, self.width, smooth=False), "alpha")
        return stack([top, Image.new("RGB", (self.width, GAP), BACKGROUND), bottom])

    def pictures(self, directory, width, limit):
        """The map in one frame with no text: its colour, and its alpha where that says anything (else None).
        A frame without the map gets it black."""
        found = read_map(directory, self.name)
        colour, alpha = found if found else (Image.new("RGB", self.size), Image.new("L", self.size))
        return enlarged(colour, width, limit), enlarged(alpha, width, limit) if self.with_alpha else None

    def holds(self, directory):
        """Whether the map has anything to show in this frame."""
        pictures = read_map(directory, self.name)
        return pictures is not None and not (is_flat(pictures[0]) and is_flat(pictures[1]))


def effects_cover(directory):
    """The share of a frame's effects map that holds something, 0 where it has none."""
    path = os.path.join(directory, "main-effects-map-a.png")
    if not os.path.exists(path):
        return 0.0
    alpha = Image.open(path).convert("L")
    return sum(alpha.histogram()[32:]) / (alpha.width * alpha.height)


def added(before, after, width):
    """What a step adds: the difference between the frame before it and after it, brightened."""
    # Both at the width they are shown at first: the Game view's picture is not the size of the camera's.
    difference = ImageChops.difference(fit(after, width), fit(before, width))
    return difference.point(lambda level: min(255, level * ADDED_GAIN))


def step_cell(folder, index, width, note="", screen=None):
    """One step of the build-up over what it adds. Step `len(STEPS)` is the HUD's: the Game view (`screen`) over
    what it adds to the last picture the main camera makes."""
    def picture(step):
        return Image.open(os.path.join(folder, "steps", STEPS[step][0] + ".png"))

    current = screen if index == len(STEPS) else picture(index)
    title = f"{len(STEPS) + 1} + HUD" if index == len(STEPS) else STEPS[index][1]
    top = labelled(fit(current, width), title, note)
    if index == 0:
        # The first step adds itself.
        bottom = labelled(fit(current, width), "what it adds", "all of it")
    else:
        bottom = labelled(added(picture(index - 1), current, width), "what it adds")
    return stack([top, bottom])


def compose(out_dir, animations=("webp",), video=True):
    """Writes OUT/doc/ and returns the paths written. `animations` names the types each animation is written as;
    `video` adds composition.mp4, everything side by side."""
    doc = os.path.join(out_dir, "doc")
    os.makedirs(doc, exist_ok=True)
    with open(os.path.join(out_dir, "capture.json"), encoding="utf-8") as handle:
        info = json.load(handle)
    taken = info["frames"]
    every = [os.path.join(out_dir, "frames", f"{index:03d}") for index in range(len(taken))]
    if not every:
        raise SystemExit(f"no captured frames under {os.path.join(out_dir, 'frames')}: capture first")
    glory = info["glory"]
    glory_dir = every[glory]
    # The animations show the key frames (each part of the shot at its own cadence); a capture made for a smooth
    # video holds the frames between them too.
    keys = [index for index, frame in enumerate(taken) if frame.get("key", True)]
    frames = [every[index] for index in keys]
    durations = [PHASE_MS.get(taken[index]["phase"], 120) for index in keys]
    written = []

    def keep(name):
        written.append(os.path.join(doc, name))
        return written[-1]

    def animate(name, pictures, shown):
        for kind in animations:
            save_animation(keep(f"{name}.{kind}"), pictures, shown)

    def screen(directory):
        return Image.open(os.path.join(directory, "screen.png")).convert("RGB")

    def step(directory, index):
        """A step's picture: the Game view for the HUD's, which is the last."""
        if index == len(STEPS):
            return screen(directory)
        return Image.open(os.path.join(directory, "steps", STEPS[index][0] + ".png")).convert("RGB")

    names = [name for name, _ in STEPS] + ["hud"]

    # The frame itself, the Game view: the glory frame, and the whole shot.
    save_still(screen(glory_dir), keep("frame.jpg"))
    animate("frame", [fit(screen(d), FRAME_ANIMATION_WIDTH) for d in frames], durations)

    # The build-up: each step and what it adds (the first adds itself, so it has no such picture), at the glory
    # frame and over the whole shot. The HUD's step is the frame itself, which is written above.
    for index, name in enumerate(names):
        if name != "hud":
            save_still(step(glory_dir, index), keep(f"step-{name}.jpg"))
            animate(f"step-{name}", [fit(step(d, index), ANIMATION_WIDTH) for d in frames], durations)
        if index:
            wide = step(glory_dir, index - 1).width
            save_still(added(step(glory_dir, index - 1), step(glory_dir, index), wide), keep(f"added-{name}.jpg"))
            animate(f"added-{name}", [added(step(d, index - 1), step(d, index), ANIMATION_WIDTH) for d in frames],
                    durations)

    # Every map: at the glory frame and over the whole shot, its alpha beside it where that says anything.
    looks = [MapLook(name, title, frames, 200) for name, title in MAPS]
    looks = {look.name: look for look in looks if look.shown}

    def map_stills(look, directory, name):
        colour, alpha = look.pictures(directory, MAP_STILL_WIDTH, MAP_STILL_LIMIT)
        save_still(colour, keep(f"{name}.jpg"))
        if alpha is not None:
            save_still(alpha, keep(f"{name}-alpha.jpg"))

    for look in looks.values():
        # The lens's maps have their stills from a frame of the flight, below: the glory frame has no ball in it.
        if look.holds(glory_dir) and look.name not in LENS_MAPS:
            map_stills(look, glory_dir, f"map-{look.name}")
        moving = [look.pictures(d, MAP_ANIMATION_WIDTH, MAP_ANIMATION_LIMIT) for d in frames]
        animate(f"map-{look.name}", [colour for colour, _ in moving], durations)
        if look.with_alpha:
            animate(f"map-{look.name}-alpha", [alpha.convert("RGB") for _, alpha in moving], durations)

    # Everything side by side through the shot, as one video: the frame, its steps, and under them the maps.
    if video:
        def sheets():
            # The maps in a row along the bottom set the sheet's width. Above them, the steps in five columns,
            # and the frame beside them as tall as they are, so the two fill that width between them: a step's
            # width follows from that (a cell is a step over what it adds, each 16:9 under a label).
            maps_width = len(looks) * (200 + GAP) + GAP
            step_width = max(160, (maps_width - 130) // 9)
            for directory, frame in zip(every, taken):
                view = screen(directory)
                steps = grid([step_cell(directory, step, step_width, screen=view) for step in range(len(STEPS) + 1)], 5)
                tall = steps.height - LABEL - 2 * GAP
                large = view.resize((round(tall * view.width / view.height), tall), Image.LANCZOS)
                yield stack([row([labelled(large, "The frame", frame["label"]), steps]),
                             row([look.panel(directory) for look in looks.values()])])

        # The times between captures as they were in the game: a frame is as long as the capture stepped (1/60 s
        # for one made before it wrote that down).
        plan = info.get("setup", {}).get("timeline", {})
        since = [frame["gap"] if frame.get("gap") is not None else plan.get(PHASE_GAP.get(frame["phase"], ""), 4)
                 for frame in taken]
        # A capture writes down the game frames since the one before it; a sheet shows until the next one.
        gaps = since[1:] + since[-1:]
        frame_ms = 1000 * info.get("frame_seconds", 1 / 60)
        real = [frame_ms * max(1, gap) for gap in gaps]
        slow = [shown * (VIDEO_SLOW if gap <= LONGEST_MOTION_GAP else 1) for shown, gap in zip(real, gaps)]
        path = save_video(os.path.join(doc, "composition.mp4"), sheets(), [real, slow])
        if path:
            written.append(path)

    # The lens, from the frame of the flight with the most under it: without the effects, the mask the ball
    # stamps, the effects drawn into their own map, the effects put back through the mask, and the same on the
    # HUD's side, which has a map of its own for the score shapes and trails.
    flight = max(frames, key=effects_cover)
    if effects_cover(flight) > 0 and "effects-clearing-mask" in looks and "main-effects-map" in looks:
        save_still(screen(flight), keep("flight.jpg"))
        save_still(step(flight, names.index("board")), keep("lens-board.jpg"))
        save_still(step(flight, names.index("effects")), keep("lens-effects.jpg"))
        map_stills(looks["effects-clearing-mask"], flight, "lens-mask")
        map_stills(looks["main-effects-map"], flight, "lens-effects-map")
        if "hud-effects-map" in looks and looks["hud-effects-map"].holds(flight):
            map_stills(looks["hud-effects-map"], flight, "lens-hud-map")

    # The corner of the glory frame the setup names, built up close.
    if os.path.isdir(os.path.join(glory_dir, "closeup")):
        for name in ["flat"] + [name for name, _ in STEPS]:
            save_still(Image.open(os.path.join(glory_dir, "closeup", name + ".png")), keep(f"balloon-{name}.jpg"))

    # One balloon as the light goes round, and its build-up at the four named times of day.
    angles = sorted(glob.glob(os.path.join(out_dir, "light", "angle-*.png")))
    if angles:
        animate("balloon-turning", [Image.open(angle).convert("RGB") for angle in angles], [TURN_MS] * len(angles))
    for time in ("dawn", "day", "dusk", "night"):
        if os.path.isdir(os.path.join(out_dir, "light", time)):
            for part in ("flat", "shadows", "bodies", "shines", "post"):
                save_still(Image.open(os.path.join(out_dir, "light", time, part + ".png")),
                           keep(f"balloon-{time}-{part}.jpg"))
    return written
