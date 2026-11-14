#!/usr/bin/env python3
"""Plays BalloonParty in a running Unity Editor and reports how the runs went.

Drives the Editor through the Unity CLI and its Pipeline package (com.unity.pipeline): enters Play
mode, sends bot_tick.cs once per tick (the first installs the shooter in the Editor; every tick
reports state, and answers a game over as --on-loss says), then stops Play mode and builds a report from the
game's own telemetry (JsonLinesTelemetrySink, under Application.persistentDataPath/telemetry).

Two players:
    --player best    the best-shot solver, fired the moment the cannon is ready
    --player human   an average player: waits a reaction time after the cannon is ready (a floor
                     plus an exponential wait, so shots come as a Poisson process) and aims the
                     solver's angle with a normal error

Usage:
    python3 Tools/playtest/playtest_bot.py --seconds 240 --player human --label reload-2s

Writes Logs/playtest/<stamp>[-label]/ticks.jsonl and report.md, and prints the report.
See Tools/playtest/README.md.
"""

import argparse
import datetime
import glob
import json
import os
import re
import shutil
import statistics
import subprocess
import sys
import time

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(TOOL_DIR))
TICK_RE = re.compile(
    r"^(?P<t>[\d.]+) L(?P<level>\d+) score (?P<score>-?\d+) hearts (?P<hearts>-?\d+) "
    r"(?P<nav>\w+)/(?P<phase>\w+) shots (?P<shots>\d+)(?: fill (?P<fill>[\d.]+))?(?: inflight (?P<inflight>\d+))?"
    r"(?: breather (?P<breather>[\d.]+))?"
    r"(?: rewards (?P<golden_placed>\d+) (?P<heart_placed>\d+)(?P<rewards_off> off)?)?$")
FLOAT_FIELDS = ("t", "fill", "breather")
# A loss the tick answered with a retry, which keeps the run going: "<time> RETRY ad <0|1>" or "<time> BONUS ad 1".
RETRY_RE = re.compile(r" (RETRY|BONUS) ad ([01])$")
# What a game over gets, per --on-loss: the tick's own words for the report.
ON_LOSS = {
    "stop": "the session ends at the first game over",
    "restart": "Restart, a new run",
    "retry": "Retry while the run has one (free first, then behind an ad), else Restart",
    "bonus": "the bonus retry (a rewarded ad for a bonus card) while it is offered, else Retry, else Restart",
}
# A gap between two playing ticks longer than this (a level-up, a pause, a restart) ends a release or tension stretch.
STRETCH_BREAK_SECONDS = 2.0

# The human player's knobs per skill: reaction floor and mean (s), aim error sd (degrees), and the share of
# shots fired unaimed. First guesses, not fitted to real players; compare skills with each other.
SKILLS = {
    "low": {"react_floor": 0.4, "react_mean": 1.2, "aim_sd": 10.0, "wild": 0.2},
    "average": {"react_floor": 0.25, "react_mean": 0.6, "aim_sd": 4.0, "wild": 0.05},
    "high": {"react_floor": 0.15, "react_mean": 0.25, "aim_sd": 1.5, "wild": 0.0},
}

# How far back a heart loss looks for what led to it.
LEAD_UP_SECONDS = 10.0
# Config asset -> the keys the report's tuning line shows from it.
TUNING = {
    "Assets/Configuration/ProjectileFlightConfig.asset":
        ("_projectileLoadDuration", "_reloadCooldownSeconds", "_maxBallsInFlight"),
    "Assets/Configuration/RunConfig.asset": ("_startingHitPoints", "_levelUpHeartRefill"),
}


def find_unity():
    found = shutil.which("unity")
    if found:
        return found
    fallback = os.path.expanduser("~/AppData/Local/Unity/bin/unity.exe")
    if os.path.exists(fallback):
        return fallback
    sys.exit("The Unity CLI ('unity') wasn't found. Install it, or put it on PATH.")


