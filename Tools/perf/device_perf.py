#!/usr/bin/env python3
"""Measures BalloonParty's performance on an Android phone while the build plays itself.

Launches the installed build with an autoplay spec (Cheats/Autoplay, in a checked build or a release build
with cheats), then samples it over adb every few seconds: CPU time per thread (from /proc, so it is exact,
not top's rounding), frame delivery (SurfaceFlinger's latency log of the game's layer), the thermal status,
the CPU clocks and the app's memory. Writes samples.csv and report.md under Logs/perf/<stamp>-<label>/.

The number to compare between two builds is "UnityMain ms per frame": the main thread's CPU time for one
frame. At 120 Hz a frame is 8.33 ms, and the phone slows its cores as it warms, so every millisecond there
is headroom before frames start to miss. Compare runs taken at the same thermal status, same seed and player.

Usage:
    python3 Tools/perf/device_perf.py --serial 192.168.0.219:41379 --seconds 180 --label baseline

See Tools/perf/README.md.
"""

import argparse
import datetime
import os
import re
import statistics
import subprocess
import sys
import time

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ADB = os.path.join(os.path.expanduser("~"), "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe")
PACKAGE = "com.DefaultCompany.BalloonParty"
CLOCK_TICKS = 100  # jiffies per second on Android (sysconf(_SC_CLK_TCK))
PENDING = 9223372036854775807
# One shell round trip per sample: the clock, every thread's stat line, the CPU clocks, the thermal status.
SAMPLE = (
    "P=$(pidof {pkg}); echo \"PID $P\"; echo \"UP $(cat /proc/uptime)\"; "
    "for t in /proc/$P/task/*; do echo \"T $(cat $t/stat 2>/dev/null)\"; done; "
    "echo \"F $(cat /sys/devices/system/cpu/cpufreq/policy*/scaling_cur_freq | tr '\\n' ' ')\"; "
    "echo \"TH $(dumpsys thermalservice | grep 'Thermal Status')\"; "
    "echo \"B $(dumpsys battery | grep temperature)\""
)


def adb(serial, *args, timeout=60):
    result = subprocess.run([ADB, "-s", serial, *args], capture_output=True, text=True, timeout=timeout,
                            encoding="utf-8", errors="replace")
    return result.stdout


def launch_activity(serial):
    return adb(serial, "shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE).strip().splitlines()[-1]


def game_layer(serial):
    """The game's surface in SurfaceFlinger: the BLAST layer of Unity's SurfaceView."""
    names = [line.strip() for line in adb(serial, "shell", "dumpsys", "SurfaceFlinger", "--list").splitlines()
             if PACKAGE in line]
    blast = [n for n in names if "BLAST" in n and "SurfaceView" in n]
    return (blast or names or [None])[-1]


def read_sample(serial):
    out = adb(serial, "shell", SAMPLE.format(pkg=PACKAGE))
    sample = {"threads": {}, "pid": None, "uptime": None, "freqs": [], "thermal": None, "battery": None}
    for line in out.splitlines():
        if line.startswith("PID "):
            sample["pid"] = line[4:].strip() or None
        elif line.startswith("UP "):
            sample["uptime"] = float(line.split()[1])
        elif line.startswith("T ") and ")" in line:
            # "tid (comm) state ppid ... utime stime": comm may hold spaces, so split after its closing bracket.
            head, tail = line[2:].rsplit(")", 1)
            tid, comm = head.split(" (", 1)
            fields = tail.split()
            sample["threads"][tid.strip()] = (comm, int(fields[11]) + int(fields[12]))
        elif line.startswith("F "):
            sample["freqs"] = [int(v) for v in line[2:].split() if v.isdigit()]
        elif line.startswith("TH "):
            match = re.search(r"(\d+)", line)
            sample["thermal"] = int(match.group(1)) if match else None
        elif line.startswith("B "):
            match = re.search(r"(\d+)", line)
            sample["battery"] = int(match.group(1)) / 10.0 if match else None
    return sample


def read_frames(serial, layer):
    """Present times (ns) of the layer's last ~127 frames, oldest first."""
    out = adb(serial, "shell", f"dumpsys SurfaceFlinger --latency '{layer}'")
    rows = out.strip().splitlines()
    if len(rows) < 2:
        return 0, []
    period = int(rows[0]) if rows[0].strip().isdigit() else 0
    presents = []
    for row in rows[1:]:
        parts = row.split()
        if len(parts) == 3 and parts[1].isdigit():
            present = int(parts[1])
            if 0 < present < PENDING:
                presents.append(present)
    return period, presents


def percentile(values, share):
    if not values:
        return 0.0
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(share * len(ordered)))]


def meminfo(serial):
    out = adb(serial, "shell", "dumpsys", "meminfo", PACKAGE)
    wanted = ("Java Heap", "Native Heap", "Graphics", "Code", "Private Other", "System", "TOTAL PSS")
    found = {}
    for line in out.splitlines():
        for key in wanted:
            match = re.match(rf"\s*{key}:\s+(\d+)", line)
            if match and key not in found:
                found[key] = int(match.group(1)) / 1024.0
    return found


