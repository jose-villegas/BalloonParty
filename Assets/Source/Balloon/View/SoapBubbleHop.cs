using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The path of a lone soap bubble hopping from one balloon to another (<see cref="BubbleMergeView" />,
    ///     <see cref="RipeBubbleBurstView" />): eased along the line between them, bowed to one side at the middle,
    ///     swelling mid-hop and smaller as it sinks into where it lands.
    /// </summary>
    internal static class SoapBubbleHop
    {
        internal const float LandingScale = 0.7f;
        internal const float MidHopSwell = 0.15f;

        /// <summary>
        ///     Where the hop is <paramref name="t" /> (0 to 1) of the way from <paramref name="from" /> to
        ///     <paramref name="to" />, bowed <paramref name="arcHeight" /> to the side <paramref name="side" /> (1 or -1)
        ///     picks at the middle. Points too close together to have a side bow upward.
        /// </summary>
        internal static Vector3 PositionAt(Vector3 from, Vector3 to, float side, float arcHeight, float t)
        {
            var line = (Vector2)(to - from);
            var normal = line.sqrMagnitude > 1e-6f ? new Vector2(-line.y, line.x).normalized * side : Vector2.up;
            var eased = 0.5f - (0.5f * Mathf.Cos(Mathf.PI * t));
            var bow = Mathf.Sin(Mathf.PI * t);
            return Vector3.Lerp(from, to, eased) + (Vector3)(normal * (arcHeight * bow));
        }

        /// <summary>The hop's size at <paramref name="t" />, as a share of its size as it leaves.</summary>
        internal static float ScaleAt(float t)
        {
            return Mathf.Lerp(1f, LandingScale, t) * (1f + (MidHopSwell * Mathf.Sin(Mathf.PI * t)));
        }

        internal static void Place(Transform bubble, Vector3 from, Vector3 to, float side, float arcHeight,
            Vector3 restScale, float t)
        {
            bubble.position = PositionAt(from, to, side, arcHeight, t);
            bubble.localScale = restScale * ScaleAt(t);
        }
    }
}
