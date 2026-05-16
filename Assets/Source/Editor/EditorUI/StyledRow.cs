using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor.EditorUI
{
    /// <summary>
    ///     Small drawing helpers for styled labels and highlighted rows.
    /// </summary>
    internal static class StyledRow
    {
        /// <summary>
        ///     Draws a label with optional bold styling.
        /// </summary>
        internal static void DrawStyledLabel(string text, bool bold, float width)
        {
            var style = bold
                ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold }
                : EditorStyles.label;

            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }

        /// <summary>
        ///     Begins a horizontal row with an optional background tint.
        ///     Restores <see cref="GUI.backgroundColor"/> immediately after
        ///     <see cref="EditorGUILayout.BeginHorizontal(GUIStyle, GUILayoutOption[])"/>
        ///     so only the row background is tinted.
        ///     The caller must call <see cref="EditorGUILayout.EndHorizontal"/> to close the row.
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

