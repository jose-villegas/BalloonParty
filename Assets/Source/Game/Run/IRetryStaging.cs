namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Stages a retry for the next run reset. Written only by <see cref="IFailureOffers" />, once the
    ///     retry's cost is settled, so nothing can stage a retry around the ad gate.
    /// </summary>
    internal interface IRetryStaging
    {
        /// <summary>
        ///     The next reset restarts at <paramref name="level" /> instead of a fresh run, dealing a bonus card
        ///     for that attempt when <paramref name="withBonusCard" />.
        /// </summary>
        void PrepareRetry(int level, bool withBonusCard);
    }
}
