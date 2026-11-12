"""Retakes the README's stills: drives the open Unity Editor through the Unity CLI to play a fixed script
(Assets/Source/Editor/ReadmeShotCapture.cs) and composes the shots into the grids the README shows.

    python3 Tools/readme_shots/capture.py            # capture, then compose into Docs/readme/
    python3 Tools/readme_shots/capture.py --compose  # compose the last capture again, no Editor needed
    python3 Tools/readme_shots/capture.py --burst    # items at a run of moments, to retune their timings

The Editor must be open on this project (the main checkout: it only sees committed, merged code) and not
in Play mode; the script enters Play mode itself and leaves it when done. The raw shots land in
Logs/readme_shots/ (git-ignored), the grids in Docs/readme/.
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
DRIVER = "BalloonParty.Editor.ReadmeShotCapture, BalloonParty.Editor"

SCENARIOS = (("sky", "Sky"), ("meadow", "Meadow"), ("water", "Water"))
TIMES = (("morning", "Morning"), ("night", "Night"))
ITEMS = (("bomb", "Bomb"), ("laser", "Laser"), ("lightning", "Lightning"),
         ("paint", "Paint"), ("shield", "Shield"), ("snipe", "Snipe"))

TILE = (540, 960)
GAP = 12
LABEL = 64
BACKGROUND = (24, 28, 40)
INK = (240, 236, 226)
QUALITY = 88
LAUNCH_SECONDS = 180
CAPTURE_SECONDS = 900


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

    unity("UnityEditor.EditorApplication.isPlaying = true; return \"on\";")
    wait_for(lambda: str(unity("return (UnityEngine.Object.FindAnyObjectByType("
                               "System.Type.GetType(\"BalloonParty.Game.GameLifetimeScope, BalloonParty.Runtime\"))"
                               " != null).ToString();")) == "True", LAUNCH_SECONDS, "the game scene")
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


def grid(rows, columns, cell, column_labels, row_labels, out):
    """Tiles cell(row, column) into a grid under column labels, each row headed by its label."""
    font = ImageFont.truetype(str(FONT), 44)
    side = LABEL * 3 if row_labels else 0
    top = LABEL if column_labels else 0
    width = side + columns * TILE[0] + (columns + 1) * GAP
    height = top + rows * (TILE[1] + (0 if column_labels else LABEL)) + (rows + 1) * GAP
    sheet = Image.new("RGB", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    for c in range(columns):
        x = side + GAP + c * (TILE[0] + GAP)
        if column_labels:
            draw.text((x + TILE[0] / 2, top / 2 + GAP / 2), column_labels[c], fill=INK, font=font, anchor="mm")
    for r in range(rows):
        y = top + GAP + r * (TILE[1] + GAP + (0 if column_labels else LABEL))
        if row_labels:
            draw.text((side / 2 + GAP / 2, y + TILE[1] / 2), row_labels[r], fill=INK, font=font, anchor="mm")
        for c in range(columns):
            x = side + GAP + c * (TILE[0] + GAP)
            path, label = cell(r, c)
            sheet.paste(Image.open(path).convert("RGB").resize(TILE, Image.LANCZOS), (x, y))
            if label:
                draw.text((x + TILE[0] / 2, y + TILE[1] + LABEL / 2), label, fill=INK, font=font, anchor="mm")
    OUT.mkdir(parents=True, exist_ok=True)
    sheet.save(out, quality=QUALITY, optimize=True, progressive=True)
    print(f"{out.relative_to(ROOT)}: {sheet.size[0]}x{sheet.size[1]}")


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
