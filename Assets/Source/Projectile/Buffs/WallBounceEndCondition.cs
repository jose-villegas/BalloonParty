using System;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>Ends the buff when the projectile loses a shield to a wall (a <see cref="ShieldLostMessage" />).</summary>
    internal sealed class WallBounceEndCondition : IProjectileBuffEndCondition
    {
        private readonly ReactiveProperty<bool> _expired = new(false);
        private readonly IDisposable _subscription;

        public IReadOnlyReactiveProperty<bool> Expired => _expired;

        internal WallBounceEndCondition(ISubscriber<ShieldLostMessage> wallBounces)
        {
            _subscription = wallBounces.Subscribe(_ => _expired.Value = true);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _expired.Dispose();
        }
    }
}
