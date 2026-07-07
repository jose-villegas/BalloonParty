namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The level-up ceremony as one explicit state, owned by <see cref="LevelController" />. Replaces
    ///     the scattered guard flags (pending / transitioning / nav / pause) that had to be kept in sync:
    ///     a level-up is only detected in <see cref="Playing" />, and every out-of-phase input (a second
    ///     detection, a straggler trail, a duplicate dismissal) is rejected because no transition exists
    ///     for it. Cycles <c>Playing → Pending → Transitioning → Playing</c>.
    /// </summary>
    internal enum LevelUpPhase
    {
        /// <summary>Normal play: scoring is open and a completed colour set can trigger a level-up.</summary>
        Playing,

        /// <summary>Detected and published; the popup ceremony is up. The level and progress have not
        /// advanced yet — that waits for dismissal.</summary>
        Pending,

        /// <summary>Dismissed: the level has advanced and the Ascent is running. Ends when the Ascent
        /// reports completion.</summary>
        Transitioning
    }
}
