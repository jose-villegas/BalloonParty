using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    /// <summary>Drawing helpers for styled labels and highlighted rows.</summary>
    public static class StyledRow
    {
        public static void DrawStyledLabel(string text, bool bold, float width)
        {
            var style = bold
                ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold }
                : EditorStyles.label;

            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }

        /// <summary>Begins a horizontal row with an optional background tint; caller must call EndHorizontal.</summary>
        public static void BeginHighlightedRow(bool highlight, Color highlightColor)
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
