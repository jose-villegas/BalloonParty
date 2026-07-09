using System.Threading;
using Cysharp.Threading.Tasks;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     A board-wide clear effect — the pop wave, the float-away dissolve, and future variants. A consumer
    ///     snapshots the board (<see cref="Collect" />, while it's still populated), sizes a synced beat off
    ///     <see cref="EstimateSeconds" />, then plays it out. Implementations return the balloons to the pool.
    /// </summary>
    internal interface IBoardEffect
    {
        void Collect();

        float EstimateSeconds();

        UniTask PlayAsync(CancellationToken ct);
    }
}
