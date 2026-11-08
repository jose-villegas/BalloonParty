#!/usr/bin/env python3
"""Plays BalloonParty in a running Unity Editor and reports how the runs went.

Drives the Editor through the Unity CLI and its Pipeline package (com.unity.pipeline): enters Play
mode, sends bot_tick.cs once per tick (the first installs the shooter in the Editor; every tick
restarts a run on game over and reports state), then stops Play mode and builds a report from the
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
    r"(?P<nav>\w+)/(?P<phase>\w+) shots (?P<shots>\d+)$")
FLIGHT_CONFIG = "Assets/Configuration/ProjectileFlightConfig.asset"
TUNING_KEYS = ("_projectileLoadDuration", "_reloadCooldownSeconds", "_maxBallsInFlight")


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
    path = os.path.join(project, FLIGHT_CONFIG)
    tuning = {}
    if os.path.exists(path):
        text = open(path, encoding="utf-8").read()
        for key in TUNING_KEYS:
            match = re.search(rf"^\s*{key}: (.+)$", text, re.MULTILINE)
            if match:
                tuning[key.lstrip("_")] = match.group(1).strip()
    return tuning


def play(editor, seconds, tick_code, ticks_path):
    deadline = time.time() + seconds
    samples = []
    with open(ticks_path, "w", encoding="utf-8") as out:
        while time.time() < deadline:
            try:
                line = editor.eval(tick_code)
            except (RuntimeError, subprocess.TimeoutExpired) as error:
                line = f"error {error}"
            sample = {"wall": round(time.time(), 2), "line": line}
            match = TICK_RE.match(line or "")
            if match:
                sample.update({k: (v if k in ("nav", "phase") else float(v) if k == "t" else int(v))
                               for k, v in match.groupdict().items()})
            elif line and line.endswith("RESTART"):
                sample["restart"] = True
            samples.append(sample)
            out.write(json.dumps(sample) + "\n")
            out.flush()
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
        if sample.get("restart") or current is None:
            if current and current["ticks"]:
                runs.append(current)
            current = {"ticks": 0, "level": 0, "score": 0, "hearts": None, "shots": 0, "first": None, "last": None,
                       "shots_at_start": None}
            if sample.get("restart"):
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


def build_report(label, seconds, player, tuning, samples, records):
    lines = [f"# Playtest bot report{f' — {label}' if label else ''}", ""]
    lines.append(f"- Played {seconds} s of wall time, {len(samples)} ticks "
                 f"({seconds / max(1, len(samples)):.2f} s per tick).")
    if tuning:
        lines.append("- Tuning: " + ", ".join(f"{k} {v}" for k, v in tuning.items()) + ".")
    lines.append(f"- Player: {player}.")
    lines.append("")

    runs = summarize_runs(samples)
    if runs:
        lines += ["## Runs", "", "| Run | Reached level | Score | Hearts at end | Shots | Seconds |",
                  "|---|---|---|---|---|---|"]
        for index, run in enumerate(runs, 1):
            span = (run["last"] or 0) - (run["first"] or 0)
            lines.append(f"| {index} | {run['level']} | {run['score']} | {run['hearts']} | {run['shots']} | {span:.0f} |")
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
    parser.add_argument("--keep-playing", action="store_true", help="leave Play mode running afterwards")
    parser.add_argument("--player", choices=("best", "human"), default="human",
                        help="best: the solver, at once. human (default): reaction time and aim error")
    parser.add_argument("--react-floor", type=float, default=0.25,
                        help="human: seconds before any reaction to a ready cannon (default 0.25)")
    parser.add_argument("--react-mean", type=float, default=0.6,
                        help="human: mean of the exponential wait on top of the floor, seconds (default 0.6)")
    parser.add_argument("--aim-sd", type=float, default=4.0,
                        help="human: standard deviation of the aim error, degrees (default 4)")
    args = parser.parse_args()
    # The Windows console defaults to a legacy code page; the report has dashes and en dashes.
    sys.stdout.reconfigure(encoding="utf-8")

    editor = Editor(find_unity(), os.path.abspath(args.project))
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    tick_code = (open(os.path.join(TOOL_DIR, "bot_tick.cs"), encoding="utf-8").read()
                 .replace("__MODE__", args.player)
                 .replace("__REACT_FLOOR__", repr(args.react_floor))
                 .replace("__REACT_MEAN__", repr(args.react_mean))
                 .replace("__AIM_SD__", repr(args.aim_sd))
                 .replace("__BOT_ID__", stamp))
    if args.player == "best":
        player = "best (the solver, fired the moment the cannon is ready)"
    else:
        player = (f"human (reaction {args.react_floor:g} s + exponential wait, mean {args.react_mean:g} s; "
                  f"aim error sd {args.aim_sd:g} degrees)")
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

    if not args.keep_playing:
        # Leaving Play mode ends the session, which flushes the telemetry still buffered.
        editor.command("editor.playmode", "stop")
        editor.wait_ready()

    report = build_report(args.label, args.seconds, player, read_tuning(editor.project), samples,
                          telemetry_since(telemetry_dir, started))
    with open(os.path.join(out_dir, "report.md"), "w", encoding="utf-8") as out:
        out.write(report + "\n")
    print(report)


if __name__ == "__main__":
    main()
