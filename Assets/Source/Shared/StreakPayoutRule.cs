namespace BalloonParty.Shared
{
    /// <summary>
    ///     The one place that says what a colour streak multiplies a pop's points by.
    /// </summary>
    /// <remarks>
    ///     A streak pays its own length: the nth same-colour pop in a row scores n times. Uncapped, one
    ///     lucky shot that chains dozens of pops earned more than a whole level asks for, so the payout
    ///     stops growing at <see cref="IRunConfig.MaxStreakMultiplier" /> while the streak itself keeps
    ///     counting (the bar and the streak shields still read the real length). Shared by the live score
    ///     and the solver's flight simulation, which must agree or its "best" shot chases a payout the
    ///     game no longer gives.
    /// </remarks>
    internal static class StreakPayoutRule
    {
        /// <summary><paramref name="streakMultiplier" />, at most <paramref name="cap" />; a cap of 0 or less is no cap.</summary>
        internal static int Multiplier(int streakMultiplier, int cap)
        {
            return cap > 0 && streakMultiplier > cap ? cap : streakMultiplier;
        }
    }
}
