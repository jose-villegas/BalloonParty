"""How long a level takes, measured and fitted: the numbers and charts behind Docs/level-balance.md.

    python3 Tools/balance/level_balance.py collect "Logs/playtest/*from1-fit*tablet*"
    python3 Tools/balance/level_balance.py fit            # the points each level asks, from the target and the runs
    python3 Tools/balance/level_balance.py fit --apply    # ... written into the level pacing asset too
    python3 Tools/balance/level_balance.py charts         # every chart of the document, one picture each
    python3 Tools/balance/level_balance.py report         # the document's tables, to paste

`collect` reads the autoplay sessions a sweep left (their ticks.jsonl) and writes one row per level per run to
Docs/balance/data/runs-<curve>.csv, <curve> being a fingerprint of the points curve those runs played (the
asset's as it is now, or as it was at `--asset COMMIT`). Only runs that began at level 1 count. Everything else
reads those files, so the charts and the fit can be redone from the repository alone.

Every run tells how fast a player scores at a level, whatever curve it played. Only the runs that played the
asset's present curve tell how long its levels take: after a refit there are none until the next sweep.
Needs matplotlib for `charts`.
"""
import argparse
import collections
import csv
import glob
import hashlib
import json
import math
import os
import re
import statistics
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(REPO, "Docs", "balance")
DATA = os.path.join(OUT, "data")
ASSET = os.path.join(REPO, "Assets", "Configuration", "LevelPacingConfiguration.asset")
FIELDS = ["player", "run", "level", "seconds", "points", "shots", "hearts_lost", "retries"]

# The target: a level's seconds are a logarithm of its number, through these two (level, seconds). 55 s is what
# level 10 takes; 160 s at level 25 leaves two levels to a six-minute session.
ANCHORS = ((10, 55.0), (25, 160.0))
SESSION_SECONDS = 360
# What a level adds to a session outside play: the level-up, its card, the next board dealt.
OVERHEAD_SECONDS = 12
# The first level the fit sets (those before it keep their control points) and the last with a control point of
# its own: past it the curve's tail carries on.
FIRST_FITTED, LAST_POINT, FIT_UP_TO = 10, 40, 80
# Points a second are read as a trend from this level on, where at least this many runs gave a level: past
# where every run still plays, only the fastest are left, and they would read too high.
TREND_FROM, LEAST_RUNS = 19, 8

PLAYERS = (("average", "Average player", "#2a78d6"), ("high", "Expert", "#eb6834"), ("best", "Best-shot solver", "#1baf7a"))
SURFACE, INK, MUTED, GRID = "#fcfcfb", "#0b0b0b", "#52514e", "#dddcd6"
BANDS = ((1, 5), (6, 10), (11, 15), (16, 20), (21, 25), (26, 30), (31, 35), (36, 40), (41, 45), (46, 50), (51, 55),
         (56, 60))


def target(level):
    (l0, t0), (l1, t1) = ANCHORS
    return t0 + (t1 - t0) / math.log(l1 / l0) * math.log(level / l0)


def asset_text(commit=None):
    if commit is None:
        return open(ASSET, encoding="utf-8").read()
    relative = os.path.relpath(ASSET, REPO).replace("\\", "/")
    return subprocess.run(["git", "-C", REPO, "show", f"{commit}:{relative}"], capture_output=True, text=True,
                          encoding="utf-8", check=True).stdout


def asset_curve(text):
    """(cumulative control points by level, tail mode, tail rate) from a level pacing asset's text."""
    block = text[text.index("_scoringCurve:"):text.index("_generation:")]
    points = {int(level): float(score) for level, score in
              re.findall(r"- _level: (\d+)\s+_cumulativeScore: ([\d.]+)", block)}
    mode = int(re.search(r"_tailGrowth:\s+_mode: (\d)", block).group(1))
    rate = float(re.search(r"_rate: ([\d.]+)", block).group(1))
    return points, mode, rate


def fingerprint(text):
    """A short name for a points curve: the same control points and tail give the same one."""
    points, mode, rate = asset_curve(text)
    return hashlib.sha1(json.dumps([sorted(points.items()), mode, rate]).encode()).hexdigest()[:8]


