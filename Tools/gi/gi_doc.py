#!/usr/bin/env python3
"""Stages a board and captures the GI light buffer under each of its settings: the pictures behind
Docs/gi-light-buffer.md.

    python3 Tools/gi/gi_doc.py               # stage, capture, compose into Logs/gi/doc/
    python3 Tools/gi/gi_doc.py --compose     # compose the last capture again; no Editor needed
    python3 Tools/gi/gi_doc.py --publish     # stage, capture, compose, copy the pictures to Docs/gi/

The board is the sky at dusk: unbreakables standing in a ring round a golden balloon, a disco ball beside them,
a snake of toughs and toughers above, and coloured balloons round them all. With the board held, the tool sets each GI setting in turn to a
lower and a higher value on the settings asset the running game reads, and writes the frame, every render map
and the light quad drawn alone. The settings and the formation weights it changes are put back afterwards, and it
stops if they don't read back the same. See Tools/gi/README.md.
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

SCENE = {"scenario": "sky", "level": 7, "seed": 7, "strip": "enemies", "time": "dusk"}
COLOURS = ("Red", "Blue", "Green", "Purple")
# Filled first, round the level's puff clouds, so what comes after rests in the middle of the board, away from the
# danger gradient at the top.
FILLER = [(col, row) for row in range(4) for col in range(6)]
# Five unbreakables: one ring, round the golden balloon.
UNBREAKABLES = [(2, 4), (3, 4), (1, 5), (3, 5), (2, 6)]
BESIDE = [("Golden", (2, 5), ""), ("Tough", (0, 4), ""), ("Tougher", (1, 4), ""), ("Tougher", (4, 4), ""),
          ("Tough", (5, 4), ""), ("Simple", (0, 5), "Red"), ("Simple", (4, 5), "Green"), ("Simple", (5, 5), "Blue"),
          ("Disco", (1, 6), ""), ("Simple", (0, 6), "Purple"), ("Simple", (3, 6), "Red"), ("Tough", (2, 7), "")]
# Long enough for the run's first reshape, when the free unbreakables form up.
FORM_SECONDS = 8.0
# The slot the ring stands round on this deal. A formation rolls where its ring stands, so the tool says when the
# golden balloon isn't its centre.
RING_CENTRE = (2, 5)
# The texel the tap diagram follows: the gap between the golden balloon and two of its ring on this deal.
TAP_TEXEL = (0.35, -0.35)
# The squares the pictures show, in world units: the ring and the tough snake above it, and, for the shadow, the
# balloons above the ring, which no local light reaches (the ring's glow lifts the sky round it through the bounce).
CROP = {"x": -0.45, "y": 0.7, "size": 3.8}
SHADOW_CROP = {"x": -0.3, "y": 2.45, "size": 2.6}

FORMATION_WEIGHTS = ["_horizontalWeight", "_diagonalWeight", "_itemCoverWeight", "_snakeWeight", "_ringWeight",
                     "_spinnerWeight"]
LIGHT_FIELDS = ["_smearDistance", "_tapDecay", "_tapStart", "_mipSpread", "_shadowMipSpread", "_shadowStrength",
                "_bounceStrength", "_secondaryBounceWeight", "_cloudShadowGate"]

# Each setting at a lower and a higher value than the game's. "as-is" keeps the game's settings; "shadow-alone", for
# the settings that move the shadow, sets the cloud gate to 0 so the whole shadow shows and the bounce strength to 0
# so none of it is taken back.
VARIANTS = {
    "_shadowMipSpread": (0.01, 2.0),
    "_smearDistance": (0.3, 1.5),
    "_tapDecay": (0.35, 0.95),
    "_tapStart": (0.0, 3.0),
    "_mipSpread": (0.0, 3.0),
    "_secondaryBounceWeight": (0.0, 1.0),
    "_bounceStrength": (0.0, 1.0),
    "_shadowStrength": (0.0, 1.0),
    "_cloudShadowGate": (0.0, 1.0),
}
PASSES = {
    "as-is": ({}, list(VARIANTS)),
    "shadow-alone": ({"_cloudShadowGate": 0.0, "_bounceStrength": 0.0},
                     ["_shadowMipSpread", "_smearDistance", "_tapDecay", "_tapStart", "_shadowStrength"]),
}

REFLECT = r'''
var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var svcType = System.Type.GetType("BalloonParty.Display.ScreenSpaceLightService, BalloonParty.Runtime");
var svc = UnityEngine.Object.FindFirstObjectByType(svcType);
var light = svcType.GetField("_settings", bf).GetValue(svc);
UnityEngine.ScriptableObject balloons = null;
foreach (var o in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.ScriptableObject>())
{
    if (o.GetType().Name == "BalloonsConfiguration") { balloons = o; break; }
}
var formation = balloons.GetType().GetField("_unbreakableFormation", bf).GetValue(balloons);
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

    def read(self, owner, fields):
        code = "var sb = new System.Text.StringBuilder();"
        for field in fields:
            code += (f'sb.Append("{field}=" + ((float){owner}.GetType().GetField("{field}", bf).GetValue({owner}))'
                     f'.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";");')
        answer = self.eval(code + "return sb.ToString();")
        return {key: float(value) for key, value in (pair.split("=") for pair in answer.strip(";").split(";"))}

    def write(self, owner, values):
        code = "".join(f'{owner}.GetType().GetField("{field}", bf).SetValue({owner}, {float(value)!r}f);'
                       for field, value in values.items())
        self.eval(code + 'return "ok";')

    def relight(self, values, phone=False, quad=True):
        """Sets the light settings, has the buffer worked out again, and lets three frames run."""
        self.write("light", values)
        keyword = "EnableKeyword" if phone else "DisableKeyword"
        self.eval('svcType.GetField("_lastContentVersion", bf).SetValue(svc, -1);'
                  'var smear = (UnityEngine.Material)svcType.GetField("_smearMaterial", bf).GetValue(svc);'
                  f'smear.{keyword}("_LOW_QUALITY_SMEAR"); return "ok";')
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
    # running one does not, so the level's clouds stand where the second deal puts them on every run.
    scene = dict(SCENE)
    time_of_day = scene.pop("time")
    stage.step(1)
    for _ in range(2):
        stage.setup(**scene)
    stage.time_of_day(time_of_day)
    # The level's puff clouds keep their slots: the board is placed round them.
    taken = {slot: kind for kind, slot, _ in stage.list()[0]}
    filler = [slot for slot in FILLER if slot not in taken]
    cluster = [("Unbreakable", slot, "") for slot in UNBREAKABLES] + BESIDE
    for index, slot in enumerate(filler):
        stage.place("Simple", slot, color=COLOURS[(index * 3) % 4], settle=0.1)
    for kind, slot, colour in cluster:
        if slot in taken:
            print(f"slot {slot[0]},{slot[1]} holds a {taken[slot]}: no {kind} placed there", flush=True)
            continue
        stage.place(kind, slot, color=colour, settle=0.1)
    stage.advance(FORM_SECONDS)
    balloons, _ = stage.list()
    taken = {slot: kind for kind, slot, _ in balloons}
    if taken.get(RING_CENTRE) != "Golden":
        print("the ring may not stand round the golden balloon on this deal: check crop.png before publishing")


def capture(project):
    board = Board(project)
    stage = board.stage
    originals = {}
    # The settings are reached through the running game, so Play mode comes first.
    stage.begin()
    try:
        originals["formation"] = board.read("formation", FORMATION_WEIGHTS)
        # Rings only: of the shapes the formation rolls, the one this page is about.
        board.write("formation", {field: (1.0 if field == "_ringWeight" else 0.0) for field in FORMATION_WEIGHTS})
        stage_board(board)
        originals["light"] = board.read("light", LIGHT_FIELDS)
        view = board.eval('var c = UnityEngine.Camera.main; var d = UnityEngine.Shader.GetGlobalVector("_SceneLightDir");'
                          'var ci = System.Globalization.CultureInfo.InvariantCulture;'
                          'return c.orthographicSize.ToString("R", ci) + " " + c.aspect.ToString("R", ci) + " " + '
                          'c.pixelHeight + " " + d.x.ToString("R", ci) + " " + d.y.ToString("R", ci);').split()
        board.view = {"ortho": float(view[0]), "aspect": float(view[1]), "height_pixels": int(view[2]),
                      "toward_light": [float(view[3]), float(view[4])]}
        game = originals["light"]
        for name, (base, fields) in PASSES.items():
            root = os.path.join(OUT_DIR, name)
            board.relight({**game, **base})
            board.take(os.path.join(root, "game"), frame=True)
            if name == "as-is":
                board.relight(game, quad=False)
                board.take(os.path.join(root, "unlit"), maps=False)
                board.relight(game, phone=True)
                board.take(os.path.join(root, "phone"))
            for field in fields:
                for value in VARIANTS[field]:
                    board.relight({**game, **base, field: value})
                    board.take(os.path.join(root, f"{field.lstrip('_')}-{value:g}"))
        with open(os.path.join(OUT_DIR, "capture.json"), "w", encoding="utf-8") as handle:
            json.dump({"view": board.view, "crop": CROP, "shadow_crop": SHADOW_CROP, "settings": game,
                       "tap_texel": TAP_TEXEL,
                       "variants": {field.lstrip("_"): values for field, values in VARIANTS.items()}}, handle, indent=1)
    finally:
        # Changed in Play mode, an asset's values outlive it: put back and read back before leaving.
        if "light" in originals:
            board.relight(originals["light"])
        if "formation" in originals:
            board.write("formation", originals["formation"])
        fields_of = {"light": LIGHT_FIELDS, "formation": FORMATION_WEIGHTS}
        changed = [owner for owner in originals if board.read(owner, fields_of[owner]) != originals[owner]]
        stage.end()
    if changed:
        raise RuntimeError(f"the {' and '.join(changed)} settings did not read back as they were: check them in the "
                           f"Editor before saving anything")


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
        # The shadow's square is cut from the maps when composing, so a new one needs no new capture.
        path = os.path.join(OUT_DIR, "capture.json")
        with open(path, encoding="utf-8") as handle:
            taken = json.load(handle)
        taken["shadow_crop"] = SHADOW_CROP
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
