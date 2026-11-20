"""Retakes the README's pictures: drives the open Unity Editor through the Unity CLI to play a fixed script
(Assets/Source/Editor/ReadmeShotCapture.cs) and composes the shots into the grids the README shows.

    python3 Tools/readme_shots/capture.py            # capture, then compose into Docs/readme/
    python3 Tools/readme_shots/capture.py --compose  # compose the last capture again, no Editor needed
    python3 Tools/readme_shots/capture.py --burst    # items at a run of moments, to retune their timings

The Editor must be open on this project (the main checkout: it only sees committed, merged code) and not
in Play mode; the script enters Play mode itself and leaves it when done. The raw shots land in
Logs/readme_shots/ (git-ignored), the grids in Docs/readme/.

A scenario's still is the best moment of a level a bot plays, the one with the most balloons popping at
once; the driver lists the moments it took in moments.txt, beside the raw shots. The gift balloons are
filmed for a few seconds and composed into one looping GIF.
"""
import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "Logs" / "readme_shots"
OUT = ROOT / "Docs" / "readme"
FONT = ROOT / "Assets" / "Fonts" / "PatrickHand-Regular.ttf"
DONE = "done.txt"
MOMENTS = "moments.txt"
DRIVER = "BalloonParty.Editor.ReadmeShotCapture, BalloonParty.Editor"

SCENARIOS = (("sky", "Sky"), ("meadow", "Meadow"), ("water", "Water"), ("bonus", "Bonus level"))
TIMES = (("morning", "Morning"), ("night", "Night"))
ITEMS = (("bomb", "Bomb"), ("laser", "Laser"), ("lightning", "Lightning"),
         ("paint", "Paint"), ("shield", "Shield"), ("snipe", "Snipe"))
GIFTS = (("golden", "Golden"), ("heart", "Heart"), ("disco", "Disco ball"), ("nuke", "Nuke"))

TILE = (540, 960)
GAP = 12
LABEL = 64
BACKGROUND = (24, 28, 40)
INK = (240, 236, 226)
QUALITY = 88
LAUNCH_SECONDS = 180
CAPTURE_SECONDS = 1800

# The gifts' GIF: the driver films a picture every 0.06 s of game time (3 physics steps), and the last
# GIFT_FADE of them melt into the first ones, so the loop has no jump.
GIFT_TILE = (240, 240)
GIFT_FRAME_MS = 60
GIFT_FADE = 8
GIFT_COLOURS = 256


def unity(code):
    """Runs C# in the Editor and returns its result."""
    done = subprocess.run(["unity", "command", "eval", "--code", code, "--result-only"],
                          capture_output=True, text=True, shell=sys.platform == "win32")
    try:
        reply = json.loads(done.stdout)
    except json.JSONDecodeError:
        sys.exit(f"The Unity CLI didn't answer (is the Editor open on this project?):\n{done.stdout}{done.stderr}")
    if not reply.get("success"):
        sys.exit(f"The Editor couldn't run it:\n{json.dumps(reply, indent=2)}")
    return reply.get("result")


def wait_for(check, seconds, what):
    deadline = time.time() + seconds
    while time.time() < deadline:
        if check():
            return
        time.sleep(3)
    sys.exit(f"Gave up waiting for {what} after {seconds} s")


def capture(burst):
    if str(unity("return UnityEditor.EditorApplication.isPlaying.ToString();")) == "True":
        sys.exit("The Editor is in Play mode; stop it first (this script won't interrupt a session).")
    RAW.mkdir(parents=True, exist_ok=True)
    for old in RAW.glob("*.png"):
        old.unlink()
    (RAW / DONE).unlink(missing_ok=True)
    (RAW / MOMENTS).unlink(missing_ok=True)

    unity("UnityEditor.EditorApplication.isPlaying = true; return \"on\";")
    # The driver waits for the launch screen itself and taps Play: the game scene loads only after that. In the
    # background an Editor plays no frames until told to, so no frame count can be waited on here.
    wait_for(lambda: str(unity("UnityEngine.Application.runInBackground = true; "
                               "return UnityEditor.EditorApplication.isPlaying.ToString();")) == "True",
             LAUNCH_SECONDS, "Play mode")
    raw = str(RAW).replace("\\", "/")
    started = unity(f"return System.Type.GetType(\"{DRIVER}\").GetMethod(\"Run\", "
                    "System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)"
                    f".Invoke(null, new object[] {{ \"{raw}\", {str(burst).lower()} }});")
    print(f"Capturing: {started}")
    try:
        wait_for(lambda: (RAW / DONE).exists(), CAPTURE_SECONDS, "the shots")
    finally:
        unity("UnityEditor.EditorApplication.isPlaying = false; return \"off\";")
    outcome = (RAW / DONE).read_text().strip()
    if outcome != "ok":
        sys.exit(f"The capture stopped: {outcome}")
    print(f"{len(list(RAW.glob('*.png')))} shots in {RAW}")
    if (RAW / MOMENTS).exists():
        print("The stills the bot's play gave, the last of each level kept:")
        print((RAW / MOMENTS).read_text().rstrip())


