using System;
using BalloonParty.Projectile.Model;
using UniRx;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Ends the buff when the projectile stops piercing — the wall that spends the pierce after a
    ///     tough plow, or the shot despawning. Unlike <see cref="WallBounceEndCondition" /> (which ends on
    ///     the first wall), this rides the whole pierce, so a Snipe lance keeps its speed until the pierce
    ///     itself is consumed.
    /// </summary>
    internal sealed class PierceEndedEndCondition : IProjectileBuffEndCondition
    {
        private readonly ReactiveProperty<bool> _expired = new(false);
        private readonly IDisposable _subscription;

        public IReadOnlyReactiveProperty<bool> Expired => _expired;

        internal PierceEndedEndCondition(IReadOnlyReactiveProperty<bool> isPiercing)
        {
            _subscription = isPiercing.Subscribe(OnPiercingChanged);
        }

        private void OnPiercingChanged(bool piercing)
        {
            if (!piercing)
            {
                _expired.Value = true;
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _expired.Dispose();
        }
    }
}
