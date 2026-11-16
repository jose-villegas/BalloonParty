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
        ///     Share of the current wait a ball fired while another is still flying takes off the next line on
        ///     an empty board: 0.35 = that line arrives 35% of an interval sooner. It shrinks as the board fills,
        ///     to nothing at its normal fill. One ball at a time costs nothing.
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
        ///     Pace balls in flight add on top of the board's: at full flight pressure, on an empty board, the
        ///     clock runs this many times faster than the board alone would have it. The extra shrinks as the
        ///     board fills, to nothing at its normal fill (1 = flight never matters).
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

        /// <summary>
        ///     Share of the play area that BigScore shapes drawn in the air must cover to earn a breather, as a big
        ///     clear does (same length, pace and cooldown). 0 = never.
        /// </summary>
        float BreatherShapeCover { get; }

        /// <summary>
        ///     Danger (<c>IDangerLevel</c>: the share of the hearts left the next line would cost) at or above which
        ///     the board earns a breather, as a big clear does (same length, pace and cooldown). 0 = never.
        /// </summary>
        float BreatherDanger { get; }

        /// <summary>
        ///     Past full hearts, how much each extra heart takes off the chance that a big clear or a shape burst
        ///     earns its breather: 0.25 = 75% with one extra, none with four. A miss is spent like a breather (the
        ///     cooldown runs). The danger's breather and the nuke's never roll. 0 = always earned.
        /// </summary>
        float BreatherChanceLostPerExtraHeart { get; }

        /// <summary>
        ///     How much faster the clock runs at full pressure (1 = never). Pressure is the push back on a comfortable
        ///     player: it builds while the board stays under <see cref="PressureHoldOccupancy" /> and the player
        ///     is not in need, holds above it, and partly goes when a line costs a heart.
        /// </summary>
        float PressureSpeedup { get; }

        /// <summary>Seconds of comfortable play (no need) that build pressure from none to full; need slows it.</summary>
        float PressureBuildSeconds { get; }

        /// <summary>Share of the open slots in balloons at or above which pressure stops building and holds.</summary>
        float PressureHoldOccupancy { get; }

        /// <summary>Share of the pressure kept when a line costs a heart, 0 to 1.</summary>
        float PressureKeptOnHeartLoss { get; }

        /// <summary>Seconds between lines at <paramref name="level" />; levels below 1 run at level 1's pace.</summary>
        float IntervalForLevel(int level);
    }
}
