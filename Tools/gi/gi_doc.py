#!/usr/bin/env python3
"""Stages a board and captures the GI light buffer under each of its settings: the pictures behind
Docs/gi-light-buffer.md.

    python3 Tools/gi/gi_doc.py               # stage, capture, compose into Logs/gi/doc/
    python3 Tools/gi/gi_doc.py --compose     # compose the last capture again; no Editor needed
    python3 Tools/gi/gi_doc.py --publish     # stage, capture, compose, copy the pictures to Docs/gi/

The board is the meadow at night with its obstacles taken off: coloured balloons, three columns of them hanging
below the rest, with the grass open between them. With the board held, the tool sets each GI setting in
turn to a lower and a higher value on the settings asset the running game reads, and writes the frame, every render
map and the light quad drawn alone. The settings are put back afterwards, and it stops if they don't read back the
same. See Tools/gi/README.md.
"""

import argparse
import json
import os
import shutil
import sys

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(TOOL_DIR))
sys.path.insert(0, os.path.join(REPO_ROOT, "Tools", "stage"))

from stage import EDITOR_PROJECT, Stage  # noqa: E402

OUT_DIR = os.path.join(REPO_ROOT, "Logs", "gi")
DOCS_DIR = os.path.join(REPO_ROOT, "Docs", "gi")

SCENE = {"scenario": "meadow", "level": 7, "seed": 7, "strip": "obstacles", "time": "night"}
COLOURS = ("Red", "Blue", "Green", "Purple")
# The top four rows full, so what comes after rests in the middle of the board, away from the danger gradient at
# the top.
FILLER = [(col, row) for row in range(4) for col in range(6)]
# Below them three columns of different lengths, with open grass beside each. The night's light comes from below,
# so a balloon's shadow falls up its column's side, on the grass. Simple balloons only: toughs walk, and a golden
# balloon gives off light of its own.
LOWER = [("Simple", (1, 4), "Blue"), ("Simple", (1, 5), "Red"), ("Simple", (3, 4), "Green"),
         ("Simple", (3, 5), "Purple"), ("Simple", (3, 6), "Blue"), ("Simple", (3, 7), "Red"),
         ("Simple", (3, 8), "Green"), ("Simple", (5, 4), "Red"), ("Simple", (5, 5), "Blue"),
         ("Simple", (5, 6), "Purple")]
# The texel the tap diagram follows: on the grass beside the green balloon at the foot of the longest column, on the
# side away from the light, so its shadow march runs into the balloon. Read again by --compose.
TAP_TEXEL = (-0.19, -1.57)
# The square the pictures show, in world units: the three columns and the grass round them.
CROP = {"x": 0.15, "y": -0.4, "size": 4.2}

LIGHT_FIELDS = ["_smearDistance", "_tapDecay", "_tapStart", "_mipSpread", "_shadowMipSpread", "_shadowStrength",
                "_bounceStrength", "_secondaryBounceWeight"]

# Each setting at a lower and a higher value than the game's. "as-is" keeps the game's settings; "shadow-alone", for
# the settings that move the shadow, sets the bounce strength to 0 so none of the shadow is taken back.
VARIANTS = {
    "_shadowMipSpread": (0.01, 2.0),
    "_smearDistance": (0.3, 1.5),
    "_tapDecay": (0.35, 0.95),
    "_tapStart": (0.0, 3.0),
    "_mipSpread": (0.0, 3.0),
    "_secondaryBounceWeight": (0.0, 1.0),
    "_bounceStrength": (0.0, 1.0),
    "_shadowStrength": (0.0, 1.0),
}
PASSES = {
    "as-is": ({}, list(VARIANTS)),
    "shadow-alone": ({"_bounceStrength": 0.0},
                     ["_shadowMipSpread", "_smearDistance", "_tapDecay", "_tapStart", "_shadowStrength"]),
}

