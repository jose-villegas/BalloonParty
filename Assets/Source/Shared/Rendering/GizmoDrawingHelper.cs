#if UNITY_EDITOR
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Shared Gizmo drawing primitives for <c>OnDrawGizmos</c> callbacks; mirrors <c>SceneDrawingHelper</c>.
    /// </summary>
    public static class GizmoDrawingHelper
    {
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
