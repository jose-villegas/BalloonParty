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

        /// <summary>Seconds between lines at <paramref name="level" />; levels below 1 run at level 1's pace.</summary>
        float IntervalForLevel(int level);
    }
}
