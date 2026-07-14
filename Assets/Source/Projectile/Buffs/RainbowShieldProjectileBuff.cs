using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Turns the active projectile iridescent: its glow cycles the palette, every pop keeps the
    ///     score multiplier climbing regardless of colour, and popped balloons' neighbours convert to
    ///     rainbow. It declares its own end-condition — <see cref="WallBounceEndCondition" />, so it ends
    ///     on the first wall bounce. Its effects live in the layers that own their dependencies (view,
    ///     hit resolver, scoring) and query <c>HasBuff&lt;RainbowShieldProjectileBuff&gt;()</c>.
    /// </summary>
    internal sealed class RainbowShieldProjectileBuff : IProjectileBuff
    {
        private readonly IProjectileBuffEndCondition _endCondition;

        public IProjectileBuffEndCondition EndCondition => _endCondition;

        internal RainbowShieldProjectileBuff(ISubscriber<ShieldLostMessage> wallBounces)
        {
            _endCondition = new WallBounceEndCondition(wallBounces);
        }

        public void Dispose()
        {
            _endCondition.Dispose();
        }
    }
}
