using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Multiplies the projectile's flight speed until it loses a shield to a wall. The multiplier is
    ///     read by <c>ProjectileMotionResolver</c> via <c>GetBuff&lt;SpeedProjectileBuff&gt;()</c>.
    /// </summary>
    internal sealed class SpeedProjectileBuff : IProjectileBuff
    {
        private readonly IProjectileBuffEndCondition _endCondition;

        public float Multiplier { get; }

        public IProjectileBuffEndCondition EndCondition => _endCondition;

        internal SpeedProjectileBuff(ISubscriber<ShieldLostMessage> wallBounces, float multiplier = 2f)
        {
            _endCondition = new WallBounceEndCondition(wallBounces);
            Multiplier = multiplier;
        }

        public void Dispose()
        {
            _endCondition.Dispose();
        }
    }
}
