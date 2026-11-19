using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     One cap a lance cut (a meadow mushroom's) that <see cref="ProjectileMotionResolver" /> resolved during
    ///     a fixed step. A step can hold several; the resolver keeps them in order for the view to announce
    ///     (<see cref="ProjectileMotionResolver.StepCapCuts" />). The cap has already left the bumper field.
    /// </summary>
    internal readonly struct CapCut
    {
        // How far from the cap's centre, as a share of its own radius, the line it splits along may lie. A lance
        // whose centre passes outside the cap still clips it with its edge, and the line is drawn in from there
        // so that a sliver comes off.
        private const float OutermostCutShare = 0.85f;

        /// <summary>The cap's id: which cap of the level was cut.</summary>
        public readonly int BumperId;

        /// <summary>The mushroom's grid slot.</summary>
        public readonly Vector2Int Slot;

        /// <summary>
        ///     A point inside the cap that the split passes through; with <see cref="Direction" /> it is the line
        ///     the cap splits along. It is the point of the lance's line nearest the cap's centre, the line
        ///     carried on both ways: for a lance that began its step inside the circle that point can lie behind
        ///     the ball. When the line passes outside the cap itself and only the ball's edge clipped it, the
        ///     point is drawn in toward the centre, off the line the ball's centre flew, so that the split still
        ///     crosses the cap and a sliver comes off. So it is not always a point the ball's centre flew through.
        /// </summary>
        public readonly Vector2 Point;

        /// <summary>The lance's unit direction of travel.</summary>
        public readonly Vector2 Direction;

        /// <summary>How many caps the flight has cut, this one included: the charge this cut banked is the
        /// flight's first, second and so on.</summary>
        public readonly int Charges;

        /// <param name="capRadius">The cap's own radius, without the ball's.</param>
        /// <param name="charges">The flight's count of cut caps with this one.</param>
        internal CapCut(
            in BumperCircle cap, Vector2Int slot, Vector2 from, Vector2 heading, float capRadius, int charges = 0)
        {
            BumperId = cap.Id;
            Slot = slot;
            Direction = heading;
            Charges = charges;

            var along = Vector2.Dot(cap.Center - from, heading);
            var offset = from + (heading * along) - cap.Center;
            var outermost = Mathf.Max(0f, capRadius) * OutermostCutShare;
            Point = cap.Center + Vector2.ClampMagnitude(offset, outermost);
        }

        /// <summary>The cut as the rest of the game hears of it.</summary>
        internal MushroomCutMessage ToMessage()
        {
            return new MushroomCutMessage(Slot, Point, Direction, MushroomCutCause.Lance, Charges);
        }
    }
}
