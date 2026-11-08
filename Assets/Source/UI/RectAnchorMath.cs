using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Stateless <see cref="RectTransform"/> position math shared across views.
    /// </summary>
    internal static class RectAnchorMath
    {
        internal static Vector3 Center(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        internal static Vector3 RandomPosition(RectTransform rect)
        {
            var bounds = rect.rect;
            var local = new Vector3(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax),
                0f);
            return rect.TransformPoint(local);
        }

        /// <summary>
        ///     Where a point of the game world, seen through <paramref name="gameCamera" />, falls in
        ///     <paramref name="rect" />'s local space on <paramref name="canvas" />, whatever its render mode.
        /// </summary>
        internal static Vector2 GameWorldToLocalPoint(RectTransform rect, Canvas canvas, Camera gameCamera,
            Vector3 worldPosition)
        {
            var canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(gameCamera, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, canvasCamera, out var localPoint);
            return localPoint;
        }

        /// <summary>
        ///     <paramref name="point" />, moved just far enough that a box of <paramref name="halfSize" /> around it
        ///     stays inside <paramref name="bounds" />; centred on an axis where the box is wider than the bounds.
        /// </summary>
        internal static Vector2 ClampInside(Rect bounds, Vector2 point, Vector2 halfSize)
        {
            var min = bounds.min + halfSize;
            var max = bounds.max - halfSize;
            return new Vector2(
                min.x <= max.x ? Mathf.Clamp(point.x, min.x, max.x) : bounds.center.x,
                min.y <= max.y ? Mathf.Clamp(point.y, min.y, max.y) : bounds.center.y);
        }

        internal static Vector2 WorldToAnchoredPosition(RectTransform rect, Vector3 worldPosition)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPoint,
                null,
                out var localPoint);
            return localPoint;
        }
    }
}
