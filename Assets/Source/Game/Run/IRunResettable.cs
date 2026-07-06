namespace BalloonParty.Game.Run
{
    /// <summary><see cref="RunController"/> invokes implementations in ascending <see cref="ResetOrder"/>, lower first.</summary>
    internal interface IRunResettable
    {
        int ResetOrder { get; }

        /// <param name="generation">Monotonically increasing run number; frame-deferred async can compare it to drop stale work.</param>
        void ResetRun(int generation);
    }
}
