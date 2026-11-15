namespace BalloonParty.Solver
{
    /// <summary>
    ///     Which reward balloon a simulated target is, if any. A reward's pop pays no score and leaves the streak
    ///     alone; each kind is counted apart, so a ranking can weigh what each one pays.
    /// </summary>
    internal enum ShotRewardKind
    {
        None,
        Golden,
        Heart,
        Disco
    }
}
