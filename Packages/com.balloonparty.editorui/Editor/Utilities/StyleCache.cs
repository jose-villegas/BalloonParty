using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.EditorUI.Utilities
{
    /// <summary>Lazy-init GUIStyle factory. Call <see cref="Get"/> in OnGUI to avoid per-frame allocations.</summary>
    public static class StyleCache
    {
        private static readonly Dictionary<string, GUIStyle> Cache = new();

        /// <summary>Returns a cached GUIStyle, creating it via <paramref name="factory"/> on first access.</summary>
        public static GUIStyle Get(string key, Func<GUIStyle> factory)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (!Cache.TryGetValue(key, out var style))
            {
                style = factory();
                Cache[key] = style;
            }

            return style;
        }

        /// <summary>Clears the entire cache. Useful on domain reload.</summary>
        public static void Clear()
        {
            Cache.Clear();
        }
    }
}
