#!/usr/bin/env python3
"""Plays BalloonParty on Android emulators (or phones) in parallel and reports each session.

The Editor bot (playtest_bot.py) plays one session at a time in the Editor. This one hands each session
to a device instead: a checked (Debug or Checked code variant) build plays itself when launched with an
autoplay spec (Cheats/Autoplay/AutoplayController), writes its ticks and the telemetry under the app's
files, and quits when the time is up. The runner launches it, waits, pulls the files with adb and writes
the same report as the Editor bot.

Each player in the list is one session, on its own device; with more players than devices they queue.

Usage:
    python3 Tools/playtest/device_bot.py --boot 3 --apk Builds/local/BalloonParty-autoplay.apk \\
        --seconds 420 --label meadow average high best

Writes Logs/playtest/<stamp>-<label>-<player>/ticks.jsonl, telemetry/ and report.md per session.
See Tools/playtest/README.md.
"""

import argparse
import datetime
import glob
import json
import os
import queue
import subprocess
import sys
import threading
import time

from playtest_bot import REPO_ROOT, SKILLS, build_report, read_tuning

SDK = os.path.join(os.path.expanduser("~"), "AppData", "Local", "Android", "Sdk")
ADB = os.path.join(SDK, "platform-tools", "adb.exe")
EMULATOR = os.path.join(SDK, "emulator", "emulator.exe")
PACKAGE = "com.DefaultCompany.BalloonParty"
POLL_SECONDS = 5
# Past the session's own length: boot of the app, the launch screen, and a slow emulator's last frames.
GRACE_SECONDS = 180


def adb(serial, *args, timeout=120, check=True):
    result = subprocess.run([ADB, "-s", serial, *args], capture_output=True, text=True, timeout=timeout)
    if check and result.returncode != 0:
        raise RuntimeError(f"adb {' '.join(args)} on {serial}: {result.stderr.strip() or result.stdout.strip()}")
    return result.stdout.strip()


def devices():
    out = subprocess.run([ADB, "devices"], capture_output=True, text=True).stdout
    return [line.split()[0] for line in out.splitlines()[1:] if line.strip().endswith("device")]


