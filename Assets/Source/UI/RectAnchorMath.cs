using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Stateless <see cref="RectTransform"/> position math, with no feature knowledge so any
    ///     RectTransform-backed view can reuse it.
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
