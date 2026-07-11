using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// Dumps the Frame Debugger's current captured event list to a diff-friendly text file under
    /// <c>Baselines~/</c>, for comparing batch composition across the URP migration (see
    /// <c>PLAN-URPMigration.md</c> tasks B0/B4/B7).
    /// <para>
    /// Workflow: open Window → Analysis → Frame Debugger, click Enable, and step to/freeze the
    /// frame you want captured, THEN run this menu item. This tool never enables the Frame
    /// Debugger or advances frames itself — it only reads whatever is currently captured.
    /// </para>
    /// <para>
    /// Everything here goes through reflection (see <see cref="FrameDebuggerReflection"/>) because
    /// <c>FrameDebuggerUtility</c> and <c>FrameDebuggerEvent</c>/<c>FrameDebuggerEventData</c> live
    /// in the internal <c>UnityEditorInternal.FrameDebuggerInternal</c> namespace inside
    /// <c>UnityEditor.CoreModule</c> and their exact member signatures are not part of the public
    /// API contract.
    /// </para>
    /// </summary>
    internal static class FrameDebuggerDumper
    {
        private const string OutputDirectory = "Baselines~";

        [MenuItem("Tools/BalloonParty/Dump Frame Debugger")]
        private static void Dump()
        {
            try
            {
                TryDump();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FrameDebuggerDumper] Unhandled exception during dump: {exception}");
            }
        }

        // Runs the text dump and, on success, captures the companion Game View screenshot. Returns
        // the path of the written <c>.txt</c> file, or null when resolution failed or the Frame
        // Debugger reported no events. Callers that chain further work (the per-step capturer)
        // derive their output paths from this base path.
        internal static string TryDump()
        {
            var utilityType = FrameDebuggerReflection.FindType(FrameDebuggerReflection.UtilityTypeName);
            var eventDataType = FrameDebuggerReflection.FindType(FrameDebuggerReflection.EventDataTypeName);
            if (utilityType == null || eventDataType == null)
            {
                FrameDebuggerReflection.LogResolutionFailure("could not find type(s): " +
                    $"{(utilityType == null ? FrameDebuggerReflection.UtilityTypeName + " " : string.Empty)}" +
                    $"{(eventDataType == null ? FrameDebuggerReflection.EventDataTypeName : string.Empty)}", null, null);
                return null;
            }

            var countMember = FrameDebuggerReflection.ResolveStaticMember(utilityType, "count");
            var getFrameEventDataMethod = utilityType.GetMethod("GetFrameEventData", FrameDebuggerReflection.StaticFlags);
            var getFrameEventObjectMethod = utilityType.GetMethod("GetFrameEventObject", FrameDebuggerReflection.StaticFlags);
            var getFrameEventInfoNameMethod = utilityType.GetMethod("GetFrameEventInfoName", FrameDebuggerReflection.StaticFlags);
            var getBatchBreakCauseStringsMethod = utilityType.GetMethod("GetBatchBreakCauseStrings", FrameDebuggerReflection.StaticFlags);

            if (countMember == null || getFrameEventDataMethod == null || getBatchBreakCauseStringsMethod == null)
            {
                FrameDebuggerReflection.LogResolutionFailure(
                    "missing one or more required FrameDebuggerUtility members " +
                    "(count, GetFrameEventData, GetBatchBreakCauseStrings)",
                    utilityType, eventDataType);
                return null;
            }

            var count = FrameDebuggerReflection.ReadStaticInt(countMember);
            if (count <= 0)
            {
                ShowZeroEventsDialog();
                return null;
            }

            var batchBreakCauseStrings = FrameDebuggerReflection.InvokeStatic(getBatchBreakCauseStringsMethod, null) as string[]
                ?? Array.Empty<string>();

            var eventDataFields = eventDataType
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .ToArray();
            var shaderNameField = FindFieldContaining(eventDataFields, "shadername");
            var passNameField = FindFieldContaining(eventDataFields, "passname");
            var drawCallCountField = FindFieldContaining(eventDataFields, "drawcallcount");
            var batchBreakCauseField = FindFieldContaining(eventDataFields, "batchbreakcause");
            var renderTargetNameField = eventDataFields.FirstOrDefault(f =>
                f.Name.ToLowerInvariant().Contains("rendertarget") && f.Name.ToLowerInvariant().Contains("name"));

            var lines = new List<string>(count);
            var totalDrawCalls = 0;
            var causeHistogram = new Dictionary<string, int>();

            for (var i = 0; i < count; i++)
            {
                // Optional columns degrade to "-" on signature mismatch instead of killing the
                // dump — the core event data below is the part worth failing loudly for.
                var eventTypeName = "-";
                if (getFrameEventInfoNameMethod != null)
                {
                    try
                    {
                        eventTypeName = FrameDebuggerReflection.InvokeStatic(getFrameEventInfoNameMethod, new object[] { i }) as string ?? "-";
                    }
                    catch (Exception)
                    {
                        getFrameEventInfoNameMethod = null;
                    }
                }

                var hierarchyPath = "-";
                if (getFrameEventObjectMethod != null)
                {
                    try
                    {
                        var obj = FrameDebuggerReflection.InvokeStatic(getFrameEventObjectMethod, new object[] { i }) as UnityEngine.Object;
                        hierarchyPath = BuildHierarchyPath(obj);
                    }
                    catch (Exception)
                    {
                        getFrameEventObjectMethod = null;
                    }
                }

                var eventData = ReadFrameEventData(getFrameEventDataMethod, eventDataType, i);

                var shaderName = ReadFieldAsString(eventData, shaderNameField);
                var passName = ReadFieldAsString(eventData, passNameField);
                var drawCallCount = ReadFieldAsInt(eventData, drawCallCountField);
                var batchBreakCause = ResolveBatchBreakCause(eventData, batchBreakCauseField, batchBreakCauseStrings);

                if (drawCallCount.HasValue)
                {
                    totalDrawCalls += drawCallCount.Value;
                }

                causeHistogram[batchBreakCause] = causeHistogram.TryGetValue(batchBreakCause, out var existing)
                    ? existing + 1
                    : 1;

                var columns = new List<string>
                {
                    i.ToString(),
                    eventTypeName,
                    hierarchyPath,
                    shaderName,
                    passName,
                    drawCallCount?.ToString() ?? "-",
                    batchBreakCause
                };

                if (renderTargetNameField != null)
                {
                    columns.Add(ReadFieldAsString(eventData, renderTargetNameField));
                }

                lines.Add(string.Join(" | ", columns));
            }

            return WriteOutput(count, totalDrawCalls, causeHistogram, lines, renderTargetNameField != null);
        }

        // Shared 0-events message so the step capturer surfaces the same guidance as the plain dump.
        internal static void ShowZeroEventsDialog()
        {
            EditorUtility.DisplayDialog(
                "Frame Debugger Dumper",
                "The Frame Debugger reports 0 captured events.\n\n" +
                "Open Window → Analysis → Frame Debugger, click Enable, and freeze " +
                "the frame you want to dump before running this tool again.",
                "OK");
        }

        // Companion image for the text dump. The capture respects the Frame Debugger's current
        // event limit, so the PNG shows the frame exactly as frozen in the window.
        internal static void CaptureGameViewScreenshot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);

            // The capture completes at the end of the next rendered frame; with the editor idle
            // (paused play mode / frozen frame) that frame never comes unless we request one.
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log($"[FrameDebuggerDumper] Screenshot queued — lands at {path} after the next repaint.");
        }

        private static object ReadFrameEventData(MethodInfo method, Type eventDataType, int index)
        {
            var parameters = method.GetParameters();

            // Pattern A: FrameDebuggerEventData GetFrameEventData(int)
            if (parameters.Length == 1)
            {
                return FrameDebuggerReflection.InvokeStatic(method, new object[] { index });
            }

            // Pattern B: bool GetFrameEventData(int, ref/out FrameDebuggerEventData)
            if (parameters.Length == 2)
            {
                var args = new object[] { index, Activator.CreateInstance(eventDataType) };
                FrameDebuggerReflection.InvokeStatic(method, args);
                return args[1];
            }

            return null;
        }

        private static string ResolveBatchBreakCause(object eventData, FieldInfo field, string[] causeStrings)
        {
            var causeIndex = ReadFieldAsInt(eventData, field);
            if (!causeIndex.HasValue)
            {
                return "-";
            }

            if (causeIndex.Value >= 0 && causeIndex.Value < causeStrings.Length)
            {
                return causeStrings[causeIndex.Value];
            }

            return causeIndex.Value.ToString();
        }

        private static string BuildHierarchyPath(UnityEngine.Object obj)
        {
            var gameObject = obj as GameObject;
            if (gameObject == null && obj is Component component)
            {
                gameObject = component.gameObject;
            }

            if (gameObject == null)
            {
                return "-";
            }

            var segments = new List<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string WriteOutput(
            int count, int totalDrawCalls, Dictionary<string, int> causeHistogram, IReadOnlyList<string> lines, bool hasRenderTargetField)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory);
            Directory.CreateDirectory(directory);

            var timestamp = DateTime.Now;
            var fileName = $"framedump_{timestamp:yyyyMMdd_HHmmss}.txt";
            var filePath = Path.Combine(directory, fileName);

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
            CaptureGameViewScreenshot(Path.ChangeExtension(filePath, ".png"));
            return filePath;
        }

        private static FieldInfo FindFieldContaining(IEnumerable<FieldInfo> fields, string needle)
        {
            return fields.FirstOrDefault(f => f.Name.ToLowerInvariant().Contains(needle));
        }

        private static string ReadFieldAsString(object instance, FieldInfo field)
        {
            if (instance == null || field == null)
            {
                return "-";
            }

            var value = field.GetValue(instance);
            return value == null ? "-" : value.ToString();
        }

        private static int? ReadFieldAsInt(object instance, FieldInfo field)
        {
            if (instance == null || field == null)
            {
                return null;
            }

            var value = field.GetValue(instance);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
