using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// Runs the plain <see cref="FrameDebuggerDumper"/> text dump, then walks the frozen Frame
    /// Debugger event list one event at a time — setting <c>FrameDebuggerUtility.limit</c> to
    /// <c>i + 1</c>, re-rendering, and capturing a Game View screenshot per event into a
    /// <c>&lt;dumpBaseName&gt;_steps/event_&lt;i:D4&gt;.png</c> sibling folder. Each PNG lines up
    /// with row <c>i</c> of the text dump.
    /// <para>
    /// <c>ScreenCapture.CaptureScreenshot</c> only completes at the end of the next rendered
    /// player-loop frame, so the walk cannot run in a synchronous loop. It is driven off
    /// <see cref="EditorApplication.update"/> as a small state machine (index + phase + tick
    /// counter). Cleanup — restoring the original limit, clearing the progress bar, and
    /// unsubscribing — runs on every exit path (finish, cancel, timeout exhaustion, exception).
    /// </para>
    /// </summary>
    internal static class FrameDebuggerStepCapture
    {
        private const int PerStepTimeoutTicks = 150;

        private enum Phase
        {
            SetLimitAndCapture,
            WaitForFile
        }

        private static bool isRunning;
        private static Phase phase;
        private static int eventCount;
        private static int currentIndex;
        private static int waitTicks;
        private static int capturedCount;
        private static int skippedCount;
        private static int originalLimit;
        private static string stepsDirectory;
        private static string currentStepPath;
        private static MemberInfo limitMember;

        [MenuItem("Tools/BalloonParty/Dump Frame Debugger With Step Screenshots")]
        private static void DumpWithStepScreenshots()
        {
            if (isRunning)
            {
                EditorUtility.DisplayDialog(
                    "Frame Debugger Step Capture",
                    "A step-screenshot walk is already running. Wait for it to finish (or cancel it) " +
                    "before starting another.",
                    "OK");
                return;
            }

            try
            {
                Begin();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FrameDebuggerStepCapture] Unhandled exception during setup: {exception}");
                Stop();
            }
        }

        private static void Begin()
        {
            var utilityType = FrameDebuggerReflection.FindType(FrameDebuggerReflection.UtilityTypeName);
            if (utilityType == null)
            {
                FrameDebuggerReflection.LogResolutionFailure(
                    $"could not find type: {FrameDebuggerReflection.UtilityTypeName}", null, null);
                return;
            }

            var countMember = FrameDebuggerReflection.ResolveStaticMember(utilityType, "count");
            limitMember = FrameDebuggerReflection.ResolveStaticMember(utilityType, "limit");
            if (countMember == null || limitMember == null)
            {
                FrameDebuggerReflection.LogResolutionFailure(
                    "missing required FrameDebuggerUtility members (count, limit)", utilityType, null);
                limitMember = null;
                return;
            }

            // Read before anything below can throw — the Stop() exception path restores this, and
            // a late read would restore a stale value from a previous run instead.
            originalLimit = FrameDebuggerReflection.ReadStaticInt(limitMember);

            var count = FrameDebuggerReflection.ReadStaticInt(countMember);
            if (count <= 0)
            {
                FrameDebuggerDumper.ShowZeroEventsDialog();
                return;
            }

            var proceed = EditorUtility.DisplayDialog(
                "Frame Debugger Step Capture",
                $"The Frame Debugger has {count} events.\n\n" +
                $"This will write {count} PNG screenshots (one per event) next to the text dump, " +
                "re-rendering the frame for each. It can take several minutes and blocks the editor " +
                "while it runs.\n\nProceed?",
                "OK",
                "Cancel");
            if (!proceed)
            {
                limitMember = null;
                return;
            }

            // Run the shared text dump first so the PNG rows have a canonical text twin, and so the
            // steps folder is named after the same base file.
            var dumpPath = FrameDebuggerDumper.TryDump();
            if (string.IsNullOrEmpty(dumpPath))
            {
                // TryDump already surfaced the reason (resolution failure or 0 events).
                limitMember = null;
                return;
            }

            stepsDirectory = Path.Combine(
                Path.GetDirectoryName(dumpPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(dumpPath) + "_steps");
            Directory.CreateDirectory(stepsDirectory);

            eventCount = count;
            currentIndex = 0;
            capturedCount = 0;
            skippedCount = 0;
            phase = Phase.SetLimitAndCapture;
            currentStepPath = null;
            isRunning = true;

            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FrameDebuggerStepCapture] Exception during walk (event {currentIndex}): {exception}");
                Stop();
            }
        }

        private static void Tick()
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                "Frame Debugger Step Capture",
                $"event {currentIndex} / {eventCount}",
                eventCount == 0 ? 0f : (float)currentIndex / eventCount))
            {
                Debug.Log("[FrameDebuggerStepCapture] Cancelled by user.");
                Stop();
                return;
            }

            switch (phase)
            {
                case Phase.SetLimitAndCapture:
                    StartStep();
                    break;
                case Phase.WaitForFile:
                    PollStep();
                    break;
            }
        }

        private static void StartStep()
        {
            // limit semantics: the number of events rendered, so event index i is shown at i + 1.
            FrameDebuggerReflection.WriteStaticInt(limitMember, currentIndex + 1);

            currentStepPath = Path.Combine(stepsDirectory, $"event_{currentIndex:D4}.png");
            FrameDebuggerDumper.CaptureGameViewScreenshot(currentStepPath);

            // CaptureGameViewScreenshot already queued one player-loop update; repaint the views too
            // so the Frame Debugger honours the new limit for the frame we are about to capture.
            InternalEditorUtility.RepaintAllViews();

            waitTicks = 0;
            phase = Phase.WaitForFile;
        }

        private static void PollStep()
        {
            if (File.Exists(currentStepPath))
            {
                capturedCount++;
                Advance();
                return;
            }

            waitTicks++;
            if (waitTicks >= PerStepTimeoutTicks)
            {
                Debug.LogWarning(
                    $"[FrameDebuggerStepCapture] Timed out waiting for screenshot of event {currentIndex} " +
                    $"after {PerStepTimeoutTicks} ticks — skipping.");
                skippedCount++;
                Advance();
                return;
            }

            // The editor idles between ticks, so the queued frame never renders unless we keep
            // re-requesting one on every waiting tick.
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Advance()
        {
            currentIndex++;
            if (currentIndex >= eventCount)
            {
                Stop();
                return;
            }

            phase = Phase.SetLimitAndCapture;
        }

        // Unconditional teardown for every exit path (finish, cancel, timeout exhaustion,
        // exception). Restores the pre-walk limit, clears the progress bar, and unsubscribes so a
        // failed walk can never leave the Frame Debugger clamped or the progress bar stuck.
        private static void Stop()
        {
            EditorApplication.update -= OnUpdate;
            EditorUtility.ClearProgressBar();

            if (limitMember != null)
            {
                try
                {
                    FrameDebuggerReflection.WriteStaticInt(limitMember, originalLimit);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[FrameDebuggerStepCapture] Failed to restore Frame Debugger limit: {exception}");
                }
            }

            if (isRunning)
            {
                Debug.Log(
                    $"[FrameDebuggerStepCapture] Done — captured {capturedCount}, skipped {skippedCount} " +
                    $"of {eventCount} events → {stepsDirectory}");
                if (!string.IsNullOrEmpty(stepsDirectory) && Directory.Exists(stepsDirectory))
                {
                    EditorUtility.RevealInFinder(stepsDirectory);
                }
            }

            isRunning = false;
            limitMember = null;
            currentStepPath = null;
        }
    }
}
