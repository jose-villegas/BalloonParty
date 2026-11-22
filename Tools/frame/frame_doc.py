#!/usr/bin/env python3
"""Stages a glory shot and takes it apart: the pictures behind Docs/frame-composition.md.

    python3 Tools/frame/frame_doc.py Tools/frame/glory.json            # stage, capture, compose
    python3 Tools/frame/frame_doc.py Tools/frame/glory.json --compose  # compose the last capture again
    python3 Tools/frame/frame_doc.py Tools/frame/glory.json --publish  # and copy the pictures to Docs/frame/
    python3 Tools/frame/frame_doc.py Tools/frame/glory.json --compose --publish --animations gif   # as GIFs

The setup file says what the scene is (scenario, level, seed, time of day), what stands on the board (rows of
text, one character a balloon), the shot that sets it off and which frame after it is the glory frame. The
tool builds that scene in the open Editor through Tools/stage (so nothing is left to a bot's luck), fires the
shot, and at each of a run of frames around the glory frame writes every render map, the frame redrawn one
draw group at a time, and the Game view. Then it composes them into the stills and animations the document
shows. See Tools/frame/README.md.
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

DOCS_DIR = os.path.join(REPO_ROOT, "Docs", "frame")
DOCUMENT = os.path.join(REPO_ROOT, "Docs", "frame-composition.md")


def load_setup(path):
    with open(path, encoding="utf-8") as handle:
        setup = json.load(handle)
    setup.setdefault("name", os.path.splitext(os.path.basename(path))[0])
    return setup


def cells(setup):
    """The board's rows of text as [(col, row, entry)], entry being the legend's {"type", "color", "item"}.

    The first row of text is the board's top row, row 0 (balloons rest against the top; the cannon is below the
    last row), and a row's first character is column 0. A space or a dot is an empty slot.
    """
    legend = setup.get("legend", {})
    rows = setup.get("board", [])
    top = setup.get("top_row", 0)
    left = setup.get("left_column", 0)
    placed = []
    for line_index, line in enumerate(rows):
        for char_index, char in enumerate(line):
            if char in " .":
                continue
            if char not in legend:
                raise ValueError(f"board character {char!r} is not in the legend")
            placed.append((left + char_index, top + line_index, legend[char]))
    return placed


class ShotMissed(RuntimeError):
    """The shot was fired and did not do what the solver's flight of it did: the real flight can part from the
    solved one (a formation moved, a cap rocked)."""


class NoShot(RuntimeError):
    """No aim angle does what the setup's shot asks on the board as it was dealt."""


def build_with_shot(stage, setup, tries=6):
    """Builds the scene until the shot it asks for exists: a level's clouds and bushes land differently on every
    deal, and now and then one stands in the shot's way."""
    for attempt in range(tries):
        build(stage, setup)
        shot = setup.get("shot") or {}
        if not shot.get("chain") or stage.chain(shot.get("then", ""))[0] is not None:
            return
        print(f"no such shot on this deal (try {attempt + 1} of {tries}); dealing again", flush=True)
    raise SystemExit("no deal of this board has the shot the setup asks for")


def build(stage, setup):
    """The scene, held: the board dealt and stripped, the time of day set, every balloon of the setup placed."""
    scene = dict(setup.get("scene", {}))
    strip = scene.pop("strip", ())
    time_of_day = scene.pop("time", None)
    stage.begin(strip=strip, **scene)
    # The level's own obstacles (its clouds, bushes, mushrooms) keep their slots: the board is drawn round them.
    taken = {slot: kind for kind, slot, _ in stage.list()[0]}
    # The setup's own bushes first, as the deal stands them before any balloon: balloons float round them.
    placed = sorted(cells(setup), key=lambda cell: "actor" not in cell[2])
    for col, row, entry in placed:
        if (col, row) in taken:
            print(f"slot {col},{row} holds a {taken[(col, row)]}: nothing placed there", flush=True)
            continue
        if "actor" in entry:
            stage.place_actor(entry["actor"], (col, row))
            continue
        stage.place(entry.get("type", "Simple"), (col, row), entry.get("item", "None"), settle=0,
                    color=entry.get("color", ""))
    stage.advance(setup.get("settle", 1.5))
    if time_of_day is not None:
        stage.time_of_day(time_of_day)
    # A little game time for the fields that follow the scene (the light's gradient, the capture, the GI).
    stage.advance(setup.get("warm", 0.5))


