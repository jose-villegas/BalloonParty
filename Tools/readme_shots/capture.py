"""Retakes the README's pictures: drives the open Unity Editor through the Unity CLI to play a fixed script
(Assets/Source/Editor/ReadmeShotCapture.cs) and writes each shot as a picture of its own for the README's tables.

    python3 Tools/readme_shots/capture.py            # capture, then compose into Docs/readme/
    python3 Tools/readme_shots/capture.py --compose  # compose the last capture again, no Editor needed
    python3 Tools/readme_shots/capture.py --burst    # items at a run of moments, to retune their timings

The Editor must be open on this project (the main checkout: it only sees committed, merged code) and not
in Play mode; the script enters Play mode itself and leaves it when done. The raw shots land in
Logs/readme_shots/ (git-ignored; --raw names another folder of them), the pictures in Docs/readme/.

A scenario's still is the best moment of a level a bot plays, the one with the most balloons popping at
once; the driver lists the moments it took in moments.txt, beside the raw shots. Each gift balloon is filmed
for a few seconds at night and written as a looping GIF of its own. No picture carries a label or shares a
file with another: the README lays them out in Markdown tables, so each opens at its full size.
"""
import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "Logs" / "readme_shots"
OUT = ROOT / "Docs" / "readme"
DONE = "done.txt"
MOMENTS = "moments.txt"
DRIVER = "BalloonParty.Editor.ReadmeShotCapture, BalloonParty.Editor"

SCENARIOS = (("sky", "Sky"), ("meadow", "Meadow"), ("water", "Water"), ("bonus", "Bonus level"))
TIMES = (("morning", "Morning"), ("night", "Night"))
ITEMS = (("bomb", "Bomb"), ("laser", "Laser"), ("lightning", "Lightning"),
         ("paint", "Paint"), ("shield", "Shield"), ("snipe", "Snipe"))
GIFTS = (("golden", "Golden"), ("heart", "Heart"), ("disco", "Disco ball"), ("nuke", "Nuke"))

# A still keeps the Game view's own size.
QUALITY = 86
LAUNCH_SECONDS = 180
CAPTURE_SECONDS = 1800

# A gift's GIF: the driver films a picture every 0.06 s of game time (3 physics steps), and the last
# GIFT_FADE of them melt into the first ones, so the loop has no jump.
GIFT_TIME = "night"
GIFT_TILE = (280, 280)
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


def still(name):
    """One raw shot as a JPEG of the same size."""
    out = OUT / f"{name}.jpg"
    picture = Image.open(RAW / f"{name}.png").convert("RGB")
    picture.save(out, quality=QUALITY, optimize=True, progressive=True)
    return out, picture.size


def gift_film(gift):
    """One gift's pictures, the loop closed: its last few melted into its first."""
    paths = sorted(RAW.glob(f"gift_{gift}_{GIFT_TIME}_*.png"))
    if len(paths) <= GIFT_FADE * 2:
        sys.exit(f"Missing shots (capture first): gift_{gift}_{GIFT_TIME}_NN.png")
    pictures = [Image.open(path).convert("RGB").resize(GIFT_TILE, Image.LANCZOS) for path in paths]
    kept = len(pictures) - GIFT_FADE
    return [Image.blend(pictures[kept + i], pictures[i], (i + 1) / (GIFT_FADE + 1)) if i < GIFT_FADE
            else pictures[i] for i in range(kept)]


def gift_gif(gift):
    """One gift balloon as a looping GIF."""
    film = gift_film(gift)
    # One palette for every frame, from a few of them side by side: a palette per frame flickers.
    sample = Image.new("RGB", (GIFT_TILE[0] * 3, GIFT_TILE[1]))
    for i, index in enumerate((0, len(film) // 3, 2 * len(film) // 3)):
        sample.paste(film[index], (i * GIFT_TILE[0], 0))
    palette = sample.quantize(GIFT_COLOURS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    # No dithering: its grain changes from frame to frame, which reads as noise and triples the file.
    frames = [picture.quantize(palette=palette, dither=Image.Dither.NONE) for picture in film]
    out = OUT / f"gift_{gift}.gif"
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=GIFT_FRAME_MS, loop=0)
    return out, len(frames)


def compose():
    names = [f"scene_{s}_{t}" for s, _ in SCENARIOS for t, _ in TIMES] + [f"item_{i}" for i, _ in ITEMS]
    missing = [name for name in names if not (RAW / f"{name}.png").exists()]
    if missing:
        sys.exit("Missing shots (capture first): " + ", ".join(f"{name}.png" for name in missing))

    OUT.mkdir(parents=True, exist_ok=True)
    total = 0
    for name in names:
        out, size = still(name)
        total += out.stat().st_size
        print(f"{out.relative_to(ROOT)}: {size[0]}x{size[1]}, {out.stat().st_size // 1024} KB")
    for gift, _ in GIFTS:
        out, length = gift_gif(gift)
        total += out.stat().st_size
        print(f"{out.relative_to(ROOT)}: {GIFT_TILE[0]}x{GIFT_TILE[1]}, {length} frames, {out.stat().st_size // 1024} KB")
    print(f"{total / (1024 * 1024):.1f} MB in {OUT.relative_to(ROOT)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--compose", action="store_true", help="only compose the last capture")
    parser.add_argument("--burst", action="store_true", help="items at a run of moments; no compose")
    parser.add_argument("--raw", type=Path, help="compose the raw shots in this folder (default: Logs/readme_shots)")
    args = parser.parse_args()
    if args.raw:
        global RAW
        RAW = args.raw
    if not args.compose:
        capture(args.burst)
    if not args.burst:
        compose()


if __name__ == "__main__":
    main()
