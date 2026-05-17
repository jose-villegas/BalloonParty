using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>
    ///     Helpers for converting between world space and RectTransform local
    ///     space. Accounts for anchor layout, pivot, and CanvasScaler so the
    ///     resulting values can be applied directly to anchoredPosition.
    /// </summary>
    internal static class RectTransformExtensions
    {
        /// <summary>
        ///     Converts a world-space offset to the equivalent anchoredPosition
        ///     delta inside <paramref name="rect"/>. Handles any anchor/pivot
        ///     configuration and CanvasScaler reference resolution.
        /// </summary>
        internal static Vector2 WorldDeltaToLocalDelta(
            this RectTransform rect,
            Camera cam,
            Vector3 worldOrigin,
            Vector3 worldDelta)
        {
            var screenA = RectTransformUtility.WorldToScreenPoint(cam, worldOrigin);
            var screenB = RectTransformUtility.WorldToScreenPoint(cam, worldOrigin + worldDelta);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, screenA, cam, out var localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, screenB, cam, out var localB);

            return localB - localA;
        }

        /// <summary>
        ///     Returns the anchoredPosition offset needed to shift
        ///     <paramref name="rect"/> so that <paramref name="worldTarget"/>
        ///     appears at the RectTransform's pivot point on screen.
        /// </summary>
        internal static Vector2 WorldPointToAnchoredOffset(
            this RectTransform rect,
            Camera cam,
            Vector3 worldTarget)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(cam, worldTarget);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, screen, cam, out var local);

            return local;
        }
    }
}

