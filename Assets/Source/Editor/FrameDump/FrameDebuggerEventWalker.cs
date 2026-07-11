using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// Shared async walk behind both dump menu items. Scrubs <c>FrameDebuggerUtility.limit</c>
    /// through every captured event so the native side populates fresh
    /// <c>FrameDebuggerEventData</c> per event — reading all indices without scrubbing returns the
    /// currently-selected event's data every time (the stale-data bug the first dump run exposed).
    /// <para>
    /// The walk is an <see cref="EditorApplication.update"/> state machine (index + phase + tick
    /// counter) because both the per-event data population and
    /// <c>ScreenCapture.CaptureScreenshot</c> only complete after the NEXT rendered player-loop
    /// frame — a synchronous loop can never observe either. With <c>captureScreenshots</c> the
    /// walk additionally writes one Game View PNG per event into a
    /// <c>&lt;dumpBaseName&gt;_steps/event_&lt;i:D4&gt;.png</c> folder, each pairing with row
    /// <c>i</c> of the text dump.
    /// </para>
    /// <para>
    /// The text dump is written at walk end from the fresh rows; the full-frame PNG twin is
    /// captured while the limit still equals the event count (that IS the full frame), and only
    /// then is the original limit restored. Cleanup runs on every exit path — finish, cancel,
    /// timeout exhaustion, exception.
    /// </para>
    /// </summary>
    internal static class FrameDebuggerEventWalker
    {
        private const int PerStepTimeoutTicks = 150;

        private enum Phase
        {
            BeginEvent,
            WaitEvent,
            WaitFullFrame
        }

        private static bool isRunning;
        private static bool captureScreenshots;
        private static Phase phase;
        private static FrameDebuggerEventReader reader;
        private static int eventCount;
        private static int currentIndex;
        private static int waitTicks;
        private static int originalLimit;
        private static bool originalLimitCaptured;
        private static DateTime startTimestamp;
        private static string dumpFilePath;
        private static string fullFramePngPath;
        private static string stepsDirectory;
        private static string currentStepPath;
        private static string currentEventTypeName;
        private static string currentHierarchyPath;
        private static bool dataReady;
        private static bool screenshotPending;
        private static bool dumpWritten;
        private static FrameDebuggerEventRow pendingRow;
        private static List<string> lines;
        private static Dictionary<string, int> causeHistogram;
        private static int totalDrawCalls;
        private static int capturedScreenshots;
        private static int skippedScreenshots;
        private static int dataTimeouts;

        internal static void Begin(bool withScreenshots)
        {
            if (isRunning)
            {
                EditorUtility.DisplayDialog(
                    "Frame Debugger Dump",
                    "A frame-dump walk is already running. Wait for it to finish (or cancel it) " +
                    "before starting another.",
                    "OK");
                return;
            }

            try
            {
                BeginCore(withScreenshots);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FrameDebuggerEventWalker] Unhandled exception during setup: {exception}");
                Stop(finished: false);
            }
        }

        private static void BeginCore(bool withScreenshots)
        {
            reader = FrameDebuggerEventReader.Create();
            if (reader == null)
            {
                return;
            }

            var count = reader.ReadCount();
            if (count <= 0)
            {
                FrameDebuggerDumper.ShowZeroEventsDialog();
                reader = null;
                return;
            }

            if (withScreenshots && !ConfirmScreenshotWalk(count))
            {
                reader = null;
                return;
            }

            originalLimit = reader.ReadLimit();
            originalLimitCaptured = true;

            captureScreenshots = withScreenshots;
            eventCount = count;
            currentIndex = 0;
            waitTicks = 0;
            dataReady = false;
            screenshotPending = false;
            dumpWritten = false;
            lines = new List<string>(count);
            causeHistogram = new Dictionary<string, int>();
            totalDrawCalls = 0;
            capturedScreenshots = 0;
            skippedScreenshots = 0;
            dataTimeouts = 0;

            startTimestamp = DateTime.Now;
            dumpFilePath = FrameDebuggerDumper.BuildDumpFilePath(startTimestamp);
            fullFramePngPath = Path.ChangeExtension(dumpFilePath, ".png");
            stepsDirectory = null;
            if (withScreenshots)
            {
                stepsDirectory = Path.Combine(
                    Path.GetDirectoryName(dumpFilePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(dumpFilePath) + "_steps");
                Directory.CreateDirectory(stepsDirectory);
            }

            phase = Phase.BeginEvent;
            isRunning = true;
            EditorApplication.update += OnUpdate;
        }

        private static bool ConfirmScreenshotWalk(int count)
        {
            return EditorUtility.DisplayDialog(
                "Frame Debugger Step Capture",
                $"The Frame Debugger has {count} events.\n\n" +
                $"This will write {count} PNG screenshots (one per event) next to the text dump, " +
                "re-rendering the frame for each. It can take several minutes and blocks the editor " +
                "while it runs.\n\nProceed?",
                "OK",
                "Cancel");
        }

        private static void OnUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FrameDebuggerEventWalker] Exception during walk (event {currentIndex}): {exception}");
                Stop(finished: false);
            }
        }

        private static void Tick()
        {
            var info = phase == Phase.WaitFullFrame
                ? "writing dump + full-frame screenshot"
                : $"event {currentIndex} / {eventCount}";
            if (EditorUtility.DisplayCancelableProgressBar(
                "Frame Debugger Dump",
                info,
                eventCount == 0 ? 0f : (float)currentIndex / eventCount))
            {
                Debug.Log($"[FrameDebuggerEventWalker] Cancelled by user at event {currentIndex} / {eventCount}.");
                Stop(finished: false);
                return;
            }

            switch (phase)
            {
                case Phase.BeginEvent:
                    StartEvent();
                    break;
                case Phase.WaitEvent:
                    PollEvent();
                    break;
                case Phase.WaitFullFrame:
                    PollFullFrame();
                    break;
            }
        }

        private static void StartEvent()
        {
            // limit semantics: the number of events rendered, so event index i is shown at i + 1.
            reader.SetLimit(currentIndex + 1);

            // These two were correct without scrubbing, so they can be read any time.
            currentEventTypeName = reader.ReadEventTypeName(currentIndex);
            currentHierarchyPath = reader.ReadHierarchyPath(currentIndex);

            dataReady = false;
            screenshotPending = captureScreenshots;
            if (captureScreenshots)
            {
                currentStepPath = Path.Combine(stepsDirectory, $"event_{currentIndex:D4}.png");
                FrameDebuggerDumper.CaptureGameViewScreenshot(currentStepPath, announceInLog: false);
            }

            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();

            waitTicks = 0;
            phase = Phase.WaitEvent;
        }

        // The event-data read and the screenshot file-wait overlap; the step completes when both
        // are satisfied. Pattern A's read gets its wait-at-least-one-tick-after-the-limit-change
        // for free: PollEvent first runs the tick after StartEvent.
        private static void PollEvent()
        {
            if (!dataReady && reader.TryReadEventData(currentIndex, out var eventData))
            {
                pendingRow = reader.BuildRow(currentIndex, currentEventTypeName, currentHierarchyPath, eventData);
                dataReady = true;
            }

            if (screenshotPending && File.Exists(currentStepPath))
            {
                screenshotPending = false;
                capturedScreenshots++;
            }

            if (dataReady && !screenshotPending)
            {
                CommitRow(pendingRow);
                AdvanceEvent();
                return;
            }

            waitTicks++;
            if (waitTicks >= PerStepTimeoutTicks)
            {
                TimeoutEvent();
                return;
            }

            // The editor idles between ticks, so the queued frame never renders unless we keep
            // re-requesting one on every waiting tick.
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void TimeoutEvent()
        {
            var missing = new List<string>();
            if (!dataReady)
            {
                missing.Add("event data");
            }

            if (screenshotPending)
            {
                missing.Add("screenshot");
            }

            Debug.LogWarning(
                $"[FrameDebuggerEventWalker] Timed out on event {currentIndex} after {PerStepTimeoutTicks} ticks " +
                $"(missing: {string.Join(" + ", missing)}) — continuing.");

            if (!dataReady)
            {
                dataTimeouts++;
                pendingRow = reader.BuildRow(currentIndex, currentEventTypeName, currentHierarchyPath, null);
            }

            if (screenshotPending)
            {
                screenshotPending = false;
                skippedScreenshots++;
            }

            CommitRow(pendingRow);
            AdvanceEvent();
        }

        private static void CommitRow(FrameDebuggerEventRow row)
        {
            lines.Add(row.Line);
            if (row.DrawCalls.HasValue)
            {
                totalDrawCalls += row.DrawCalls.Value;
            }

            causeHistogram[row.BatchBreakCause] = causeHistogram.TryGetValue(row.BatchBreakCause, out var existing)
                ? existing + 1
                : 1;
        }

        private static void AdvanceEvent()
        {
            currentIndex++;
            if (currentIndex >= eventCount)
            {
                BeginFullFrame();
                return;
            }

            phase = Phase.BeginEvent;
        }

        // The last event left limit == eventCount, which IS the full frame — so the txt and its
        // PNG twin are produced here, BEFORE the original limit is restored in Stop.
        private static void BeginFullFrame()
        {
            FrameDebuggerDumper.WriteOutput(
                dumpFilePath, startTimestamp, eventCount, totalDrawCalls, causeHistogram, lines,
                reader.HasRenderTargetField);
            dumpWritten = true;

            FrameDebuggerDumper.CaptureGameViewScreenshot(fullFramePngPath, announceInLog: true);
            waitTicks = 0;
            phase = Phase.WaitFullFrame;
        }

        private static void PollFullFrame()
        {
            if (File.Exists(fullFramePngPath))
            {
                Stop(finished: true);
                return;
            }

            waitTicks++;
            if (waitTicks >= PerStepTimeoutTicks)
            {
                Debug.LogWarning(
                    $"[FrameDebuggerEventWalker] Timed out waiting for the full-frame screenshot after " +
                    $"{PerStepTimeoutTicks} ticks — the text dump is intact.");
                Stop(finished: true);
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        // Unconditional teardown for every exit path (finish, cancel, timeout exhaustion,
        // exception). Restores the pre-walk limit, clears the progress bar, and unsubscribes so a
        // failed walk can never leave the Frame Debugger clamped or the progress bar stuck.
        private static void Stop(bool finished)
        {
            EditorApplication.update -= OnUpdate;
            EditorUtility.ClearProgressBar();
            RestoreLimit();

            if (finished)
            {
                LogSummaryAndReveal();
            }
            else if (isRunning)
            {
                Debug.Log(
                    $"[FrameDebuggerEventWalker] Walk stopped at event {currentIndex} / {eventCount}" +
                    $"{(dumpWritten ? $" — text dump already written to {dumpFilePath}" : " — no text dump written")}.");
            }

            isRunning = false;
            originalLimitCaptured = false;
            reader = null;
            lines = null;
            causeHistogram = null;
            currentStepPath = null;
        }

        private static void RestoreLimit()
        {
            if (!originalLimitCaptured || reader == null)
            {
                return;
            }

            try
            {
                reader.SetLimit(originalLimit);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FrameDebuggerEventWalker] Failed to restore Frame Debugger limit: {exception}");
            }
        }

        private static void LogSummaryAndReveal()
        {
            var summary =
                $"[FrameDebuggerEventWalker] Done — {eventCount} events dumped to {dumpFilePath}" +
                (dataTimeouts > 0 ? $" ({dataTimeouts} event-data timeouts, '-' rows)" : string.Empty);
            if (captureScreenshots)
            {
                summary += $"; step screenshots captured {capturedScreenshots}, skipped {skippedScreenshots} " +
                    $"→ {stepsDirectory}";
            }

            Debug.Log(summary);
            if (captureScreenshots && !string.IsNullOrEmpty(stepsDirectory) && Directory.Exists(stepsDirectory))
            {
                EditorUtility.RevealInFinder(stepsDirectory);
            }
        }
    }
}
