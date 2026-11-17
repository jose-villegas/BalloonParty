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
| UnityMain ms per frame | `/proc/<pid>/task/<tid>/stat` (utime + stime) over the frames delivered | The main thread is what misses the frame. At 120 Hz a frame is 8.33 ms |
| Render thread ms per frame | The same, for `UnityGfxDeviceW` | Draw-call submission |
| All threads ms per frame | The same, summed | Heat: every thread's work warms the phone |
| Frames per second, frame percentiles | `dumpsys SurfaceFlinger --latency` of the game's layer | What the player sees. `dumpsys gfxinfo` reports nothing for a Unity surface |
| Frames over 1.5 vsyncs | The same | Missed frames |
| Thermal status, battery temperature | `dumpsys thermalservice`, `dumpsys battery` | The phone slows its cores as it warms, so compare runs at the same status |
| Top CPU clock | `/sys/devices/system/cpu/cpufreq` | Shows that slowing |
| Memory | `dumpsys meminfo` at 60% of the run | Java heap, native heap, graphics, total PSS |

## Reading it

Compare "UnityMain ms per frame" between runs, not frames per second: the game is vsync-locked, so a
faster build shows the same 120 until the phone warms up. A run that starts at thermal status 0 and one that
starts at 2 are not comparable; let the phone cool between runs, and keep it off the charger for both or
on it for both.

Reports go to `Logs/perf/<stamp>-<label>/` (`report.md`, `samples.csv`), which git ignores.
