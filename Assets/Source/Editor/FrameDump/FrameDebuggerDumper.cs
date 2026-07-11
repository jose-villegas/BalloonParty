using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// Menu entry points and file output for the Frame Debugger dump tools, used for capturing
    /// and diffing batch composition across rendering changes (born during the URP migration).
    /// Writes a diff-friendly text file (plus a Game View PNG twin) under <c>Baselines~/</c>;
    /// the step-screenshot variant also writes one PNG per event.
    /// <para>
    /// Workflow: open Window → Analysis → Frame Debugger, click Enable, and step to/freeze the
    /// frame you want captured, THEN run a menu item. These tools never enable the Frame Debugger
    /// or advance frames themselves — they only read whatever is currently captured.
    /// </para>
    /// <para>
    /// Both items run <see cref="FrameDebuggerEventWalker"/>, an async walk that scrubs the Frame
    /// Debugger limit through every event: Unity's native side only populates
    /// <c>FrameDebuggerEventData</c> for the event at the current limit, so a synchronous
    /// read-all-indices loop returns the selected event's data for every row. Reflection details
    /// live in <see cref="FrameDebuggerReflection"/> / <see cref="FrameDebuggerEventReader"/>.
    /// </para>
    /// </summary>
    internal static class FrameDebuggerDumper
    {
        private const string OutputDirectory = "Baselines~";

        [MenuItem("Tools/BalloonParty/Dump Frame Debugger")]
        private static void Dump()
        {
            FrameDebuggerEventWalker.Begin(withScreenshots: false);
        }

        [MenuItem("Tools/BalloonParty/Dump Frame Debugger With Step Screenshots")]
        private static void DumpWithStepScreenshots()
        {
            FrameDebuggerEventWalker.Begin(withScreenshots: true);
        }

        // Shared 0-events message so both menu items surface the same guidance.
        internal static void ShowZeroEventsDialog()
        {
            EditorUtility.DisplayDialog(
                "Frame Debugger Dumper",
                "The Frame Debugger reports 0 captured events.\n\n" +
                "Open Window → Analysis → Frame Debugger, click Enable, and freeze " +
                "the frame you want to dump before running this tool again.",
                "OK");
        }

        // The walk needs the dump path up front (the steps folder is named after it) while the
        // txt itself is only written at walk end, so the path derives from the walk-start
        // timestamp rather than from write time.
        internal static string BuildDumpFilePath(DateTime timestamp)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"framedump_{timestamp:yyyyMMdd_HHmmss}.txt");
        }

        internal static void WriteOutput(
            string filePath, DateTime timestamp, int count, int totalDrawCalls,
            IReadOnlyDictionary<string, int> causeHistogram, IReadOnlyList<string> lines, bool hasRenderTargetField)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"# Frame Debugger dump — {timestamp:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"# Unity version: {Application.unityVersion}");
            builder.AppendLine($"# Event count: {count}");
            builder.AppendLine($"# Total draw calls: {totalDrawCalls}");
            builder.AppendLine($"# Render target name field present: {hasRenderTargetField}");
            builder.AppendLine("# Batch break cause histogram:");
            foreach (var pair in causeHistogram.OrderByDescending(p => p.Value))
            {
                builder.AppendLine($"#   {pair.Key}: {pair.Value}");
            }

            var header = "index | eventType | hierarchyPath | shaderName | passName | drawCalls | batchBreakCause";
            if (hasRenderTargetField)
            {
                header += " | renderTargetName";
            }

            builder.AppendLine();
            builder.AppendLine(header);
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }

            File.WriteAllText(filePath, builder.ToString());

            Debug.Log($"[FrameDebuggerDumper] Wrote {count} events to {filePath}");
            EditorUtility.RevealInFinder(filePath);
        }

        // Companion image capture. The capture respects the Frame Debugger's current event limit,
        // so the PNG shows the frame exactly as currently rendered.
        internal static void CaptureGameViewScreenshot(string path, bool announceInLog)
        {
            ScreenCapture.CaptureScreenshot(path);

            // The capture completes at the end of the next rendered frame; with the editor idle
            // (paused play mode / frozen frame) that frame never comes unless we request one.
            EditorApplication.QueuePlayerLoopUpdate();
            if (announceInLog)
            {
                Debug.Log($"[FrameDebuggerDumper] Screenshot queued — lands at {path} after the next repaint.");
            }
        }
    }
}