def boot_emulators(avd, count):
    """Starts `count` read-only instances of the AVD (read-only lets several share one) and waits for each."""
    running = [d for d in devices() if d.startswith("emulator-")]
    serials = []
    port = 5554
    while len(serials) + len(running) < count:
        serial = f"emulator-{port}"
        if serial not in running:
            subprocess.Popen([EMULATOR, "-avd", avd, "-port", str(port), "-read-only", "-no-audio",
                              "-no-boot-anim", "-no-snapshot-save", "-gpu", "host"],
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            serials.append(serial)
        port += 2
    deadline = time.time() + 300
    for serial in serials:
        while time.time() < deadline:
            done = subprocess.run([ADB, "-s", serial, "shell", "getprop", "sys.boot_completed"],
                                  capture_output=True, text=True).stdout.strip()
            if done == "1":
                print(f"{serial} booted", flush=True)
                break
            time.sleep(3)
        else:
            raise RuntimeError(f"{serial} didn't boot in time")


def spec_for(player, args):
    if player == "best":
        knobs = "player=best"
    else:
        skill = SKILLS[player]
        knobs = (f"player=human floor={skill['react_floor']:g} mean={skill['react_mean']:g} "
                 f"aim={skill['aim_sd']:g} wild={skill['wild']:g}")
    return f"{knobs} seconds={args.seconds} restart={1 if args.restart else 0}"


def describe(player):
    if player == "best":
        return "best (the solver, fired the moment the cannon is ready)"
    skill = SKILLS[player]
    return (f"human, {player} skill (reaction {skill['react_floor']:g} s + exponential wait, mean "
            f"{skill['react_mean']:g} s; aim error sd {skill['aim_sd']:g} degrees; {skill['wild']:.0%} of shots "
            "unaimed)")


def launch_activity(serial):
    out = adb(serial, "shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE)
    return out.splitlines()[-1].strip()


def load_jsonl(paths):
    records = []
    for path in paths:
        for line in open(path, encoding="utf-8"):
            if line.strip():
                records.append(json.loads(line))
    return records


def play_session(serial, player, args, stamp):
    files = f"/sdcard/Android/data/{PACKAGE}/files"
    name = "-".join(part for part in (stamp, args.label, player) if part)
    out_dir = os.path.join(REPO_ROOT, "Logs", "playtest", name)
    os.makedirs(out_dir, exist_ok=True)

    adb(serial, "shell", "am", "force-stop", PACKAGE)
    adb(serial, "shell", "rm", "-rf", f"{files}/autoplay", f"{files}/telemetry", check=False)
    spec = spec_for(player, args)
    adb(serial, "shell", f"am start -n {launch_activity(serial)} -e autoplay '{spec}'")
    print(f"{serial}: {player} for {args.seconds} s -> {out_dir}", flush=True)

    started = time.time()
    finished = False
    while time.time() - started < args.seconds + GRACE_SECONDS:
        time.sleep(POLL_SECONDS)
        if adb(serial, "shell", f"test -f {files}/autoplay/done && echo done", check=False) == "done":
            finished = True
            break
        if time.time() - started > 30 and not adb(serial, "shell", "pidof", PACKAGE, check=False):
            break
    played = time.time() - started

    adb(serial, "pull", f"{files}/autoplay/ticks.jsonl", os.path.join(out_dir, "ticks.jsonl"), check=False)
    adb(serial, "pull", f"{files}/telemetry", out_dir, check=False)
    adb(serial, "shell", "am", "force-stop", PACKAGE, check=False)

    ticks_path = os.path.join(out_dir, "ticks.jsonl")
    samples = load_jsonl([ticks_path]) if os.path.exists(ticks_path) else []
    records = load_jsonl(glob.glob(os.path.join(out_dir, "telemetry", "telemetry_*.jsonl")))
    header = f"{name} on {serial}" + ("" if finished else " (ended early: the app stopped or never finished)")
    report = build_report(header, played, describe(player), read_tuning(REPO_ROOT), samples, records)
    with open(os.path.join(out_dir, "report.md"), "w", encoding="utf-8") as out:
        out.write(report + "\n")
    return name, finished, out_dir


def worker(serial, jobs, args, stamp, results):
    if args.apk:
        print(f"{serial}: installing {os.path.basename(args.apk)}", flush=True)
        adb(serial, "install", "-r", args.apk, timeout=600)
    while True:
        try:
            player = jobs.get_nowait()
        except queue.Empty:
            return
        try:
            results.append(play_session(serial, player, args, stamp))
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            print(f"{serial}: {player} failed: {error}", flush=True)
            results.append((player, False, str(error)))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("players", nargs="+", choices=("best", *SKILLS),
                        help="one session per entry: best, or a human skill (low, average, high)")
    parser.add_argument("--seconds", type=int, default=240, help="seconds each session plays (default 240)")
    parser.add_argument("--label", default="", help="a name for this batch, used in the folders and reports")
    parser.add_argument("--restart", action="store_true",
                        help="start a new run on game over instead of ending the session there")
    parser.add_argument("--apk", help="install this APK on each device first (a checked build)")
    parser.add_argument("--boot", type=int, default=0, help="start emulators until this many are running")
    parser.add_argument("--avd", default="bp-autoplay", help="the AVD --boot starts (default bp-autoplay)")
    parser.add_argument("--serial", action="append",
                        help="play on this device (repeatable); default: every running emulator, never a phone")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    if args.boot:
        boot_emulators(args.avd, args.boot)
    serials = args.serial or [d for d in devices() if d.startswith("emulator-")]
    if not serials:
        sys.exit("No device: start emulators with --boot N, or name one with --serial.")

    jobs = queue.Queue()
    for player in args.players:
        jobs.put(player)
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    results = []
    threads = [threading.Thread(target=worker, args=(serial, jobs, args, stamp, results))
               for serial in serials[:len(args.players)]]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    for name, finished, where in results:
        print(f"{'ok  ' if finished else 'FAIL'} {name}: {where}")


if __name__ == "__main__":
    main()