def sheet_of(rows, columns, cell, column_labels, row_labels, tile):
    """Tiles the pictures cell(row, column) gives into a grid under column labels, each row headed by its label."""
    font = ImageFont.truetype(str(FONT), 44)
    side = LABEL * 3 if row_labels else 0
    top = LABEL if column_labels else 0
    width = side + columns * tile[0] + (columns + 1) * GAP
    height = top + rows * (tile[1] + (0 if column_labels else LABEL)) + (rows + 1) * GAP
    sheet = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    for c in range(columns):
        x = side + GAP + c * (tile[0] + GAP)
        if column_labels:
            draw.text((x + tile[0] / 2, top / 2 + GAP / 2), column_labels[c], fill=INK, font=font, anchor="mm")
    for r in range(rows):
        y = top + GAP + r * (tile[1] + GAP + (0 if column_labels else LABEL))
        if row_labels:
            draw.text((side / 2 + GAP / 2, y + tile[1] / 2), row_labels[r], fill=INK, font=font, anchor="mm")
        for c in range(columns):
            x = side + GAP + c * (tile[0] + GAP)
            picture, label = cell(r, c)
            sheet.paste(picture.convert("RGB").resize(tile, Image.LANCZOS), (x, y))
            if label:
                draw.text((x + tile[0] / 2, y + tile[1] + LABEL / 2), label, fill=INK, font=font, anchor="mm")
    return sheet


def grid(rows, columns, cell, column_labels, row_labels, out):
    """A grid of the shots whose paths cell(row, column) gives, as a JPEG."""
    sheet = sheet_of(rows, columns, lambda r, c: (Image.open(cell(r, c)[0]), cell(r, c)[1]),
                     column_labels, row_labels, TILE)
    OUT.mkdir(parents=True, exist_ok=True)
    sheet.save(out, quality=QUALITY, optimize=True, progressive=True)
    print(f"{out.relative_to(ROOT)}: {sheet.size[0]}x{sheet.size[1]}")


def gift_film(gift, time_of_day):
    """One gift's pictures at one time of day, the loop closed: its last few melted into its first."""
    paths = sorted(RAW.glob(f"gift_{gift}_{time_of_day}_*.png"))
    if len(paths) <= GIFT_FADE * 2:
        sys.exit(f"Missing shots (capture first): gift_{gift}_{time_of_day}_NN.png")
    pictures = [Image.open(path).convert("RGB").resize(GIFT_TILE, Image.LANCZOS) for path in paths]
    kept = len(pictures) - GIFT_FADE
    return [Image.blend(pictures[kept + i], pictures[i], (i + 1) / (GIFT_FADE + 1)) if i < GIFT_FADE
            else pictures[i] for i in range(kept)]


def gifts_gif(out):
    """The four gift balloons across, morning above night, as one looping GIF."""
    films = {(g, t): gift_film(g, t) for g, _ in GIFTS for t, _ in TIMES}
    length = min(len(film) for film in films.values())
    sheets = [sheet_of(len(TIMES), len(GIFTS), lambda r, c: (films[(GIFTS[c][0], TIMES[r][0])][i], None),
                       [name for _, name in GIFTS], [name for _, name in TIMES], GIFT_TILE)
              for i in range(length)]

    # One palette for every frame, from a few of them side by side: a palette per frame flickers.
    sample = Image.new("RGB", (sheets[0].width * 3, sheets[0].height))
    for i, index in enumerate((0, length // 3, 2 * length // 3)):
        sample.paste(sheets[index], (i * sheets[0].width, 0))
    palette = sample.quantize(GIFT_COLOURS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    # No dithering: its grain changes from frame to frame, which reads as noise and triples the file.
    frames = [sheet.quantize(palette=palette, dither=Image.Dither.NONE) for sheet in sheets]
    OUT.mkdir(parents=True, exist_ok=True)
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=GIFT_FRAME_MS, loop=0)
    megabytes = out.stat().st_size / (1024 * 1024)
    print(f"{out.relative_to(ROOT)}: {sheets[0].size[0]}x{sheets[0].size[1]}, {length} frames, {megabytes:.1f} MB")


def compose():
    missing = [p for p in
               [RAW / f"scene_{s}_{t}.png" for s, _ in SCENARIOS for t, _ in TIMES] +
               [RAW / f"item_{i}.png" for i, _ in ITEMS] if not p.exists()]
    if missing:
        sys.exit("Missing shots (capture first): " + ", ".join(p.name for p in missing))

    grid(len(TIMES), len(SCENARIOS),
         lambda r, c: (RAW / f"scene_{SCENARIOS[c][0]}_{TIMES[r][0]}.png", None),
         [name for _, name in SCENARIOS], [name for _, name in TIMES], OUT / "scenarios.jpg")
    grid(2, 3, lambda r, c: (RAW / f"item_{ITEMS[r * 3 + c][0]}.png", ITEMS[r * 3 + c][1]),
         None, None, OUT / "items.jpg")
    gifts_gif(OUT / "gifts.gif")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--compose", action="store_true", help="only compose the last capture")
    parser.add_argument("--burst", action="store_true", help="items at a run of moments; no compose")
    args = parser.parse_args()
    if not args.compose:
        capture(args.burst)
    if not args.burst:
        compose()


if __name__ == "__main__":
    main()