def fire(stage, setup):
    """Fires the setup's shot: at its angle, or with "chain" at the angle the board's mushrooms pay with the lance
    (and with "then", one whose lance goes on to pop that reward balloon: nuke, golden, disco, heart)."""
    shot = setup.get("shot")
    if not shot:
        return
    angle = shot.get("angle")
    if shot.get("chain"):
        angle, answer = stage.chain(shot.get("then", ""))
        if angle is None:
            raise NoShot(answer)
    stage.fire(angle, shot.get("lance", False))


# The nuke's fuse runs four seconds (NukeFuse: one of the heart's flash, three of countdown). This much of it is
# stepped at the timeline's pace; the rest a frame at a time, watching for the blast.
FUSE_PACED_SECONDS = 3.3


def timeline(stage, setup, screen=None):
    """The frames of the shot to capture, in order: yields (phase, label, is the glory frame, is a key frame,
    game frames since the last one) for each, having stepped the game to it. The game stands at the shot when
    this starts. `screen`, if given, answers where the Game view of the frame being stepped to is written: the
    step takes it, since the Editor is paused between frames.

    A frame is one step of the paused Editor (`stage.frame_seconds()`; 1/50 s), and every count in the setup's
    `moment` and `timeline` is in those.

    With a nuke in the shot (`moment` is {"after": "blast", "frames": N}) the phases are the flight (from the
    shot to the hit that lights the fuse, and a little past it), the fuse (its four seconds, a frame now and
    then), the blast (from its first frame, the glory frame N frames in) and after (the cloud). The blast is
    found by watching the nuke, since when a shot reaches it differs from deal to deal. Without one (`moment` is
    a number of frames after the shot) it is a plain run of frames.

    Each phase has its own cadence (`flight_every`, `blast_every`, `after_every`): the frames on it are the key
    frames, which the document's animations show. `detail`, if set, is a finer cadence for the video (1 is every
    frame of the game): the frames between key frames are captured too, the fuse's included, so the video runs
    the whole shot with no cut and stays smooth slowed down.
    """
    plan = setup.get("timeline", {})
    moment = setup.get("moment", 0)
    detail = plan.get("detail", 0)
    flown = 0

    def step(frames):
        stage.step(frames, screenshot=screen() if screen else None)

    yield "flight", "the shot", False, True, 0
    if not isinstance(moment, dict):
        every = plan.get("every", 4)
        pace = detail or every
        while flown < plan.get("frames", moment + 60):
            step(pace)
            flown += pace
            yield "flight", f"shot +{flown}", flown == moment, flown % every == 0, pace
        return

    every = plan.get("flight_every", 4)
    pace = detail or every
    armed = None
    # The nuke is asked on the key frames only: asking is a round trip to the Editor.
    while armed is None:
        if flown > 600:
            raise ShotMissed("the shot never lit the nuke's fuse")
        step(pace)
        flown += pace
        key = flown % every == 0
        yield "flight", f"shot +{flown}", False, key, pace
        if key and stage.nuke() != "idle":
            armed = flown
    for _ in range(plan.get("flight_after", 40) // pace):
        step(pace)
        flown += pace
        yield "flight", f"shot +{flown}", False, flown % every == 0, pace

    # The fuse's key frames are a few, far apart: nothing but the countdown moves. With `detail` every frame of
    # it is captured all the same, or the video would cut from the hit to the blast. Its last part goes frame by
    # frame either way, so the blast's first frame is exact.
    fuse_every = plan.get("fuse_every", 30)
    pace = detail or fuse_every
    paced = int(FUSE_PACED_SECONDS / stage.frame_length)
    while flown - armed + pace <= paced:
        step(pace)
        flown += pace
        yield "fuse", f"fuse +{flown - armed}", False, (flown - armed) % fuse_every == 0, pace
    if stage.nuke() != "armed":
        raise SystemExit("the nuke's fuse is shorter than this tool takes it to be: the blast was missed")
    waited = 0
    while True:
        step(1)
        flown += 1
        waited += 1
        if stage.nuke() == "blast":
            break
        if waited > 120:
            raise SystemExit("the nuke never went off")
        if detail:
            yield "fuse", f"fuse +{flown - armed}", False, False, 1

    blast_every, blast_frames = plan.get("blast_every", 3), plan.get("blast_frames", 48)
    glory = moment.get("frames", 9)
    if glory % blast_every or glory > blast_frames:
        raise SystemExit(f"the glory frame ({glory} into the blast) is not one of the captured ones "
                         f"(every {blast_every} up to {blast_frames})")
    pace = detail or blast_every
    for into in range(0, blast_frames + 1, pace):
        if into:
            step(pace)
        yield "blast", f"blast +{into}", into == glory, into % blast_every == 0, pace if into else (1 if detail else waited)
    after_every = plan.get("after_every", 8)
    pace = detail or after_every
    for past in range(pace, plan.get("after_frames", 72) + 1, pace):
        step(pace)
        yield "after", f"blast +{blast_frames + past}", False, past % after_every == 0, pace


def fixed_point(stage, around):
    """A balloon (its type, or "col,row") as the world point it stands at now, "x;y": a square cut round a
    point stays where it is when the balloon is popped. None when there is no such balloon."""
    if ";" in around:
        return around
    for kind, slot, world in stage.list()[0]:
        if kind.lower() == around.lower() or f"{slot[0]},{slot[1]}" == around:
            return f"{world[0]};{world[1]}"
    print(f"no balloon at {around}: no close-up", flush=True)
    return None


def capture_frame(stage, directory, closeup=None, screenshot=True):
    """Everything behind the held frame: the maps, the build-up, the Game view, and the build-up close on one
    balloon. The build-up's whole frames are written at half size: no picture shows them larger, and a shot is
    some hundreds of frames."""
    os.makedirs(directory, exist_ok=True)
    found = stage.maps(directory, sheet=False, frame=False)
    stage.breakdown(os.path.join(directory, "steps"), scale=0.5, flat=False)
    if closeup:
        stage.breakdown(os.path.join(directory, "closeup"), closeup["around"], closeup.get("size", 360))
    # A frame the game was stepped to has its Game view already: the step took it.
    if screenshot and not os.path.exists(os.path.join(directory, "screen.png")):
        stage.screenshot(os.path.join(directory, "screen.png"))
    with open(os.path.join(directory, "maps.json"), "w", encoding="utf-8") as out:
        json.dump(found, out)


def light_study(stage, setup, out_dir):
    """One balloon as the light goes round: the square around it at every angle of the study, and its build-up at
    the four named times of day."""
    study = setup.get("light_study")
    if not study:
        return
    directory = os.path.join(out_dir, "light")
    os.makedirs(directory, exist_ok=True)
    around, size = study["around"], study.get("size", 360)
    for angle in range(0, 360, study.get("step", 15)):
        stage.time_of_day(angle, settle=study.get("settle", 0.6))
        stage.capture(os.path.join(directory, f"angle-{angle:03d}.png"), around, size)
    for name, angle in (("dawn", 45), ("day", 135), ("dusk", 225), ("night", 315)):
        stage.time_of_day(angle, settle=study.get("settle", 0.6))
        stage.breakdown(os.path.join(directory, name), around, size)


def preview(setup, out_dir, project, frames):
    """The scene staged and the shot fired, with the frame written every few frames and all of them laid out on one
    sheet under their frame numbers: for choosing the setup's `moment`. The scene is left held, to look around."""
    stage = Stage(project)
    # About fifty pictures, however long the stretch.
    every = max(2, frames // 48)
    directory = os.path.join(out_dir, "preview")
    shutil.rmtree(directory, ignore_errors=True)
    os.makedirs(directory)
    taken = []
    try:
        build_with_shot(stage, setup)
        fire(stage, setup)
        for frame in range(0, frames + 1, every):
            path = os.path.join(directory, f"{frame:04d}.png")
            stage.capture(path)
            taken.append((frame, path))
            stage.step(every)
        # The stepping leaves the Editor paused; held instead, the scene can be looked around in.
        stage.hold()
    except BaseException:
        # Only a finished preview leaves its scene up to look around in.
        stage.end()
        raise
    import compose
    from PIL import Image
    panels = [compose.labelled(compose.fit(Image.open(path), 200), f"frame {frame}") for frame, path in taken]
    sheet = os.path.join(out_dir, "preview.png")
    compose.grid(panels, 8).save(sheet)
    print(f"preview sheet: {sheet} (the scene is still staged; end it with Tools/stage/stage.py end)")


def capture(setup, out_dir, project, tries=4):
    stage = Stage(project)
    frames_dir = os.path.join(out_dir, "frames")
    try:
        for attempt in range(tries):
            build_with_shot(stage, setup)
            if attempt == 0:
                light_study(stage, setup, out_dir)
                scene_time = setup.get("scene", {}).get("time")
                if scene_time is not None:
                    stage.time_of_day(scene_time)
                    stage.advance(setup.get("warm", 0.5))
            closeup = setup.get("closeup")
            if closeup:
                point = fixed_point(stage, closeup["around"])
                closeup = dict(closeup, around=point) if point else None
            shutil.rmtree(frames_dir, ignore_errors=True)
            taken, glory = [], None

            def screen():
                return os.path.join(frames_dir, f"{len(taken):03d}", "screen.png")

            # From here the Editor is paused and stepped a frame at a time, so every clock stands still while a
            # frame is captured: held game time alone leaves what the game animates on real time (an item's
            # idle spin, the shaders' clock) running through each capture. The first step measures a frame, and
            # its Game view is the first frame's: the shot is fired after it, with no frame in between.
            stage.frame_length = stage.frame_seconds(screen())
            fire(stage, setup)
            try:
                for phase, label, is_glory, key, gap in timeline(stage, setup, screen):
                    capture_frame(stage, os.path.join(frames_dir, f"{len(taken):03d}"), closeup if is_glory else None)
                    glory = len(taken) if is_glory else glory
                    taken.append({"phase": phase, "label": label, "key": key, "gap": gap})
                break
            except ShotMissed as missed:
                print(f"{missed} (try {attempt + 1} of {tries}); staging the scene again", flush=True)
        else:
            raise SystemExit("the shot missed on every try")
        if glory is None:
            raise SystemExit("the timeline never reached the glory frame the setup names")
        with open(os.path.join(out_dir, "capture.json"), "w", encoding="utf-8") as out:
            json.dump({"glory": glory, "frame_seconds": stage.frame_length, "frames": taken, "setup": setup},
                      out, indent=1)
    finally:
        stage.end()


def relink(kind):
    """Points the document's animations at the files of one type: its text names them as frame/<name>.<type>."""
    import re
    with open(DOCUMENT, encoding="utf-8", newline="") as handle:
        text = handle.read()
    # The document names a picture as src="frame/<name>.<type>" in a table's cell.
    linked = re.sub(r'"(frame/[\w.-]+?)\.(?:webp|gif)"', lambda match: f'"{match.group(1)}.{kind}"', text)
    if linked != text:
        with open(DOCUMENT, "w", encoding="utf-8", newline="") as handle:
            handle.write(linked)
        print(f"{os.path.basename(DOCUMENT)} now shows the {kind} animations")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("setup", help="the scene's setup file (JSON)")
    parser.add_argument("--project", default=EDITOR_PROJECT, help="the project the open Editor has (default: the main checkout)")
    parser.add_argument("--out", help="where the capture goes (default: Logs/frame/<name>)")
    parser.add_argument("--compose", action="store_true", help="compose the last capture again; no Editor needed")
    parser.add_argument("--publish", action="store_true", help="copy the composed pictures to Docs/frame/")
    parser.add_argument("--animations", choices=("webp", "gif", "both"), default="webp",
                        help="what the animations are written as: animated WebP (default; a third of the size), "
                             "GIF (plays everywhere), or both. With --publish the document is pointed at them: "
                             "at the WebP ones for both")
    parser.add_argument("--no-video", action="store_true",
                        help="leave out composition.mp4, the one video of everything side by side (it needs ffmpeg)")
    parser.add_argument("--preview", type=int, metavar="FRAMES", default=0,
                        help="only stage the scene, fire the shot and write a sheet of the next FRAMES frames, "
                             "to choose the glory frame by")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    setup = load_setup(args.setup)
    out_dir = os.path.abspath(args.out or os.path.join(REPO_ROOT, "Logs", "frame", setup["name"]))
    if args.preview:
        preview(setup, out_dir, args.project, args.preview)
        return
    if not args.compose:
        for stale in ("frames", "light", "doc"):
            shutil.rmtree(os.path.join(out_dir, stale), ignore_errors=True)
        capture(setup, out_dir, args.project)

    import compose
    kinds = compose.ANIMATIONS if args.animations == "both" else (args.animations,)
    shutil.rmtree(os.path.join(out_dir, "doc"), ignore_errors=True)
    written = compose.compose(out_dir, kinds, not args.no_video)
    print(f"composed {len(written)} pictures in {os.path.join(out_dir, 'doc')}")
    if args.publish:
        # The folder holds nothing but these pictures: one a setup no longer makes is not left behind.
        shutil.rmtree(DOCS_DIR, ignore_errors=True)
        os.makedirs(DOCS_DIR)
        for path in written:
            if path.endswith(".mp4"):
                # Some fifty megabytes: too much to keep in the repository for a page that can't play it inline.
                print(f"the video stays where it was written, out of the repository: {path}")
                continue
            shutil.copy2(path, os.path.join(DOCS_DIR, os.path.basename(path)))
        print(f"copied to {DOCS_DIR}")
        relink(kinds[0])


if __name__ == "__main__":
    sys.path.insert(0, TOOL_DIR)
    main()
