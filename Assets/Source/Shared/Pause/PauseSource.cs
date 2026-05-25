namespace BalloonParty.Shared.Pause
{
    public enum PauseSource
    {
        /// <summary>Cinematic sequence is playing (e.g. level-up trail pan-in).</summary>
        Cinematic,

        /// <summary>A modal overlay has taken control of the screen (e.g. level-up popup).</summary>
        LevelUp
    }
}