class Editor:
    def __init__(self, unity, project):
        self.unity = unity
        self.project = project

    def run(self, *args, timeout=120):
        cmd = [self.unity, *args, "--project-path", self.project, "--format", "json"]
        done = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        try:
            return json.loads(done.stdout)
        except json.JSONDecodeError:
            return {"success": False, "errors": [{"message": (done.stdout + done.stderr).strip()}]}

    def command(self, *args, timeout=120):
        return self.run("command", *args, timeout=timeout)

    def eval(self, code, timeout=60):
        reply = self.command("eval", code, timeout=timeout)
        if not reply.get("success"):
            errors = reply.get("errors") or [{}]
            raise RuntimeError(errors[0].get("message", "eval failed"))
        return reply["data"]["result"]["result"]

    def wait_ready(self):
        self.run("status", "--until-ready", "--timeout", "180", timeout=200)


def read_tuning(project):
    tuning = {}
    for asset, keys in TUNING.items():
        path = os.path.join(project, asset)
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        for key in keys:
            match = re.search(rf"^\s*{key}: (.+)$", text, re.MULTILINE)
            if match:
                tuning[key.lstrip("_")] = match.group(1).strip()
    return tuning


def ends_run(sample):
    return sample.get("restart") or sample.get("game_over")


def settle_retries(samples):
    """Marks a retry press that staged nothing (the next thing the ticks show is another press, not play) as failed,
    so it counts neither as a retry nor as an ad."""
    for index, sample in enumerate(samples):
        if not sample.get("retry"):
            continue
        after = next((s for s in samples[index + 1:] if "level" in s or s.get("retry") or ends_run(s)), None)
        sample["failed"] = after is not None and "level" not in after
    return samples


def parse_tick(line, wall):
    """One tick's line as a sample: its state fields, or what it did on the launch or game-over screen."""
    sample = {"wall": wall, "line": line}
    match = TICK_RE.match(line or "")
    retry = RETRY_RE.search(line or "")
    if match:
        sample.update({k: (v if k in ("nav", "phase") else True if k == "rewards_off"
                           else float(v) if k in FLOAT_FIELDS else int(v))
                       for k, v in match.groupdict().items() if v is not None})
    elif line and " SETUP " in line:
        sample["setup"] = True
    elif line and line.endswith("RESTART"):
        sample["restart"] = True
    elif retry:
        sample.update({"retry": True, "bonus": retry.group(1) == "BONUS", "ad": retry.group(2) == "1"})
    elif line and line.endswith("GAMEOVER"):
        sample["game_over"] = True
    return sample


def play(editor, seconds, tick_code, ticks_path):
    """Ticks until the time is up, or until the first game over when the tick doesn't restart."""
    deadline = time.time() + seconds
    samples = []
    with open(ticks_path, "w", encoding="utf-8") as out:
        while time.time() < deadline:
            try:
                line = editor.eval(tick_code)
            except (RuntimeError, subprocess.TimeoutExpired) as error:
                line = f"error {error}"
            sample = parse_tick(line, round(time.time(), 2))
            samples.append(sample)
            out.write(json.dumps(sample) + "\n")
            out.flush()
            if sample.get("game_over"):
                break
    return samples


def percentile(values, fraction):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(fraction * len(ordered)))]


def telemetry_since(directory, started_utc):
    records = []
    for path in glob.glob(os.path.join(directory, "telemetry_*.jsonl")):
        if os.path.getmtime(path) < started_utc.timestamp() - 5:
            continue
        for line in open(path, encoding="utf-8"):
            if not line.strip():
                continue
            record = json.loads(line)
            stamp = record.get("timestamp_utc", "")[:19]
            if stamp and datetime.datetime.fromisoformat(stamp) >= started_utc.replace(tzinfo=None):
                records.append(record)
    return records


