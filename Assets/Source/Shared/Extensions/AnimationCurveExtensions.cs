using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class AnimationCurveExtensions
    {
        internal static float Duration(this AnimationCurve curve)
        {
            return curve.length > 0 ? curve[curve.length - 1].time : 0f;
        }

        /// <summary>Shared velocity-scaling formula: appliedValue = baseValue * (curve.Evaluate(t) + 1). Author
        /// the curve's y≈0 at t=0 so a normal-speed sample reproduces the un-scaled base value. A null/empty
        /// (unauthored) curve evaluates to 0 everywhere, so it's also a safe no-op default.</summary>
        internal static float ScaleByVelocity(this AnimationCurve curve, float baseValue, float t)
        {
            return curve == null ? baseValue : baseValue * (curve.Evaluate(t) + 1f);
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

        /// <summary>Largest keyframed Y value on the curve (0 for a null/empty curve). Cubic interpolation can
        /// overshoot this between keys, so treat it as the authored peak, not a hard interpolated bound.</summary>
        internal static float MaxKeyValue(this AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
            {
                return 0f;
            }

            var max = 0f;
            var keys = curve.keys;
            for (var i = 0; i < keys.Length; i++)
            {
                max = Mathf.Max(max, keys[i].value);
            }

            return max;
        }
    }
}
