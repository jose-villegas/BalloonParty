using BalloonParty.Projectile.Model;
using UniRx;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>Never expires — the buff lives as long as the shot does (each shot gets a fresh
    /// model, so "permanent" naturally ends at reload). For buffs earned in flight, like the
    /// cruise-tap piercing state.</summary>
    internal sealed class ShotLifetimeEndCondition : IProjectileBuffEndCondition
    {
        private readonly ReactiveProperty<bool> _expired = new(false);

        public IReadOnlyReactiveProperty<bool> Expired => _expired;

        public void Dispose()
        {
            _expired.Dispose();
        }
    }
}