def summarize_runs(samples):
    runs, current = [], None
    for sample in samples:
        if ends_run(sample) or current is None:
            if current and current["ticks"]:
                runs.append(current)
            current = {"ticks": 0, "level": 0, "score": 0, "hearts": None, "shots": 0, "first": None, "last": None,
                       "shots_at_start": None, "retries": 0, "bonus": 0, "ads": 0}
            if ends_run(sample):
                continue
        if sample.get("retry"):
            if sample.get("failed"):
                continue
            current["retries"] += 1
            current["bonus"] += sample["bonus"]
            current["ads"] += sample["ad"]
            continue
        if "level" not in sample:
            continue
        current["ticks"] += 1
        current["level"] = max(current["level"], sample["level"])
        current["score"] = max(current["score"], sample["score"])
        current["hearts"] = sample["hearts"]
        if current["shots_at_start"] is None:
            current["shots_at_start"] = sample["shots"]
        current["shots"] = sample["shots"] - current["shots_at_start"]
        current["first"] = sample["t"] if current["first"] is None else current["first"]
        current["last"] = sample["t"]
    if current and current["ticks"]:
        runs.append(current)
    return runs


def split_runs(samples):
    """The ticks of each run, in order; a run ends at a game over."""
    runs, current = [], []
    for sample in samples:
        if ends_run(sample):
            if current:
                runs.append((current, True))
            current = []
        elif "level" in sample:
            current.append(sample)
    if current:
        runs.append((current, False))
    return runs


def heart_losses(samples):
    """Each heart lost, with what the ticks show of the seconds before it. The bot's own setup (--hearts) setting
    the hearts between two ticks is not a loss."""
    losses = []
    setups = [s["wall"] for s in samples if s.get("setup")]
    for run_index, (ticks, _) in enumerate(split_runs(samples), 1):
        level_start = ticks[0]["t"]
        for previous, tick in zip(ticks, ticks[1:]):
            if tick["level"] != previous["level"]:
                level_start = tick["t"]
                continue
            if any(previous["wall"] < wall < tick["wall"] for wall in setups):
                continue
            lost = previous["hearts"] - tick["hearts"]
            if lost <= 0:
                continue
            window = [s for s in ticks if tick["t"] - LEAD_UP_SECONDS <= s["t"] <= tick["t"]]
            span = max(0.1, window[-1]["t"] - window[0]["t"])
            fills = [s["fill"] for s in window if "fill" in s]
            flying = [s["inflight"] for s in window if "inflight" in s]
            losses.append({
                "run": run_index,
                "level": tick["level"],
                "into_level": tick["t"] - level_start,
                "lost": lost,
                "hearts_left": tick["hearts"],
                "fill_before": previous.get("fill"),
                "fill_mean": statistics.mean(fills) if fills else None,
                "shots_per_10s": (window[-1]["shots"] - window[0]["shots"]) / span * 10.0,
                "inflight_mean": statistics.mean(flying) if flying else None,
            })
    return losses


def release_and_tension(samples):
    """Stretches of play inside a breather (release) and between breathers (tension), from the ticks' breather
    field. A level change or a break in play ends a stretch, so a tension stretch that runs into a level-up is
    cut there. Returns None when the ticks carry no breather field (an older build or the device bot)."""
    stretches = []
    seen = False

    def close(stretch, ended_by):
        if stretch and stretch["seconds"] > 0:
            stretch["ended_by"] = ended_by
            stretches.append(stretch)

    def start(tick):
        return {"kind": "release" if tick["breather"] > 0 else "tension", "level": tick["level"],
                "seconds": 0.0, "fills": [tick.get("fill", 0.0)], "ended_by": "end"}

    for ticks, _ in split_runs(samples):
        current = None
        previous = None
        for tick in ticks:
            if not (tick.get("nav") == "Game" and tick.get("phase") == "Playing" and "breather" in tick):
                close(current, "break")
                current = previous = None
                continue
            seen = True
            if (previous is None or tick["level"] != previous["level"]
                    or tick["t"] - previous["t"] > STRETCH_BREAK_SECONDS):
                close(current, "break")
                current = start(tick)
                previous = tick
                continue
            # The time since the last tick belongs to the state that tick was in.
            current["seconds"] += tick["t"] - previous["t"]
            current["fills"].append(tick.get("fill", 0.0))
            kind = "release" if tick["breather"] > 0 else "tension"
            if kind != current["kind"]:
                close(current, kind)
                current = start(tick)
            previous = tick
        close(current, "end")
    return stretches if seen else None


