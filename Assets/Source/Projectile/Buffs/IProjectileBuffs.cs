using BalloonParty.Projectile.Model;

namespace BalloonParty.Projectile.Buffs
{
    /// <summary>
    ///     Applies a buff to the active projectile. Injectable anywhere — activation is not tied to
    ///     items; anything can grant a projectile buff. The buff carries its own end-condition, so
    ///     nothing else is passed here.
    /// </summary>
    public interface IProjectileBuffs
    {
        void Apply(ProjectileBuff buff);
    }
}