def asked_of(text, up_to):
    """The points each level asks under an asset (before the game rounds each colour's share down to 50):
    control points a level apart, then the tail, geometric (mode 0) or linear (mode 1), as LevelScoringCurve does."""
    points, mode, rate = asset_curve(text)
    last = max(points)
    base = points[last] - points[last - 1]
    asked = {}
    for level in range(1, up_to + 1):
        if level <= last:
            asked[level] = points[level] - points.get(level - 1, 0)
        else:
            out = level - last
            asked[level] = base * rate ** (out - 1) if mode == 0 else base + rate * out
    return asked


def levels_of(folder):
    """The levels finished in a session's first run from level 1, as dicts; a level's time runs from its first
    tick to the next level's, retries and all."""
    out, start, previous = [], None, None
    lost = retries = 0
    for line in open(os.path.join(folder, "ticks.jsonl"), encoding="utf-8"):
        try:
            tick = json.loads(line)
        except json.JSONDecodeError:
            continue
        text = tick.get("line") or ""
        if text.endswith("RESTART") or " SETUP " in text or tick.get("restart"):
            if out:
                break
            start = previous = None
            continue
        if " RETRY " in text or tick.get("retry"):
            retries += 1
            continue
        if "level" not in tick or "t" not in tick or "score" not in tick:
            continue
        if start is None:
            if tick["level"] != 1 and not out:
                return []
            start, lost, retries = tick, 0, 0
        elif tick["level"] == start["level"] + 1:
            out.append({"level": start["level"], "seconds": round(tick["t"] - start["t"], 1),
                        "points": tick["score"] - start["score"], "shots": tick["shots"] - start["shots"],
                        "hearts_lost": lost, "retries": retries})
            start, lost, retries = tick, 0, 0
        elif tick["level"] != start["level"]:
            start, lost, retries = tick, 0, 0
        if previous is not None and tick["level"] == previous["level"] and tick.get("hearts", 0) < previous.get("hearts", 0):
            lost += previous["hearts"] - tick["hearts"]
        previous = tick
    return out


def read_rows(path):
    with open(path, encoding="utf-8") as handle:
        return [{**row, **{field: float(row[field]) for field in FIELDS[3:]}, "level": int(row["level"])}
                for row in csv.DictReader(handle)]


