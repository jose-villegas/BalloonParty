using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>One board actor that would bounce the next shot instead of letting it through.</summary>
    internal readonly struct DeflectorCircle
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public DeflectorCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    ///     The deflectors the aim telegraph must bounce off, in the geometry the projectile will
    ///     actually meet.
    /// </summary>
    /// <remarks>
    ///     Positions come from the views, not from slot coordinates: the real deflection reflects off
    ///     where the balloon is drawn (see <c>BalloonController.Deflect</c>), and balloons drift between
    ///     slots while the board settles — which is exactly when a player is lining a shot up.
    /// </remarks>
    internal interface IDeflectorField
    {
        /// <summary>Fills <paramref name="results" /> with the live deflectors. Clears it first.</summary>
        void CollectDeflectors(List<DeflectorCircle> results);
    }
}