def release_and_tension_lines(stretches):
    lines = ["## Release and tension (ticks)", "",
             "Release is play inside a spawn-clock breather (a big clear slowed the lines); tension is play between "
             "breathers. A tension stretch \"ended by a release\" was broken by a big clear; one cut by a level-up "
             "or the end of the session ran out before the player earned one.", ""]
    releases = [s for s in stretches if s["kind"] == "release"]
    tensions = [s for s in stretches if s["kind"] == "tension"]
    release_seconds = sum(s["seconds"] for s in releases)
    tension_seconds = sum(s["seconds"] for s in tensions)
    total = release_seconds + tension_seconds
    if total <= 0:
        return lines + ["No play was recorded.", ""]

    def spread(values):
        if not values:
            return "-"
        return (f"mean {statistics.mean(values):.1f} s, median {statistics.median(values):.1f} s, "
                f"90th percentile {percentile(values, 0.9):.1f} s, longest {max(values):.1f} s")

    def mean_fill(group):
        fills = [f for s in group for f in s["fills"]]
        return f"{statistics.mean(fills):.2f}" if fills else "-"

    broken_by_release = [s["seconds"] for s in tensions if s["ended_by"] == "release"]
    lines += [f"- {len(releases)} release{'' if len(releases) == 1 else 's'}, {release_seconds:.0f} s of {total:.0f} s played "
              f"({release_seconds / total:.0%}); one every {total / max(1, len(releases)):.0f} s of play.",
              f"- Release stretches: {spread([s['seconds'] for s in releases])}.",
              f"- Tension stretches: {spread([s['seconds'] for s in tensions])}.",
              f"- Tension stretches ended by a release ({len(broken_by_release)}): {spread(broken_by_release)}.",
              f"- Mean board fill: {mean_fill(releases)} in release, {mean_fill(tensions)} in tension.", "",
              "| Level | Seconds played | Releases | Share in release | Longest tension | Mean fill, release | "
              "Mean fill, tension |",
              "|---|---|---|---|---|---|---|"]
    for level in sorted({s["level"] for s in stretches}):
        here = [s for s in stretches if s["level"] == level]
        rel = [s for s in here if s["kind"] == "release"]
        ten = [s for s in here if s["kind"] == "tension"]
        played = sum(s["seconds"] for s in here)
        rel_seconds = sum(s["seconds"] for s in rel)
        longest = f"{max(s['seconds'] for s in ten):.1f}" if ten else "-"
        share = f"{rel_seconds / played:.0%}" if played > 0 else "-"
        lines.append(f"| {level} | {played:.0f} | {len(rel)} | {share} | {longest} | {mean_fill(rel)} | "
                     f"{mean_fill(ten)} |")
    lines.append("")
    return lines


def loss_lines(samples):
    """Every game over the bot answered with a retry, and the run's last, with the level it came on."""
    lines, run, level = [], 1, None
    for sample in samples:
        if "level" in sample:
            level = sample["level"]
        if sample.get("retry"):
            answer = "bonus retry (ad)" if sample["bonus"] else "retry (ad)" if sample["ad"] else "retry (free)"
            lines.append(f"| {run} | {level} | {answer}{', staged nothing' if sample.get('failed') else ''} |")
        elif sample.get("restart"):
            lines.append(f"| {run} | {level} | restart |")
            run += 1
    if not any(s.get("retry") for s in samples):
        return []
    return ["## Losses", "",
            "Every game over and how the bot answered it. A retry replays the level the run was lost on, with "
            "the hearts refilled; a bonus retry adds a card for that attempt. A press that staged nothing (an ad "
            "not watched) is listed but not counted. A retried level's times in the other sections count from "
            "its first attempt, game-over screens included.", "",
            "| Run | Level | Answer |", "|---|---|---|"] + lines + [""]