REFLECT = r'''
var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var svcType = System.Type.GetType("BalloonParty.Display.ScreenSpaceLightService, BalloonParty.Runtime");
var svc = UnityEngine.Object.FindFirstObjectByType(svcType);
var light = svcType.GetField("_settings", bf).GetValue(svc);
'''

# The light quad drawn alone over mid grey into a linear target: its blend is 2 * src * dst, so over 0.5 it writes
# its own colour, and grey is a pixel the light leaves as it is.
LIGHT_ALONE = r'''
var quad = (UnityEngine.Material)svcType.GetField("_overlayMaterial", bf).GetValue(svc);
var rt = new UnityEngine.RenderTexture(540, 960, 0, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.Linear);
var previous = UnityEngine.RenderTexture.active;
UnityEngine.RenderTexture.active = rt;
UnityEngine.GL.Clear(true, true, new UnityEngine.Color(0.5f, 0.5f, 0.5f, 1f));
UnityEngine.Graphics.Blit(UnityEngine.Texture2D.whiteTexture, rt, quad, 0);
UnityEngine.RenderTexture.active = rt;
var picture = new UnityEngine.Texture2D(540, 960, UnityEngine.TextureFormat.RGBA32, false, true);
picture.ReadPixels(new UnityEngine.Rect(0, 0, 540, 960), 0, 0);
picture.Apply();
UnityEngine.RenderTexture.active = previous;
System.IO.File.WriteAllBytes(PATH, picture.EncodeToPNG());
rt.Release();
UnityEngine.Object.DestroyImmediate(rt);
UnityEngine.Object.DestroyImmediate(picture);
return "ok";
'''


class Board:
    def __init__(self, project):
        self.stage = Stage(project)

    def eval(self, code):
        answer = str(self.stage.editor.eval(REFLECT + code))
        if answer.startswith("error") or "Exception" in answer:
            raise RuntimeError(answer)
        return answer

    def read(self, fields):
        code = "var sb = new System.Text.StringBuilder();"
        for field in fields:
            code += (f'sb.Append("{field}=" + ((float)light.GetType().GetField("{field}", bf).GetValue(light))'
                     f'.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";");')
        answer = self.eval(code + "return sb.ToString();")
        return {key: float(value) for key, value in (pair.split("=") for pair in answer.strip(";").split(";"))}

    def relight(self, values, quad=True):
        """Sets the light settings, has the buffer worked out again, and lets three frames run."""
        code = "".join(f'light.GetType().GetField("{field}", bf).SetValue(light, {float(value)!r}f);'
                       for field, value in values.items())
        self.eval(code + 'svcType.GetField("_lastContentVersion", bf).SetValue(svc, -1); return "ok";')
        self.stage.step(3)
        if not quad:
            self.eval('((UnityEngine.Renderer)svcType.GetField("_overlayRenderer", bf).GetValue(svc)).enabled = false;'
                      'return "ok";')

    def take(self, folder, maps=True, frame=False):
        os.makedirs(folder, exist_ok=True)
        side = round(CROP["size"] / (2 * self.view["ortho"]) * self.view["height_pixels"])
        self.stage.capture(os.path.join(folder, "crop.png"), around=(CROP["x"], CROP["y"]), size=side)
        if maps:
            self.stage.maps(folder, sheet=False, frame=frame)
            path = json.dumps(os.path.join(folder, "light.png").replace("\\", "/"))
            self.eval(LIGHT_ALONE.replace("PATH", path))


def stage_board(board):
    stage = board.stage
    # Dealt twice: a game just come into Play mode draws random numbers over the frames after its first deal that a
    # running one does not, so the board is the second deal's on every run.
    scene = dict(SCENE)
    time_of_day = scene.pop("time")
    stage.step(1)
    for _ in range(2):
        stage.setup(**scene)
    stage.time_of_day(time_of_day)
    for index, slot in enumerate(FILLER):
        stage.place("Simple", slot, color=COLOURS[(index * 3) % 4], settle=0.1)
    for kind, slot, colour in LOWER:
        stage.place(kind, slot, color=colour, settle=0.1)
    stage.advance(1.0)
    balloons, _ = stage.list()
    standing = {slot: kind for kind, slot, _ in balloons}
    moved = [slot for kind, slot, _ in LOWER if standing.get(slot) != kind]
    if moved:
        print(f"the balloons placed in {moved} are not where they were placed: check crop.png before publishing")


