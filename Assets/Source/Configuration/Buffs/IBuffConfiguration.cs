using BalloonParty.Projectile.Model;

namespace BalloonParty.Configuration.Buffs
{
    public interface IBuffConfiguration
    {
        /// <summary>
        ///     Returns the default modifier value for the given buff.
        ///     E.g. Speed → 2 means "×2 multiplicative" when used with <see cref="BuffModifierOp.Multiplicative" />.
        /// </summary>
        float GetValue(ProjectileBuffId id);
    }
}