def reward_lines(samples, records):
    """The reward balloons the session saw: placed from the ticks' running counts (the last tick holds the totals),
    popped from the telemetry's pops by balloon type."""
    counted = [s for s in samples if "golden_placed" in s]
    if not counted:
        return []
    last = counted[-1]
    popped = {"Golden": 0, "Heart": 0}
    for record in records:
        if record.get("record_kind") == "level":
            by_type = record.get("axes", {}).get("pops_balloon_type", {})
            for name in popped:
                popped[name] += by_type.get(name, 0)
    lines = ["## Reward balloons", "",
             "Placed: counted on the bot's 50 ms loop when the reward service marks one on the board. Popped: the "
             "telemetry's pops by balloon type. A golden pop pays a card (or a heart with every card at its limit), "
             "a heart pop a heart.", "",
             "| Balloon | Placed | Popped |", "|---|---|---|",
             f"| Golden | {last['golden_placed']} | {popped['Golden']} |",
             f"| Heart | {last['heart_placed']} | {popped['Heart']} |", ""]
    if any(s.get("rewards_off") for s in counted):
        lines += ["The placement counter switched itself off after an error (a renamed field: the Editor console has "
                  "it), so the placed counts stop there.", ""]
    return lines


def build_report(label, seconds, player, tuning, samples, records, setup="", on_loss="stop"):
    lines = [f"# Playtest bot report{f' — {label}' if label else ''}", ""]
    lines.append(f"- Played {seconds:.0f} s of wall time, {len(samples)} ticks "
                 f"({seconds / max(1, len(samples)):.2f} s per tick).")
    lines.append(f"- On a game over: {ON_LOSS[on_loss]}.")
    if samples and samples[-1].get("game_over"):
        lines.append("- Stopped at the first game over.")
    if tuning:
        lines.append("- Tuning: " + ", ".join(f"{k} {v}" for k, v in tuning.items()) + ".")
    lines.append(f"- Player: {player}.")
    if setup:
        lines.append(f"- Setup: {setup}.")
    lines.append("")

    runs = summarize_runs(samples)
    if runs:
        ended = [ended for _, ended in split_runs(samples)]
        lines += ["## Runs", "",
                  "| Run | Ended | Reached level | Score | Hearts at end | Shots | Seconds | Retries | Of them bonus | Ads |",
                  "|---|---|---|---|---|---|---|---|---|---|"]
        for index, run in enumerate(runs, 1):
            span = (run["last"] or 0) - (run["first"] or 0)
            outcome = "game over" if index <= len(ended) and ended[index - 1] else "still playing"
            lines.append(f"| {index} | {outcome} | {run['level']} | {run['score']} | {run['hearts']} | "
                         f"{run['shots']} | {span:.0f} | {run['retries']} | {run['bonus']} | {run['ads']} |")
        failures = sum(ended)
        lines += ["", f"{failures} of {len(runs)} runs ended in a game over."]
        lines.append("")

    lines += loss_lines(samples)
    lines += reward_lines(samples, records)

    losses = heart_losses(samples)
    lines += ["## Hearts lost (ticks)", ""]
    if losses:
        lines += ["Every heart lost, with the ticks of the "
                  f"{LEAD_UP_SECONDS:g} s before it. A heart goes when a new line finds no room on the board.", "",
                  "| Run | Level | Seconds into level | Lost | Hearts left | Fill just before | "
                  f"Mean fill, last {LEAD_UP_SECONDS:g} s | Shots per 10 s | Mean balls in flight |",
                  "|---|---|---|---|---|---|---|---|---|"]

        def number(value, digits=2):
            return "-" if value is None else f"{value:.{digits}f}"

        for loss in losses:
            lines.append(
                f"| {loss['run']} | {loss['level']} | {loss['into_level']:.0f} | {loss['lost']} | "
                f"{loss['hearts_left']} | {number(loss['fill_before'])} | {number(loss['fill_mean'])} | "
                f"{loss['shots_per_10s']:.1f} | {number(loss['inflight_mean'], 1)} |")
    else:
        lines.append("No hearts were lost.")
    lines.append("")

    levels = [r for r in records if r.get("record_kind") == "level"]
    if levels:
        lines += ["## Levels (telemetry)", "",
                  "| Level | Cleared | Seconds | Shots | Pops | Pops per shot | Hearts lost | Max streak |",
                  "|---|---|---|---|---|---|---|---|"]
        for record in levels:
            metrics = record.get("metrics", {})
            shots = metrics.get("shots_fired", 0)
            pops = metrics.get("pops", 0)
            per_shot = f"{pops / shots:.1f}" if shots else "-"
            lines.append(
                f"| {record.get('level_index')} | {'yes' if record.get('completed') else 'no'} | "
                f"{record.get('timers', {}).get('gameplay_seconds', 0):.1f} | {shots} | {pops} | {per_shot} | "
                f"{metrics.get('hearts_lost', 0)} | {metrics.get('max_streak', 0)} |")
        lines.append("")

    # Hearts are lost only when a spawning line finds the board full, so how full it runs is the danger.
    fills = {}
    for sample in samples:
        if "fill" in sample and sample.get("phase") == "Playing":
            fills.setdefault(sample["level"], []).append(sample["fill"])
    if fills:
        lines += ["## Board fill (ticks)", "",
                  "Share of open slots holding a balloon while playing. Hearts are lost only when a new line "
                  "finds no room, so the closer to 1, the closer to danger.", "",
                  "| Level | Ticks | Mean | Median | 90th percentile | Max | Ticks at 0.9 or more |",
                  "|---|---|---|---|---|---|---|"]
        for level in sorted(fills):
            values = fills[level]
            high = sum(1 for v in values if v >= 0.9)
            lines.append(
                f"| {level} | {len(values)} | {statistics.mean(values):.2f} | {statistics.median(values):.2f} | "
                f"{percentile(values, 0.9):.2f} | {max(values):.2f} | {high} ({high / len(values):.0%}) |")
        lines.append("")

    stretches = release_and_tension(samples)
    if stretches is not None:
        lines += release_and_tension_lines(stretches)

    flights = [r["timers"]["gameplay_seconds"] for r in records
               if r.get("record_kind") == "flight" and r.get("timers")]
    if flights:
        lines += ["## Flights", "",
                  f"- {len(flights)} flights: mean {statistics.mean(flights):.2f} s, median "
                  f"{statistics.median(flights):.2f} s, middle half {percentile(flights, 0.25):.2f}–"
                  f"{percentile(flights, 0.75):.2f} s, 90th percentile {percentile(flights, 0.9):.2f} s, "
                  f"longest {max(flights):.2f} s.", ""]
    if not levels and not flights:
        lines += ["No telemetry records arrived for this session. Telemetry is written by the "
                  "development build only (UNITY_ENABLE_CHECKS), and a level writes its record when it ends.", ""]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--seconds", type=int, default=240, help="wall-clock seconds to play (default 240)")
    parser.add_argument("--label", default="", help="a name for this session, used in the folder and report")
    parser.add_argument("--project", default=REPO_ROOT,
                        help="the Unity project the Editor has open (default: this checkout)")
    parser.add_argument("--on-loss", choices=tuple(ON_LOSS), default="stop",
                        help="what a game over gets: stop (default) ends the session; restart starts a new run; "
                             "retry takes the run's retries (free, then behind an ad); bonus takes the rewarded-ad "
                             "bonus retry while it is offered. Past the run's retries, a new run")
    parser.add_argument("--restart", action="store_true", help="the same as --on-loss restart")
    parser.add_argument("--keep-playing", action="store_true", help="leave Play mode running afterwards")
    parser.add_argument("--player", choices=("best", "human"), default="human",
                        help="best: the solver, at once. human (default): reaction time and aim error")
    parser.add_argument("--skill", choices=tuple(SKILLS), default="average",
                        help="human: the preset for the knobs below (default average); a knob given wins")
    parser.add_argument("--react-floor", type=float,
                        help="human: seconds before any reaction to a ready cannon")
    parser.add_argument("--react-mean", type=float,
                        help="human: mean of the exponential wait on top of the floor, seconds")
    parser.add_argument("--aim-sd", type=float, help="human: standard deviation of the aim error, degrees")
    parser.add_argument("--wild", type=float, help="human: share of shots fired unaimed, at any angle (0-1)")
    parser.add_argument("--purple", action="store_true",
                        help="restart the first run on the first purple (extra colour) level its seed rolls")
    parser.add_argument("--hearts", type=int, default=0,
                        help="start the first run with this many hearts (default: as configured)")
    args = parser.parse_args()
    if args.restart and args.on_loss == "stop":
        args.on_loss = "restart"
    for knob, value in SKILLS[args.skill].items():
        if getattr(args, knob) is None:
            setattr(args, knob, value)
    # The Windows console defaults to a legacy code page; the report has dashes and en dashes.
    sys.stdout.reconfigure(encoding="utf-8")

    editor = Editor(find_unity(), os.path.abspath(args.project))
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    tick_code = (open(os.path.join(TOOL_DIR, "bot_tick.cs"), encoding="utf-8").read()
                 .replace("__MODE__", args.player)
                 .replace("__REACT_FLOOR__", repr(args.react_floor))
                 .replace("__REACT_MEAN__", repr(args.react_mean))
                 .replace("__AIM_SD__", repr(args.aim_sd))
                 .replace("__WILD__", repr(args.wild))
                 .replace("__BOT_ID__", stamp)
                 .replace("__ON_LOSS__", args.on_loss)
                 .replace("__PURPLE__", "true" if args.purple else "false")
                 .replace("__HEARTS__", str(args.hearts)))
    setup = ", ".join(([("started on the run's first purple level, through Start From Level")] if args.purple else [])
                      + ([f"started with {args.hearts} heart{'s' if args.hearts != 1 else ''}"] if args.hearts > 0 else []))
    if args.player == "best":
        player = "best (the solver, fired the moment the cannon is ready)"
    else:
        player = (f"human, {args.skill} skill (reaction {args.react_floor:g} s + exponential wait, mean "
                  f"{args.react_mean:g} s; aim error sd {args.aim_sd:g} degrees; {args.wild:.0%} of shots unaimed)")
    out_dir = os.path.join(REPO_ROOT, "Logs", "playtest", stamp + (f"-{args.label}" if args.label else ""))
    os.makedirs(out_dir, exist_ok=True)

    editor.wait_ready()
    # An unfocused Editor stops ticking the game; these keep it running while the bot plays.
    editor.command("editor", "set_auto_tick")
    editor.command("editor.playmode", "set_run_in_background")
    telemetry_dir = os.path.join(editor.eval("return UnityEngine.Application.persistentDataPath;"), "telemetry")
    if editor.eval("return UnityEditor.EditorApplication.isPlaying;") is not True:
        editor.command("editor.playmode", "start")
        editor.wait_ready()

    started = datetime.datetime.now(datetime.timezone.utc)
    print(f"Playing for {args.seconds} s; ticks go to {out_dir}", flush=True)
    samples = play(editor, args.seconds, tick_code, os.path.join(out_dir, "ticks.jsonl"))
    played = (datetime.datetime.now(datetime.timezone.utc) - started).total_seconds()

    if not args.keep_playing:
        # Leaving Play mode ends the session, which flushes the telemetry still buffered.
        editor.command("editor.playmode", "stop")
        editor.wait_ready()

    report = build_report(args.label, played, player, read_tuning(editor.project), settle_retries(samples),
                          telemetry_since(telemetry_dir, started), setup, args.on_loss)
    with open(os.path.join(out_dir, "report.md"), "w", encoding="utf-8") as out:
        out.write(report + "\n")
    print(report)


if __name__ == "__main__":
    main()