def capture(project):
    board = Board(project)
    stage = board.stage
    original = None
    changed = False
    # The settings are reached through the running game, so Play mode comes first.
    stage.begin()
    try:
        stage_board(board)
        original = board.read(LIGHT_FIELDS)
        view = board.eval('var c = UnityEngine.Camera.main; var d = UnityEngine.Shader.GetGlobalVector("_SceneLightDir");'
                          'var ci = System.Globalization.CultureInfo.InvariantCulture;'
                          'return c.orthographicSize.ToString("R", ci) + " " + c.aspect.ToString("R", ci) + " " + '
                          'c.pixelHeight + " " + d.x.ToString("R", ci) + " " + d.y.ToString("R", ci);').split()
        board.view = {"ortho": float(view[0]), "aspect": float(view[1]), "height_pixels": int(view[2]),
                      "toward_light": [float(view[3]), float(view[4])]}
        for name, (base, fields) in PASSES.items():
            root = os.path.join(OUT_DIR, name)
            board.relight({**original, **base})
            board.take(os.path.join(root, "game"), frame=True)
            if name == "as-is":
                board.relight(original, quad=False)
                board.take(os.path.join(root, "unlit"), maps=False)
            for field in fields:
                for value in VARIANTS[field]:
                    board.relight({**original, **base, field: value})
                    board.take(os.path.join(root, f"{field.lstrip('_')}-{value:g}"))
        with open(os.path.join(OUT_DIR, "capture.json"), "w", encoding="utf-8") as handle:
            json.dump({"view": board.view, "crop": CROP, "settings": original, "tap_texel": TAP_TEXEL,
                       "variants": {field.lstrip("_"): values for field, values in VARIANTS.items()}}, handle, indent=1)
    finally:
        # Changed in Play mode, an asset's values outlive it: put back and read back before leaving.
        if original is not None:
            board.relight(original)
            changed = board.read(LIGHT_FIELDS) != original
        stage.end()
    if changed:
        raise RuntimeError("the light settings did not read back as they were: check them in the Editor before "
                           "saving anything")


def publish(written):
    # The folder holds nothing but these pictures: one the tool no longer makes is not left behind.
    shutil.rmtree(DOCS_DIR, ignore_errors=True)
    os.makedirs(DOCS_DIR)
    for path in written:
        shutil.copy2(path, os.path.join(DOCS_DIR, os.path.basename(path)))
    print(f"copied {len(written)} pictures to {DOCS_DIR}")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--project", default=EDITOR_PROJECT, help="the project the open Editor has (default: the main checkout)")
    parser.add_argument("--compose", action="store_true", help="compose the last capture again; no Editor needed")
    parser.add_argument("--publish", action="store_true", help="copy the composed pictures to Docs/gi/")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    if not args.compose:
        for stale in list(PASSES) + ["doc"]:
            shutil.rmtree(os.path.join(OUT_DIR, stale), ignore_errors=True)
        capture(args.project)
    else:
        # The tap diagram is drawn when composing, so a new texel needs no new capture.
        path = os.path.join(OUT_DIR, "capture.json")
        with open(path, encoding="utf-8") as handle:
            taken = json.load(handle)
        taken["tap_texel"] = TAP_TEXEL
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(taken, handle, indent=1)

    import compose
    written = compose.compose(OUT_DIR)
    print(f"composed {len(written)} pictures in {os.path.join(OUT_DIR, 'doc')}")
    if args.publish:
        publish(written)


if __name__ == "__main__":
    sys.path.insert(0, TOOL_DIR)
    main()
