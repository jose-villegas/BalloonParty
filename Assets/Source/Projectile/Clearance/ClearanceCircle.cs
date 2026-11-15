using UnityEngine;

namespace BalloonParty.Projectile.Clearance
{
    /// <summary>One ball's clear circle this frame, in the board's XY plane.</summary>
    internal readonly struct ClearanceCircle
    {
        /// <summary>
        ///     The most circles at once: the ball cap (3) plus every Extra Pen card (3), plus room for circles
        ///     still fading. The C# side of <c>_ClearanceCircles[8]</c> in <c>Include/Clearance.cginc</c>;
        ///     change both together.
        /// </summary>
        internal const int MaxCircles = 8;

        internal readonly Vector2 Center;

        /// <summary>Effective radius: the settings' radius already scaled by <see cref="Strength" />.</summary>
        internal readonly float Radius;

        /// <summary>0..1, easing in after launch and out after the ball lands.</summary>
        internal readonly float Strength;

        internal ClearanceCircle(Vector2 center, float radius, float strength)
        {
            Center = center;
            Radius = radius;
            Strength = strength;
        }
    }
}
