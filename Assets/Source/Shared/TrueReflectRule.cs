using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>The plain bounce: the ball leaves a bumper along the mirror image of how it came in.</summary>
    internal sealed class TrueReflectRule : IBumperBounceRule
    {
        // Below this a vector has no direction worth trusting (Vector2.normalized gives zero under 1e-5 long).
        private const float MinSqrLength = 1e-9f;

        public Vector2 Resolve(
            in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain)
        {
            return Reflect(contact);
        }

        /// <summary>
        ///     The unit mirror reflection of the contact's incoming direction about the bumper's surface at the
        ///     contact point. A ball already heading away from the surface is not turned back into it: its own
        ///     direction is returned. A ball with no direction leaves straight out from the centre.
        /// </summary>
        internal static Vector2 Reflect(in BumperContact contact)
        {
            var outward = contact.Point - contact.Bumper.Center;
            var incoming = contact.Incoming;
            if (incoming.sqrMagnitude < MinSqrLength)
            {
                return outward.sqrMagnitude < MinSqrLength ? Vector2.up : outward.normalized;
            }

            incoming = incoming.normalized;
            if (outward.sqrMagnitude < MinSqrLength)
            {
                return -incoming;
            }

            var normal = outward.normalized;
            if (Vector2.Dot(incoming, normal) >= 0f)
            {
                return incoming;
            }

            return Vector2.Reflect(incoming, normal).normalized;
        }
    }
}
