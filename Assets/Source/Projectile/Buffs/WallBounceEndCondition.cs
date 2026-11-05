using System;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>Ends the buff when its shot loses a shield to a wall (a <see cref="ShieldLostMessage" />).</summary>
    internal sealed class WallBounceEndCondition : IProjectileBuffEndCondition
    {
        private readonly ReactiveProperty<bool> _expired = new(false);
        private readonly IProjectileModel _projectile;
        private readonly IDisposable _subscription;

        public IReadOnlyReactiveProperty<bool> Expired => _expired;

        internal WallBounceEndCondition(ISubscriber<ShieldLostMessage> wallBounces, IProjectileModel projectile)
        {
            _projectile = projectile;
            _subscription = wallBounces.Subscribe(OnShieldLost);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _expired.Dispose();
        }

        private void OnShieldLost(ShieldLostMessage msg)
        {
            if (msg.Projectile == _projectile)
            {
                _expired.Value = true;
            }
        }
    }
}
