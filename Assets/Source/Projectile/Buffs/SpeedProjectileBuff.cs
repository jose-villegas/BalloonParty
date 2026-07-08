using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Doubles the projectile's flight speed until it loses a shield to a wall. The speedup is applied
    ///     in <c>ProjectileMotionResolver</c> (the layer that owns movement), which queries
    ///     <c>HasBuff&lt;SpeedProjectileBuff&gt;()</c>; this type is only identity + end-condition.
    /// </summary>
    internal sealed class SpeedProjectileBuff : IProjectileBuff
    {
        private readonly IProjectileBuffEndCondition _endCondition;

        public IProjectileBuffEndCondition EndCondition => _endCondition;

        internal SpeedProjectileBuff(ISubscriber<ShieldLostMessage> wallBounces)
        {
            _endCondition = new WallBounceEndCondition(wallBounces);
        }

        public void Dispose()
        {
            _endCondition.Dispose();
        }
    }
}