def collect(patterns, commit):
    """Adds the sessions' runs to the data file of the curve they played; a run collected before is replaced."""
    curve = fingerprint(asset_text(commit))
    path = os.path.join(DATA, f"runs-{curve}.csv")
    rows = read_rows(path) if os.path.exists(path) else []
    for pattern in patterns:
        for folder in sorted(glob.glob(pattern)):
            name = os.path.basename(folder.rstrip("/\\"))
            player = next((key for key, _, _ in PLAYERS if f"-{key}" in name), None)
            if player is None or not os.path.exists(os.path.join(folder, "ticks.jsonl")):
                continue
            rows = [row for row in rows if row["run"] != name]
            rows += [{"player": player, "run": name, **row} for row in levels_of(folder)]
    os.makedirs(DATA, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as out:
        writer = csv.DictWriter(out, fieldnames=FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    runs = {(row["player"], row["run"]) for row in rows}
    print(f"{os.path.relpath(path, REPO)}: {len(rows)} levels from " +
          ", ".join(f"{sum(1 for p, _ in runs if p == key)} {key}" for key, _, _ in PLAYERS) + " runs" +
          (" (the asset's present curve)" if curve == fingerprint(asset_text()) else ""))


def every_run():
    return [row for path in sorted(glob.glob(os.path.join(DATA, "runs-*.csv"))) for row in read_rows(path)]


def present_runs():
    """The runs that played the curve the asset holds now."""
    path = os.path.join(DATA, f"runs-{fingerprint(asset_text())}.csv")
    return read_rows(path) if os.path.exists(path) else []


def by_level(rows, player, field):
    got = collections.defaultdict(list)
    for row in rows:
        if row["player"] == player and row["seconds"] > 0:
            got[row["level"]].append(row[field])
    return got


def rate_by_level(rows, player):
    """Points a second at each level, pooled over the runs: (rate, runs)."""
    points, seconds, count = collections.Counter(), collections.Counter(), collections.Counter()
    for row in rows:
        if row["player"] == player and row["seconds"] > 0 and row["points"] > 0:
            points[row["level"]] += row["points"]
            seconds[row["level"]] += row["seconds"]
            count[row["level"]] += 1
    return {level: (points[level] / seconds[level], count[level]) for level in sorted(count)}


def trend(rates):
    """ln(rate) = c + d * level from TREND_FROM on, over the levels enough runs gave, weighted by runs."""
    xs = [(level, math.log(rate), n) for level, (rate, n) in rates.items() if level >= TREND_FROM and n >= LEAST_RUNS]
    weight = sum(n for _, _, n in xs)
    mean_level = sum(level * n for level, _, n in xs) / weight
    mean_log = sum(y * n for _, y, n in xs) / weight
    slope = (sum(n * (level - mean_level) * (y - mean_log) for level, y, n in xs) /
             sum(n * (level - mean_level) ** 2 for level, _, n in xs))
    return mean_log - slope * mean_level, slope


class Rate:
    """A player's points a second by level: the measured number where enough runs gave one (half of it the
    trend from TREND_FROM on, since a level's own number is some dozens of runs), the trend beyond."""

    def __init__(self, rows, player):
        self.rates = rate_by_level(rows, player)
        self.c, self.d = trend(self.rates)
        self.deep = max(level for level, (_, n) in self.rates.items() if n >= LEAST_RUNS)

    def growth(self):
        return math.exp(self.d) - 1

    def at(self, level):
        on_trend = math.exp(self.c + self.d * level)
        if level > self.deep:
            return on_trend
        near = [(self.rates[l][0], self.rates[l][1] * w) for l, w in ((level - 1, 0.5), (level, 1.0), (level + 1, 0.5))
                if l in self.rates and self.rates[l][1] >= LEAST_RUNS]
        if not near:
            return on_trend
        measured = sum(r * w for r, w in near) / sum(w for _, w in near)
        return measured if level < TREND_FROM else 0.5 * measured + 0.5 * on_trend


def fit(apply, sources):
    """The points each level asks: the target's seconds times the average player's points a second, never
    fewer than the level before. The points a second are read from every run collected, or with `sources` from
    the data files of those curves only. Written to Docs/balance/fit.json, and with `apply` into the asset."""
    paths = [os.path.join(DATA, f"runs-{curve}.csv") for curve in sources] if sources else \
        sorted(glob.glob(os.path.join(DATA, "runs-*.csv")))
    rows = [row for path in paths for row in read_rows(path)]
    average, expert = Rate(rows, "average"), Rate(rows, "high")
    kept, _, _ = asset_curve(asset_text())
    cumulative = {level: kept[level] for level in range(1, FIRST_FITTED)}
    previous = cumulative[FIRST_FITTED - 1] - cumulative[FIRST_FITTED - 2]
    asks = {}
    for level in range(FIRST_FITTED, FIT_UP_TO + 1):
        asks[level] = previous = 50 * round(max(previous, target(level) * average.at(level)) / 50)
        cumulative[level] = cumulative[level - 1] + asks[level]
    tail = (asks[FIT_UP_TO] / asks[LAST_POINT]) ** (1 / (FIT_UP_TO - LAST_POINT))
    result = {
        "anchors": ANCHORS, "data": [os.path.basename(path) for path in paths],
        "growth_average": round(average.growth(), 4), "growth_expert": round(expert.growth(), 4),
        "read_up_to": {"average": average.deep, "expert": expert.deep}, "tail_rate": round(tail, 3),
        "control_points": {str(level): int(cumulative[level]) for level in range(1, LAST_POINT + 1)},
        "levels": [{"level": level, "target": round(target(level), 1), "asked": int(asks[level]),
                    "average_rate": round(average.at(level), 1), "expert_rate": round(expert.at(level), 1)}
                   for level in range(FIRST_FITTED, FIT_UP_TO + 1)],
    }
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "fit.json"), "w", encoding="utf-8", newline="\n") as out:
        json.dump(result, out, indent=1)
    print(f"points a second grow {100 * average.growth():.1f}% a level for the average player (read up to level "
          f"{average.deep}), {100 * expert.growth():.1f}% for the expert (up to {expert.deep}); tail past level "
          f"{LAST_POINT}: {tail:.3f} a level")
    points, _, rate = asset_curve(asset_text())
    same = all(points.get(int(level)) == value for level, value in result["control_points"].items()) and rate == result["tail_rate"]
    print("the asset holds this fit" if same else "the asset holds another curve: --apply writes this one")
    if apply and not same:
        write_asset(result)


