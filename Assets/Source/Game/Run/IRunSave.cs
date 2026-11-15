namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Where the interrupted run is kept between launches: one save at most, overwritten at each level
    ///     boundary. Progression like <see cref="IRunMeta" />, never telemetry. Lives in the app scope, so the
    ///     launch screen can offer Continue before the game is shown.
    /// </summary>
    internal interface IRunSave
    {
        /// <summary>The saved run, or false when there's none (a corrupt or outdated save is dropped).</summary>
        bool TryLoad(out RunSnapshot snapshot);

        void Write(RunSnapshot snapshot);

        void Clear();
    }
}
