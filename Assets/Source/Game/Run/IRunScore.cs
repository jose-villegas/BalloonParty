using UniRx;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Read-only view of the live run score, snapshotted by <see cref="RunController"/>
    ///     when a run ends.
    /// </summary>
    internal interface IRunScore
    {
        IReadOnlyReactiveProperty<int> TotalScore { get; }
    }
}
