using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     <see cref="Projectile" /> bounced off a meadow mushroom (@ref plan_meadow_mushrooms). Published once
    ///     for every bounce, in the order they happened, the ones that did not count included. A ball the
    ///     mushrooms let through (<see cref="MushroomReward.PassThrough" />) publishes nothing, and neither does
    ///     a lance: it bounces off none and cuts their caps (<see cref="MushroomCutMessage" />).
    /// </summary>
    /// <remarks>
    ///     <see cref="Count" /> is the flight's chain after this bounce was counted, and <see cref="Reward" />
    ///     what the chain rule answered for it: the lance to arm, or nothing. The
    ///     reward itself is paid by whoever listens; the ball is not changed by the bounce beyond its turn.
    ///     <see cref="Incoming" /> and <see cref="Speed" /> are the ball's as it arrived, for a reaction that
    ///     leans away from the hit.
    /// </remarks>
    internal readonly struct MushroomBouncedMessage
    {
        public readonly IProjectileModel Projectile;

        /// <summary>The mushroom's grid slot.</summary>
        public readonly Vector2Int Slot;

        /// <summary>Which cap of the level was hit, 0 or more: the id of its bumper circle. Its view is the one
        /// that leans.</summary>
        public readonly int ChainIndex;

        /// <summary>The ball's centre at the touch.</summary>
        public readonly Vector3 Point;

        /// <summary>The ball's unit direction of travel coming in.</summary>
        public readonly Vector2 Incoming;

        /// <summary>The ball's speed at the bounce, in world units a second.</summary>
        public readonly float Speed;

        /// <summary>Mushrooms counted since the flight began or last touched a wall, this one included when it
        /// counted.</summary>
        public readonly int Count;

        public readonly MushroomReward Reward;

        public MushroomBouncedMessage(
            IProjectileModel projectile, Vector2Int slot, int chainIndex, Vector3 point, Vector2 incoming,
            float speed, int count, MushroomReward reward)
        {
            Projectile = projectile;
            Slot = slot;
            ChainIndex = chainIndex;
            Point = point;
            Incoming = incoming;
            Speed = speed;
            Count = count;
            Reward = reward;
        }
    }
}
