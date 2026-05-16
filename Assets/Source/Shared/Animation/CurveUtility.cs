using UnityEngine;

namespace BalloonParty.Shared.Animation
{
    public static class CurveUtility
    {
        /// <summary>
        ///     Lerps between two points with a curve-driven vertical offset
        ///     at normalized progress <paramref name="t" /> (0→1).
        /// </summary>
        public static Vector3 LerpWithVerticalCurve(
            Vector3 from,
            Vector3 to,
            float t,
            float height,
            AnimationCurve curve)
        {
            var pos = Vector3.Lerp(from, to, t);
            pos.y += height * curve.Evaluate(t);
            return pos;
        }

        /// <summary>
        ///     Multiplies a base value by a curve sample at normalized progress
        ///     <paramref name="t" /> (0→1).
        /// </summary>
        public static float SampleMultiplied(float t, float baseValue, AnimationCurve curve)
        {
            return baseValue * curve.Evaluate(t);
        }
    }
}
