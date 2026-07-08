using System;

namespace BalloonParty.Projectile.Model
{
    /// <summary>
    ///     A temporary modifier on the active projectile. Each buff type declares its own
    ///     <see cref="IProjectileBuffEndCondition" />; the buff system removes and disposes the buff when
    ///     that condition's <c>Expired</c> flips, without knowing what the condition is.
    /// </summary>
    public interface IProjectileBuff : IDisposable
    {
        IProjectileBuffEndCondition EndCondition { get; }
    }
}
