using System;
using UniRx;

namespace BalloonParty.Projectile.Model
{
    /// <summary>
    ///     Decides when a projectile buff ends. An implementation encapsulates its own lifecycle logic —
    ///     a message subscription, a timer, a running tally — and flips <see cref="Expired" /> true once.
    ///     New end-conditions (time elapsed, pops counted, ...) are new implementers; nothing switches.
    /// </summary>
    public interface IProjectileBuffEndCondition : IDisposable
    {
        IReadOnlyReactiveProperty<bool> Expired { get; }
    }
}
