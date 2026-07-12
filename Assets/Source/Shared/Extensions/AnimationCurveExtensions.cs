using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class AnimationCurveExtensions
    {
        internal static float Duration(this AnimationCurve curve)
        {
            return curve.length > 0 ? curve[curve.length - 1].time : 0f;
        }

        internal static float EndValue(this AnimationCurve curve)
        {
            return curve.length > 0 ? curve[curve.length - 1].value : 0f;
        }

        /// <summary>Weighted-random draw from a curve keyed by integer count (X = count, Y = weight); empty/all-zero weights return 0.</summary>
        internal static int SampleWeightedCount(this AnimationCurve weights, float roll01)
        {
            if (weights == null || weights.length == 0)
            {
                return 0;
            }

            var maxCount = Mathf.Max(0, Mathf.RoundToInt(weights[weights.length - 1].time));

            var total = 0f;
            for (var i = 0; i <= maxCount; i++)
            {
                total += Mathf.Max(0f, weights.Evaluate(i));
            }

            if (total <= 0f)
            {
                return 0;
            }

            var target = Mathf.Clamp01(roll01) * total;
            var accumulated = 0f;
            for (var i = 0; i <= maxCount; i++)
            {
                accumulated += Mathf.Max(0f, weights.Evaluate(i));
                if (target < accumulated)
                {
                    return i;
                }
            }

            return maxCount;
        }
    }
}
