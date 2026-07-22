using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    /// <summary>Low-level drawing helpers for absolute-positioned IMGUI table layouts.</summary>
    public static class TableDrawHelper
    {
        private static readonly Color DefaultSeparatorColor = new(0.35f, 0.35f, 0.35f, 0.5f);

        /// <summary>Inset a cell rect by padding on all sides.</summary>
        public static Rect InsetCell(Rect cell, float padding = 2f)
        {
            return new Rect(cell.x + padding, cell.y + padding, cell.width - padding * 2f, cell.height - padding * 2f);
        }

        /// <summary>Draws a 1px horizontal line at the bottom edge of a row rect.</summary>
        public static void DrawHorizontalSeparator(Rect rowRect, float thickness = 1f)
        {
            var rect = new Rect(rowRect.x, rowRect.yMax - thickness, rowRect.width, thickness);
            EditorGUI.DrawRect(rect, DefaultSeparatorColor);
        }

        /// <summary>Draws a 1px horizontal line at the bottom edge with a custom color.</summary>
        public static void DrawHorizontalSeparator(Rect rowRect, Color color, float thickness = 1f)
        {
            var rect = new Rect(rowRect.x, rowRect.yMax - thickness, rowRect.width, thickness);
            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>Draws a vertical separator at the right edge of a cell.</summary>
        public static void DrawVerticalSeparator(Rect cell, float height)
        {
            var rect = new Rect(cell.xMax, cell.y, 1f, height);
            EditorGUI.DrawRect(rect, DefaultSeparatorColor);
        }

        /// <summary>Draws a vertical separator at a specific X with a given height.</summary>
        public static void DrawVerticalSeparator(float x, float y, float height)
        {
            var rect = new Rect(x, y, 1f, height);
            EditorGUI.DrawRect(rect, DefaultSeparatorColor);
        }

        /// <summary>Draws a colored background panel.</summary>
        public static void DrawGroupPanel(Rect rect, Color background)
        {
            EditorGUI.DrawRect(rect, background);
        }

        /// <summary>Fills a gap area between groups with a dark background.</summary>
        public static void DrawGapFill(Rect rowRect, Color gapColor)
        {
            EditorGUI.DrawRect(rowRect, gapColor);
        }
    }
}
