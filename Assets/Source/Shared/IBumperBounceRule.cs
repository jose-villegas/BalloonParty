using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     One permanent bumper (the cap of a meadow mushroom) as the ball meets it: a circle the ball's centre
    ///     bounces off. Caps stand in walls: one to three of them in neighbouring slots.
    /// </summary>
    internal readonly struct BumperCircle
    {
        /// <summary>Which cap this is, 0 or more. No two caps of a board share one.</summary>
        public readonly int Id;

        /// <summary>
        ///     Which wall the cap belongs to, 0 or more: the caps of one wall share it.
        ///     <see cref="MushroomChainRule" /> counts by it (one hit a wall, not the same wall twice in a row), and
        ///     the kick bends a ball only toward a cap of another wall. Caps of one wall may stand so near that
        ///     their circles overlap; caps of two walls may not.
        /// </summary>
        public readonly int Wall;

        public readonly Vector2 Center;

        /// <summary>
        ///     How far from <see cref="Center" /> the ball's centre is when the two touch: the bumper's own radius
        ///     plus the ball's. Every bounce rule works on the path of the ball's centre, so a corridor is clear of
        ///     this bumper when that path stays farther than this from its centre.
        /// </summary>
        public readonly float Radius;

        /// <summary>A cap that is a wall by itself: its wall is its id.</summary>
        public BumperCircle(int id, Vector2 center, float radius)
            : this(id, id, center, radius)
        {
        }

        public BumperCircle(int id, int wall, Vector2 center, float radius)
        {
            Id = id;
            Wall = wall;
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>The ball touching a bumper: where its centre is, how it came in, and the bumper it hit.</summary>
    internal readonly struct BumperContact
    {
        /// <summary>The ball's centre at the touch, on the circle of <see cref="Bumper" />.</summary>
        public readonly Vector2 Point;

        /// <summary>The ball's direction of travel coming in. Any length.</summary>
        public readonly Vector2 Incoming;

        public readonly BumperCircle Bumper;

        public BumperContact(Vector2 point, Vector2 incoming, BumperCircle bumper)
        {
            Point = point;
            Incoming = incoming;
            Bumper = bumper;
        }
    }

    /// <summary>
    ///     Which way the ball leaves a bumper. The game, the shot solver and the aim line all ask the one rule, so
    ///     the drawn line, the solved shot and the flight bend alike.
    /// </summary>
    internal interface IBumperBounceRule
    {
        /// <summary>
        ///     The unit direction the ball leaves <paramref name="contact" /> along. <paramref name="bumpers" /> is
        ///     every bumper on the board, the one hit included, in any order. <paramref name="chain" /> is the
        ///     flight's chain after this bounce was counted. The same arguments always give the same answer, and
        ///     nothing is allocated.
        /// </summary>
        /// <remarks>
        ///     What every caller (the projectile, the shot solver, the aim line) owes the rule:
        ///     <list type="bullet">
        ///         <item>
        ///             <see cref="MushroomChainRule.OnBounce" /> first, then this, and not this at all when the
        ///             answer was <see cref="MushroomReward.PassThrough" />: the ball flies on unturned.
        ///         </item>
        ///         <item>
        ///             The contact point is the exact point where the ball's centre enters the bumper's circle,
        ///             not where a step happened to end.
        ///         </item>
        ///         <item>
        ///             After the bounce, do not test the bumper just left again on the same step. The ball starts
        ///             on its circle, and a line leaving close to the tangent would be read as entering it again.
        ///         </item>
        ///     </list>
        /// </remarks>
        Vector2 Resolve(in BumperContact contact, IReadOnlyList<BumperCircle> bumpers, in MushroomChainState chain);
    }
}
