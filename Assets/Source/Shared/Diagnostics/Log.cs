using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BalloonParty.Shared.Diagnostics
{
    /// <summary>
    /// Static logging facade with per-tag coloring in the Unity Console.
    /// </summary>
    /// <remarks>
    /// <see cref="Info"/> and <see cref="Assert"/> are decorated with
    /// <c>[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]</c>,
    /// so they compile out of release builds automatically — no <c>#if</c> guards needed.
    /// <para/>
    /// <b>Important:</b> pass interpolated strings directly as arguments
    /// (<c>Log.Info("Tag", $"value {x}")</c>). Do not hoist them into a local variable
    /// first — the compiler strips the entire call including argument evaluation, but a
    /// pre-assigned variable would still allocate.
    /// </remarks>
    internal static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal static void Info(string tag, string message)
        {
            Debug.Log(FormatTagged(tag, message));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        internal static void Assert(bool condition, string tag, string message)
        {
            Debug.Assert(condition, FormatTagged(tag, message));
        }

        internal static void Warn(string tag, string message)
        {
            Debug.LogWarning(FormatTagged(tag, message));
        }

        internal static void Warn(string tag, string message, Object context)
        {
            Debug.LogWarning(FormatTagged(tag, message), context);
        }

        internal static void Error(string tag, string message)
        {
            Debug.LogError(FormatTagged(tag, message));
        }

        internal static void Error(string tag, string message, Object context)
        {
            Debug.LogError(FormatTagged(tag, message), context);
        }

        private static string FormatTagged(string tag, string message)
        {
            var color = TagPalette.ColorFor(tag);
            return $"<color={color}>[{tag}]</color> {message}";
        }

        private static class TagPalette
        {
            private static readonly string[] Colors =
            {
                "#61AFEF", // blue
                "#E06C75", // soft red
                "#98C379", // green
                "#C678DD", // purple
                "#E5C07B", // gold
                "#56B6C2", // cyan
                "#BE5046", // rust
                "#D19A66", // orange
                "#7EC8E3", // sky
                "#C3E88D", // lime
                "#F78C6C", // coral
                "#FFCB6B", // amber
                "#89DDFF", // ice
                "#A9DC76", // leaf
                "#FC9867", // peach
                "#AB9DF2", // lavender
            };

            internal static string ColorFor(string tag)
            {
                var index = (tag.GetHashCode() & 0x7FFFFFFF) % Colors.Length;
                return Colors[index];
            }
        }
    }
}
