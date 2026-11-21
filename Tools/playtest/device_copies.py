"""Autoplay runs on one Android device, one after another per installed copy of the game, surviving adb drops.

usage: device_copies.py SERIAL SECONDS "PACKAGE|LABEL|L,T,R,B|ADOPT_DIR" ... -- SKILL [SKILL ...]
(L,T,R,B and ADOPT_DIR may be empty: "PACKAGE|LABEL||")

Each PACKAGE is a copy of the game already installed (a build with cheats, which plays itself from an autoplay
spec). Every copy plays the skills in order, the copies side by side. A run's ticks are pulled into
Logs/playtest/<stamp>-<LABEL>-<skill>-rN/ticks.jsonl. A run is never wiped from the device before
its ticks are on the PC: a pull that keeps failing stops that copy's runs. ADOPT_DIR names the folder of a run
already playing on that copy: it is waited for and pulled, not started. L,T,R,B places the copy's freeform window
after each launch (am task resize), so the copies stand side by side and none covers another.
"""
import datetime
import os
import subprocess
import sys
import threading
import time

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(TOOL_DIR, "..", ".."))
sys.path.insert(0, TOOL_DIR)
from playtest_bot import SKILLS  # noqa: E402

ADB = os.path.join(os.path.expanduser("~"), "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe")
LOG = os.path.join(REPO, "Logs", "playtest", "device_copies.log")
LOCK = threading.Lock()
GRACE = 420
LAUNCH_FLAGS = os.environ.get("LAUNCH_FLAGS", "")


def log(text):
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    with LOCK, open(LOG, "a", encoding="utf-8") as out:
        out.write(f"{time.strftime('%H:%M:%S')} {text}\n")


def adb(serial, *args, timeout=120):
    """stdout, or None when the device can't be reached (after one reconnect)."""
    for attempt in range(2):
        try:
            done = subprocess.run([ADB, "-s", serial, *args], capture_output=True, text=True, timeout=timeout)
        except subprocess.TimeoutExpired:
            done = None
        if done is not None and done.returncode == 0:
            return done.stdout.strip()
        if done is not None and "not found" not in done.stderr and "offline" not in done.stderr and attempt == 0:
            return done.stdout.strip() if done.stdout.strip() else ""
        subprocess.run([ADB, "connect", serial], capture_output=True, text=True, timeout=30)
        time.sleep(3)
    return None


def spec_for(skill, seconds):
    if skill == "best":
        knobs = "player=best"
    else:
        s = SKILLS[skill]
        knobs = f"player=human floor={s['react_floor']:g} mean={s['react_mean']:g} aim={s['aim_sd']:g} wild={s['wild']:g}"
    return f"{knobs} seconds={seconds} loss=retry"


def wait_done(serial, files, seconds, started):
    while time.time() - started < seconds + GRACE:
        time.sleep(15)
        if adb(serial, "shell", f"test -f {files}/autoplay/done && echo done") == "done":
            return True
    return False


def pull(serial, files, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    target = os.path.join(out_dir, "ticks.jsonl")
    for attempt in range(20):
        adb(serial, "pull", f"{files}/autoplay/ticks.jsonl", target, timeout=180)
        if os.path.exists(target) and os.path.getsize(target) > 0:
            return True
        time.sleep(30)
    return False


def place(serial, package, bounds):
    """Moves the copy's freeform window to its bounds; a covered window stops playing."""
    if not bounds:
        return
    for attempt in range(6):
        time.sleep(5)
        stacks = adb(serial, "shell", "am", "stack", "list") or ""
        for line in stacks.splitlines():
            line = line.strip()
            if line.startswith("taskId=") and f" {package}/" in line:
                task = line.split("=", 1)[1].split(":", 1)[0]
                adb(serial, "shell", "am", "task", "resize", task, *bounds.split(","))
                return
    log(f"{package}: no task found to place")


def play(serial, package, curve, bounds, adopt, skills, seconds):
    files = f"/sdcard/Android/data/{package}/files"
    for index, skill in enumerate(skills):
        round_number = index // max(1, len(set(skills))) + 1
        if index == 0 and adopt:
            out_dir, started = adopt, time.time()
            log(f"{package}: adopting the run already playing -> {os.path.basename(out_dir)}")
        else:
            stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            out_dir = os.path.join(REPO, "Logs", "playtest", f"{stamp}-{curve}-{skill}-r{round_number}")
            activity = None
            for attempt in range(10):
                resolved = adb(serial, "shell", "cmd", "package", "resolve-activity", "--brief", package)
                if resolved:
                    activity = resolved.splitlines()[-1].strip()
                    break
                time.sleep(30)
            if not activity or "/" not in activity:
                log(f"{package}: can't resolve its activity; stopping this copy")
                return
            adb(serial, "shell", "am", "force-stop", package)
            adb(serial, "shell", "rm", "-rf", f"{files}/autoplay")
            adb(serial, "shell", f"am start {LAUNCH_FLAGS} -n {activity} -e autoplay '{spec_for(skill, seconds)}'")
            started = time.time()
            place(serial, package, bounds)
            log(f"{package}: {curve} {skill} round {round_number} started -> {os.path.basename(out_dir)}")
        finished = wait_done(serial, files, seconds, started)
        if not pull(serial, files, out_dir):
            log(f"{package}: could not pull the ticks of {os.path.basename(out_dir)}; they stay on the device, stopping this copy")
            return
        lines = sum(1 for _ in open(os.path.join(out_dir, "ticks.jsonl"), encoding="utf-8"))
        battery = adb(serial, "shell", "dumpsys battery | grep -m1 ' level'") or "?"
        log(f"{package}: pulled {os.path.basename(out_dir)} ({lines} ticks, {'finished' if finished else 'not finished in time'}), battery {battery.strip()}")
    log(f"{package}: all runs done")


def main():
    split = sys.argv.index("--")
    serial, seconds = sys.argv[1], int(sys.argv[2])
    copies, skills = sys.argv[3:split], sys.argv[split + 1:]
    threads = []
    for copy in copies:
        package, curve, bounds, adopt = (copy.split("|") + ["", "", ""])[:4]
        thread = threading.Thread(target=play, args=(serial, package, curve, bounds, adopt or None, skills, seconds))
        thread.start()
        threads.append(thread)
        time.sleep(20)
    for thread in threads:
        thread.join()
    log("runner done")


main()
