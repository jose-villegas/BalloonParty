#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Shared Gizmo drawing primitives for <c>OnDrawGizmos</c> callbacks; mirrors <c>SceneDrawingHelper</c>.
    /// </summary>
    public static class GizmoDrawingHelper
    {
        /// <summary>
        ///     Draws a world-space open polyline through consecutive points (e.g. a simulated flight path).
        ///     Gizmo lines have no pixel width, so thickness (world units) is faked with parallel offset
        ///     lines perpendicular to each segment in the XY plane; 0 draws a single hairline.
        /// </summary>
        public static void DrawWorldPolyline(IReadOnlyList<Vector3> points, Color color, float thickness = 0f)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            Gizmos.color = color;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var from = points[i];
                var to = points[i + 1];
                Gizmos.DrawLine(from, to);

                if (thickness <= 0f)
                {
                    continue;
                }

                var direction = (to - from).normalized;
                var perpendicular = new Vector3(-direction.y, direction.x, 0f);
                for (var step = 1; step <= 2; step++)
                {
                    var offset = perpendicular * (thickness * 0.25f * step);
                    Gizmos.DrawLine(from + offset, to + offset);
                    Gizmos.DrawLine(from - offset, to - offset);
                }
            }
        }

        /// <summary>
        ///     Draws a world-space axis-aligned rectangle from center, width and height.
        /// </summary>
        public static void DrawWorldRect(
            Vector3 center,
            float width,
            float height,
            Color outlineColor,
            Color fillColor)
        {
            var halfW = width / 2f;
            var halfH = height / 2f;

            var bl = center + new Vector3(-halfW, -halfH, 0f);
            var br = center + new Vector3(halfW, -halfH, 0f);
            var tr = center + new Vector3(halfW, halfH, 0f);
            var tl = center + new Vector3(-halfW, halfH, 0f);

            DrawWorldQuad(bl, br, tr, tl, center, new Vector3(width, height, 0f), outlineColor, fillColor);
        }

        /// <summary>
        ///     Draws a world-space axis-aligned rectangle from edge positions (top, right, bottom, left).
        /// </summary>
        public static void DrawWorldRectFromLimits(
            float top,
            float right,
            float bottom,
            float left,
            Color outlineColor,
            Color fillColor)
        {
            var bl = new Vector3(left, bottom, 0f);
            var br = new Vector3(right, bottom, 0f);
            var tr = new Vector3(right, top, 0f);
            var tl = new Vector3(left, top, 0f);

            var center = new Vector3((left + right) / 2f, (top + bottom) / 2f, 0f);
            var size = new Vector3(right - left, top - bottom, 0f);

            DrawWorldQuad(bl, br, tr, tl, center, size, outlineColor, fillColor);
        }

        public static void DrawWireSphere(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, radius);
        }

        private static void DrawWorldQuad(
            Vector3 bl,
            Vector3 br,
            Vector3 tr,
            Vector3 tl,
            Vector3 center,
            Vector3 size,
            Color outlineColor,
            Color fillColor)
        {
            if (fillColor.a > 0f)
            {
                Gizmos.color = fillColor;
                Gizmos.DrawCube(center, size);
            }

            Gizmos.color = outlineColor;
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}
#endif
