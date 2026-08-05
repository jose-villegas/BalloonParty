using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Item.Shield
{
    /// <summary>One balloon a shield could be placed on, as the planner sees it.</summary>
    internal readonly struct ShieldHostCandidate
    {
        public readonly Vector2 Center;

        /// <summary>Balloon contact radius plus the projectile's — the circle a shot actually meets.</summary>
        public readonly float ContactRadius;

        public ShieldHostCandidate(Vector2 center, float contactRadius)
        {
            Center = center;
            ContactRadius = contactRadius;
        }
    }

    /// <summary>One planned shield, and how forgiving it is to aim at.</summary>
    internal readonly struct ShieldPlacement
    {
        public readonly int CandidateIndex;

        /// <summary>
        ///     How many sampled opening angles reach this shield. It is the tolerance, measured rather
        ///     than assumed: a wide band is a shot the player can be sloppy about, a narrow one is a
        ///     shot they have to mean. One is a shield only a single exact ray collects.
        /// </summary>
        public readonly int EntryAngles;

        public ShieldPlacement(int candidateIndex, int entryAngles)
        {
            CandidateIndex = candidateIndex;
            EntryAngles = entryAngles;
        }
    }

    /// <summary>
    ///     Places shields along flights the thrower can actually fly, so a chain is collectable by
    ///     construction rather than by luck.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Scattering shields at random puts most of them where no affordable shot reaches, which
    ///         reads to the player as "shields are rare" rather than "that one was mine to take". This
    ///         builds the shot first and hangs the shields off it.
    ///     </para>
    ///     <para>
    ///         The only free choice is the opening angle. After that the projectile's direction is not
    ///         a decision — it is whatever the last reflection left — so the recursion is "keep flying
    ///         with one more shield in the bank", and each placement extends how far the flight may
    ///         continue. That extension is the whole mechanic: shield <i>n</i> is what makes shield
    ///         <i>n+1</i> reachable.
    ///     </para>
    ///     <para>
    ///         Pure C#: no scene, no grid, no Unity object. The caller supplies circles and gets back
    ///         indices into its own candidate list.
    ///     </para>
    /// </remarks>
    internal sealed class ShieldChainPlanner
    {
        // Off a reflection the ray starts exactly on the surface it just left; without a nudge the
        // next intersection test finds that same surface at distance ~0 and the flight stalls there.
        private const float SurfaceEpsilon = 1e-3f;

        // A flight that neither dies nor places anything would otherwise spin: every leg is free when
        // deflectors keep redirecting it and no wall is ever reached.
        private const int MaxLegs = 64;

        // A shield only a couple of angles reach is the brittle chain this design exists to avoid.
        private const int MinEntryAngles = 3;

        // Reachable by more than this share of the fan means the player already sweeps it for free,
        // so a shield there extends nothing — the scattered-placement failure, restated as a number.
        private const float CheapZoneFraction = 0.6f;

        private readonly WallLimits _walls;
        private readonly IReadOnlyList<DeflectorCircle> _deflectors;

        private readonly List<int> _claimed = new();

        internal ShieldChainPlanner(WallLimits walls, IReadOnlyList<DeflectorCircle> deflectors)
        {
            _walls = walls;
            _deflectors = deflectors;
        }

        /// <summary>
        ///     Plans a chain against a fan of opening angles, so every shield has several ways in.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         A chain cannot fork: once the shot takes the first shield its heading is whatever
        ///         the last reflection left, so two openings never share a tail. Multiple ways in
        ///         therefore means choosing shield POSITIONS that many affordable flights pass
        ///         through, not branching one flight.
        ///     </para>
        ///     <para>
        ///         Each round counts, per candidate, how many fan angles reach it — crediting shields
        ///         already placed, which is what widens the reachable set as the chain grows. The pick
        ///         is then a band, not a maximum: a candidate every angle reaches sits in the zone a
        ///         player already sweeps for free and teaches nothing, while one that a single angle
        ///         reaches is the brittle chain this exists to avoid.
        ///     </para>
        /// </remarks>
        internal int PlanChain(
            Vector2 origin, IReadOnlyList<Vector2> fan, int shields, int wanted,
            IReadOnlyList<ShieldHostCandidate> candidates, List<ShieldPlacement> results)
        {
            results.Clear();
            if (fan == null || fan.Count == 0 || candidates == null || candidates.Count == 0 || wanted <= 0)
            {
                return 0;
            }

            var placed = new bool[candidates.Count];
            var reachCount = new int[candidates.Count];
            var viaDeflections = new int[candidates.Count];
            var reached = new List<int>();
            var deflections = new List<int>();

            while (results.Count < wanted)
            {
                Array.Clear(reachCount, 0, reachCount.Length);
                for (var i = 0; i < viaDeflections.Length; i++)
                {
                    viaDeflections[i] = int.MaxValue;
                }

                for (var i = 0; i < fan.Count; i++)
                {
                    Fly(origin, fan[i], shields, candidates, placed, reached, deflections);
                    for (var r = 0; r < reached.Count; r++)
                    {
                        reachCount[reached[r]]++;
                        viaDeflections[reached[r]] = Mathf.Min(viaDeflections[reached[r]], deflections[r]);
                    }
                }

                var pick = SelectInBand(reachCount, viaDeflections, placed, fan.Count);
                if (pick < 0)
                {
                    break;
                }

                placed[pick] = true;
                results.Add(new ShieldPlacement(pick, reachCount[pick]));
            }

            return results.Count;
        }

        // The band, and the two relaxations. Rejecting everything would leave shields unplaced, which
        // is worse than an easy one — so the cheap-zone filter gives way first, and the tolerance
        // floor only after that.
        private int SelectInBand(
            IReadOnlyList<int> reachCount, IReadOnlyList<int> viaDeflections, IReadOnlyList<bool> placed,
            int fanSize)
        {
            var cheapCutoff = Mathf.Max(MinEntryAngles, Mathf.RoundToInt(fanSize * CheapZoneFraction));

            var best = Pick(reachCount, viaDeflections, placed, MinEntryAngles, cheapCutoff);
            if (best >= 0)
            {
                return best;
            }

            best = Pick(reachCount, viaDeflections, placed, MinEntryAngles, int.MaxValue);
            return best >= 0 ? best : Pick(reachCount, viaDeflections, placed, 1, int.MaxValue);
        }

        /// <summary>Fewest deflections first, then most entry angles.</summary>
        /// <remarks>
        ///     Walls beat balloons on everything that matters here. A wall is always where it was, so a
        ///     route off one survives the board rebalancing and survives the balloons in between being
        ///     popped — a route through a tough breaks the moment that tough moves or dies. A wall is
        ///     also the reflection a player can read: bouncing off a circle is acutely sensitive to
        ///     where it is struck, which is why the aim trace draws only one reflection at all.
        /// </remarks>
        private static int Pick(
            IReadOnlyList<int> reachCount, IReadOnlyList<int> viaDeflections, IReadOnlyList<bool> placed,
            int min, int max)
        {
            var best = -1;
            var bestCount = 0;
            var bestDeflections = int.MaxValue;

            for (var i = 0; i < reachCount.Count; i++)
            {
                var count = reachCount[i];
                if (placed[i] || count < min || count > max)
                {
                    continue;
                }

                var deflections = viaDeflections[i];
                if (deflections > bestDeflections || (deflections == bestDeflections && count <= bestCount))
                {
                    continue;
                }

                bestDeflections = deflections;
                bestCount = count;
                best = i;
            }

            return best;
        }

        /// <summary>
        ///     Flies one shot from <paramref name="origin" /> along <paramref name="direction" /> and
        ///     records which candidates it can collect, in the order it meets them.
        /// </summary>
        /// <param name="shields">
        ///     Shields the shot starts with. Only wall bounces spend one — a deflection off a tough is
        ///     free, mirroring <c>ProjectileMotionResolver</c>, which decrements only at a wall.
        /// </param>
        /// <param name="wanted">How many to place. Fewer is a partial chain, not a failure.</param>
        /// <returns>How many were placed.</returns>
        internal int PlanChain(
            Vector2 origin, Vector2 direction, int shields, int wanted,
            IReadOnlyList<ShieldHostCandidate> candidates, List<int> results)
        {
            results.Clear();
            if (candidates == null || wanted <= 0)
            {
                return 0;
            }

            Fly(origin, direction, shields, candidates, null, results);
            if (results.Count > wanted)
            {
                results.RemoveRange(wanted, results.Count - wanted);
            }

            return results.Count;
        }

        /// <summary>
        ///     Flies one shot and records the candidates it meets, in order. Candidates marked in
        ///     <paramref name="placed" /> are already carrying a shield, so crossing one pays the
        ///     flight another bounce — that credit is what lets each planning round reach further
        ///     than the last.
        /// </summary>
        private void Fly(
            Vector2 origin, Vector2 direction, int shields,
            IReadOnlyList<ShieldHostCandidate> candidates, IReadOnlyList<bool> placed, List<int> reached,
            List<int> deflectionsAt = null)
        {
            reached.Clear();
            deflectionsAt?.Clear();
            if (direction.sqrMagnitude < SurfaceEpsilon)
            {
                return;
            }

            var position = origin;
            var heading = direction.normalized;
            var deflectionsUsed = 0;
            _claimed.Clear();

            for (var leg = 0; leg < MaxLegs; leg++)
            {
                if (!_walls.TryFindCrossing(position, heading, out var crossing, out var wallNormal))
                {
                    return;
                }

                var deflected = TryFindNearestDeflector(
                    position, heading, Vector2.Distance(position, crossing), out var contact,
                    out var deflectNormal);
                var legEnd = deflected ? contact : (Vector2)crossing;

                ClaimOnLeg(position, heading, Vector2.Distance(position, legEnd), candidates, placed,
                    ref shields, reached, deflectionsAt, deflectionsUsed);

                if (deflected)
                {
                    position = contact + (deflectNormal * SurfaceEpsilon);
                    heading = Vector2.Reflect(heading, deflectNormal);
                    deflectionsUsed++;
                    continue;
                }

                // The wall is what the shot pays for, and a shot that cannot pay ends here.
                if (--shields < 0)
                {
                    return;
                }

                position = _walls.ClampInside(crossing) + (wallNormal.normalized * SurfaceEpsilon);
                heading = Vector2.Reflect(heading, wallNormal.normalized);
            }
        }

        // At most one per leg: two on the same straight run are one opportunity the player takes
        // without aiming differently, so the second teaches nothing and extends nothing. Either way it
        // pays a shield — an already-placed one funds the next bounce, which is how each planning
        // round reaches further than the last; only a NEW one counts as reached, or a shield would be
        // credited with making itself reachable.
        private void ClaimOnLeg(
            Vector2 position, Vector2 heading, float legLength,
            IReadOnlyList<ShieldHostCandidate> candidates, IReadOnlyList<bool> placed, ref int shields,
            List<int> reached, List<int> deflectionsAt, int deflectionsUsed)
        {
            if (!TryClaimOnLeg(position, heading, legLength, candidates, out var claimed))
            {
                return;
            }

            _claimed.Add(claimed);
            shields++;

            if (placed == null || !placed[claimed])
            {
                reached.Add(claimed);
                deflectionsAt?.Add(deflectionsUsed);
            }
        }

        private bool TryClaimOnLeg(
            Vector2 position, Vector2 heading, float legLength,
            IReadOnlyList<ShieldHostCandidate> candidates, out int claimed)
        {
            claimed = -1;
            var nearest = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                if (_claimed.Contains(i))
                {
                    continue;
                }

                var candidate = candidates[i];
                if (!CircleContact.TryFindEntry(position, heading, candidate.Center, candidate.ContactRadius,
                        out _, out var entryDistance))
                {
                    continue;
                }

                if (entryDistance > legLength || entryDistance >= nearest)
                {
                    continue;
                }

                nearest = entryDistance;
                claimed = i;
            }

            return claimed >= 0;
        }

        private bool TryFindNearestDeflector(
            Vector2 position, Vector2 heading, float maxDistance, out Vector2 contact, out Vector2 normal)
        {
            contact = default;
            normal = default;
            var nearest = float.MaxValue;

            for (var i = 0; _deflectors != null && i < _deflectors.Count; i++)
            {
                var deflector = _deflectors[i];
                if (!CircleContact.TryFindEntry(position, heading, deflector.Center, deflector.Radius,
                        out var entryNormal, out var entryDistance))
                {
                    continue;
                }

                if (entryDistance > maxDistance || entryDistance >= nearest)
                {
                    continue;
                }

                nearest = entryDistance;
                normal = entryNormal;
                contact = deflector.Center + (entryNormal * deflector.Radius);
            }

            return nearest < float.MaxValue;
        }
    }
}
