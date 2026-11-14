using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>One balloon a piercing shot's forward cast crossed, already resolved off its collider.</summary>
    internal readonly struct PierceCastCandidate
    {
        internal readonly IBalloonModel Balloon;
        internal readonly float Distance;
        internal readonly Vector3 Position;

        internal PierceCastCandidate(IBalloonModel balloon, float distance, Vector3 position)
        {
            Balloon = balloon;
            Distance = distance;
            Position = position;
        }
    }

    /// <summary>
    ///     Picks the tough a piercing shot's telegraph points at out of everything its forward cast crossed:
    ///     the nearest one it hasn't already plowed this run. A piercing shot pops ordinary balloons in
    ///     passing, so they never stop the line — only a tough is an armored contact worth warning of.
    /// </summary>
    internal static class PierceTelegraphScan
    {
        internal static bool TrySelectNextTough(
            IReadOnlyList<PierceCastCandidate> candidates,
            IReadOnlyList<PendingPierceHit> plowed,
            out PierceCastCandidate next)
        {
            next = default;
            var found = false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (found && candidate.Distance >= next.Distance)
                {
                    continue;
                }

                if (candidate.Balloon == null || !candidate.Balloon.IsTough() || IsPlowed(candidate.Balloon, plowed))
                {
                    continue;
                }

                next = candidate;
                found = true;
            }

            return found;
        }

        internal static bool IsPlowed(IBalloonModel balloon, IReadOnlyList<PendingPierceHit> plowed)
        {
            for (var i = 0; i < plowed.Count; i++)
            {
                if (ReferenceEquals(plowed[i].Balloon, balloon))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
