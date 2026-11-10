namespace BalloonParty.Configuration.HeadsUp
{
    /// <summary>
    ///     The heads-ups' rules and looks (see <c>Plans/PLAN-HeadsUp.md</c> §6): how many may show at once, and one
    ///     <see cref="HeadsUpStyleSettings" /> per <see cref="HeadsUpStyleId" />.
    /// </summary>
    internal interface IHeadsUpSettings
    {
        /// <summary>Heads-ups on screen at once, held ones aside.</summary>
        int MaxConcurrent { get; }

        HeadsUpStyleSettings Style(HeadsUpStyleId id);
    }
}
