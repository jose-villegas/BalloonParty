using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>Draws reusable plot markers for chart overlays.</summary>
    public static class PlotMarker
    {
        /// <summary>Draws a filled diamond marker centered at the supplied position.</summary>
        public static void DrawDiamond(Vector2 center, float size, Color color)
        {
            float halfSize = size * 0.5f;
            Vector3[] vertices =
            {
                new Vector3(center.x, center.y - halfSize),
                new Vector3(center.x + halfSize, center.y),
                new Vector3(center.x, center.y + halfSize),
                new Vector3(center.x - halfSize, center.y)
            };

            Color previousColor = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(vertices);
            Handles.color = previousColor;
        }

        /// <summary>Draws a filled circular marker centered at the supplied position.</summary>
        public static void DrawCircle(Vector2 center, float radius, Color color)
        {
            Color previousColor = Handles.color;
            Handles.color = color;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = previousColor;
        }
    }
}
