using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BalloonParty.Editor.FrameDump
{
    /// <summary>
    /// Shared reflection plumbing for reaching into the internal
    /// <c>UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility</c> /
    /// <c>FrameDebuggerEventData</c> types. Both the text dumper and the per-step screenshot
    /// walker resolve the same members through here, so the type/member names live in exactly
    /// one place.
    /// </summary>
    internal static class FrameDebuggerReflection
    {
        internal const string UtilityTypeName = "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility";
        internal const string EventDataTypeName = "UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData";

        internal const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // The Frame Debugger exposes both as either a property or a field depending on the editor
        // build, so callers resolve through ResolveStaticMember rather than assuming one shape.
        internal static MemberInfo ResolveStaticMember(Type type, string name)
        {
            return type.GetProperty(name, StaticFlags) as MemberInfo
                ?? type.GetField(name, StaticFlags);
        }

        internal static int ReadStaticInt(MemberInfo member)
        {
            var value = member is PropertyInfo property
                ? property.GetValue(null)
                : ((FieldInfo)member).GetValue(null);
            return Convert.ToInt32(value);
        }

        internal static void WriteStaticInt(MemberInfo member, int value)
        {
            if (member is PropertyInfo property)
            {
                property.SetValue(null, value);
                return;
            }

            ((FieldInfo)member).SetValue(null, value);
        }

        internal static object InvokeStatic(MethodInfo method, object[] args)
        {
            return method.Invoke(null, args);
        }

        internal static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.FullName == fullName);
        }

        internal static IEnumerable<Type> SafeGetTypes(Assembly assembly)
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
        internal static void LogResolutionFailure(string reason, Type utilityType, Type eventDataType)
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
            foreach (var field in type.GetFields(StaticFlags))
            {
                builder.AppendLine($"  field {field.FieldType.Name} {field.Name}");
            }

            foreach (var property in type.GetProperties(StaticFlags))
            {
                builder.AppendLine($"  property {property.PropertyType.Name} {property.Name}");
            }

            foreach (var method in type.GetMethods(StaticFlags))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                builder.AppendLine($"  method {method.ReturnType.Name} {method.Name}({parameters})");
            }
        }
    }
}