def write_asset(result):
    data = open(ASSET, "rb").read()
    newline = b"\r\n" if b"\r\n" in data else b"\n"
    text = data.decode("utf-8").replace("\r\n", "\n")
    start = text.index(f"    - _level: {FIRST_FITTED}\n      _cumulativeScore:")
    end = text.index("  _generation:", start)
    points = result["control_points"]
    new = "".join(f"    - _level: {level}\n      _cumulativeScore: {points[str(level)]}\n      _segmentMode: 0\n"
                  for level in range(FIRST_FITTED, LAST_POINT + 1))
    new += f"    _tailGrowth:\n      _mode: 0\n      _rate: {result['tail_rate']}\n"
    with open(ASSET, "wb") as out:
        out.write((text[:start] + new + text[end:]).encode("utf-8").replace(b"\n", newline))
    print(f"{os.path.relpath(ASSET, REPO)}: control points to level {LAST_POINT}, geometric tail {result['tail_rate']}")


def expected(result):
    """What the asset's curve should take each player at each level: the points it asks over the fit's rates."""
    asked = asked_of(asset_text(), FIT_UP_TO)
    return [{"level": entry["level"], "target": entry["target"], "asked": asked[entry["level"]],
             "average": asked[entry["level"]] / entry["average_rate"],
             "expert": asked[entry["level"]] / entry["expert_rate"]} for entry in result["levels"]]


def median_cell(values):
    return f"{statistics.median(values):.0f} s" if len(values) >= LEAST_RUNS else ""


def band_table(rows):
    """One Markdown table: level times by band of five levels, the three players side by side."""
    seconds = {key: by_level(rows, key, "seconds") for key, _, _ in PLAYERS}
    lost = by_level(rows, "average", "hearts_lost")
    head = ["Levels", "Target", "Average player", "Its middle half", "Levels in 6 min", "Expert", "Best-shot solver",
            "Hearts the average player loses a level"]
    lines = ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
    for low, high in BANDS:
        got = {key: [s for level in range(low, high + 1) for s in seconds[key].get(level, [])] for key, _, _ in PLAYERS}
        hearts = [h for level in range(low, high + 1) for h in lost.get(level, [])]
        if all(len(values) < LEAST_RUNS for values in got.values()):
            continue
        cells = [f"{low}–{high}", f"{target((low + high) / 2):.0f} s" if low >= FIRST_FITTED else "",
                 median_cell(got["average"])]
        if len(got["average"]) >= LEAST_RUNS:
            quarters = statistics.quantiles(got["average"], n=4)
            cells += [f"{quarters[0]:.0f}–{quarters[2]:.0f} s",
                      f"{SESSION_SECONDS / (statistics.median(got['average']) + OVERHEAD_SECONDS):.1f}"]
        else:
            cells += ["", ""]
        cells += [median_cell(got["high"]), median_cell(got["best"]),
                  f"{statistics.mean(hearts):.1f}" if len(hearts) >= LEAST_RUNS else ""]
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def report():
    rows = present_runs()
    print("### Measured\n")
    if not rows:
        print("No run has played the asset's present curve yet.")
    for key, name, _ in PLAYERS:
        reached, retries = collections.Counter(), collections.Counter()
        for row in rows:
            if row["player"] == key:
                reached[row["run"]] = max(reached[row["run"]], row["level"])
                retries[row["run"]] += row["retries"]
        if reached:
            print(f"- {name}: {len(reached)} runs, the last level finished {min(reached.values())} to "
                  f"{max(reached.values())}, {min(retries[r] for r in reached):.0f} to "
                  f"{max(retries[r] for r in reached):.0f} retries a run")
    if rows:
        print("\n" + band_table(rows))
    everything = every_run()
    print(f"\nPoints a second are read from {len({row['run'] for row in everything})} runs.")
    path = os.path.join(OUT, "fit.json")
    if os.path.exists(path):
        result = json.load(open(path, encoding="utf-8"))
        print("\n### The curve\n\n| Level | Target | Points asked | Levels in 6 min, expected |\n|---|---|---|---|")
        for entry in expected(result):
            if entry["level"] % 5 == 0 and entry["level"] <= 60:
                minutes, seconds = divmod(round(entry["target"]), 60)
                print(f"| {entry['level']} | {minutes}:{seconds:02d} | {entry['asked']:,.0f} | "
                      f"{SESSION_SECONDS / (entry['average'] + OVERHEAD_SECONDS):.1f} |")


