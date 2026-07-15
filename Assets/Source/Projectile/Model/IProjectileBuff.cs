using System;

namespace BalloonParty.Projectile.Model
{
    /// <summary>
    ///     A temporary modifier on the active projectile. A buff is identity (<see cref="Id" />) plus a
    ///     numeric <see cref="Value" /> combined via <see cref="Op" />, plus an
    ///     <see cref="IProjectileBuffEndCondition" /> that decides when it expires.
    /// </summary>
    public sealed class ProjectileBuff : IDisposable
    {
        public ProjectileBuffId Id { get; }
        public float Value { get; }
        public BuffModifierOp Op { get; }
        public IProjectileBuffEndCondition EndCondition { get; }

        internal ProjectileBuff(
            ProjectileBuffId id, float value, BuffModifierOp op, IProjectileBuffEndCondition endCondition)
        {
            Id = id;
            Value = value;
            Op = op;
            EndCondition = endCondition;
        }

        public void Dispose()
        {
            EndCondition.Dispose();
        }
    }
}
