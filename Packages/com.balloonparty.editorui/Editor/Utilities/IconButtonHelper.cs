using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Utilities
{
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

        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