def charts():
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 11, "axes.edgecolor": GRID, "axes.labelcolor": MUTED,
                         "xtick.color": MUTED, "ytick.color": MUTED, "text.color": INK, "axes.titlesize": 13,
                         "axes.titleweight": "bold", "axes.titlelocation": "left", "figure.facecolor": SURFACE,
                         "axes.facecolor": SURFACE, "savefig.facecolor": SURFACE})
    os.makedirs(OUT, exist_ok=True)

    def frame(title, ylabel, xmax):
        figure, axis = plt.subplots(figsize=(9.5, 5.2))
        axis.set_title(title, pad=12)
        axis.set_xlabel("Level")
        axis.set_ylabel(ylabel)
        axis.set_xlim(0, xmax)
        axis.grid(color=GRID, linewidth=0.6)
        axis.set_axisbelow(True)
        for side in ("top", "right"):
            axis.spines[side].set_visible(False)
        return figure, axis

    def save(figure, name):
        figure.tight_layout()
        figure.savefig(os.path.join(OUT, name), dpi=140)
        plt.close(figure)
        print(f"Docs/balance/{name}")

    def end_label(axis, x, y, text, colour):
        axis.annotate(text, (x, y), xytext=(6, 0), textcoords="offset points", va="center", fontsize=10, color=INK,
                      bbox={"boxstyle": "round,pad=0.15", "facecolor": SURFACE, "edgecolor": "none", "alpha": 0.85})
        axis.plot([x], [y], "o", markersize=6, color=colour, markeredgecolor=SURFACE, markeredgewidth=1.5)

    def sessions(axis, xmax):
        for minutes, text in ((3, "two levels to a six-minute session"), (6, "one level to a session")):
            axis.axhline(minutes, color=MUTED, linewidth=0.9, linestyle=(0, (4, 3)))
            axis.text(xmax * 0.995, minutes + 0.08, text, color=MUTED, fontsize=9.5, ha="right")

    def target_line(axis, levels):
        axis.plot(levels, [target(level) / 60 for level in levels], color=INK, linewidth=1.6, linestyle=(0, (5, 3)),
                  label="Target: a logarithm of the level")

    # Time per level on the present curve: every player's median, the middle half shaded.
    rows = present_runs()
    if rows:
        xmax = max(row["level"] for row in rows) + 7
        figure, axis = frame("Minutes to finish a level", "Minutes (game time)", xmax)
        sessions(axis, xmax)
        for key, name, colour in PLAYERS:
            seconds = by_level(rows, key, "seconds")
            levels = sorted(level for level, got in seconds.items() if len(got) >= 3)
            if not levels:
                continue
            runs = len({row["run"] for row in rows if row["player"] == key})
            median = [statistics.median(seconds[level]) / 60 for level in levels]
            quarters = [statistics.quantiles(seconds[level], n=4) for level in levels]
            axis.fill_between(levels, [q[0] / 60 for q in quarters], [q[2] / 60 for q in quarters], color=colour,
                              alpha=0.14, linewidth=0)
            axis.plot(levels, median, color=colour, linewidth=2, label=f"{name} ({runs} runs)")
            end_label(axis, levels[-1], median[-1], name, colour)
        target_line(axis, list(range(FIRST_FITTED, int(xmax) - 6)))
        axis.set_ylim(0, 8)
        axis.legend(loc="upper left", frameon=False)
        save(figure, "time.png")

        figure, axis = frame("Hearts lost a level", "Hearts lost a level (mean)", xmax)
        for key, name, colour in PLAYERS:
            lost = by_level(rows, key, "hearts_lost")
            levels = sorted(level for level, got in lost.items() if len(got) >= 4)
            if not levels:
                continue
            line = [statistics.mean(lost[level]) for level in levels]
            axis.plot(levels, line, color=colour, linewidth=2, label=name)
            end_label(axis, levels[-1], line[-1], name, colour)
        axis.set_ylim(0, None)
        axis.legend(loc="upper left", frameon=False)
        save(figure, "hearts.png")

    # Points a second by level, every run together, on a log scale: a straight line is steady growth.
    rows = every_run()
    if rows:
        figure, axis = frame("Points a second, by level", "Points a second (log scale)", 62)
        for key, name, colour in PLAYERS[:2]:
            model = Rate(rows, key)
            shown = [level for level, (_, n) in model.rates.items() if n >= 3]
            axis.plot(shown, [model.rates[level][0] for level in shown], "o", markersize=5, color=colour, alpha=0.55,
                      markeredgewidth=0, label=f"{name}, measured")
            span = list(range(TREND_FROM, 56))
            line = [math.exp(model.c + model.d * level) for level in span]
            # No label at the lines' ends: the two meet there. The legend names them.
            axis.plot(span, line, color=colour, linewidth=2, label=f"{name}, trend: +{100 * model.growth():.1f}% a level")
        axis.set_yscale("log")
        axis.set_yticks([10, 20, 50, 100, 200])
        axis.set_yticklabels(["10", "20", "50", "100", "200"])
        axis.minorticks_off()
        axis.legend(loc="upper left", frameon=False)
        save(figure, "rate.png")

        figure, axis = frame("Points a shot, by level", "Points a shot", 62)
        for key, name, colour in PLAYERS[:2]:
            points, shots, count = collections.Counter(), collections.Counter(), collections.Counter()
            for row in rows:
                if row["player"] == key and row["shots"] > 0:
                    points[row["level"]] += row["points"]
                    shots[row["level"]] += row["shots"]
                    count[row["level"]] += 1
            levels = sorted(level for level in count if count[level] >= LEAST_RUNS)
            line = [points[level] / shots[level] for level in levels]
            axis.plot(levels, line, color=colour, linewidth=2, label=name)
            end_label(axis, levels[-1], line[-1], name, colour)
        axis.set_ylim(0, None)
        axis.legend(loc="upper left", frameon=False)
        save(figure, "per-shot.png")

    # The points each level asks, and what that should take each player.
    asked = asked_of(asset_text(), 60)
    figure, axis = frame("Points a level asks", "Points (log scale)", 66)
    levels = list(range(1, 61))
    axis.plot(levels, [asked[level] for level in levels], color=PLAYERS[0][2], linewidth=2)
    for level in (10, 20, 30, 40, 50, 60):
        end_label(axis, level, asked[level], f"{asked[level]:,.0f}", PLAYERS[0][2])
    axis.set_yscale("log")
    axis.set_yticks([300, 1000, 3000, 10000, 30000])
    axis.set_yticklabels(["300", "1,000", "3,000", "10,000", "30,000"])
    axis.minorticks_off()
    save(figure, "asked.png")

    path = os.path.join(OUT, "fit.json")
    if os.path.exists(path):
        result = expected(json.load(open(path, encoding="utf-8")))
        figure, axis = frame("The target, and what the curve should take", "Minutes (game time)", 88)
        sessions(axis, 88)
        levels = [entry["level"] for entry in result]
        target_line(axis, levels)
        for field, (key, name, colour) in (("average", PLAYERS[0]), ("expert", PLAYERS[1])):
            line = [entry[field] / 60 for entry in result]
            axis.plot(levels, line, color=colour, linewidth=2, label=f"{name}, expected")
            end_label(axis, levels[-1], line[-1], name, colour)
        axis.set_ylim(0, 8)
        axis.legend(loc="upper left", frameon=False)
        save(figure, "target.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    gather = sub.add_parser("collect", help="read a sweep's sessions into Docs/balance/data/")
    gather.add_argument("sessions", nargs="+", help="session folders, or globs of them")
    gather.add_argument("--asset", metavar="COMMIT", help="the commit whose points curve the sessions played "
                                                          "(default: the asset as it is now)")
    fitting = sub.add_parser("fit", help="work out the points each level asks")
    fitting.add_argument("--apply", action="store_true", help="write the control points into the level pacing asset")
    fitting.add_argument("--from", dest="sources", nargs="+", metavar="CURVE",
                         help="read the points a second from these curves' data files only (the <curve> of "
                              "runs-<curve>.csv; default: every file)")
    sub.add_parser("charts", help="write every chart of the document")
    sub.add_parser("report", help="print the document's tables")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    if args.command == "collect":
        collect(args.sessions, args.asset)
    elif args.command == "fit":
        fit(args.apply, args.sources)
    elif args.command == "charts":
        charts()
    else:
        report()


if __name__ == "__main__":
    main()
