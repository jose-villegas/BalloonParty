# Device performance runs

`device_perf.py` measures the game on an Android phone while the build plays itself, so two builds can be
compared on the same footing: same player, same seed, same length.

```bash
python3 Tools/perf/device_perf.py --serial 192.168.0.219:41379 --seconds 180 --label baseline
```

It needs a build with the autoplay in it (`Assets/Source/Cheats/README.md`, Autoplay): a checked build, or a
release build with cheats (`CHEATS_IN_RELEASE`), which is the one to measure because it runs the code that
ships. The script stops the app, launches it with an autoplay spec, waits out the launch screen, then samples
over adb every 5 seconds and stops the app at the end.

## What it measures

| Measure | Where it comes from | Why it matters |
|---|---|---|
| Main thread, million cycles per frame | `/proc/<pid>/task/<tid>/time_in_state` (time at each clock speed) over the frames delivered | The work one frame takes on the thread that misses frames |
| Render thread, million cycles per frame | The same, for `UnityGfxDeviceW` | Draw-call submission |
| All threads, million cycles per frame | The same, summed | Heat: every thread's work warms the phone, and a warm phone drops the frame rate |
| Main and render thread ms per frame | `/proc/<pid>/task/<tid>/stat` (utime + stime) | Time against the frame's budget, at whatever clock the phone chose |
| Frames per second, frame percentiles | `dumpsys SurfaceFlinger --latency` of the game's layer | What the player sees. `dumpsys gfxinfo` reports nothing for a Unity surface |
| Frames over 1.5 vsyncs | The same | Missed frames |
| Thermal status, battery temperature | `dumpsys thermalservice`, `dumpsys battery` | The phone slows its cores as it warms, so compare runs at the same status |
| Top CPU clock | `/sys/devices/system/cpu/cpufreq` | Shows that slowing |
| Memory | `dumpsys meminfo` at 60% of the run | Java heap, native heap, graphics, total PSS |

## Reading it

Compare the cycles per frame between runs. Not frames per second: the game is vsync-locked, so a faster build
shows the same rate. And not milliseconds or a share of a core: the phone lowers its clocks to fit the load,
so the same work reads as more time on a slower core.

The session holds its frame rate (`--fps`, 120 by default; the autoplay's `fps` key pins the thermal
governor). Without that the governor steps the game down to 80 and then 60 as the phone warms (at thermal
status 1, which a charging phone already reports), and a frame at 60 does more work than one at 120: the
fixed-step physics runs more steps in it, and the 60 Hz scene capture lands on every frame.

The big and little cores do different amounts of work per cycle, and the scheduler moves threads between
them, so expect a few percent of noise. Run each build twice, in turn (A, B, A, B).

## What a part of the game costs

`--off` switches parts of the game off for the session, so the difference from a normal run is what they
cost on that device:

```bash
python3 Tools/perf/device_perf.py --serial <serial> --label no-lens --off lens
```

| Name | Switches off |
|---|---|
| `lens` | The effects-clearing lens: its mask, both effects maps, their composites |
| `hudmap` | Only the HUD's effects map |
| `capture` | The scene capture the bounce light and soft shadows are made from |
| `animators` | The balloons' idle-sway Animators |
| `post` | Post-processing on the main camera: bloom, tonemapping, the colour grade |
| `hud` | The HUD canvases' drawing (their scripts keep running) |

Join several with `+`. The switches live in `CheatState.PerfOff` and compile out of a shipping build.

Reports go to `Logs/perf/<stamp>-<label>/` (`report.md`, `samples.csv`), which git ignores.
