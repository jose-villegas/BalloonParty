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
    }
}
