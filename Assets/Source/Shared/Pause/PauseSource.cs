namespace BalloonParty.Shared.Pause
{
    public enum PauseSource
    {
        /// <summary>Cinematic sequence is playing (e.g. level-up trail pan-in).</summary>
        Cinematic,

        /// <summary>A modal overlay has taken control of the screen (e.g. level-up popup).</summary>
        LevelUp,

        /// <summary>
        ///     Rejected balloons are popping below the grid after a turn's spawn. Holds the thrower
        ///     until the overflow finishes so the player can't fire into an unresolved board.
        /// </summary>
        Overflow,

        /// <summary>The board is being cleared and re-populated for the new level (the Ascent).</summary>
        LevelTransition
    }
}
