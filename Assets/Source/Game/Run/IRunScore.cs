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

        /// <summary>The score the level being played began with: what its retry, or a resume of it, starts from.</summary>
        int LevelStartScore { get; }
    }
}
