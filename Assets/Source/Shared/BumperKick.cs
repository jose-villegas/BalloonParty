using System.Collections.Generic;
using System.Diagnostics;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The forgiving kick's geometry: a ball leaving a cap close enough to the line of a cap of another wall is
    ///     bent onto it, and landed where its next bounce carries the rally on. <see cref="ForgivingKickRule" />
    ///     asks it for the game, the shot solver and the aim line.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The target.</b> From the contact point the kick takes the mirror reflection, then looks at every
    ///         cap of another wall (<see cref="BumperCircle.Wall" />) whose bearing lies within the cone of it and
    ///         whose corridor is clear. The one at the smallest angle wins, the lowest id on a tie; with none the
    ///         ball leaves along the reflection. A cap of the wall just hit is never a target: inside one wall
    ///         every bounce is the mirror reflection, so a wall cannot hand the ball along itself.
    ///     </para>
    ///     <para>
    ///         <b>The landing point looks one cap ahead.</b> The ball is aimed at the point of the target from
    ///         which a mirror reflection sends it on toward a follower. The ball then leaves the target along a
    ///         reflection that already points at the follower, so the target's own kick picks the follower and
    ///         repeats the step: the rally carries itself. The offset that lands the ball there
    ///         (<see cref="BumperKickTuning" />) is kept within the tuning's two offsets: never the centre, never a
    ///         thin graze.
    ///     </para>
    ///     <para>
    ///         <b>The follower is the nearest cap of another wall.</b> Every cap that is not of the target's own
    ///         wall is tried, the nearest to the target first (the lower id on a tie; where a cap stands in the
    ///         list does not matter). Two walls that face each other are each other's nearest, so the ball is
    ///         sent back across the gap between them, which is how two walls give three hits. A follower fits when
    ///         the offset it needs is within the tuning's two, the reflection's way to it is clear, and the kicked
    ///         line that lands the ball for it has a clear corridor. When none fits, the ball is landed for the
    ///         nearest of them as near as the offsets allow; when that line is blocked too, at the smallest
    ///         offset, as the last kick is. A contact whose cap is not in the list is not kicked.
    ///     </para>
    ///     <para>
    ///         <b>The last kick.</b> The bounce that brings the count to the chain target is aimed at the hit that
    ///         banks, whose own bounce is not kicked. With no chain left to set up it lands at the smallest offset:
    ///         a reflection aimed at a follower's centre would be a dead-centre hit with no kick left to correct
    ///         it. The side is the one the reflection leaned to, or the other when that line is blocked. A
    ///         reflection that points straight at the centre leans to neither; the side the hit mushroom's surface
    ///         faces decides then.
    ///     </para>
    ///     <para>
    ///         <b>The corridor</b> is the path of the ball's centre along the kicked line, from the contact point
    ///         to where it meets the target. It is blocked when the path first enters another cap's contact
    ///         circle (<see cref="BumperCircle.Radius" />: that cap's radius plus the ball's), when the ball
    ///         already stands inside one (the caps of a wall overlap, and a ball in the notch between two of them
    ///         bounces off the second at once), or when the kicked line does not clearly leave the cap just hit: a
    ///         line within a hair of its tangent could re-enter it on a rounding error. Balloons are ignored: they
    ///         pop, or the ball ricochets off them, on the way.
    ///     </para>
    ///     <para>
    ///         <b>It stops.</b> The bounce that takes the count past the chain target and every later one are
    ///         mirror reflections, whose spread ends any exchange between mushrooms. The chain rule's cap on
    ///         bounces (<see cref="MushroomChainRules.MaxBouncesPerStretch" />) ends what is left.
    ///     </para>
    /// </remarks>
    internal static class BumperKick
    {
        // Halvings of the arc the landing point is searched on. The arc is under half a turn, so 32 of them are
        // past what a float can tell apart.
        private const int SolveSteps = 32;

        // The sine of the most a landing point's surface normal may be off the bisector of its two ways out and
        // still be answered: 0.03 degrees, so the reflection is at most 0.06 degrees off the follower.
        private const float MaxLandingResidual = 5e-4f;

        private const float MinSqrLength = 1e-9f;

        // The sine of the smallest angle off a bearing that counts as leaning to a side: a hundredth of a degree.
        private const float MinLean = 2e-4f;

        // The least a kicked line must point away from the surface of the mushroom it leaves, as the cosine of its
        // angle to the surface normal: 0.06 degrees clear of the tangent.
        private const float MinExitDot = 1e-3f;

        private const string LogTag = "Mushrooms";

        /// <summary>
        ///     The unit direction the ball leaves <paramref name="contact" /> along: kicked while
        ///     <paramref name="chain" /> (the flight's chain after this bounce was counted) has not passed
        ///     <paramref name="chainTarget" />, the mirror reflection after that. <paramref name="bumpers" /> is
        ///     every cap, the one hit included, in any order. Nothing is allocated.
        /// </summary>
        internal static Vector2 Resolve(
            in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain,
            int chainTarget, in BumperKickTuning tuning)
        {
            TryKick(contact, bumpers, chain, chainTarget, tuning, out var leaving);
            return leaving;
        }

        /// <summary>
        ///     As <see cref="Resolve" />, also saying whether the ball was kicked: false when
        ///     <paramref name="leaving" /> is the mirror reflection.
        /// </summary>
        internal static bool TryKick(
            in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain,
            int chainTarget, in BumperKickTuning tuning, out Vector2 leaving)
        {
            leaving = TrueReflectRule.Reflect(contact);
            if (bumpers == null || chain.Count > chainTarget || IndexOf(bumpers, contact.Bumper.Id) < 0)
            {
                return false;
            }

            AssertWellFormed(bumpers);
            var reflection = leaving;
            var looksAhead = chain.Count < chainTarget;
            var bestAngle = tuning.ConeDegrees;
            var bestId = int.MaxValue;
            var found = false;
            for (var i = 0; i < bumpers.Count; i++)
            {
                var target = bumpers[i];
                if (target.Wall == contact.Bumper.Wall || !TryFindBearing(contact.Point, target, out var line))
                {
                    continue;
                }

                // The id breaks a tie, so the answer does not depend on where a mushroom stands in the list.
                var angle = Vector2.Angle(reflection, line.Bearing);
                if (angle > bestAngle || (found && angle == bestAngle && target.Id > bestId))
                {
                    continue;
                }

                if (!TryAimAt(contact, line, reflection, target, bumpers, tuning, looksAhead, out var aim))
                {
                    continue;
                }

                leaving = aim;
                bestAngle = angle;
                bestId = target.Id;
                found = true;
            }

            return found;
        }

        // The kicked direction onto the target: looking a mushroom ahead when the chain still has a hit to set
        // up, and at the smallest offset when it has not or when no line that looks ahead is clear.
        private static bool TryAimAt(
            in BumperContact contact, in BearingLine line, Vector2 reflection, in BumperCircle target,
            IReadOnlyList<BumperCircle> bumpers, in BumperKickTuning tuning, bool looksAhead, out Vector2 aim)
        {
            return (looksAhead && TryAimAhead(contact, line, target, bumpers, tuning, out aim))
                || TryAimBeside(contact, line, reflection, target, bumpers, tuning, out aim);
        }

        /// <summary>
        ///     The point of <paramref name="on" /> where a ball coming from <paramref name="from" /> must land for
        ///     its mirror reflection to point at <paramref name="toward" />, with the offset the ball lands at (0
        ///     dead centre, 1 a graze) and the side of the mushroom it lands on (+1 left of the line from
        ///     <paramref name="from" /> to its centre, -1 right). False when there is none: either end is on or
        ///     inside the mushroom, or the straight way between them crosses it. Also false, never a wrong point,
        ///     when rounding leaves the answer short of a true reflection, which an end a hair off the circle can do.
        /// </summary>
        /// <remarks>
        ///     The point lies on the arc of the circle both ends see. Along that arc the surface normal turns
        ///     steadily past the bisector of the two ways out of the point, from behind it at one end to ahead of
        ///     it at the other, and the landing point is where they meet. So the arc brackets the one answer, and
        ///     halving it finds that answer whenever there is one.
        /// </remarks>
        internal static bool TryFindLandingPoint(
            Vector2 from, in BumperCircle on, Vector2 toward, out Vector2 point, out float offset, out float side)
        {
            point = default;
            offset = 0f;
            side = 1f;
            if (!TryFindSharedArc(from, on, toward, out var low, out var high)
                || LeadOfNormal(from, on, toward, low) > 0f || LeadOfNormal(from, on, toward, high) < 0f)
            {
                return false;
            }

            for (var step = 0; step < SolveSteps; step++)
            {
                var middle = low + high;
                if (middle.sqrMagnitude < MinSqrLength)
                {
                    return false;
                }

                middle.Normalize();
                if (LeadOfNormal(from, on, toward, middle) < 0f)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            var normal = (low + high).normalized;
            if (Mathf.Abs(LeadOfNormal(from, on, toward, normal)) > MaxLandingResidual)
            {
                return false;
            }

            point = on.Center + (normal * on.Radius);
            offset = Mathf.Abs(Cross(normal, (from - point).normalized));
            side = SideOf(on.Center - from, point - from);
            return true;
        }

        // The arc of the circle that both points see, as the surface normals at its two ends, counter-clockwise
        // from low to high. False when either point is on or inside the circle, or no part of it is seen by both.
        private static bool TryFindSharedArc(
            Vector2 from, in BumperCircle on, Vector2 toward, out Vector2 low, out Vector2 high)
        {
            low = default;
            high = default;
            var toFrom = from - on.Center;
            var toToward = toward - on.Center;
            var fromDistance = toFrom.magnitude;
            var towardDistance = toToward.magnitude;
            if (on.Radius <= 0f || fromDistance <= on.Radius || towardDistance <= on.Radius)
            {
                return false;
            }

            // Each point sees the arc within this angle of its own bearing from the centre.
            var fromHalf = Mathf.Acos(on.Radius / fromDistance);
            var towardHalf = Mathf.Acos(on.Radius / towardDistance);
            var between = Mathf.Atan2(Cross(toFrom, toToward), Vector2.Dot(toFrom, toToward));
            var lowAngle = Mathf.Max(-fromHalf, between - towardHalf);
            var highAngle = Mathf.Min(fromHalf, between + towardHalf);
            if (lowAngle >= highAngle)
            {
                return false;
            }

            var axis = toFrom / fromDistance;
            low = Rotate(axis, lowAngle);
            high = Rotate(axis, highAngle);
            return true;
        }

        // How far the surface normal is turned past the bisector of the two ways out of its point, as the sine of
        // the angle: negative behind it, positive ahead, zero where a mirror reflection holds.
        private static float LeadOfNormal(Vector2 from, in BumperCircle on, Vector2 toward, Vector2 normal)
        {
            var point = on.Center + (normal * on.Radius);
            var bisector = (from - point).normalized + (toward - point).normalized;
            return bisector.sqrMagnitude < MinSqrLength ? 0f : Cross(bisector.normalized, normal);
        }

        // The kicked direction toward the target that lands the ball for the first follower that fits; for the
        // nearest follower of all, as near as the offsets allow, when none does. False when that line is blocked too.
        private static bool TryAimAhead(
            in BumperContact contact, in BearingLine line, in BumperCircle target,
            IReadOnlyList<BumperCircle> bumpers, in BumperKickTuning tuning, out Vector2 aim)
        {
            var nearest = -1;
            var lastDistance = -1f;
            var lastId = int.MinValue;
            while (TryFindNextFollower(bumpers, target, lastDistance, lastId, out var index))
            {
                lastDistance = (bumpers[index].Center - target.Center).sqrMagnitude;
                lastId = bumpers[index].Id;
                nearest = nearest < 0 ? index : nearest;
                if (TryAimFor(contact, line, target, bumpers[index], bumpers, tuning, out aim))
                {
                    return true;
                }
            }

            // The cap just hit is of another wall than the target, so there is always a nearest.
            nearest = nearest < 0 ? IndexOf(bumpers, contact.Bumper.Id) : nearest;
            aim = AimAsNearAsAllowed(contact.Point, line, target, bumpers[nearest].Center, tuning);
            return !IsBlocked(contact, aim, target, bumpers);
        }

        // The follower after the last one tried: of the caps that are not of the target's own wall, the nearest
        // to the target that is farther than the last (the next id at the same distance). The order is by
        // distance and id alone, so it does not depend on where a cap stands in the list.
        private static bool TryFindNextFollower(
            IReadOnlyList<BumperCircle> bumpers, in BumperCircle target, float lastDistance, int lastId, out int index)
        {
            index = -1;
            var best = float.MaxValue;
            var bestId = int.MaxValue;
            for (var i = 0; i < bumpers.Count; i++)
            {
                var cap = bumpers[i];
                if (cap.Wall == target.Wall)
                {
                    continue;
                }

                var distance = (cap.Center - target.Center).sqrMagnitude;
                if (distance < lastDistance || (distance == lastDistance && cap.Id <= lastId))
                {
                    continue;
                }

                if (distance < best || (distance == best && cap.Id < bestId))
                {
                    index = i;
                    best = distance;
                    bestId = cap.Id;
                }
            }

            return index >= 0;
        }

        // The kicked direction that lands the ball on the target at the smallest offset: on the side the reflection
        // leaned to, or on the other side when that line is blocked. A reflection that points straight at the
        // centre, as the one a look-ahead kick set up does, leans to neither side but by rounding; the side the
        // hit mushroom's surface faces decides then, so the same flight always takes the same side.
        private static bool TryAimBeside(
            in BumperContact contact, in BearingLine line, Vector2 reflection, in BumperCircle target,
            IReadOnlyList<BumperCircle> bumpers, in BumperKickTuning tuning, out Vector2 aim)
        {
            var lean = Cross(line.Bearing, reflection);
            var side = Mathf.Abs(lean) > MinLean
                ? Mathf.Sign(lean)
                : SideOf(line.Bearing, contact.Point - contact.Bumper.Center);
            aim = line.Aim(tuning.MinOffset, side);
            if (!IsBlocked(contact, aim, target, bumpers))
            {
                return true;
            }

            aim = line.Aim(tuning.MinOffset, -side);
            return !IsBlocked(contact, aim, target, bumpers);
        }

        // The kicked direction that lands the ball on the target for this follower, when the follower fits: the
        // offset it needs is one the kick may use, the reflection's way to the follower is clear, and so is the
        // kicked line itself.
        private static bool TryAimFor(
            in BumperContact contact, in BearingLine line, in BumperCircle target, in BumperCircle follower,
            IReadOnlyList<BumperCircle> bumpers, in BumperKickTuning tuning, out Vector2 aim)
        {
            aim = default;
            var from = contact.Point;
            if (!TryFindLandingPoint(from, target, follower.Center, out var point, out var offset, out var side)
                || offset < tuning.MinOffset || offset > tuning.MaxOffset
                || !ReachesFirst(point, follower.Center - point, follower, target.Id, bumpers))
            {
                return false;
            }

            aim = line.Aim(offset, side);
            return !IsBlocked(contact, aim, target, bumpers);
        }

        private static Vector2 AimAsNearAsAllowed(
            Vector2 from, in BearingLine line, in BumperCircle target, Vector2 toward, in BumperKickTuning tuning)
        {
            if (TryFindLandingPoint(from, target, toward, out _, out var offset, out var side))
            {
                return line.Aim(Mathf.Clamp(offset, tuning.MinOffset, tuning.MaxOffset), side);
            }

            // No landing point: the follower lies straight on, behind the target. The thinnest hit allowed, on the
            // follower's side, turns the ball the least.
            return line.Aim(tuning.MaxOffset, SideOf(line.Bearing, toward - from));
        }

        private static bool IsBlocked(
            in BumperContact contact, Vector2 aim, in BumperCircle target, IReadOnlyList<BumperCircle> bumpers)
        {
            var outward = contact.Point - contact.Bumper.Center;
            if (Vector2.Dot(aim, outward) <= MinExitDot * outward.magnitude)
            {
                return true;
            }

            return !ReachesFirst(contact.Point, aim, target, contact.Bumper.Id, bumpers);
        }

        // Whether a ball from the point along the direction meets the target before any other cap. The cap it is
        // leaving is not tested: the ball starts on its circle. A cap the ball already stands inside (the next
        // one along its own wall, in the notch between the two) is met at once, so nothing is reached first.
        private static bool ReachesFirst(
            Vector2 point, Vector2 direction, in BumperCircle target, int leavingId,
            IReadOnlyList<BumperCircle> bumpers)
        {
            if (!CircleContact.TryFindEntry(point, direction, target.Center, target.Radius, out _, out var reach))
            {
                return false;
            }

            for (var i = 0; i < bumpers.Count; i++)
            {
                var other = bumpers[i];
                if (other.Id == target.Id || other.Id == leavingId)
                {
                    continue;
                }

                if (BumperSweep.TryFindEntry(point, direction, other, out var entry) && entry < reach)
                {
                    return false;
                }
            }

            return true;
        }

        // False when the point is on or inside the target, which leaves no line to aim along.
        private static bool TryFindBearing(Vector2 point, in BumperCircle target, out BearingLine line)
        {
            var toTarget = target.Center - point;
            var distance = toTarget.magnitude;
            if (target.Radius <= 0f || distance <= target.Radius)
            {
                line = default;
                return false;
            }

            line = new BearingLine(toTarget / distance, target.Radius / distance);
            return true;
        }

        // What the kick takes for granted of its list: no two caps share an id, and no two contact circles of
        // different walls overlap (a ball caught between two walls that close has no way out but the cap on
        // bounces). The caps of one wall may: they stand in neighbouring slots, and the ball cannot pass between
        // them. The layout catalogue emits neither fault; this is here for a list that comes from anywhere else.
        [Conditional("UNITY_ENABLE_CHECKS")]
        private static void AssertWellFormed(IReadOnlyList<BumperCircle> bumpers)
        {
            for (var i = 0; i < bumpers.Count; i++)
            {
                for (var j = i + 1; j < bumpers.Count; j++)
                {
                    var a = bumpers[i];
                    var b = bumpers[j];

                    // Asked first, so a sound list builds no message.
                    if (a.Id == b.Id)
                    {
                        Log.Assert(false, LogTag, $"Two caps share the id {a.Id}.");
                    }

                    if (a.Wall != b.Wall && (a.Center - b.Center).magnitude < a.Radius + b.Radius)
                    {
                        Log.Assert(false, LogTag,
                            $"Caps {a.Id} and {b.Id} of two walls stand too close for the ball to pass between them.");
                    }
                }
            }
        }

        private static int IndexOf(IReadOnlyList<BumperCircle> bumpers, int id)
        {
            for (var i = 0; i < bumpers.Count; i++)
            {
                if (bumpers[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        // +1 when the direction leans to the left of the bearing or along it, -1 to the right.
        private static float SideOf(Vector2 bearing, Vector2 direction)
        {
            return Cross(bearing, direction) >= 0f ? 1f : -1f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static Vector2 Rotate(Vector2 direction, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2((direction.x * cos) - (direction.y * sin), (direction.x * sin) + (direction.y * cos));
        }

        // The straight line from a contact point to a target's centre: its unit direction, and the target's radius
        // as a share of the distance, which is all an aim beside the centre needs.
        private readonly struct BearingLine
        {
            public readonly Vector2 Bearing;
            private readonly float _radiusOverDistance;

            public BearingLine(Vector2 bearing, float radiusOverDistance)
            {
                Bearing = bearing;
                _radiusOverDistance = radiusOverDistance;
            }

            // The unit direction whose line passes the target's centre at the offset (a share of its radius), on
            // the side given: +1 left of the bearing, -1 right.
            public Vector2 Aim(float offset, float side)
            {
                return Rotate(Bearing, side * Mathf.Asin(Mathf.Clamp01(offset * _radiusOverDistance)));
            }
        }
    }
}
