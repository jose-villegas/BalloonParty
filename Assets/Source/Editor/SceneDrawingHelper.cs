using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Shared Scene-view drawing primitives used by custom editors that need
    ///     to render world-space guides (rectangles, bounds, etc.) via <see cref="Handles"/>.
    /// </summary>
    public static class SceneDrawingHelper
    {
        /// <summary>
        ///     Draws a world-space axis-aligned rectangle defined by center, width and height.
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

            DrawWorldQuad(bl, br, tr, tl, outlineColor, fillColor);
        }

        /// <summary>
        ///     Draws a world-space axis-aligned rectangle defined by explicit edge positions
        ///     (top, right, bottom, left — clockwise from top, matching CSS / <c>Vector4</c> convention).
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

            DrawWorldQuad(bl, br, tr, tl, outlineColor, fillColor);
        }

        private static void DrawWorldQuad(
            Vector3 bl,
            Vector3 br,
            Vector3 tr,
            Vector3 tl,
            Color outlineColor,
            Color fillColor)
        {
            if (fillColor.a > 0f)
            {
                Handles.DrawSolidRectangleWithOutline(
                    new[] { bl, br, tr, tl },
                    fillColor,
                    Color.clear);
            }

            Handles.color = outlineColor;
            Handles.DrawLine(bl, br);
            Handles.DrawLine(br, tr);
            Handles.DrawLine(tr, tl);
            Handles.DrawLine(tl, bl);
        }
    }
}

