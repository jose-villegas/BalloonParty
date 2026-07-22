using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Utilities
{
    /// <summary>
    /// Caches icon-based GUIContent and draws icon buttons.
    /// Cache key is icon name only — each icon name maps to one tooltip.
    /// </summary>
    public static class IconButtonHelper
    {
        private static readonly Dictionary<string, GUIContent> Cache = new();

        public static GUIContent Get(string iconName, string fallbackGlyph, string tooltip = "")
        {
            if (iconName == null)
            {
                throw new ArgumentNullException(nameof(iconName));
            }

            if (Cache.TryGetValue(iconName, out var content))
            {
                return content;
            }

            var icon = EditorGUIUtility.FindTexture(iconName);
            content = icon != null
                ? new GUIContent(icon, tooltip)
                : new GUIContent(fallbackGlyph ?? string.Empty, tooltip);

            Cache[iconName] = content;
            return content;
        }

        /// <summary>Draws an icon button at the given rect, returns true if clicked.</summary>
        public static bool DrawButton(Rect rect, string iconName, string fallbackGlyph, string tooltip = "", GUIStyle style = null)
        {
            return GUI.Button(rect, Get(iconName, fallbackGlyph, tooltip), style ?? GUI.skin.button);
        }

        /// <summary>Draws an icon button using GUILayout, returns true if clicked.</summary>
        public static bool DrawButton(string iconName, string fallbackGlyph, string tooltip = "", GUIStyle style = null, params GUILayoutOption[] options)
        {
            return GUILayout.Button(Get(iconName, fallbackGlyph, tooltip), style ?? GUI.skin.button, options);
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
