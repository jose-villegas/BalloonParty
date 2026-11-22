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
        """Builds the scene a test needs, each part a choice: a fresh board in a `scenario` (sky, meadow, water, paper), at
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

    def step(self, frames=1, screenshot=None, timeout=20.0):
        """Lets the game run exactly `frames` frames, then stops it again; returns once it has. The Editor is
        paused and stepped, so every clock stands still between frames, the real-time one too (hold() stops only
        game time): what the game animates on real time moves a frame's worth per frame. With `screenshot`, the
        Game view of the last frame is written there (screenshot() can't take one from a paused Editor).
        frame_seconds() tells how long a stepped frame is."""
        if screenshot:
            screenshot = os.path.abspath(screenshot).replace("\\", "/")
            os.makedirs(os.path.dirname(screenshot), exist_ok=True)
        self.call("Step", int(frames), screenshot or "")
        while "advancing" in self._quiet("Status"):
            time.sleep(0.05)
        if screenshot:
            deadline = time.time() + timeout
            while not os.path.exists(screenshot) or os.path.getsize(screenshot) == 0:
                if time.time() > deadline:
                    raise StageError(f"no screenshot at {screenshot}")
                time.sleep(0.05)

    def clock(self):
        """The game's real-time clock, in seconds."""
        return float(self._quiet("Clock"))

    def frame_seconds(self, screenshot=None):
        """How long a stepped frame is, measured over one step (it steps the game two frames, and leaves the Editor
        paused as step() does)."""
        # A step before the measured one: from a held game, the first also spans the moment the pause took to
        # take hold.
        self.step(1)
        before = self.clock()
        self.step(1, screenshot)
        return round(self.clock() - before, 4)

    def nuke(self):
        """Where the nuke stands: idle, armed (its fuse is lit) or blast (its disc is sweeping the board)."""
        return self._quiet("Nuke")

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

    def place(self, kind, slot, item="None", replace=True, settle=1.5, color=""):
        """Puts a `kind` balloon at slot (col, row), painted `color` (a palette name: Red, Green, Blue, Purple) if
        given; with `replace`, pops what's there in the same frame (and lets that pop play out for `settle`)."""
        col, row = slot
        taken = any(s == (col, row) for _, s, _ in self.list()[0])
        result = self.call("Place", kind, int(col), int(row), item, bool(replace), color)
        if taken and settle:
            self.advance(settle)
        return result

    def place_actor(self, kind, slot):
        """Stands a grid actor (`Bush`, `Puff`) in the empty slot (col, row), as the level's deal would. Not a
        mushroom: its caps belong to the walls the seed picks."""
        return self.call("PlaceActor", kind, int(slot[0]), int(slot[1]))

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

    def ball(self, at, start=None, seconds=0.0, shields=1):
        """Puts a ball in flight at world point `at` (x, y), held still; with `start` and `seconds`, it flies there
        from `start` over that much game time first, and this returns once it has. The effects clear around it, as
        wide as its `shields` make the clearing."""
        start_text = f"{start[0]},{start[1]}" if start else ""
        result = self.call("Ball", float(at[0]), float(at[1]), start_text, float(seconds), int(shields))
        if start and seconds > 0:
            while "advancing" in self._quiet("Status"):
                time.sleep(POLL_SECONDS)
        return result

    def fire(self, degrees, lance=False):
        """Fires the loaded ball for real at `degrees` (0 = right, 90 = straight up); with `lance`, armed as a Snipe
        lance first. It flies once game time runs (`advance`, `release`). Returns where it was fired from."""
        return self.call("Fire", float(degrees), bool(lance))

    def cut(self, slot, degrees, offset=0.0):
        """Cuts the cap off the mushroom in `slot` (col, row) as a lance flying at `degrees` would, through its middle
        or `offset` world units to the left of it (a sliver near the edge). The cut a lance makes in play: a ball
        flies through where the cap stood, and the cap grows back by itself on the board's beat."""
        return self.call("CutCap", int(slot[0]), int(slot[1]), float(degrees), float(offset))

    def regrow(self, slot):
        """Grows the cut cap in `slot` (col, row) back now, without waiting for the board's beat."""
        return self.call("RegrowCap", int(slot[0]), int(slot[1]))

    def aim(self, degrees=None):
        """Holds the thrower's aim at `degrees` (0 = right, 90 = straight up) and draws its aim line without firing,
        so a capture shows the line the game predicts, its bend off a mushroom included. None lets go; so does `fire`."""
        return self.call("AimOff") if degrees is None else self.call("Aim", float(degrees))

    def chain(self, then=""):
        """Where to aim to be paid by the meadow's mushrooms on the board as it stands: the angle to pass to `fire`
        (the middle of the widest run of aim angles whose solved flight arms the lance, three hits from wall of
        caps to wall of caps), or None when none does. The second value is the stage's whole answer: how many
        angles arm, how many caps stand in how many walls, and where the cannon stands."""
        try:
            result = self.call("Chain", then)
        except StageError as error:
            if "none of" in str(error):
                return None, str(error)
            raise
        return float(result.split()[1]), result

    def level_up_ready(self):
        """Readies a real level-up for the next shot: every balloon one colour, every other bar full, that one a point
        short, the level-up let through. Fire the Fire Best Shot cheat after `release` to see the cinematic."""
        return self.call("LevelUpReady")

    def clear_effects(self):
        return self.call("ClearEffects")

    def capture(self, path, around="", size=480):
        """Renders the camera to `path`: the whole frame, or a `size`-pixel square around a balloon (type or
        "col,row") or a world point (x, y)."""
        if isinstance(around, (tuple, list)):
            around = f"{around[0]};{around[1]}"
        return self.call("Capture", os.path.abspath(path).replace("\\", "/"), around, int(size))

    def maps(self, directory, sheet=True, title="", frame=True):
        """Writes every render map (the ones the Game Render Maps window lists) into `directory` as it stands
        this frame: <map>-rgb.png and <map>-a.png at the map's own resolution, the frame itself as frame.png,
        and with `sheet` all of them side by side as sheet.png. Returns {map: (width, height, max colour, max
        alpha)}, with None for a map that doesn't exist yet."""
        directory = os.path.abspath(directory).replace("\\", "/")
        found = {}
        for entry in self.call("Maps", directory).split("|"):
            name, _, value = entry.partition("=")
            if value == "none":
                found[name] = None
                continue
            size, top, top_alpha = value.split(",")
            width, height = size.split("x")
            found[name] = (int(width), int(height), float(top), float(top_alpha))
        if frame or sheet:
            self.capture(os.path.join(directory, "frame.png"))
        if sheet:
            import map_sheet
            map_sheet.compose(directory, found, title)
        return found

    def breakdown(self, directory, around="", size=480, scale=1.0, flat=True):
        """Redraws the held frame one draw group at a time into `directory`: backdrop.png, specks.png, shadows.png,
        bodies.png, shines.png, board.png, effects.png and light.png (each with the groups before it), post.png
        (post-processing on) and flat.png (the art under white light, the local lights off; not with `flat`
        off). `scale` below 1 writes whole frames that much smaller, for a run of every frame of a shot. With
        `around`, `size`-pixel squares centred on a balloon or a world point. Returns {group: renderers in it}."""
        if isinstance(around, (tuple, list)):
            around = f"{around[0]};{around[1]}"
        directory = os.path.abspath(directory).replace("\\", "/")
        answer = self.editor.eval(f"return BalloonParty.Editor.Staging.FrameBreakdown.Write("
                                  f"{cs(directory)}, {cs(around)}, {int(size)}, {cs(float(scale))}, {cs(bool(flat))});")
        if self.verbose:
            print(f"Breakdown: {answer}", flush=True)
        if str(answer).startswith("error"):
            raise StageError(answer)
        counts = {name: int(count) for name, _, count in (entry.partition("=") for entry in str(answer).split("|"))}
        if not counts.get("backdrop"):
            # Groups go by sorting layer, layer, shader and prefab names: an empty backdrop means one was renamed.
            print("Breakdown: no renderer counted as backdrop; FrameBreakdown.GroupOf may be out of date", flush=True)
        return counts

    def screenshot(self, path, timeout=20.0):
        """The Game view as the player sees it, HUD and all, written to `path` (the Game view must be showing)."""
        path = os.path.abspath(path).replace("\\", "/")
        self.editor.eval(f"return BalloonParty.Editor.Staging.FrameBreakdown.Screenshot({cs(path)});")
        deadline = time.time() + timeout
        while not os.path.exists(path) or os.path.getsize(path) == 0:
            if time.time() > deadline:
                raise StageError(f"no screenshot at {path}")
            time.sleep(0.2)
        return path

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
    scene.add_argument("--scenario", choices=("sky", "meadow", "water", "paper"), help="deal the first level in this scenario")
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
    step = sub.add_parser("step", help="let the game run an exact number of frames, then stop it again, every clock held")
    step.add_argument("frames", type=int, nargs="?", default=1)
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
    place.add_argument("--color", default="", help="a palette colour: Red, Green, Blue, Purple")
    actor = sub.add_parser("place-actor", help="stand a bush or a cloud in an empty slot, as the deal would")
    actor.add_argument("kind", help="a GridActorType: Bush, Puff")
    actor.add_argument("slot", help="col,row")
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
    sub.add_parser("level-up-ready", help="the next pop completes the level, cinematic and all")
    fire = sub.add_parser("fire", help="fire the loaded ball for real at an angle")
    fire.add_argument("degrees", type=float, help="0 = right, 90 = straight up")
    fire.add_argument("--lance", action="store_true", help="arm it as a Snipe lance first")
    chain = sub.add_parser("chain", help="the angle that arms the lance off this board's mushroom walls, for fire")
    chain.add_argument("--then", default="", choices=("", "nuke", "golden", "disco", "heart"),
                       help="only angles whose flight also pops a balloon of this kind")
    cut = sub.add_parser("cut", help="cut the cap off a mushroom as a lance does: a ball then flies through")
    cut.add_argument("slot", help="col,row")
    cut.add_argument("degrees", type=float, help="the lance's way: 0 = right, 90 = straight up")
    cut.add_argument("--offset", type=float, default=0.0,
                     help="world units to the left of the cap's middle the lance passes (0.2 takes a sliver off)")
    regrow = sub.add_parser("regrow", help="grow a cut mushroom's cap back now")
    regrow.add_argument("slot", help="col,row")
    aim = sub.add_parser("aim", help="hold the aim at an angle and draw its aim line, without firing")
    aim.add_argument("degrees", help="0 = right, 90 = straight up; off lets go")
    ball = sub.add_parser("ball", help="put a ball in flight at a world point, held still")
    ball.add_argument("at", help="x,y")
    ball.add_argument("--from", dest="start", help="x,y: fly there from here first")
    ball.add_argument("--seconds", type=float, default=0.5, help="how long the flight from --from takes")
    ball.add_argument("--shields", type=int, default=1, help="the ball's shields, which size its clearing")
    sub.add_parser("clear-effects", help="remove frozen pops and staged balls, and show the balloons the pops hid")
    capture = sub.add_parser("capture", help="render the camera to a PNG")
    capture.add_argument("path")
    capture.add_argument("--around", default="", help="a balloon type, col,row, or a world point x;y")
    capture.add_argument("--size", type=int, default=480)
    maps = sub.add_parser("maps", help="write every render map, the frame and a sheet of them all to a folder")
    maps.add_argument("directory")
    maps.add_argument("--title", default="", help="a heading for the sheet")
    maps.add_argument("--no-sheet", action="store_true", help="only the PNGs, no sheet.png (which needs Pillow)")
    breakdown = sub.add_parser("breakdown", help="redraw the held frame one draw group at a time, into a folder")
    breakdown.add_argument("directory")
    breakdown.add_argument("--around", default="", help="a balloon type, col,row or a world point x;y")
    breakdown.add_argument("--size", type=int, default=480)
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
        elif args.command == "step":
            stage.step(args.frames)
        elif args.command == "breakdown":
            stage.breakdown(args.directory, args.around, args.size)
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
            stage.place(args.kind, pair(args.slot, int), args.item, color=args.color)
        elif args.command == "place-actor":
            stage.place_actor(args.kind, pair(args.slot, int))
        elif args.command == "pop":
            stage.pop(args.target, args.settle)
        elif args.command == "freeze-pop":
            stage.freeze_pop(args.target, args.seconds, args.hide, args.dx, args.dy, args.seed, args.texture,
                             args.start_frame, args.shader, args.shader_file,
                             dict(item.split("=", 1) for item in args.set))
        elif args.command == "fire":
            stage.fire(args.degrees, args.lance)
        elif args.command == "chain":
            stage.call("Chain", args.then)
        elif args.command == "cut":
            stage.cut(pair(args.slot, int), args.degrees, args.offset)
        elif args.command == "regrow":
            stage.regrow(pair(args.slot, int))
        elif args.command == "aim":
            stage.aim(None if args.degrees == "off" else float(args.degrees))
        elif args.command == "level-up-ready":
            stage.level_up_ready()
        elif args.command == "ball":
            stage.ball(pair(args.at), pair(args.start) if args.start else None, args.seconds if args.start else 0.0,
                       args.shields)
        elif args.command == "capture":
            stage.capture(args.path, args.around, args.size)
        elif args.command == "maps":
            stage.maps(args.directory, not args.no_sheet, args.title)
        elif args.command == "end":
            stage.end()
    except StageError as error:
        sys.exit(str(error))


if __name__ == "__main__":
    main()
