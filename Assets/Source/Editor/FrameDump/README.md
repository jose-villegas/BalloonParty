# FrameDump

Editor-only tooling for dumping the Unity Frame Debugger's captured event list to a
diff-friendly text file, born during the URP 2D Renderer migration to compare batch
composition before/after a rendering change. Two menu items under
**Tools > BalloonParty**: **Dump Frame Debugger** and **Dump Frame Debugger With Step
Screenshots**.

## Contents

| File | What it does |
|---|---|
| `FrameDebuggerDumper` | Menu entry points (`Dump` / `DumpWithStepScreenshots`), both just kick off `FrameDebuggerEventWalker.Begin`. Also owns file output: `BuildDumpFilePath` (names the dump from the walk-start timestamp, under `Baselines~/`), `WriteOutput` (header + rows), and `CaptureGameViewScreenshot` (queues a player-loop update so the screenshot lands even with the editor idle). |
| `FrameDebuggerEventWalker` | The shared async walk — an `EditorApplication.update` state machine that scrubs `FrameDebuggerUtility.limit` through every captured event so each one gets fresh `FrameDebuggerEventData`, optionally screenshotting each step. |
| `FrameDebuggerEventReader` | Resolves the reflection surface once and exposes typed per-event reads (`ReadCount`, `ReadLimit`, `SetLimit`, `TryReadEventData`, `ReadEventTypeName`, `ReadHierarchyPath`, `BuildRow`) for the walker. |
| `FrameDebuggerReflection` | Shared low-level reflection plumbing — resolving `UnityEditorInternal.FrameDebuggerInternal` members as either a property or a field, reading/writing static ints, and `LogResolutionFailure` for self-diagnosis. |

## Workflow

1. Open **Window > Analysis > Frame Debugger**, click **Enable**, and step to (or
   freeze) the frame you want captured.
2. Run **Tools > BalloonParty > Dump Frame Debugger** (or the screenshot variant).

Neither menu item enables the Frame Debugger or advances frames itself — they only read
whatever is currently captured. Running with 0 captured events shows a dialog explaining
this instead of writing an empty dump.

## Output (`Baselines~/`)

Every dump writes `framedump_<yyyyMMdd_HHmmss>.txt` under `Baselines~/` at the project
root, plus a full-frame Game View PNG twin (`.png` at the same base name):

- A header with the Unity version, event count, total draw calls, whether this editor
  build exposes a render-target-name field, and a batch-break-cause histogram
  (descending by count).
- One pipe-separated row per event: `index | eventType | hierarchyPath | shaderName |
  passName | drawCalls | batchBreakCause`, plus a trailing `renderTargetName` column
  when the field is present in this Unity version.

The screenshot variant additionally writes one PNG per event into a
`<dumpBaseName>_steps/event_<i:D4>.png` folder, each pairing with row `i` of the text
dump — useful for eyeballing exactly what a given batch break drew. It re-renders the
frame once per event, so it warns before running (it can take minutes and blocks the
editor).

`Baselines~/` is both git-ignored and Unity-ignored (the trailing `~` keeps
`AssetDatabase` from importing it) — dumps are throwaway local artifacts for diffing
across a change, not checked-in assets.

## Architecture

The Frame Debugger's dump surface (`FrameDebuggerUtility`, `FrameDebuggerEventData`) is
internal to `UnityEditorInternal.FrameDebuggerInternal`, so every read goes through
reflection in `FrameDebuggerReflection` / `FrameDebuggerEventReader`. Member shapes vary
across editor versions (`count`/`limit` can be a property or a field; the render-target
name field may not exist at all), so resolution is signature-discovering rather than
hardcoded to one Unity version, and degrades gracefully — a missing optional member
reads as `"-"` in the dump instead of throwing. If a *required* member can't be resolved,
`LogResolutionFailure` logs the full reflected API shape (every field/property/method on
both types) in one round-trip, since the tool is only ever driven by a human re-running
it after a Unity upgrade.

The walk exists because of one behavior: Unity's native side only populates
`FrameDebuggerEventData` for the event currently at `FrameDebuggerUtility.limit`. A
synchronous loop that reads every index without changing the limit gets the
currently-selected event's data back for every row — the stale-data bug that motivated
this tool. `FrameDebuggerEventWalker` instead scrubs the limit to `i + 1`, pumps the
player loop (`EditorApplication.QueuePlayerLoopUpdate` + `InternalEditorUtility
.RepaintAllViews`), and polls until the data reads as fresh for that index (with a
per-step timeout that degrades to a `"-"` row rather than hanging the walk forever).
Screenshot capture has the same one-frame-later constraint (`ScreenCapture
.CaptureScreenshot` completes on the next rendered frame), so it's polled the same way
and both the per-event and full-frame screenshots share the walk's tick loop. Cleanup
(restoring the original limit, clearing the progress bar, unsubscribing from
`EditorApplication.update`) runs on every exit path — finish, user cancel, timeout
exhaustion, or exception — so a failed walk can never leave the Frame Debugger clamped.

## Dependencies

- `BalloonParty.Editor` assembly (editor-only platform); no runtime references.
- `UnityEditorInternal.InternalEditorUtility` directly, for `RepaintAllViews`. The
  Frame Debugger's own internal types (`FrameDebuggerUtility`, `FrameDebuggerEventData`,
  under `UnityEditorInternal.FrameDebuggerInternal`) are resolved by name via reflection
  instead, since they're `internal` and not compile-time referenceable.
