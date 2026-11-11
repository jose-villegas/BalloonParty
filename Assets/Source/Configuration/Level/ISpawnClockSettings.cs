namespace BalloonParty.Configuration.Level
{
    internal interface ISpawnClockSettings
    {
        /// <summary>Seconds of play after a level starts, or a run restarts, before its first line arrives.</summary>
        float GraceSeconds { get; }

        /// <summary>
        ///     Lines grouped as one wave. Wave quotas, roamer jumps and the item cadence were tuned per
        ///     multi-line turn, so they count waves, not single lines.
        /// </summary>
        int LinesPerWave { get; }

        /// <summary>
        ///     Share of the current wait a ball fired while another is still flying takes off the next line:
        ///     0.35 = that line arrives 35% of an interval sooner. One ball at a time costs nothing.
        /// </summary>
        float OverlapShotPull { get; }

        /// <summary>
        ///     How much faster the clock runs on an empty board. It scales down with the board's fill
        ///     to 1 (normal pace) once the board is back to the level's starting fill and at
        ///     <see cref="TargetOccupancy" />.
        /// </summary>
        float SparseBoardSpeedup { get; }

        /// <summary>Share of the board's open slots the clock pushes balloons towards, 0–1.</summary>
        float TargetOccupancy { get; }

        /// <summary>
        ///     How much the board's fill is read as occupancy against <see cref="TargetOccupancy" />
        ///     rather than against the level's starting fill: 0 = starting fill only, 1 = occupancy only.
        /// </summary>
        float OccupancyWeight { get; }

        /// <summary>
        ///     Pace balls in flight add on top of the board's: at full flight pressure the clock runs
        ///     this many times faster than the board alone would have it (1 = flight never matters).
        /// </summary>
        float FlightSpeedup { get; }

        /// <summary>
        ///     Summed flight seconds of every ball in the air that reach full flight pressure: 6 = one ball
        ///     cruising 6 s, or three balls 2 s each.
        /// </summary>
        float FlightPressureSeconds { get; }

        /// <summary>
        ///     Share of the board's open slots that, cleared within <see cref="BreatherClearWindow" />, earns a
        ///     breather: the clock slows to <see cref="BreatherPace" /> and eases back to its full pace over
        ///     <see cref="BreatherSeconds" />. 0 = never.
        /// </summary>
        float BreatherClear { get; }

        /// <summary>Seconds a clear must happen within to earn a breather.</summary>
        float BreatherClearWindow { get; }

        /// <summary>Seconds a breather lasts, easing from <see cref="BreatherPace" /> back to full pace.</summary>
        float BreatherSeconds { get; }

        /// <summary>The clock's pace as a breather starts, as a share of its full pace, 0–1.</summary>
        float BreatherPace { get; }

        /// <summary>Seconds of play after a breather ends before a big clear can earn the next; 0 at a level's start.</summary>
        float BreatherCooldownSeconds { get; }

        /// <summary>Seconds between lines at <paramref name="level" />; levels below 1 run at level 1's pace.</summary>
        float IntervalForLevel(int level);
    }
}
