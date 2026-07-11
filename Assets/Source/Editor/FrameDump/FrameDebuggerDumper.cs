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
    /// Everything here goes through reflection because <c>FrameDebuggerUtility</c> and
    /// <c>FrameDebuggerEvent</c>/<c>FrameDebuggerEventData</c> live in the internal
    /// <c>UnityEditorInternal.FrameDebuggerInternal</c> namespace inside <c>UnityEditor.CoreModule</c>
    /// and their exact member signatures are not part of the public API contract.
    /// </para>
    /// </summary>
    internal static class FrameDebuggerDumper
    {
        private const string OutputDirectory = "Baselines~";
        private const string UtilityTypeName = "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility";
        private const string EventDataTypeName = "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData";

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

        private static void TryDump()
        {
            var utilityType = FindType(UtilityTypeName);
            var eventDataType = FindType(EventDataTypeName);
            if (utilityType == null || eventDataType == null)
            {
                LogResolutionFailure($"could not find type(s): " +
                    $"{(utilityType == null ? UtilityTypeName + " " : string.Empty)}" +
                    $"{(eventDataType == null ? EventDataTypeName : string.Empty)}", null, null);
                return;
            }

            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            var countMember = utilityType.GetProperty("count", staticFlags) as MemberInfo
                ?? utilityType.GetField("count", staticFlags);
            var getFrameEventDataMethod = utilityType.GetMethod("GetFrameEventData", staticFlags);
            var getFrameEventObjectMethod = utilityType.GetMethod("GetFrameEventObject", staticFlags);
            var getFrameEventInfoNameMethod = utilityType.GetMethod("GetFrameEventInfoName", staticFlags);
            var getBatchBreakCauseStringsMethod = utilityType.GetMethod("GetBatchBreakCauseStrings", staticFlags);

            if (countMember == null || getFrameEventDataMethod == null || getBatchBreakCauseStringsMethod == null)
            {
                LogResolutionFailure(
                    "missing one or more required FrameDebuggerUtility members " +
                    "(count, GetFrameEventData, GetBatchBreakCauseStrings)",
                    utilityType, eventDataType);
                return;
            }

            var count = ReadStaticInt(countMember);
            if (count <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Frame Debugger Dumper",
                    "The Frame Debugger reports 0 captured events.\n\n" +
                    "Open Window → Analysis → Frame Debugger, click Enable, and freeze " +
                    "the frame you want to dump before running this tool again.",
                    "OK");
                return;
            }

            var batchBreakCauseStrings = InvokeStatic(getBatchBreakCauseStringsMethod, null) as string[]
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
                        eventTypeName = InvokeStatic(getFrameEventInfoNameMethod, new object[] { i }) as string ?? "-";
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
                        var obj = InvokeStatic(getFrameEventObjectMethod, new object[] { i }) as UnityEngine.Object;
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

            WriteOutput(count, totalDrawCalls, causeHistogram, lines, renderTargetNameField != null);
        }

        private static object ReadFrameEventData(MethodInfo method, Type eventDataType, int index)
        {
            var parameters = method.GetParameters();

            // Pattern A: FrameDebuggerEventData GetFrameEventData(int)
            if (parameters.Length == 1)
            {
                return InvokeStatic(method, new object[] { index });
            }

            // Pattern B: bool GetFrameEventData(int, ref/out FrameDebuggerEventData)
            if (parameters.Length == 2)
            {
                var args = new object[] { index, Activator.CreateInstance(eventDataType) };
                InvokeStatic(method, args);
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

        private static void WriteOutput(
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

        private static int ReadStaticInt(MemberInfo member)
        {
            var value = member is PropertyInfo property
                ? property.GetValue(null)
                : ((FieldInfo)member).GetValue(null);
            return Convert.ToInt32(value);
        }

        private static object InvokeStatic(MethodInfo method, object[] args)
        {
            return method.Invoke(null, args);
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => SafeGetTypes(assembly))
                .FirstOrDefault(t => t.FullName == fullName);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(t => t != null);
            }
        }

        // The tool is only ever driven by a human, so a resolution failure must carry the full
        // API shape back in one round-trip rather than requiring a second debugging pass.
        private static void LogResolutionFailure(string reason, Type utilityType, Type eventDataType)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[FrameDebuggerDumper] Reflection resolution failed: {reason}");

            if (utilityType != null)
            {
                builder.AppendLine("FrameDebuggerUtility members:");
                AppendMembers(builder, utilityType);
            }

            if (eventDataType != null)
            {
                builder.AppendLine("FrameDebuggerEventData fields:");
                foreach (var field in eventDataType.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    builder.AppendLine($"  {field.FieldType.Name} {field.Name}");
                }
            }

            Debug.LogError(builder.ToString());
        }

        private static void AppendMembers(StringBuilder builder, Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var field in type.GetFields(flags))
            {
                builder.AppendLine($"  field {field.FieldType.Name} {field.Name}");
            }

            foreach (var property in type.GetProperties(flags))
            {
                builder.AppendLine($"  property {property.PropertyType.Name} {property.Name}");
            }

            foreach (var method in type.GetMethods(flags))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                builder.AppendLine($"  method {method.ReturnType.Name} {method.Name}({parameters})");
            }
        }
    }
}
