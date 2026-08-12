using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>One board actor whose hosted item, if hit, arms the shot's piercing lance.</summary>
    internal readonly struct PierceItemCircle
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public PierceItemCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    ///     The pierce-arming item hosts the aim telegraph must know about, in the geometry the projectile
    ///     will actually meet — so it can stop drawing a deflection off every tough beyond the first one
    ///     hit, matching a piercing shot's real behaviour.
    /// </summary>
    /// <remarks>
    ///     Positions come from the views, not from slot coordinates, for the same reason as
    ///     <see cref="IDeflectorField" />: balloons drift between slots while the board settles.
    /// </remarks>
    internal interface IPierceItemField
    {
        /// <summary>Fills <paramref name="results" /> with the live pierce-item hosts. Clears it first.</summary>
        void CollectPierceItems(List<PierceItemCircle> results);
    }
}
