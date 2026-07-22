using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Charts
{
    /// <summary>Draws value trends across an existing plot area.</summary>
    public static class PolylineOverlay
    {
        /// <summary>Normalizes values into plot-space points.</summary>
        public static Vector2[] NormalizePoints(Rect plotArea, IReadOnlyList<float> values, float max)
        {
            if (values == null || values.Count == 0)
            {
                return System.Array.Empty<Vector2>();
            }

            var points3D = NormalizePolyline(plotArea, values, max);
            var points = new Vector2[points3D.Length];

            for (var i = 0; i < points3D.Length; i++)
            {
                points[i] = new Vector2(points3D[i].x, points3D[i].y);
            }

            return points;
        }

        /// <summary>Draws an anti-aliased polyline for the provided values.</summary>
        public static void Draw(Rect plotArea, IReadOnlyList<float> values, float max, Color color, float thickness = 2f)
        {
            Vector3[] points = NormalizePolyline(plotArea, values, max);
            if (points.Length < 2)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, points);
            Handles.color = previousColor;
        }

        private static Vector3[] NormalizePolyline(Rect plotArea, IReadOnlyList<float> values, float max)
        {
            if (values == null || values.Count == 0)
            {
                return System.Array.Empty<Vector3>();
            }

            var points = new Vector3[values.Count];
            var xStep = values.Count > 1 ? plotArea.width / (values.Count - 1) : 0f;

            for (var i = 0; i < values.Count; i++)
            {
                var normalized = max > 0f ? Mathf.Clamp01(values[i] / max) : 0f;
                var x = plotArea.xMin + (xStep * i);
                var y = Mathf.Lerp(plotArea.yMax, plotArea.yMin, normalized);
                points[i] = new Vector3(x, y, 0f);
            }

            return points;
        }
    }
}
