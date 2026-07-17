using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;

namespace BalloonParty.Projectile.Model
{
    public interface IProjectileModel
    {
        IReadOnlyReactiveProperty<string> ColorName { get; }
        IReadOnlyReactiveProperty<int> ShieldsRemaining { get; }

        // True while the shot is ping-ponging through empty space (consecutive wall bounces with no
        // balloon contact) — the earned long-flight moment feedback systems key off.
        IReadOnlyReactiveProperty<bool> IsCruising { get; }

        // True once the shot has earned piercing (pops everything, unbreakables included) via a long
        // enough cruise — until a wall consumes it after a tough plow. Feedback/visuals subscribe.
        IReadOnlyReactiveProperty<bool> IsPiercing { get; }

        // True while the shot is on its doomed final segment (0 shields, a clear shield-less path to
        // the wall it will die on) — the 'last breath'. Systems pause off this (spawning, time scale).
        IReadOnlyReactiveProperty<bool> IsLastShieldApproach { get; }
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
