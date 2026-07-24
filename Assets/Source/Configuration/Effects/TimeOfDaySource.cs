namespace BalloonParty.Configuration.Effects
{
    /// <summary>What drives the night-mode time-of-day angle (see @ref plan_night_mode).</summary>
    internal enum TimeOfDaySource
    {
        /// <summary>The light steps forward one notch per level and sweeps during the level-up
        /// transition (the level-paced cycle).</summary>
        LevelSweep = 0,

        /// <summary>The light rotates continuously on a wall-clock, one full circle per
        /// <see cref="ITimeOfDaySettings.SecondsPerCycle"/> — independent of levels.</summary>
        Realtime = 1
    }
}
