using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// One text row of a frame dump, plus the two values the walker aggregates across rows
    /// (draw-call total and batch-break-cause histogram).
    /// </summary>
    internal readonly struct FrameDebuggerEventRow
    {
        internal string Line { get; }
        internal int? DrawCalls { get; }
        internal string BatchBreakCause { get; }

        internal FrameDebuggerEventRow(string line, int? drawCalls, string batchBreakCause)
        {
            Line = line;
            DrawCalls = drawCalls;
            BatchBreakCause = batchBreakCause;
        }
    }

    /// <summary>
    /// Resolves the internal Frame Debugger reflection surface once and exposes typed per-event
    /// reads for the walker. Unity's native side only populates <c>FrameDebuggerEventData</c> for
    /// the event at the current <c>FrameDebuggerUtility.limit</c>, so <see cref="TryReadEventData"/>
    /// is a polling API: the caller scrubs the limit, pumps the player loop, and retries until the
    /// data reads as ready for the requested index.
    /// </summary>
    internal sealed class FrameDebuggerEventReader
    {
        private readonly Type eventDataType;
        private readonly MemberInfo countMember;
        private readonly MemberInfo limitMember;
        private readonly MethodInfo getFrameEventDataMethod;
        private readonly int eventDataParameterCount;
        private readonly string[] batchBreakCauseStrings;
        private readonly FieldInfo shaderNameField;
        private readonly FieldInfo passNameField;
        private readonly FieldInfo drawCallCountField;
        private readonly FieldInfo batchBreakCauseField;
        private readonly FieldInfo renderTargetNameField;
        private readonly FieldInfo frameEventIndexField;
        private readonly bool isValid;
        private MethodInfo getFrameEventObjectMethod;
        private MethodInfo getFrameEventInfoNameMethod;

        internal bool HasRenderTargetField => renderTargetNameField != null;

        private FrameDebuggerEventReader(Type utilityType, Type dataType)
        {
            eventDataType = dataType;
            countMember = FrameDebuggerReflection.ResolveStaticMember(utilityType, "count");
            limitMember = FrameDebuggerReflection.ResolveStaticMember(utilityType, "limit");
            getFrameEventDataMethod = utilityType.GetMethod("GetFrameEventData", FrameDebuggerReflection.StaticFlags);
            getFrameEventObjectMethod = utilityType.GetMethod("GetFrameEventObject", FrameDebuggerReflection.StaticFlags);
            getFrameEventInfoNameMethod = utilityType.GetMethod("GetFrameEventInfoName", FrameDebuggerReflection.StaticFlags);
            var getBatchBreakCauseStringsMethod =
                utilityType.GetMethod("GetBatchBreakCauseStrings", FrameDebuggerReflection.StaticFlags);

            isValid = countMember != null && limitMember != null
                && getFrameEventDataMethod != null && getBatchBreakCauseStringsMethod != null;
            if (!isValid)
            {
                return;
            }

            eventDataParameterCount = getFrameEventDataMethod.GetParameters().Length;
            batchBreakCauseStrings =
                FrameDebuggerReflection.InvokeStatic(getBatchBreakCauseStringsMethod, null) as string[]
                ?? Array.Empty<string>();

            var fields = dataType
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .ToArray();
            shaderNameField = FindFieldContaining(fields, "shadername");
            passNameField = FindFieldContaining(fields, "passname");
            drawCallCountField = FindFieldContaining(fields, "drawcallcount");
            batchBreakCauseField = FindFieldContaining(fields, "batchbreakcause");
            renderTargetNameField = fields.FirstOrDefault(f =>
                f.Name.ToLowerInvariant().Contains("rendertarget") && f.Name.ToLowerInvariant().Contains("name"));
            frameEventIndexField = fields.FirstOrDefault(f =>
            {
                var name = f.Name.ToLowerInvariant();
                return name == "frameeventindex" || name == "m_frameeventindex";
            });
        }

        internal static FrameDebuggerEventReader Create()
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

            var reader = new FrameDebuggerEventReader(utilityType, eventDataType);
            if (!reader.isValid)
            {
                FrameDebuggerReflection.LogResolutionFailure(
                    "missing one or more required FrameDebuggerUtility members " +
                    "(count, limit, GetFrameEventData, GetBatchBreakCauseStrings)",
                    utilityType, eventDataType);
                return null;
            }

            return reader;
        }

        internal int ReadCount()
        {
            return FrameDebuggerReflection.ReadStaticInt(countMember);
        }

        internal int ReadLimit()
        {
            return FrameDebuggerReflection.ReadStaticInt(limitMember);
        }

        internal void SetLimit(int value)
        {
            FrameDebuggerReflection.WriteStaticInt(limitMember, value);
        }

        // Returns false while the native side hasn't populated data for this index yet — the
        // caller must pump the player loop and retry. A false negative just costs a tick; a false
        // positive would silently reproduce the stale-data bug this API exists to fix.
        internal bool TryReadEventData(int index, out object eventData)
        {
            eventData = null;

            // Pattern A: FrameDebuggerEventData GetFrameEventData(int). No readiness signal, so
            // the caller's wait-a-tick-after-limit-change plus the index check below is all we have.
            if (eventDataParameterCount == 1)
            {
                eventData = FrameDebuggerReflection.InvokeStatic(getFrameEventDataMethod, new object[] { index });
                return IsFreshForIndex(index, eventData);
            }

            // Pattern B: bool GetFrameEventData(int, ref/out FrameDebuggerEventData) — false means
            // the data hasn't arrived from the native side yet.
            if (eventDataParameterCount == 2)
            {
                var args = new object[] { index, Activator.CreateInstance(eventDataType) };
                var result = FrameDebuggerReflection.InvokeStatic(getFrameEventDataMethod, args);
                if (result is bool ready && !ready)
                {
                    return false;
                }

                eventData = args[1];
                return IsFreshForIndex(index, eventData);
            }

            // Unknown signature: report ready with null data so the row degrades to "-" columns
            // instead of stalling the walk until timeout.
            return true;
        }

        internal string ReadEventTypeName(int index)
        {
            // Optional column: degrades to "-" on signature mismatch instead of killing the dump.
            if (getFrameEventInfoNameMethod == null)
            {
                return "-";
            }

            try
            {
                return FrameDebuggerReflection.InvokeStatic(getFrameEventInfoNameMethod, new object[] { index }) as string ?? "-";
            }
            catch (Exception)
            {
                getFrameEventInfoNameMethod = null;
                return "-";
            }
        }

        internal string ReadHierarchyPath(int index)
        {
            // Optional column: degrades to "-" on signature mismatch instead of killing the dump.
            if (getFrameEventObjectMethod == null)
            {
                return "-";
            }

            try
            {
                var obj = FrameDebuggerReflection.InvokeStatic(getFrameEventObjectMethod, new object[] { index }) as UnityEngine.Object;
                return BuildHierarchyPath(obj);
            }
            catch (Exception)
            {
                getFrameEventObjectMethod = null;
                return "-";
            }
        }

        // Passing null eventData produces the timeout/fallback row: real index/eventType/hierarchy
        // columns with "-" for everything sourced from FrameDebuggerEventData.
        internal FrameDebuggerEventRow BuildRow(int index, string eventTypeName, string hierarchyPath, object eventData)
        {
            var drawCallCount = ReadFieldAsInt(eventData, drawCallCountField);
            var batchBreakCause = ResolveBatchBreakCause(eventData);

            var columns = new List<string>
            {
                index.ToString(),
                eventTypeName,
                hierarchyPath,
                ReadFieldAsString(eventData, shaderNameField),
                ReadFieldAsString(eventData, passNameField),
                drawCallCount?.ToString() ?? "-",
                batchBreakCause
            };

            if (renderTargetNameField != null)
            {
                columns.Add(ReadFieldAsString(eventData, renderTargetNameField));
            }

            return new FrameDebuggerEventRow(string.Join(" | ", columns), drawCallCount, batchBreakCause);
        }

        // Cross-checks the event-index field inside the returned data when this editor build has
        // one — the strongest stale-data detector available. Absent the field, trust the caller's
        // pattern handling.
        private bool IsFreshForIndex(int index, object eventData)
        {
            if (frameEventIndexField == null || eventData == null)
            {
                return true;
            }

            var value = ReadFieldAsInt(eventData, frameEventIndexField);
            return !value.HasValue || value.Value == index;
        }

        private string ResolveBatchBreakCause(object eventData)
        {
            var causeIndex = ReadFieldAsInt(eventData, batchBreakCauseField);
            if (!causeIndex.HasValue)
            {
                return "-";
            }

            if (causeIndex.Value >= 0 && causeIndex.Value < batchBreakCauseStrings.Length)
            {
                return batchBreakCauseStrings[causeIndex.Value];
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
