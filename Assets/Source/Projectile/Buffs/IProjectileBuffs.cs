using BalloonParty.Projectile.Model;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Applies a buff to a shot. Injectable anywhere — activation is not tied to items; anything can
    ///     grant a projectile buff. The buff carries its own end-condition, so nothing else is passed here.
    ///     A buff for a shot that is no longer loaded or in flight is dropped.
    /// </summary>
    public interface IProjectileBuffs
    {
        void Apply(IProjectileModel projectile, ProjectileBuff buff);
    }
}
