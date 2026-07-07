namespace BalloonParty.Shared.Pause
{
    public enum PauseSource
    {
        /// <summary>Cinematic sequence is playing (e.g. level-up trail pan-in).</summary>
        Cinematic,

        /// <summary>A modal overlay has taken control of the screen (e.g. level-up popup).</summary>
        LevelUp,

        /// <summary>Holds the thrower during overflow pops so the player can't fire into an unresolved board.</summary>
        Overflow,

        /// <summary>The board is being cleared and re-populated for the new level (the Ascent).</summary>
        LevelTransition,

        /// <summary>The debug cheat console is open — holds the thrower so stray fires don't disturb testing.</summary>
        Cheat
    }
}
