#!/usr/bin/env python3
"""Puts a still scene together in the open Unity Editor, to judge a look without playing for it.

Drives Assets/Source/Editor/Staging/SceneStage.cs through the Unity CLI: holds the world still, clears
slots, places balloons, sets the time of day, pops a balloon or freezes its pop at a chosen moment,
and renders the camera to a PNG. One command per call, or import it and script a whole scene:

    python3 Tools/stage/stage.py begin --scenario meadow --seed 7 --strip clouds,enemies --time night
    python3 Tools/stage/stage.py place Golden 4,9
    python3 Tools/stage/stage.py freeze-pop Golden 0.2 --hide
    python3 Tools/stage/stage.py capture Logs/stage/golden-night.png --around Golden

    from stage import Stage
    stage = Stage()
    stage.begin(scenario="meadow", seed=7, strip=("clouds", "enemies"), time="night")

The Editor must be open on the main checkout (it only sees committed, merged code). See
Tools/stage/README.md.
"""

import argparse
import json
import os
import subprocess
import sys
import time

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(TOOL_DIR))
sys.path.insert(0, os.path.join(REPO_ROOT, "Tools", "playtest"))

from playtest_bot import Editor, find_unity  # noqa: E402


def main_checkout():
    """The repository's main checkout, the one the Editor has open, even when run from a worktree."""
    try:
        common = subprocess.run(["git", "-C", REPO_ROOT, "rev-parse", "--path-format=absolute", "--git-common-dir"],
                                capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return REPO_ROOT
    return os.path.dirname(common) if os.path.basename(common) == ".git" else REPO_ROOT


EDITOR_PROJECT = main_checkout()

API = "BalloonParty.Editor.Staging.SceneStage"
# The light's angle at the named times of day (the day/night gradient's keys, see SceneLightFieldSettings).
TIMES = {"dawn": 45.0, "day": 135.0, "noon": 135.0, "dusk": 225.0, "night": 315.0}
BEGIN_TIMEOUT = 180.0
POLL_SECONDS = 0.5


def cs(value):
    """A C# literal for a Python string, number or bool."""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return repr(float(value)) + "f" if isinstance(value, float) else str(value)
    return json.dumps(str(value))


class StageError(RuntimeError):
    pass


class Stage:
    def __init__(self, project=EDITOR_PROJECT, verbose=True):
        self.editor = Editor(find_unity(), os.path.abspath(project))
        self.verbose = verbose

    def call(self, method, *args):
        """Calls SceneStage.<method>(args) in the Editor and returns its answer; an "error: ..." answer raises."""
        result = str(self.editor.eval(f"return {API}.{method}({', '.join(cs(a) for a in args)});"))
        if self.verbose:
            print(f"{method}: {result}", flush=True)
        if result.startswith("error"):
            raise StageError(result)
        return result

    def status(self):
        return self.call("Status")

    def begin(self, settle=1.5, **scene):
        """Enters Play mode if needed, gets past the launch screen and holds the game, then sets the scene up as
        `setup` does with the same keywords."""
        self.editor.wait_ready()
        # An unfocused Editor stops ticking the game; these keep it running.
        self.editor.command("editor", "set_auto_tick")
        self.editor.command("editor.playmode", "set_run_in_background")
        deadline = time.time() + BEGIN_TIMEOUT
        while self.call("Begin") != "ready":
            if time.time() > deadline:
                raise StageError("the game didn't come up")
            time.sleep(1.0)
            self.editor.wait_ready()
        if scene:
            self.setup(settle=settle, **scene)

    def setup(self, scenario=None, level=None, seed=None, strip=(), time=None, settle=1.5):
        """Builds the scene a test needs, each part a choice: a fresh board in a `scenario` (sky, meadow, water), at
        a `level`, on a run `seed` (the same seed deals the same board); then `strip` takes kinds off it (balloons,
        clouds, bushes, enemies, obstacles); then the `time` of day. Parts left out stay as they are."""
        if scenario or level or seed:
            self.deal(scenario, level, seed, settle)
        if strip:
            self.strip(*([strip] if isinstance(strip, str) else strip))
        if time is not None:
            self.time_of_day(time)

    def hold(self):
        return self.call("Hold")

    def release(self):
        return self.call("Release")

    def advance(self, seconds):
        """Lets game time run `seconds`, then holds again; returns once it has."""
        self.call("Advance", float(seconds))
        while "advancing" in self._quiet("Status"):
            time.sleep(POLL_SECONDS)

    def deal(self, scenario=None, level=None, seed=None, settle=1.5):
        """A fresh board: the run restarted at `level` (default the current one) or the first level from there in
        `scenario`, on run `seed` (default the current one), and `settle` seconds of game time for its obstacles and the thrower to come up."""
        self.call("Deal", scenario or "", int(level or 0), int(seed or 0))
        if settle > 0:
            self.advance(settle)

    def strip(self, *kinds):
        """Takes kinds of things off the board at once, with no pop: balloons, clouds, bushes, enemies (deflectors,
        absorbers, gatekeepers), obstacles (all but the balloons), or a grid actor type by name."""
        return self.call("Strip", ",".join(kinds))

    def time_of_day(self, when, settle=1.0):
        """Sets the light to `when`: an angle in degrees, or dawn, day, dusk or night."""
        angle = TIMES[when] if str(when) in TIMES else float(when)
        self.call("SetTime", float(angle))
        # The light's globals follow on the next ticks; real time passes while the game is held.
        time.sleep(settle)

    def list(self):
        """The balloons on the board: [(type, (col, row), (x, y))], and the grid size."""
        lines = self._quiet("List").splitlines()
        balloons = []
        for line in lines[:-1]:
            kind, slot, world = line.split(" ")
            balloons.append((kind, tuple(int(v) for v in slot.split(",")), tuple(float(v) for v in world.split(","))))
        return balloons, lines[-1]

    def clear(self, slots=None, around=None, radius=0.8, settle=1.5):
        """Pops the balloons in a slot rectangle ((c0, r0), (c1, r1)) or within `radius` of a world point, and lets
        the pops play out."""
        if slots is not None:
            (c0, r0), (c1, r1) = slots
            self.call("Clear", int(c0), int(r0), int(c1), int(r1))
        elif around is not None:
            self.call("ClearAround", float(around[0]), float(around[1]), float(radius))
        self.advance(settle)

    def place(self, kind, slot, item="None", replace=True, settle=1.5):
        """Puts a `kind` balloon at slot (col, row); with `replace`, pops what's there in the same frame (and lets
        that pop play out for `settle`)."""
        col, row = slot
        taken = any(s == (col, row) for _, s, _ in self.list()[0])
        result = self.call("Place", kind, int(col), int(row), item, bool(replace))
        if taken and settle:
            self.advance(settle)
        return result

    def pop(self, target, settle=None):
        """Pops a balloon for real (its type, or "col,row"); with `settle`, lets that much game time run."""
        result = self.call("Pop", target)
        if settle:
            self.advance(settle)
        return result

    def freeze_pop(self, target, seconds, hide=False, dx=0.0, dy=0.0, seed=None, texture=None, start_frame=None,
                   shader=None, shader_file=None, floats=None):
        """Shows `target`'s pop stopped `seconds` in, from a fixed seed; see SceneStage.FreezePop for the swaps."""
        options = {"hide": 1 if hide else 0, "dx": dx, "dy": dy}
        if seed is not None:
            options["seed"] = seed
        if texture:
            options["texture"] = os.path.abspath(texture).replace("\\", "/")
        if start_frame is not None:
            options["startFrame"] = start_frame
        if shader:
            options["shader"] = shader
        if shader_file:
            options["shaderFile"] = os.path.abspath(shader_file).replace("\\", "/")
        options.update(floats or {})
        return self.call("FreezePop", target, float(seconds), ";".join(f"{k}={v}" for k, v in options.items()))

    def clear_effects(self):
        return self.call("ClearEffects")

    def capture(self, path, around="", size=480):
        """Renders the camera to `path`: the whole frame, or a `size`-pixel square around a balloon (type or
        "col,row") or a world point (x, y)."""
        if isinstance(around, (tuple, list)):
            around = f"{around[0]};{around[1]}"
        return self.call("Capture", os.path.abspath(path).replace("\\", "/"), around, int(size))

    def end(self):
        """Clears frozen effects, gives the game back its clock, level-ups and balloons, and leaves Play mode."""
        self.call("End")
        self.editor.wait_ready()

    def _quiet(self, method, *args):
        verbose, self.verbose = self.verbose, False
        try:
            return self.call(method, *args)
        finally:
            self.verbose = verbose


def pair(text, kind=float):
    a, b = text.split(",")
    return kind(a), kind(b)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--project", default=EDITOR_PROJECT,
                        help="the Unity project the Editor has open (default: the main checkout)")
    sub = parser.add_subparsers(dest="command", required=True)

    scene = argparse.ArgumentParser(add_help=False)
    scene.add_argument("--scenario", choices=("sky", "meadow", "water"), help="deal the first level in this scenario")
    scene.add_argument("--level", type=int, help="deal this level (or search from it, with --scenario)")
    scene.add_argument("--seed", type=int, help="the run seed: the same seed deals the same board")
    scene.add_argument("--strip", default="",
                       help="kinds to take off the board: balloons, clouds, bushes, enemies, obstacles (comma list)")
    scene.add_argument("--time", help="the time of day: dawn, day, dusk, night, or an angle")
    sub.add_parser("begin", parents=[scene],
                   help="enter Play mode, get past the launch screen, hold the game, and set the scene up")
    sub.add_parser("setup", parents=[scene], help="set the scene up: deal a board, strip kinds off it, set the time")
    sub.add_parser("status", help="where things stand")
    sub.add_parser("hold", help="stop game time")
    sub.add_parser("release", help="let game time run")
    advance = sub.add_parser("advance", help="let game time run a while, then hold again")
    advance.add_argument("seconds", type=float)
    when = sub.add_parser("time", help="set the time of day: dawn, day, dusk, night, or an angle")
    when.add_argument("when")
    sub.add_parser("list", help="the balloons on the board")
    clear = sub.add_parser("clear", help="pop the balloons in an area")
    clear.add_argument("--slots", help="a slot rectangle, c0,r0:c1,r1")
    clear.add_argument("--around", help="a world point, x,y")
    clear.add_argument("--radius", type=float, default=0.8)
    place = sub.add_parser("place", help="put a balloon in a slot (what's there is popped first)")
    place.add_argument("kind", help="a BalloonType: Ordinary, Unbreakable, Golden, Heart, ...")
    place.add_argument("slot", help="col,row")
    place.add_argument("--item", default="None")
    pop = sub.add_parser("pop", help="pop a balloon for real")
    pop.add_argument("target", help="a balloon type or col,row")
    pop.add_argument("--settle", type=float, help="then let this much game time run")
    freeze = sub.add_parser("freeze-pop", help="show a balloon's pop stopped some seconds in")
    freeze.add_argument("target", help="a balloon type or col,row")
    freeze.add_argument("seconds", type=float)
    freeze.add_argument("--hide", action="store_true", help="hide the balloon, as if popped")
    freeze.add_argument("--dx", type=float, default=0.0)
    freeze.add_argument("--dy", type=float, default=0.0)
    freeze.add_argument("--seed", type=int)
    freeze.add_argument("--texture", help="a PNG to draw the shards from instead")
    freeze.add_argument("--start-frame", type=float, help="the shards' random first frame, 0..1 of the sheet")
    freeze.add_argument("--shader", help="a shader by name, for the shards")
    freeze.add_argument("--shader-file", help="a .shader file, compiled on the spot (one not yet merged)")
    freeze.add_argument("--set", action="append", default=[], metavar="_NAME=VALUE",
                        help="a material float, e.g. _AmbientHue=0.25")
    sub.add_parser("clear-effects", help="remove frozen pops and show the balloons they hid")
    capture = sub.add_parser("capture", help="render the camera to a PNG")
    capture.add_argument("path")
    capture.add_argument("--around", default="", help="a balloon type, col,row, or a world point x;y")
    capture.add_argument("--size", type=int, default=480)
    sub.add_parser("end", help="clear frozen pops, let the game run and leave Play mode")
    args = parser.parse_args()

    stage = Stage(args.project)
    try:
        if args.command in ("begin", "setup"):
            scene_args = dict(scenario=args.scenario, level=args.level, seed=args.seed,
                              strip=[k for k in args.strip.split(",") if k], time=args.time)
            if args.command == "begin":
                stage.begin(**{k: v for k, v in scene_args.items() if v})
            else:
                stage.setup(**scene_args)
        elif args.command in ("status", "hold", "release", "clear-effects"):
            stage.call({"status": "Status", "hold": "Hold", "release": "Release",
                        "clear-effects": "ClearEffects"}[args.command])
        elif args.command == "advance":
            stage.advance(args.seconds)
        elif args.command == "time":
            stage.time_of_day(args.when)
        elif args.command == "list":
            balloons, grid = stage.list()
            for kind, slot, world in balloons:
                print(f"{kind:12} {slot[0]},{slot[1]:<4} {world[0]:.2f},{world[1]:.2f}")
            print(grid)
        elif args.command == "clear":
            if args.slots:
                a, b = args.slots.split(":")
                stage.clear(slots=(pair(a, int), pair(b, int)))
            elif args.around:
                stage.clear(around=pair(args.around), radius=args.radius)
            else:
                parser.error("clear needs --slots or --around")
        elif args.command == "place":
            stage.place(args.kind, pair(args.slot, int), args.item)
        elif args.command == "pop":
            stage.pop(args.target, args.settle)
        elif args.command == "freeze-pop":
            stage.freeze_pop(args.target, args.seconds, args.hide, args.dx, args.dy, args.seed, args.texture,
                             args.start_frame, args.shader, args.shader_file,
                             dict(item.split("=", 1) for item in args.set))
        elif args.command == "capture":
            stage.capture(args.path, args.around, args.size)
        elif args.command == "end":
            stage.end()
    except StageError as error:
        sys.exit(str(error))


if __name__ == "__main__":
    main()
