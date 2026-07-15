using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public interface IProjectileModel
    {
        IReadOnlyReactiveProperty<string> ColorName { get; }
        IReadOnlyReactiveProperty<int> ShieldsRemaining { get; }
        Vector3 Direction { get; }
        float Speed { get; }
        bool IsFree { get; }
        IBalloonModel LastHitBalloon { get; }

        bool HasBuff(ProjectileBuffId id);

        /// <summary>
        ///     Aggregates all active modifiers for <paramref name="id" /> onto <paramref name="baseValue" />.
        ///     Order: base + flat sum → × (1 + additive sum) → × multiplicative product.
        /// </summary>
        float ComputeBuffedValue(ProjectileBuffId id, float baseValue);
    }
}
