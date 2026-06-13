using UniRx;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Persisted cross-run record — best level and best score. The only progression
    ///     that survives a run; loaded for display, never fed back into a live run.
    /// </summary>
    internal interface IRunMeta
    {
        IReadOnlyReactiveProperty<int> BestLevel { get; }

        IReadOnlyReactiveProperty<int> BestScore { get; }

        void RecordRun(int level, int score);
    }
}
