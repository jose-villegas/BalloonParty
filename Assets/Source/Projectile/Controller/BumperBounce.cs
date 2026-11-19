using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     One bounce off a bumper (a meadow mushroom) that <see cref="ProjectileMotionResolver" /> resolved
    ///     during a fixed step. A step can hold several; the resolver keeps them in order for the view to
    ///     announce (<see cref="ProjectileMotionResolver.StepBumperBounces" />).
    /// </summary>
    internal readonly struct BumperBounce
    {
        /// <summary>The bumper's id: which cap of the level was hit.</summary>
        public readonly int BumperId;

        /// <summary>The mushroom's grid slot.</summary>
        public readonly Vector2Int Slot;

        /// <summary>The ball's centre at the touch: the exact point its path entered the bumper's circle.</summary>
        public readonly Vector3 Point;

        /// <summary>Where the straight run that ended on this bumper began: the wall, deflect or bumper before
        /// it, or the muzzle.</summary>
        public readonly Vector3 LegStart;

        /// <summary>The ball's unit direction coming in, and the one it left along.</summary>
        public readonly Vector2 Incoming;

        public readonly Vector2 Outgoing;

        /// <summary>The flight's chain count after this bounce, and what the chain rule answered for it.</summary>
        public readonly int Count;

        public readonly MushroomReward Reward;

        /// <summary>
        ///     Whether the bounce came after the step's wall bounce, on the stretch the ball flew back off the
        ///     wall. False for one on the way there, and on a step that met no wall.
        /// </summary>
        public readonly bool AfterWall;

        /// <param name="leg">The run that ended on the bumper: where it began, and the ball's centre at the touch
        /// (the contact's point, at the ball's own depth).</param>
        internal BumperBounce(
            in BumperContact contact, Vector2Int slot, (Vector3 Start, Vector3 End) leg, Vector2 outgoing,
            in MushroomChainState chain, MushroomReward reward, bool afterWall)
        {
            BumperId = contact.Bumper.Id;
            Slot = slot;
            Point = leg.End;
            LegStart = leg.Start;
            Incoming = contact.Incoming;
            Outgoing = outgoing;
            Count = chain.Count;
            Reward = reward;
            AfterWall = afterWall;
        }

        /// <summary>The bounce as the rest of the game hears of it, for <paramref name="projectile" /> flying at
        /// <paramref name="speed" />.</summary>
        internal MushroomBouncedMessage ToMessage(IProjectileModel projectile, float speed)
        {
            return new MushroomBouncedMessage(projectile, Slot, BumperId, Point, Incoming, speed, Count, Reward);
        }
    }
}