def run(args):
    serial = args.serial
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    out_dir = os.path.join(REPO_ROOT, "Logs", "perf", f"{stamp}-{args.label}")
    os.makedirs(out_dir, exist_ok=True)

    spec = f"player={args.player} seconds={args.seconds + args.warmup + 30} seed={args.seed} restart=1"
    adb(serial, "shell", "am", "force-stop", PACKAGE)
    time.sleep(1)
    adb(serial, "shell", f"am start -n {launch_activity(serial)} -e autoplay '{spec}'")
    print(f"launched with '{spec}'; warming up {args.warmup}s", flush=True)
    time.sleep(args.warmup)

    layer = game_layer(serial)
    if not layer:
        print("the game has no surface: is it running?", file=sys.stderr)
        return 1

    rows = []
    intervals_ms = []
    seen = set()
    period_ns = 0
    memory = {}
    previous = read_sample(serial)
    started = time.time()
    while time.time() - started < args.seconds:
        time.sleep(args.interval)
        sample = read_sample(serial)
        if not sample["pid"] or sample["pid"] != previous["pid"]:
            print("the game stopped or restarted; ending the run", file=sys.stderr)
            break

        period, presents = read_frames(serial, layer)
        period_ns = period or period_ns
        fresh = []
        for a, b in zip(presents, presents[1:]):
            if b not in seen:
                fresh.append((b - a) / 1e6)
            seen.add(b)
        intervals_ms.extend(fresh)

        dt = sample["uptime"] - previous["uptime"]
        fps = 1000.0 / statistics.mean(fresh) if fresh else 0.0
        frames = max(1.0, fps * dt)
        cpu_ms = {}
        for tid, (comm, ticks) in sample["threads"].items():
            before = previous["threads"].get(tid)
            if before:
                cpu_ms[comm] = cpu_ms.get(comm, 0.0) + (ticks - before[1]) * 1000.0 / CLOCK_TICKS
        total = sum(cpu_ms.values())
        row = {
            "t": round(time.time() - started, 1),
            "thermal": sample["thermal"],
            "battery_c": sample["battery"],
            "fps": round(fps, 1),
            "frame_p95_ms": round(percentile(fresh, 0.95), 2),
            "main_ms_per_frame": round(cpu_ms.get("UnityMain", 0.0) / frames, 3),
            "gfx_ms_per_frame": round(cpu_ms.get("UnityGfxDeviceW", 0.0) / frames, 3),
            "all_ms_per_frame": round(total / frames, 3),
            "main_core_share": round(cpu_ms.get("UnityMain", 0.0) / (dt * 1000.0), 3),
            "max_cpu_mhz": max(sample["freqs"]) // 1000 if sample["freqs"] else 0,
            "top_threads": " ".join(f"{name}:{ms / frames:.2f}" for name, ms in
                                    sorted(cpu_ms.items(), key=lambda kv: -kv[1])[:6]),
        }
        rows.append(row)
        print(f"{row['t']:6.1f}s thermal {row['thermal']} fps {row['fps']:5.1f} "
              f"main {row['main_ms_per_frame']:.2f} gfx {row['gfx_ms_per_frame']:.2f} "
              f"all {row['all_ms_per_frame']:.2f} ms/frame", flush=True)
        if not memory and time.time() - started > args.seconds * 0.6:
            memory = meminfo(serial)
        previous = sample

    adb(serial, "shell", "am", "force-stop", PACKAGE)
    if not rows:
        return 1

    keys = list(rows[0].keys())
    with open(os.path.join(out_dir, "samples.csv"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(",".join(keys) + "\n")
        for row in rows:
            handle.write(",".join(str(row[k]) for k in keys) + "\n")

    vsync_ms = period_ns / 1e6 if period_ns else 8.33
    missed = [v for v in intervals_ms if v > vsync_ms * 1.5]
    report = [
        f"# Device performance: {args.label}",
        "",
        f"- Build: the one installed on {serial}. Player {args.player}, seed {args.seed}, "
        f"{len(rows)} samples over {rows[-1]['t']:.0f} s after a {args.warmup} s warm-up.",
        f"- Thermal status {rows[0]['thermal']} at the start, {rows[-1]['thermal']} at the end; "
        f"battery {rows[0]['battery_c']} to {rows[-1]['battery_c']} C.",
        "",
        "| Measure | Mean | Median | Worst sample |",
        "|---|---|---|---|",
    ]
    for key, title in (("main_ms_per_frame", "UnityMain CPU, ms per frame"),
                       ("gfx_ms_per_frame", "Render thread CPU, ms per frame"),
                       ("all_ms_per_frame", "All threads CPU, ms per frame"),
                       ("fps", "Frames per second")):
        values = [row[key] for row in rows]
        worst = min(values) if key == "fps" else max(values)
        report.append(f"| {title} | {statistics.mean(values):.2f} | {statistics.median(values):.2f} | {worst:.2f} |")
    report += [
        "",
        f"- Frames timed: {len(intervals_ms)}; vsync {vsync_ms:.2f} ms. Mean {statistics.mean(intervals_ms):.2f} ms, "
        f"95th percentile {percentile(intervals_ms, 0.95):.2f} ms, 99th {percentile(intervals_ms, 0.99):.2f} ms.",
        f"- Frames that took more than 1.5 vsyncs: {len(missed)} ({100.0 * len(missed) / max(1, len(intervals_ms)):.2f}%).",
        "- Memory (MB): " + ", ".join(f"{k} {v:.0f}" for k, v in memory.items()) + ".",
        "",
        "Threads by CPU per frame, last sample: " + rows[-1]["top_threads"] + ".",
        "",
    ]
    with open(os.path.join(out_dir, "report.md"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(report))
    print("\n".join(report))
    print(f"wrote {out_dir}")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--serial", required=True, help="adb serial (ip:port over Wi-Fi)")
    parser.add_argument("--seconds", type=int, default=180, help="seconds measured, after the warm-up")
    parser.add_argument("--warmup", type=int, default=25, help="seconds from launch before measuring")
    parser.add_argument("--interval", type=float, default=5.0, help="seconds between samples")
    parser.add_argument("--player", default="best", choices=("best", "human"))
    parser.add_argument("--seed", type=int, default=7)
    parser.add_argument("--label", default="run")
    return run(parser.parse_args())


if __name__ == "__main__":
    sys.exit(main())
