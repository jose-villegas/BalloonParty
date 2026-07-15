using System;

namespace BalloonParty.Projectile.Model
{
    /// <summary>
    ///     A temporary modifier on the active projectile. A buff is identity (<see cref="Id" />) plus a
    ///     numeric <see cref="Factor" /> whose meaning is defined by the consumer, plus an
    ///     <see cref="IProjectileBuffEndCondition" /> that decides when it expires.
    /// </summary>
    public sealed class ProjectileBuff : IDisposable
    {
        public ProjectileBuffId Id { get; }
        public float Factor { get; }
        public IProjectileBuffEndCondition EndCondition { get; }

        internal ProjectileBuff(ProjectileBuffId id, float factor, IProjectileBuffEndCondition endCondition)
        {
            Id = id;
            Factor = factor;
            EndCondition = endCondition;
        }

        public void Dispose()
        {
            EndCondition.Dispose();
        }
    }
}
