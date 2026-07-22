using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Small drawing helpers for styled labels and highlighted rows.
    /// </summary>
    internal static class StyledRow
    {
        internal static void DrawStyledLabel(string text, bool bold, float width)
        {
            var style = bold
                ? StyleCache.Get("StyledRow.BoldLabel", () => new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold })
                : EditorStyles.label;

            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }

        /// <summary>
        ///     Begins a horizontal row with an optional background tint; caller must call <see cref="EditorGUILayout.EndHorizontal"/>.
        /// </summary>
        internal static void BeginHighlightedRow(bool highlight, Color highlightColor)
        {
            var previous = GUI.backgroundColor;

            if (highlight)
            {
                GUI.backgroundColor = highlightColor;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = previous;
        }
    }
}
