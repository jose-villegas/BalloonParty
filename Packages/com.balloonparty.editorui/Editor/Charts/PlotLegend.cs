using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>Draws compact legend entries for plot overlays.</summary>
    public static class PlotLegend
    {
        /// <summary>Draws a legend entry and advances the next entry position.</summary>
        public static void DrawEntry(ref float x, float y, Color color, string label, GUIStyle style = null, bool isSwatch = true)
        {
            GUIStyle resolvedStyle = style ?? EditorStyles.miniLabel;
            float textHeight = resolvedStyle.lineHeight;

            if (isSwatch)
            {
                Rect swatchRect = new Rect(x, y + ((textHeight - 8f) * 0.5f), 8f, 8f);
                EditorGUI.DrawRect(swatchRect, color);
                x += swatchRect.width + 4f;
            }

            Vector2 size = resolvedStyle.CalcSize(new GUIContent(label));
            GUI.Label(new Rect(x, y, size.x, textHeight), label, resolvedStyle);
            x += size.x + 8f;
        }

        /// <summary>Draws a diamond marker legend entry and advances the next entry position.</summary>
        public static void DrawDiamondEntry(ref float x, float y, Color color, string label, float size = 6f, GUIStyle style = null)
        {
            GUIStyle resolvedStyle = style ?? EditorStyles.miniLabel;
            float textHeight = resolvedStyle.lineHeight;
            PlotMarker.DrawDiamond(new Vector2(x + (size * 0.5f), y + (textHeight * 0.5f)), size, color);
            x += size + 4f;
            Vector2 labelSize = resolvedStyle.CalcSize(new GUIContent(label));
            GUI.Label(new Rect(x, y, labelSize.x, textHeight), label, resolvedStyle);
            x += labelSize.x + 8f;
        }
    }
}
